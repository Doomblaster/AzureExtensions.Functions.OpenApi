namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Defines a reusable response declaration for OpenAPI discovery.
/// </summary>
/// <remarks>
/// Implementations are metadata-only contracts that describe a response once so it can be reused
/// across multiple endpoints via <see cref="OpenApiResponseAttribute{T}"/>.
/// </remarks>
public interface IOpenApiResponseDefinition
{
    /// <summary>
    /// The HTTP status code this response documents.
    /// </summary>
    int StatusCode { get; }

    /// <summary>
    /// The CLR type used to derive the response body schema. A <see langword="null"/> value
    /// indicates a response with no body.
    /// </summary>
    Type? Type { get; }

    /// <summary>
    /// The media type of the response body. Ignored when <see cref="Type"/> is
    /// <see langword="null"/>, since a bodyless response has no content to describe.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Optional description of the response.
    /// </summary>
    string? Description { get; }
}
