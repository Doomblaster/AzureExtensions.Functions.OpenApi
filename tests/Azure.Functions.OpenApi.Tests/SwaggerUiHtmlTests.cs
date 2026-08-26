using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Unit tests for <see cref="SwaggerUiHtml.Build"/>. These assert purely on the produced markup:
/// that the pinned CDN asset URLs are embedded, the OpenAPI JSON URL is wired into the
/// <c>SwaggerUIBundle</c> init, the document skeleton is valid, the page title is HTML-encoded,
/// and the JSON URL is emitted as a safely-escaped JS string literal.
/// </summary>
public sealed class SwaggerUiHtmlTests
{
    private const string CdnBase = "https://cdn.jsdelivr.net/npm/swagger-ui-dist";
    private const string Version = "5.32.14";

    [Fact]
    public void Build_EmbedsPinnedCdnAssetUrls()
    {
        var html = SwaggerUiHtml.Build("https://localhost/api/openapi.json", CdnBase, Version, "My API");

        Assert.Contains($"{CdnBase}@{Version}/swagger-ui.css", html);
        Assert.Contains($"{CdnBase}@{Version}/swagger-ui-bundle.js", html);
        Assert.Contains($"{CdnBase}@{Version}/swagger-ui-standalone-preset.js", html);

        // The literal pinned version must appear in the asset path.
        Assert.Contains("swagger-ui-dist@5.32.14/swagger-ui.css", html);
    }

    [Fact]
    public void Build_ReferencesProvidedJsonUrlInBundleInit()
    {
        const string jsonUrl = "https://localhost/api/openapi.json";

        var html = SwaggerUiHtml.Build(jsonUrl, CdnBase, Version, "My API");

        Assert.Contains("SwaggerUIBundle({", html);
        Assert.Contains($"url: \"{jsonUrl}\"", html);
    }

    [Fact]
    public void Build_ProducesValidSkeleton()
    {
        var html = SwaggerUiHtml.Build("https://localhost/api/openapi.json", CdnBase, Version, "My API");

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("id=\"swagger-ui\"", html);
        Assert.Contains("StandaloneLayout", html);
        Assert.Contains("SwaggerUIStandalonePreset", html);
    }

    [Fact]
    public void Build_HtmlEncodesPageTitle()
    {
        var html = SwaggerUiHtml.Build("https://localhost/api/openapi.json", CdnBase, Version, "Fish & Chips <script>");

        Assert.Contains("<title>Fish &amp; Chips &lt;script&gt;</title>", html);
        // The raw, unencoded title must never appear inside the title element context.
        Assert.DoesNotContain("<title>Fish & Chips <script></title>", html);
    }

    [Fact]
    public void Build_EmitsJsonUrlAsSafeJsStringLiteral()
    {
        // A quote in the URL must be escaped so it cannot break out of the JS string literal.
        const string maliciousUrl = "https://localhost/api/openapi.json\";alert(1);//";

        var html = SwaggerUiHtml.Build(maliciousUrl, CdnBase, Version, "My API");

        // JsonSerializer's default encoder escapes the embedded double quote (as \u0022), so the
        // unescaped break-out sequence must not appear verbatim in the output.
        Assert.Contains("\\u0022", html);
        Assert.DoesNotContain("openapi.json\";alert(1);//", html);
    }
}
