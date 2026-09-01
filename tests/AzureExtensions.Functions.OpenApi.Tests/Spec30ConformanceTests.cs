using System.Collections.Generic;
using System.Text.Json;
using Microsoft.OpenApi;
using AzureExtensions.Functions.OpenApi.Schema;
using Xunit;

namespace AzureExtensions.Functions.OpenApi.Tests;

/// <summary>
/// Guards that serializing at <see cref="OpenApiSpecVersion.OpenApi3_0"/> emits a document that
/// conforms to OpenAPI 3.0.x — i.e. Microsoft.OpenApi's down-converter rewrites the 3.1/JSON-Schema
/// nullability the schema generator builds (<c>"type":[x,"null"]</c> and
/// <c>anyOf:[$ref,{"type":"null"}]</c>) into legal 3.0 (<c>nullable:true</c>, no <c>type</c> arrays,
/// no bare <c>"type":"null"</c>). Locks in the behavior relied on by <see cref="OpenApiOptions.SpecVersion"/>.
/// </summary>
public sealed class Spec30ConformanceTests
{
    private enum Color { Red, Green, Blue }

    private sealed class Nested
    {
        public int Count { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private sealed class Node
    {
        public string Name { get; set; } = string.Empty;
        public Node? Parent { get; set; }
    }

    private sealed class Host
    {
        public Color? Favorite { get; set; }
        public Color Status { get; set; }
        public Nested? Child { get; set; }
        public Nested Mandatory { get; set; } = new();
        public Node? Root { get; set; }
        public string? Nickname { get; set; }
        public string Required { get; set; } = string.Empty;
        public int? Age { get; set; }
        public decimal Amount { get; set; }
    }

    private static JsonElement SerializeHostAs(OpenApiSpecVersion version)
    {
        var components = new OpenApiComponents { Schemas = new Dictionary<string, IOpenApiSchema>() };
        new OpenApiSchemaGenerator().GetOrCreateSchema(typeof(Host), components);

        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Conformance", Version = "1.0.0" },
            Paths = new OpenApiPaths(),
            Components = components,
        };

        var json = OpenApiDocumentSerializer.SerializeJson(document, version);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static IEnumerable<JsonElement> DescendantsAndSelf(JsonElement element)
    {
        yield return element;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var descendant in DescendantsAndSelf(property.Value))
                    {
                        yield return descendant;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var descendant in DescendantsAndSelf(item))
                    {
                        yield return descendant;
                    }
                }

                break;
        }
    }

    [Fact]
    public void ThreePointZero_ReportsThreePointZeroVersion()
    {
        var root = SerializeHostAs(OpenApiSpecVersion.OpenApi3_0);

        Assert.StartsWith("3.0", root.GetProperty("openapi").GetString());
    }

    [Fact]
    public void ThreePointZero_NeverEmitsTypeArrays()
    {
        // "type" as an array (e.g. ["string","null"]) is JSON-Schema-2020-12 / 3.1 only; it is
        // invalid in 3.0 where "type" must be a single string.
        var root = SerializeHostAs(OpenApiSpecVersion.OpenApi3_0);

        foreach (var node in DescendantsAndSelf(root))
        {
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("type", out var type))
            {
                Assert.NotEqual(JsonValueKind.Array, type.ValueKind);
            }
        }
    }

    [Fact]
    public void ThreePointZero_NeverEmitsNullType()
    {
        // Bare {"type":"null"} is invalid in 3.0; nullability must use "nullable": true instead.
        var root = SerializeHostAs(OpenApiSpecVersion.OpenApi3_0);

        foreach (var node in DescendantsAndSelf(root))
        {
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String)
            {
                Assert.NotEqual("null", type.GetString());
            }
        }
    }

    [Fact]
    public void ThreePointZero_NullableScalar_UsesNullableTrue()
    {
        var root = SerializeHostAs(OpenApiSpecVersion.OpenApi3_0);
        var nickname = root
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("Host").GetProperty("properties").GetProperty("Nickname");

        Assert.Equal("string", nickname.GetProperty("type").GetString());
        Assert.True(nickname.GetProperty("nullable").GetBoolean());
    }

    [Fact]
    public void ThreePointOne_StillUsesJsonSchemaNullability()
    {
        // Sanity counter-check: the 3.1 output deliberately keeps the JSON-Schema encoding, so the
        // 3.0 rewrite above is genuinely the serializer down-converting (not a build-time change).
        var root = SerializeHostAs(OpenApiSpecVersion.OpenApi3_1);
        var nickname = root
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("Host").GetProperty("properties").GetProperty("Nickname");

        Assert.Equal(JsonValueKind.Array, nickname.GetProperty("type").ValueKind);
    }
}
