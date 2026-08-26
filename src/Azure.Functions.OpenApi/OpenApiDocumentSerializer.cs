using System.Globalization;
using System.Text;
using Microsoft.OpenApi;

namespace Azure.Functions.OpenApi;

/// <summary>
/// Serializes an <see cref="OpenApiDocument"/> to JSON or YAML text using Microsoft.OpenApi's
/// own writers. No hand-rolled JSON/YAML is produced — the library controls the exact wire format
/// (including the <c>openapi</c> version field) for the requested <see cref="OpenApiSpecVersion"/>.
/// </summary>
/// <remarks>
/// This is the seam the HTTP trigger (Functions) uses: resolve the document from
/// <see cref="IOpenApiDocumentProvider"/>, then call <see cref="SerializeJson"/> or
/// <see cref="SerializeYaml"/> at <see cref="OpenApiOptions.SpecVersion"/>.
/// </remarks>
internal static class OpenApiDocumentSerializer
{
    /// <summary>
    /// Serializes <paramref name="document"/> to a JSON string at the requested spec version.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <param name="specVersion">The OpenAPI Specification version to emit.</param>
    /// <returns>The document serialized as JSON.</returns>
    public static string SerializeJson(OpenApiDocument document, OpenApiSpecVersion specVersion)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var stringWriter = new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);
        var writer = new OpenApiJsonWriter(stringWriter);
        document.SerializeAs(specVersion, writer);
        stringWriter.Flush();
        return stringWriter.ToString();
    }

    /// <summary>
    /// Serializes <paramref name="document"/> to a YAML string at the requested spec version.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <param name="specVersion">The OpenAPI Specification version to emit.</param>
    /// <returns>The document serialized as YAML.</returns>
    public static string SerializeYaml(OpenApiDocument document, OpenApiSpecVersion specVersion)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var stringWriter = new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);
        var writer = new OpenApiYamlWriter(stringWriter);
        document.SerializeAs(specVersion, writer);
        stringWriter.Flush();
        return stringWriter.ToString();
    }
}
