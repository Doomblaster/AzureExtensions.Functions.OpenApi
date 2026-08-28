namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Defines a reusable header declaration for OpenAPI discovery.
/// </summary>
/// <remarks>
/// Implementations are metadata-only contracts that describe a header once so it can be reused
/// for either request-header parameters or response-header objects.
/// </remarks>
public interface IOpenApiHeaderDefinition
{
    /// <summary>
    /// The header name as it appears on the wire.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The CLR type used to derive the header schema.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Optional description of the header.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// When <see langword="true"/>, the header is required.
    /// </summary>
    bool Required { get; }

    /// <summary>
    /// When <see langword="true"/>, the header is deprecated.
    /// </summary>
    bool Deprecated { get; }
}
