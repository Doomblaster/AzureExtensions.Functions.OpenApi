using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Azure.Functions.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using SampleFunctionApp.Models;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// End-to-end tests: build a document over the real <c>SampleFunctionApp</c> assembly and assert
/// the CRUD surface, component schemas, meta-endpoint exclusion, and valid OpenAPI 3.1 serialization.
/// </summary>
public sealed class DocumentGenerationTests
{
    private static async Task<OpenApiDocument> BuildSampleDocumentAsync()
    {
        var options = new OpenApiOptions
        {
            Title = "Sample API",
            Version = "1.0.0",
        };
        options.DocumentAssemblies.Add(typeof(Item).Assembly);

        var provider = new OpenApiDocumentProvider(Options.Create(options));
        return await provider.GetDocumentAsync();
    }

    [Fact]
    public async Task Document_ContainsCrudPaths()
    {
        var document = await BuildSampleDocumentAsync();

        Assert.True(document.Paths!.ContainsKey("/api/items"));
        Assert.True(document.Paths!.ContainsKey("/api/items/{id}"));
    }

    [Fact]
    public async Task Document_ContainsExpectedOperationsPerPath()
    {
        var document = await BuildSampleDocumentAsync();

        var items = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items"]);
        var itemById = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items/{id}"]);

        Assert.True(items.Operations!.ContainsKey(HttpMethod.Get));   // ListItems
        Assert.True(items.Operations!.ContainsKey(HttpMethod.Post));  // CreateItem
        Assert.True(itemById.Operations!.ContainsKey(HttpMethod.Get));    // GetItem
        Assert.True(itemById.Operations!.ContainsKey(HttpMethod.Put));    // UpdateItem
        Assert.True(itemById.Operations!.ContainsKey(HttpMethod.Delete)); // DeleteItem
    }

    [Fact]
    public async Task Document_RegistersModelComponentSchemas()
    {
        var document = await BuildSampleDocumentAsync();

        var schemas = document.Components!.Schemas!;
        Assert.True(schemas.ContainsKey(nameof(Item)));
        Assert.True(schemas.ContainsKey(nameof(ItemDimensions)));
        Assert.True(schemas.ContainsKey(nameof(CreateItemRequest)));
        Assert.True(schemas.ContainsKey(nameof(UpdateItemRequest)));
    }

    [Fact]
    public async Task Document_HoistsEnumToSingleComponent_ReferencedByModels()
    {
        var document = await BuildSampleDocumentAsync();

        var schemas = document.Components!.Schemas!;

        // ItemStatus is used at four sites (status query param + Item/CreateItemRequest/
        // UpdateItemRequest) but must be registered exactly once as a component.
        Assert.Single(schemas.Keys, k => k == nameof(ItemStatus));

        var enumSchema = Assert.IsType<OpenApiSchema>(schemas[nameof(ItemStatus)]);
        Assert.Equal(JsonSchemaType.String, enumSchema.Type);
        Assert.NotNull(enumSchema.Enum);
        var names = enumSchema.Enum!.Select(n => n!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "Active", "Discontinued", "Backordered" }, names);

