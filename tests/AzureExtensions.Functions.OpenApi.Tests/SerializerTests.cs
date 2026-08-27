using System.Text.Json;
using Microsoft.OpenApi;
using Xunit;

namespace AzureExtensions.Functions.OpenApi.Tests;

/// <summary>
/// Tests for <see cref="OpenApiDocumentSerializer"/> JSON/YAML output and spec-version fidelity.
/// </summary>
public sealed class SerializerTests
{
    private static OpenApiDocument BuildDocument(string title = "Serializer API", string version = "1.2.3")
        => new()
        {
            Info = new OpenApiInfo { Title = title, Version = version },
            Paths = new OpenApiPaths(),
        };

    [Fact]
    public void SerializeJson_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(() => OpenApiDocumentSerializer.SerializeJson(null!, OpenApiSpecVersion.OpenApi3_1));

    [Fact]
    public void SerializeYaml_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(() => OpenApiDocumentSerializer.SerializeYaml(null!, OpenApiSpecVersion.OpenApi3_1));

    [Theory]
    [InlineData(OpenApiSpecVersion.OpenApi3_1, "3.1")]
    [InlineData(OpenApiSpecVersion.OpenApi3_0, "3.0")]
    public void SerializeJson_ProducesParseableJson_WithExpectedOpenApiVersionPrefix(OpenApiSpecVersion specVersion, string expectedPrefix)
    {
        var json = OpenApiDocumentSerializer.SerializeJson(BuildDocument(), specVersion);

        Assert.False(string.IsNullOrWhiteSpace(json));

        using var doc = JsonDocument.Parse(json);
        var openapi = doc.RootElement.GetProperty("openapi").GetString();

        Assert.NotNull(openapi);
        Assert.StartsWith(expectedPrefix, openapi);
    }

    [Fact]
    public void SerializeJson_ContainsInfoWithConfiguredTitleAndVersion()
    {
        var json = OpenApiDocumentSerializer.SerializeJson(BuildDocument("Round Trip API", "9.9.9"), OpenApiSpecVersion.OpenApi3_1);

        using var doc = JsonDocument.Parse(json);
        var info = doc.RootElement.GetProperty("info");

        Assert.Equal("Round Trip API", info.GetProperty("title").GetString());
        Assert.Equal("9.9.9", info.GetProperty("version").GetString());
    }

    [Theory]
    [InlineData(OpenApiSpecVersion.OpenApi3_1, "3.1")]
    [InlineData(OpenApiSpecVersion.OpenApi3_0, "3.0")]
    public void SerializeYaml_ProducesNonEmptyYaml_WithOpenApiVersionLine(OpenApiSpecVersion specVersion, string expectedPrefix)
    {
        var yaml = OpenApiDocumentSerializer.SerializeYaml(BuildDocument(), specVersion);

        Assert.False(string.IsNullOrWhiteSpace(yaml));

        var openapiLine = yaml
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.StartsWith("openapi:", StringComparison.Ordinal));

        Assert.NotNull(openapiLine);
        var value = openapiLine!["openapi:".Length..].Trim().Trim('\'', '"');
        Assert.StartsWith(expectedPrefix, value);
    }

    [Fact]
    public void SerializeYaml_ContainsConfiguredTitle()
    {
        var yaml = OpenApiDocumentSerializer.SerializeYaml(BuildDocument("Yaml Title API"), OpenApiSpecVersion.OpenApi3_1);

        Assert.Contains("Yaml Title API", yaml);
    }
}
