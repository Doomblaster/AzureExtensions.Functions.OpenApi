using System.Net;
using System.Text.Json;

namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Builds the minimal HTML shell that loads Swagger UI from a CDN and renders an interactive
/// client pointed at the OpenAPI JSON document. The assets themselves are not embedded; only the
/// page markup and initialization script are produced here.
/// </summary>
internal static class SwaggerUiHtml
{
    /// <summary>
    /// Builds the Swagger UI HTML page.
    /// </summary>
    /// <param name="openApiJsonUrl">The URL Swagger UI should fetch the OpenAPI document from.</param>
    /// <param name="cdnBaseUrl">The CDN base URL for the <c>swagger-ui-dist</c> package (no trailing version).</param>
    /// <param name="version">The pinned <c>swagger-ui-dist</c> version appended to <paramref name="cdnBaseUrl"/>.</param>
    /// <param name="pageTitle">The browser tab title for the page.</param>
    /// <returns>A complete, standalone HTML document as a string.</returns>
    public static string Build(string openApiJsonUrl, string cdnBaseUrl, string version, string pageTitle)
    {
        // The CDN base + version are trusted configuration, so plain concatenation is fine.
        var assetBase = $"{cdnBaseUrl}@{version}";

        // pageTitle is HTML-encoded for the element context; the JSON URL is emitted as a JS string
        // literal via JsonSerializer so it is safely escaped for the double-quoted script context.
        var encodedTitle = WebUtility.HtmlEncode(pageTitle);
        var jsonUrlLiteral = JsonSerializer.Serialize(openApiJsonUrl);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{encodedTitle}}</title>
              <link rel="stylesheet" href="{{assetBase}}/swagger-ui.css">
            </head>
            <body>
              <div id="swagger-ui"></div>
              <script src="{{assetBase}}/swagger-ui-bundle.js"></script>
              <script src="{{assetBase}}/swagger-ui-standalone-preset.js"></script>
              <script>
                window.onload = function () {
                  window.ui = SwaggerUIBundle({
                    url: {{jsonUrlLiteral}},
                    dom_id: '#swagger-ui',
                    presets: [SwaggerUIBundle.presets.apis, SwaggerUIStandalonePreset],
                    layout: "StandaloneLayout"
                  });
                };
              </script>
            </body>
            </html>
            """;
    }
}
