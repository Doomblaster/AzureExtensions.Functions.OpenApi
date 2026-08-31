using System.Linq;
using System.Net.Http;
using System.Reflection;
using AzureExtensions.Functions.OpenApi.Discovery;
using AzureExtensions.Functions.OpenApi.Generation;
using Microsoft.OpenApi;
using Xunit;

namespace AzureExtensions.Functions.OpenApi.Tests;

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
    [OpenApiRequestHeaderParameter("X-Trace", typeof(string), Required = false, Description = "Trace header.")]
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

    [OpenApiOperation(OperationId = "singleHeaderWidget", Summary = "Single-status header")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponseHeader("X-RateLimit-Remaining", typeof(int), 200, Required = true, Description = "Requests remaining.")]
    public void SingleHeaderWidget() { }

    [OpenApiOperation(OperationId = "multiHeaderWidget", Summary = "Multi-status header")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(400, Description = "Bad.")]
    [OpenApiResponseHeader("X-Request-Id", typeof(System.Guid), 200, 400, Description = "Correlation id.")]
    public void MultiHeaderWidget() { }

    [OpenApiOperation(OperationId = "missingResponseHeaderWidget", Summary = "Header on undocumented status")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponseHeader("Location", typeof(System.Uri), 201, Description = "Created resource URL.")]
    public void MissingResponseHeaderWidget() { }

    [OpenApiOperation(OperationId = "emptyStatusHeaderWidget", Summary = "Header on all documented responses")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(404, Description = "Missing.")]
    [OpenApiResponseHeader("X-Trace-Id", typeof(string), Description = "Trace id on all responses.")]
    public void EmptyStatusHeaderWidget() { }

    [OpenApiOperation(OperationId = "emptyStatusNoResponseWidget", Summary = "Header with no documented responses")]
    [OpenApiResponseHeader("X-Trace-Id", typeof(string), Description = "Trace id on synthetic 200.")]
    public void EmptyStatusNoResponseWidget() { }

    [OpenApiOperation(OperationId = "requestHeaderSetWidget", Summary = "Request header set")]
    [OpenApiRequestHeaderParameterSet(typeof(RequestHeaderSetFixture))]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void RequestHeaderSetWidget() { }

    [OpenApiOperation(OperationId = "requestHeaderSetCollisionWidget", Summary = "Request header set collision")]
    [OpenApiRequestHeaderParameterSet(typeof(RequestHeaderCollisionFixture))]
    [OpenApiRequestHeaderParameter("X-Trace-Id", typeof(Guid), Required = true, Description = "Trace identifier override.")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void RequestHeaderSetCollisionWidget() { }

    [OpenApiOperation(OperationId = "requestHeaderDuplicatesWidget", Summary = "Request header duplicates")]
    [OpenApiRequestHeaderParameter("X-Trace-Id", typeof(string), Required = false, Description = "First trace header.")]
    [OpenApiRequestHeaderParameter("x-trace-id", typeof(Guid), Required = true, Description = "Second trace header.")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void RequestHeaderDuplicatesWidget() { }

    [OpenApiOperation(OperationId = "deprecatedRequestHeaderWidget", Summary = "Deprecated request header")]
    [OpenApiRequestHeaderParameter("X-Deprecated-Trace", typeof(string), Required = false, Deprecated = true, Description = "Deprecated trace header.")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void DeprecatedRequestHeaderWidget() { }

    [OpenApiOperation(OperationId = "deprecatedRequestHeaderSetWidget", Summary = "Deprecated request header set")]
    [OpenApiRequestHeaderParameterSet(typeof(DeprecatedRequestHeaderSetFixture))]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void DeprecatedRequestHeaderSetWidget() { }

    [OpenApiOperation(OperationId = "responseHeaderSetTargetedWidget", Summary = "Response header set targeted")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(201, Type = typeof(Widget), Description = "Created.")]
    [OpenApiResponse(400, Description = "Bad.")]
    [OpenApiResponseHeaderSet(typeof(ResponseHeaderSetFixture), 200, 201)]
    public void ResponseHeaderSetTargetedWidget() { }

    [OpenApiOperation(OperationId = "responseHeaderSetAllWidget", Summary = "Response header set all")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(404, Description = "Missing.")]
    [OpenApiResponseHeaderSet(typeof(ResponseHeaderSetFixture))]
    public void ResponseHeaderSetAllWidget() { }

    [OpenApiOperation(OperationId = "responseHeaderSetCollisionWidget", Summary = "Response header set collision")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(201, Type = typeof(Widget), Description = "Created.")]
    [OpenApiResponseHeaderSet(typeof(ResponseHeaderCollisionFixture), 200)]
    [OpenApiResponseHeader("X-Request-Id", typeof(Guid), 200, Required = true, Deprecated = true, Description = "Correlation identifier override.")]
    public void ResponseHeaderSetCollisionWidget() { }

    [OpenApiOperation(OperationId = "responseHeaderDuplicatesWidget", Summary = "Response header duplicates")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponseHeader("X-Request-Id", typeof(string), 200, Description = "First response header.")]
    [OpenApiResponseHeader("x-request-id", typeof(Guid), 200, Description = "Second response header.")]
    public void ResponseHeaderDuplicatesWidget() { }

    [OpenApiOperation(OperationId = "genericRequestHeaderWidget", Summary = "Generic request header")]
    [OpenApiRequestHeaderParameter<GenericRequestHeaderDefinition>]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void GenericRequestHeaderWidget() { }

    [OpenApiOperation(OperationId = "genericRequestHeaderSetWidget", Summary = "Generic request header set")]
    [OpenApiRequestHeaderParameterSet<GenericRequestHeaderSetFixture>]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void GenericRequestHeaderSetWidget() { }

    [OpenApiOperation(OperationId = "genericResponseHeaderWidget", Summary = "Generic response header")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(202, Type = typeof(Widget), Description = "Accepted.")]
    [OpenApiResponseHeader<GenericResponseHeaderDefinition>(202)]
    public void GenericResponseHeaderWidget() { }

    [OpenApiOperation(OperationId = "genericResponseHeaderSetWidget", Summary = "Generic response header set")]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    [OpenApiResponse(202, Type = typeof(Widget), Description = "Accepted.")]
    [OpenApiResponseHeaderSet<GenericResponseHeaderSetFixture>(200, 202)]
    public void GenericResponseHeaderSetWidget() { }

    [OpenApiOperation(OperationId = "nonGenericResponseDefinitionWidget", Summary = "Non-generic response definition")]
    [OpenApiResponse(404, Type = typeof(NotFoundResponseBodyFixture), ContentType = "application/json", Description = "Not found.")]
    public void NonGenericResponseDefinitionWidget() { }

    [OpenApiOperation(OperationId = "genericResponseDefinitionWidget", Summary = "Generic response definition")]
    [OpenApiResponse<NotFoundResponseDefinitionFixture>]
    public void GenericResponseDefinitionWidget() { }

    [OpenApiOperation(OperationId = "malformedRequestHeaderSetWidget", Summary = "Malformed request header set")]
    [OpenApiRequestHeaderParameterSet(typeof(InvalidRequestHeaderSetWithoutPublicParameterlessConstructor))]
    [OpenApiResponse(200, Type = typeof(Widget), Description = "Found.")]
    public void MalformedRequestHeaderSetWidget() { }
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

    [Fact]
    public void Populate_ResponseHeader_SingleStatus_AttachesToThatResponse()
    {
        var endpoint = Endpoint("/api/widgets/rl", "GET", nameof(PathsBuilderFakeFunctions.SingleHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out var document);

        var headers = operation.Responses!["200"].Headers!;
        var header = Assert.IsType<OpenApiHeader>(headers["X-RateLimit-Remaining"]);
        Assert.True(header.Required);
        Assert.Equal("Requests remaining.", header.Description);

        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Equal("int32", schema.Format);
    }

    [Fact]
    public void Populate_ResponseHeader_MultipleStatuses_AttachesToEach()
    {
        var endpoint = Endpoint("/api/widgets/multi", "GET", nameof(PathsBuilderFakeFunctions.MultiHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Request-Id"));
        Assert.True(operation.Responses!["400"].Headers!.ContainsKey("X-Request-Id"));
    }

    [Fact]
    public void Populate_ResponseHeader_UndocumentedStatus_CreatesBareResponse()
    {
        var endpoint = Endpoint("/api/widgets/created", "GET", nameof(PathsBuilderFakeFunctions.MissingResponseHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!.ContainsKey("201"));
        var created = operation.Responses!["201"];
        Assert.Equal(string.Empty, created.Description);
        Assert.True(created.Headers!.ContainsKey("Location"));
        Assert.Null(created.Content);
    }

    [Fact]
    public void Populate_ResponseHeader_EmptyStatus_AttachesToAllDocumentedResponses()
    {
        var endpoint = Endpoint("/api/widgets/all", "GET", nameof(PathsBuilderFakeFunctions.EmptyStatusHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Trace-Id"));
        Assert.True(operation.Responses!["404"].Headers!.ContainsKey("X-Trace-Id"));
    }

    [Fact]
    public void Populate_ResponseHeader_EmptyStatus_NoResponseAttributes_AttachesToSynthetic200()
    {
        var endpoint = Endpoint("/api/widgets/synthetic", "GET", nameof(PathsBuilderFakeFunctions.EmptyStatusNoResponseWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!.ContainsKey("200"));
        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Trace-Id"));
    }

    [Fact]
    public void Populate_RequestHeaderSet_AddsDeclaredHeaderParameters()
    {
        var endpoint = Endpoint("/api/widgets/request-headers", "GET", nameof(PathsBuilderFakeFunctions.RequestHeaderSetWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var tenant = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Tenant-Id"));
        Assert.Equal(ParameterLocation.Header, tenant.In);
        Assert.True(tenant.Required);
        Assert.Equal("Tenant identifier.", tenant.Description);
        var tenantSchema = Assert.IsType<OpenApiSchema>(tenant.Schema);
        Assert.Equal(JsonSchemaType.String, tenantSchema.Type);
        Assert.Equal("uuid", tenantSchema.Format);

        var trace = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Trace-Id"));
        Assert.Equal(ParameterLocation.Header, trace.In);
        Assert.False(trace.Required);
        Assert.Equal("Trace identifier.", trace.Description);
        var traceSchema = Assert.IsType<OpenApiSchema>(trace.Schema);
        Assert.Equal(JsonSchemaType.String, traceSchema.Type);
        Assert.Null(traceSchema.Format);
    }

    [Fact]
    public void Populate_RequestHeaderParameters_DuplicateIndividualAttributes_AreAppendedInOrder()
    {
        var endpoint = Endpoint("/api/widgets/request-headers/duplicates", "GET", nameof(PathsBuilderFakeFunctions.RequestHeaderDuplicatesWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var headers = operation.Parameters!
            .OfType<OpenApiParameter>()
            .Where(p => string.Equals(p.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, headers.Count);
        Assert.Equal("X-Trace-Id", headers[0].Name);
        Assert.Equal("First trace header.", headers[0].Description);
        Assert.Equal("x-trace-id", headers[1].Name);
        Assert.Equal("Second trace header.", headers[1].Description);
    }

    [Fact]
    public void Populate_RequestHeaderSet_IndividualAttributeWinsOnCaseInsensitiveCollision()
    {
        var endpoint = Endpoint("/api/widgets/request-headers/collision", "GET", nameof(PathsBuilderFakeFunctions.RequestHeaderSetCollisionWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var trace = Assert.Single(
            operation.Parameters!,
            p => string.Equals(p.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("X-Trace-Id", trace.Name);
        Assert.Equal(ParameterLocation.Header, trace.In);
        Assert.True(trace.Required);
        Assert.Equal("Trace identifier override.", trace.Description);
        var traceSchema = Assert.IsType<OpenApiSchema>(trace.Schema);
        Assert.Equal(JsonSchemaType.String, traceSchema.Type);
        Assert.Equal("uuid", traceSchema.Format);
    }

    [Fact]
    public void Populate_RequestHeaderParameter_MapsDeprecatedFlag()
    {
        var endpoint = Endpoint("/api/widgets/request-headers/deprecated", "GET", nameof(PathsBuilderFakeFunctions.DeprecatedRequestHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var header = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Deprecated-Trace"));
        Assert.Equal(ParameterLocation.Header, header.In);
        Assert.True(header.Deprecated);
        Assert.Equal("Deprecated trace header.", header.Description);
    }

    [Fact]
    public void Populate_RequestHeaderSet_MapsDeprecatedFlag()
    {
        var endpoint = Endpoint("/api/widgets/request-headers/deprecated-set", "GET", nameof(PathsBuilderFakeFunctions.DeprecatedRequestHeaderSetWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var header = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Deprecated-Request"));
        Assert.Equal(ParameterLocation.Header, header.In);
        Assert.True(header.Deprecated);
        Assert.Equal("Deprecated request header.", header.Description);
    }

    [Fact]
    public void Populate_ResponseHeaderSet_TargetedStatuses_AttachesOnlyToMatchingResponses()
    {
        var endpoint = Endpoint("/api/widgets/response-headers/targeted", "GET", nameof(PathsBuilderFakeFunctions.ResponseHeaderSetTargetedWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Request-Id"));
        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Served-By"));
        Assert.True(operation.Responses!["201"].Headers!.ContainsKey("X-Request-Id"));
        Assert.True(operation.Responses!["201"].Headers!.ContainsKey("X-Served-By"));
        Assert.False(operation.Responses!["400"].Headers?.ContainsKey("X-Request-Id") ?? false);
        Assert.False(operation.Responses!["400"].Headers?.ContainsKey("X-Served-By") ?? false);
    }

    [Fact]
    public void Populate_GenericRequestHeaderAttribute_CopiesDefinitionMetadata()
    {
        var endpoint = Endpoint("/api/widgets/request-headers/generic", "GET", nameof(PathsBuilderFakeFunctions.GenericRequestHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var header = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Correlation-Id"));
        Assert.Equal(ParameterLocation.Header, header.In);
        Assert.True(header.Required);
        Assert.False(header.Deprecated);
        Assert.Equal("Correlation identifier from generic request header.", header.Description);
        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("uuid", schema.Format);
    }

    [Fact]
    public void Populate_GenericRequestHeaderSetAttribute_UsesPolymorphicAttributeDiscovery()
    {
        var endpoint = Endpoint("/api/widgets/request-headers/generic-set", "GET", nameof(PathsBuilderFakeFunctions.GenericRequestHeaderSetWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var correlation = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Correlation-Id"));
        Assert.Equal(ParameterLocation.Header, correlation.In);
        Assert.True(correlation.Required);
        Assert.Equal("Correlation identifier from generic request header.", correlation.Description);

        var trace = Assert.IsType<OpenApiParameter>(operation.Parameters!.Single(p => p.Name == "X-Generic-Trace-Id"));
        Assert.Equal(ParameterLocation.Header, trace.In);
        Assert.False(trace.Required);
        Assert.True(trace.Deprecated);
        Assert.Equal("Trace identifier from generic request header set.", trace.Description);
        var traceSchema = Assert.IsType<OpenApiSchema>(trace.Schema);
        Assert.Equal(JsonSchemaType.String, traceSchema.Type);
        Assert.Null(traceSchema.Format);
    }

    [Fact]
    public void Populate_GenericResponseHeaderAttribute_CopiesDefinitionMetadataToTargetedStatus()
    {
        var endpoint = Endpoint("/api/widgets/response-headers/generic", "GET", nameof(PathsBuilderFakeFunctions.GenericResponseHeaderWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.False(operation.Responses!["200"].Headers?.ContainsKey("X-Processed-By") ?? false);

        var header = Assert.IsType<OpenApiHeader>(operation.Responses!["202"].Headers!["X-Processed-By"]);
        Assert.False(header.Required);
        Assert.True(header.Deprecated);
        Assert.Equal("Processing node from generic response header.", header.Description);
        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Null(schema.Format);
    }

    [Fact]
    public void Populate_GenericResponseHeaderSetAttribute_UsesPolymorphicAttributeDiscovery()
    {
        var endpoint = Endpoint("/api/widgets/response-headers/generic-set", "GET", nameof(PathsBuilderFakeFunctions.GenericResponseHeaderSetWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        foreach (var statusCode in new[] { "200", "202" })
        {
            var processedBy = Assert.IsType<OpenApiHeader>(operation.Responses![statusCode].Headers!["X-Processed-By"]);
            Assert.True(processedBy.Deprecated);
            Assert.Equal("Processing node from generic response header.", processedBy.Description);
            var processedBySchema = Assert.IsType<OpenApiSchema>(processedBy.Schema);
            Assert.Equal(JsonSchemaType.String, processedBySchema.Type);

            var requestId = Assert.IsType<OpenApiHeader>(operation.Responses![statusCode].Headers!["X-Generic-Request-Id"]);
            Assert.True(requestId.Required);
            Assert.False(requestId.Deprecated);
            Assert.Equal("Correlation identifier from generic response header set.", requestId.Description);
            var requestIdSchema = Assert.IsType<OpenApiSchema>(requestId.Schema);
            Assert.Equal(JsonSchemaType.String, requestIdSchema.Type);
            Assert.Equal("uuid", requestIdSchema.Format);
        }
    }

    [Fact]
    public void Populate_GenericResponseAttribute_MatchesNonGenericResponseOutput()
    {
        var genericEndpoint = Endpoint("/api/widgets/response/generic", "GET", nameof(PathsBuilderFakeFunctions.GenericResponseDefinitionWidget));
        var nonGenericEndpoint = Endpoint("/api/widgets/response/non-generic", "GET", nameof(PathsBuilderFakeFunctions.NonGenericResponseDefinitionWidget));

        var genericOperation = Populate(genericEndpoint, includeUnannotated: false, out var genericDocument);
        var nonGenericOperation = Populate(nonGenericEndpoint, includeUnannotated: false, out var nonGenericDocument);

        var genericResponse = genericOperation.Responses!["404"];
        var nonGenericResponse = nonGenericOperation.Responses!["404"];

        Assert.Equal("Not found.", genericResponse.Description);
        Assert.Equal(nonGenericResponse.Description, genericResponse.Description);

        var genericContent = genericResponse.Content!;
        var nonGenericContent = nonGenericResponse.Content!;
        Assert.True(genericContent.ContainsKey("application/json"));
        Assert.True(nonGenericContent.ContainsKey("application/json"));
        Assert.Equal(nonGenericContent.Keys, genericContent.Keys);

        var genericSchema = Assert.IsType<OpenApiSchemaReference>(genericContent["application/json"].Schema);
        var nonGenericSchema = Assert.IsType<OpenApiSchemaReference>(nonGenericContent["application/json"].Schema);
        Assert.Equal(nameof(NotFoundResponseBodyFixture), genericSchema.Reference!.Id);
        Assert.Equal(nonGenericSchema.Reference!.Id, genericSchema.Reference!.Id);

        Assert.True(genericDocument.Components!.Schemas!.ContainsKey(nameof(NotFoundResponseBodyFixture)));
        Assert.True(nonGenericDocument.Components!.Schemas!.ContainsKey(nameof(NotFoundResponseBodyFixture)));
    }

    [Fact]
    public void Populate_ResponseHeaderSet_EmptyStatusCodes_AttachesToAllPresentResponses()
    {
        var endpoint = Endpoint("/api/widgets/response-headers/all", "GET", nameof(PathsBuilderFakeFunctions.ResponseHeaderSetAllWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Request-Id"));
        Assert.True(operation.Responses!["200"].Headers!.ContainsKey("X-Served-By"));
        Assert.True(operation.Responses!["404"].Headers!.ContainsKey("X-Request-Id"));
        Assert.True(operation.Responses!["404"].Headers!.ContainsKey("X-Served-By"));
    }

    [Fact]
    public void Populate_ResponseHeaderSet_IndividualAttributeWinsOnCaseInsensitiveCollision()
    {
        var endpoint = Endpoint("/api/widgets/response-headers/collision", "GET", nameof(PathsBuilderFakeFunctions.ResponseHeaderSetCollisionWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var response200Headers = operation.Responses!["200"].Headers!;
        var headerName = Assert.Single(
            response200Headers.Keys,
            k => string.Equals(k, "X-Request-Id", StringComparison.OrdinalIgnoreCase));
        var header = Assert.IsType<OpenApiHeader>(response200Headers[headerName]);

        Assert.True(header.Required);
        Assert.True(header.Deprecated);
        Assert.Equal("Correlation identifier override.", header.Description);
        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("uuid", schema.Format);

        Assert.False(operation.Responses!["201"].Headers?.ContainsKey("X-Request-Id") ?? false);
    }

    [Fact]
    public void Populate_ResponseHeaders_DuplicateIndividualAttributes_WithDifferentCaseRemainDistinct()
    {
        var endpoint = Endpoint("/api/widgets/response-headers/duplicates", "GET", nameof(PathsBuilderFakeFunctions.ResponseHeaderDuplicatesWidget));

        var operation = Populate(endpoint, includeUnannotated: false, out _);

        var headers = operation.Responses!["200"].Headers!;

        Assert.Equal(2, headers.Count);
        Assert.True(headers.ContainsKey("X-Request-Id"));
        Assert.True(headers.ContainsKey("x-request-id"));
    }

    [Fact]
    public void Populate_MalformedHeaderSetCollection_DoesNotThrow_AndDoesNotBlockOtherEndpoints()
    {
        var malformed = Endpoint("/api/widgets/malformed-request-headers", "GET", nameof(PathsBuilderFakeFunctions.MalformedRequestHeaderSetWidget));
        var valid = Endpoint("/api/widgets/request-headers", "GET", nameof(PathsBuilderFakeFunctions.RequestHeaderSetWidget));
        var document = NewDocument();

        var exception = Record.Exception(() =>
            new OpenApiPathsBuilder().Populate(document, new[] { malformed, valid }, includeUnannotated: false));

        Assert.Null(exception);

        var validPath = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/widgets/request-headers"]);
        Assert.True(validPath.Operations!.ContainsKey(HttpMethod.Get));

        Assert.False(document.Paths.ContainsKey("/api/widgets/malformed-request-headers"));
    }

    [Fact]
    public void OpenApiResponseHeaderAttribute_NullStatusCodes_NormalizesToEmptyArray()
    {
        var attribute = new OpenApiResponseHeaderAttribute("X-Trace-Id", typeof(string), null!);

        Assert.NotNull(attribute.StatusCodes);
        Assert.Empty(attribute.StatusCodes);
    }

    [Fact]
    public void OpenApiResponseHeaderSetAttribute_NullStatusCodes_NormalizesToEmptyArray()
    {
        var attribute = new OpenApiResponseHeaderSetAttribute(typeof(ResponseHeaderSetFixture), null!);

        Assert.NotNull(attribute.StatusCodes);
        Assert.Empty(attribute.StatusCodes);
    }

    [Fact]
    public void OpenApiResponseAttribute_Generic_CopiesDefinitionMetadata()
    {
        var attribute = new OpenApiResponseAttribute<NotFoundResponseDefinitionFixture>();

        Assert.Equal(404, attribute.StatusCode);
        Assert.Equal(typeof(NotFoundResponseBodyFixture), attribute.Type);
        Assert.Equal("application/json", attribute.ContentType);
        Assert.Equal("Not found.", attribute.Description);
    }
}