        // Every use site emits a $ref to the single component.
        var item = Assert.IsType<OpenApiSchema>(schemas[nameof(Item)]);
        var statusRef = Assert.IsType<OpenApiSchemaReference>(item.Properties![nameof(Item.Status)]);
        Assert.Equal(nameof(ItemStatus), statusRef.Reference!.Id);
    }

    [Fact]
    public async Task Document_ModelsNullableNestedObjectAsAnyOfRefPlusNull()
    {
        var document = await BuildSampleDocumentAsync();

        var item = Assert.IsType<OpenApiSchema>(document.Components!.Schemas![nameof(Item)]);
        var dimensions = Assert.IsType<OpenApiSchema>(item.Properties![nameof(Item.Dimensions)]);

        Assert.NotNull(dimensions.AnyOf);
        Assert.Equal(2, dimensions.AnyOf!.Count);

        var dimRef = Assert.IsType<OpenApiSchemaReference>(dimensions.AnyOf[0]);
        Assert.Equal(nameof(ItemDimensions), dimRef.Reference!.Id);

        var nullMember = Assert.IsType<OpenApiSchema>(dimensions.AnyOf[1]);
        Assert.Equal(JsonSchemaType.Null, nullMember.Type);
    }

    [Fact]
    public async Task Document_MapsTemporalAndGuidItemProperties()
    {
        var document = await BuildSampleDocumentAsync();

        var item = Assert.IsType<OpenApiSchema>(document.Components!.Schemas![nameof(Item)]);

        var publicId = Assert.IsType<OpenApiSchema>(item.Properties![nameof(Item.PublicId)]);
        Assert.Equal(JsonSchemaType.String, publicId.Type);
        Assert.Equal("uuid", publicId.Format);

        var createdAt = Assert.IsType<OpenApiSchema>(item.Properties![nameof(Item.CreatedAt)]);
        Assert.Equal(JsonSchemaType.String, createdAt.Type);
        Assert.Equal("date-time", createdAt.Format);

        var releaseDate = Assert.IsType<OpenApiSchema>(item.Properties![nameof(Item.ReleaseDate)]);
        Assert.Equal(JsonSchemaType.String, releaseDate.Type);
        Assert.Equal("date", releaseDate.Format);

        var restockTime = Assert.IsType<OpenApiSchema>(item.Properties![nameof(Item.RestockTime)]);
        Assert.Equal(JsonSchemaType.String, restockTime.Type);
        Assert.Equal("time", restockTime.Format);

        // Nullable DateTime stays inline as ["string","null"] with the format preserved.
        var discontinuedAt = Assert.IsType<OpenApiSchema>(item.Properties![nameof(Item.DiscontinuedAt)]);
        Assert.True(discontinuedAt.Type!.Value.HasFlag(JsonSchemaType.String));
        Assert.True(discontinuedAt.Type!.Value.HasFlag(JsonSchemaType.Null));
        Assert.Equal("date-time", discontinuedAt.Format);
    }

    [Fact]
    public async Task Document_ExcludesLibraryMetaEndpoints()
    {
        var document = await BuildSampleDocumentAsync();

        Assert.DoesNotContain(document.Paths!.Keys, k => k.Contains("openapi.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(document.Paths!.Keys, k => k.Contains("openapi.yaml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Document_SerializesToValidOpenApi31Json()
    {
        var document = await BuildSampleDocumentAsync();

        var json = OpenApiDocumentSerializer.SerializeJson(document, OpenApiSpecVersion.OpenApi3_1);

        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        Assert.StartsWith("3.1", root.GetProperty("openapi").GetString());

        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/items", out _));
        Assert.True(paths.TryGetProperty("/api/items/{id}", out _));

        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty(nameof(Item), out _));
    }

    [Fact]
    public async Task Document_GetItemById_Has404ProblemDetailsResponse()
    {
        var document = await BuildSampleDocumentAsync();

        var itemById = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items/{id}"]);
        var getItem = itemById.Operations![HttpMethod.Get];

        var response = getItem.Responses!["404"];
        var content = response.Content!;
        Assert.True(content.ContainsKey("application/problem+json"));

        var reference = Assert.IsType<OpenApiSchemaReference>(content["application/problem+json"].Schema);
        Assert.Equal("ProblemDetails", reference.Reference!.Id);
    }

    [Fact]
    public async Task Document_SearchItems_HasRequiredNameQueryParam()
    {
        var document = await BuildSampleDocumentAsync();

        var search = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items/search"]);
        var operation = search.Operations![HttpMethod.Get];

        var name = operation.Parameters!.Single(p => p.Name == "name");
        Assert.Equal(ParameterLocation.Query, name.In);
        Assert.True(name.Required);
    }

    [Fact]
    public async Task Document_SearchItems_Has400ValidationProblemAnd200List()
    {
        var document = await BuildSampleDocumentAsync();

        var search = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items/search"]);
        var operation = search.Operations![HttpMethod.Get];

        // 400 -> HttpValidationProblemDetails served as application/problem+json.
        var badRequest = operation.Responses!["400"].Content!;
        Assert.True(badRequest.ContainsKey("application/problem+json"));
        var problemRef = Assert.IsType<OpenApiSchemaReference>(badRequest["application/problem+json"].Schema);
        Assert.Equal("HttpValidationProblemDetails", problemRef.Reference!.Id);

        // 200 -> the items list.
        var ok = operation.Responses!["200"].Content!;
        Assert.True(ok.ContainsKey("application/json"));
        var okSchema = Assert.IsType<OpenApiSchema>(ok["application/json"].Schema);
        Assert.Equal(JsonSchemaType.Array, okSchema.Type);
        var itemRef = Assert.IsType<OpenApiSchemaReference>(okSchema.Items);
        Assert.Equal(nameof(Item), itemRef.Reference!.Id);
    }

    [Fact]
    public async Task Document_RegistersProblemDetailsComponent_ExactlyOnce()
    {
        var document = await BuildSampleDocumentAsync();

        Assert.Single(document.Components!.Schemas!.Keys, k => k == "ProblemDetails");
    }

    [Fact]
    public async Task Document_CreateItem_Has201LocationResponseHeader()
    {
        var document = await BuildSampleDocumentAsync();

        var items = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items"]);
        var create = items.Operations![HttpMethod.Post];

        var header = Assert.IsType<OpenApiHeader>(create.Responses!["201"].Headers!["Location"]);
        Assert.Equal("URL of the newly created item.", header.Description);

        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("uri", schema.Format);
    }

    [Fact]
    public async Task Document_ListItems_Has200RateLimitResponseHeader()
    {
        var document = await BuildSampleDocumentAsync();

        var items = Assert.IsType<OpenApiPathItem>(document.Paths!["/api/items"]);
        var list = items.Operations![HttpMethod.Get];

        var header = Assert.IsType<OpenApiHeader>(list.Responses!["200"].Headers!["X-RateLimit-Remaining"]);

        var schema = Assert.IsType<OpenApiSchema>(header.Schema);
        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Equal("int32", schema.Format);
    }

    [Fact]
    public async Task Document_ViaAddOpenApi_ResolvesProviderFromDi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenApi(o =>
        {
            o.Title = "DI Sample API";
            o.DocumentAssemblies.Add(typeof(Item).Assembly);
        });

        using var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IOpenApiDocumentProvider>();

        var document = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Equal("DI Sample API", document.Info!.Title);
        Assert.True(document.Paths!.ContainsKey("/api/items"));
    }
}
