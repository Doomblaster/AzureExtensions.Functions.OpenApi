namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Defines a reusable request-header declaration for OpenAPI discovery.
/// </summary>
public interface IOpenApiRequestHeaderDefinition
{
    /// <summary>
    /// The name of the header as it appears in the request.
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
}
