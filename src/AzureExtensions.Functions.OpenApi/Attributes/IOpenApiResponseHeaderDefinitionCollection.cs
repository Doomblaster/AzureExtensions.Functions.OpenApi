namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Exposes a reusable collection of response-header definitions for OpenAPI discovery.
/// </summary>
public interface IOpenApiResponseHeaderDefinitionCollection
{
    /// <summary>
    /// The response headers declared by this collection.
    /// </summary>
    IReadOnlyList<IOpenApiResponseHeaderDefinition> Headers { get; }
}
