using System.Linq;
using System.Net.Http;
using System.Reflection;
using Azure.Functions.OpenApi.Discovery;
using Azure.Functions.OpenApi.Generation;
using Microsoft.OpenApi;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Fake methods carrying only OpenAPI attributes (no Functions triggers) used to drive
/// <see cref="OpenApiPathsBuilder"/> through hand-built <see cref="DiscoveredEndpoint"/> records.
/// </summary>
public sealed class PathsBuilderFakeFunctions
{
    public sealed class Widget
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [OpenApiOperation(OperationId = "getWidget", Summary = "Get widget", Description = "Fetch a widget.", Tags = new[] { "Widgets" })]
    [OpenApiPathParameter("id", typeof(int), Description = "The widget id.")]
    [OpenApiQueryParameter("verbose", typeof(bool), Required = false, Description = "Include detail.")]
    [OpenApiHeaderParameter("X-Trace", typeof(string), Required = false, Description = "Trace header.")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(404, Description = "Missing.")]
    public void GetWidget() { }

    [OpenApiOperation(OperationId = "createWidget", Summary = "Create widget")]
    [OpenApiRequestBody(typeof(Widget), Description = "The widget to create.")]
    [OpenApiResponse(201, Type = typeof(Widget), Description = "Created.")]
    public void CreateWidget() { }

    [OpenApiOperation(OperationId = "pingWidget", Summary = "Ping")]
    public void PingWidget() { }

    public void UnannotatedWidget() { }

    [OpenApiOperation(OperationId = "getProblemWidget", Summary = "Get widget or problem")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(404, Type = typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), Description = "Missing.")]
    public void GetProblemWidget() { }

    [OpenApiOperation(OperationId = "getXmlProblemWidget", Summary = "Get widget or xml problem")]
    [OpenApiResponse(400, Type = typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), ContentType = "application/xml", Description = "Bad request.")]
    public void GetXmlProblemWidget() { }
}

/// <summary>
/// Tests for <see cref="OpenApiPathsBuilder"/> attribute-to-operation mapping.
/// </summary>
public sealed class PathsBuilderTests
{
    private static MethodInfo Method(string name) =>
        typeof(PathsBuilderFakeFunctions).GetMethod(name)!;

    private static DiscoveredEndpoint Endpoint(
        string path,
        string verb,
        string methodName,
        IReadOnlyList<string>? routeParams = null) =>
        new(path, new[] { verb }, Method(methodName), routeParams ?? Array.Empty<string>());

