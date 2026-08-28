namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Exposes a reusable collection of request-header definitions for OpenAPI discovery.
/// </summary>
public interface IOpenApiRequestHeaderDefinitionCollection
{
    /// <summary>
    /// The request headers declared by this collection.
    /// </summary>
    IReadOnlyList<IOpenApiRequestHeaderDefinition> Headers { get; }
}
