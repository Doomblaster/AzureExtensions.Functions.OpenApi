namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Exposes a reusable collection of header definitions for OpenAPI discovery.
/// </summary>
/// <remarks>
/// A single collection can be consumed by either request-header parameter sets or response-header
/// sets.
/// </remarks>
public interface IOpenApiHeaderDefinitionCollection
{
    /// <summary>
    /// The headers declared by this reusable collection.
    /// </summary>
    IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; }
}