    private static OpenApiDocument NewDocument() => new()
    {
        Info = new OpenApiInfo { Title = "T", Version = "1.0.0" },
        Paths = new OpenApiPaths(),
        Components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema>() },
    };

    private static OpenApiOperation Populate(
        DiscoveredEndpoint endpoint,
        bool includeUnannotated,
        out OpenApiDocument document)
    {
        document = NewDocument();
        new OpenApiPathsBuilder().Populate(document, new[] { endpoint }, includeUnannotated);
        var pathItem = Assert.IsType<OpenApiPathItem>(document.Paths![endpoint.Path]);
        return pathItem.Operations![HttpMethod.Parse(endpoint.HttpMethods[0])];
    }

    [Fact]
    public void Populate_MapsPathQueryAndHeaderParameters_WithCorrectLocation()
    {
        var endpoint = Endpoint("/api/widgets/{id}", "GET", nameof(PathsBuilderFakeFunctions.GetWidget), new[] { "id" });

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var id = operation.Parameters!.Single(p => p.Name == "id");
        var verbose = operation.Parameters!.Single(p => p.Name == "verbose");
        var trace = operation.Parameters!.Single(p => p.Name == "X-Trace");

        Assert.Equal(ParameterLocation.Path, id.In);
        Assert.True(id.Required);
        Assert.Equal(ParameterLocation.Query, verbose.In);
        Assert.Equal(ParameterLocation.Header, trace.In);
    }

    [Fact]
    public void Populate_AppliesOperationMetadata()
    {
        var endpoint = Endpoint("/api/widgets/{id}", "GET", nameof(PathsBuilderFakeFunctions.GetWidget), new[] { "id" });

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.Equal("getWidget", operation.OperationId);
        Assert.Equal("Get widget", operation.Summary);
        Assert.Equal("Fetch a widget.", operation.Description);
        Assert.NotNull(operation.Tags);
        Assert.Contains(operation.Tags!, t => t.Reference?.Id == "Widgets");
    }

    [Fact]
    public void Populate_IncludesAllResponseCodes()
    {
        var endpoint = Endpoint("/api/widgets/{id}", "GET", nameof(PathsBuilderFakeFunctions.GetWidget), new[] { "id" });

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!.ContainsKey("200"));
        Assert.True(operation.Responses!.ContainsKey("404"));
    }

    [Fact]
    public void Populate_AddsRequestBodyContentAndSchema()
    {
        var endpoint = Endpoint("/api/widgets", "POST", nameof(PathsBuilderFakeFunctions.CreateWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.NotNull(operation.RequestBody);
        var content = operation.RequestBody!.Content!;
        Assert.True(content.ContainsKey("application/json"));
        Assert.NotNull(content["application/json"].Schema);
    }

    [Fact]
    public void Populate_DefaultsTo200_WhenNoResponseAttributes()
    {
        var endpoint = Endpoint("/api/widgets/ping", "GET", nameof(PathsBuilderFakeFunctions.PingWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!.ContainsKey("200"));
    }

    [Fact]
    public void Populate_ExcludesUnannotatedEndpoint_WhenIncludeUnannotatedFalse()
    {
        var endpoint = Endpoint("/api/widgets/raw", "GET", nameof(PathsBuilderFakeFunctions.UnannotatedWidget));
        var document = NewDocument();

        new OpenApiPathsBuilder().Populate(document, new[] { endpoint }, includeUnannotated: false);

        Assert.False(document.Paths!.ContainsKey("/api/widgets/raw"));
    }

    [Fact]
    public void Populate_IncludesUnannotatedEndpoint_WhenIncludeUnannotatedTrue()
    {
        var endpoint = Endpoint("/api/widgets/raw", "GET", nameof(PathsBuilderFakeFunctions.UnannotatedWidget), new[] { "raw" });

        var operation = Populate(endpoint, includeUnannotated: true, out var document);

        Assert.True(document.Paths!.ContainsKey("/api/widgets/raw"));
        Assert.True(operation.Responses!.ContainsKey("200"));
    }

    [Fact]
    public void Populate_MergesSharedPath_IntoDistinctOperations()
    {
        var get = Endpoint("/api/widgets", "GET", nameof(PathsBuilderFakeFunctions.PingWidget));
        var post = Endpoint("/api/widgets", "POST", nameof(PathsBuilderFakeFunctions.CreateWidget));
        var document = NewDocument();

        new OpenApiPathsBuilder().Populate(document, new[] { get, post }, includeUnannotated: false);

        var pathItem = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/widgets"]);
        Assert.True(pathItem.Operations!.ContainsKey(HttpMethod.Get));
        Assert.True(pathItem.Operations!.ContainsKey(HttpMethod.Post));
    }

    [Fact]
    public void Populate_RegistersComplexResponseSchema_InComponents()
    {
        var endpoint = Endpoint("/api/widgets/{id}", "GET", nameof(PathsBuilderFakeFunctions.GetWidget), new[] { "id" });

        Populate(endpoint, includeUnannotated: false, out var document);

        Assert.True(document.Components!.Schemas!.ContainsKey(nameof(PathsBuilderFakeFunctions.Widget)));
    }

    [Fact]
    public void Populate_ProblemResponse_UsesProblemJsonContentType()
    {
        var endpoint = Endpoint("/api/widgets/{id}", "GET", nameof(PathsBuilderFakeFunctions.GetProblemWidget), new[] { "id" });

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var problem = operation.Responses!["404"].Content!;
        Assert.True(problem.ContainsKey("application/problem+json"));
        Assert.False(problem.ContainsKey("application/json"));
    }

    [Fact]
    public void Populate_ProblemResponse_RespectsExplicitContentType()
    {
        var endpoint = Endpoint("/api/widgets/xml", "GET", nameof(PathsBuilderFakeFunctions.GetXmlProblemWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var content = operation.Responses!["400"].Content!;
        Assert.True(content.ContainsKey("application/xml"));
        Assert.False(content.ContainsKey("application/problem+json"));
    }

    [Fact]
    public void Populate_NonProblemResponse_UsesJsonContentType()
    {
        var endpoint = Endpoint("/api/widgets/{id}", "GET", nameof(PathsBuilderFakeFunctions.GetProblemWidget), new[] { "id" });

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        // The 200 response carries a plain model and stays application/json.
        var content = operation.Responses!["200"].Content!;
        Assert.True(content.ContainsKey("application/json"));
        Assert.False(content.ContainsKey("application/problem+json"));
    }
}
