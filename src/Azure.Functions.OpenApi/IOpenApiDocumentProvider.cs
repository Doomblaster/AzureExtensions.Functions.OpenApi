using Microsoft.OpenApi;

namespace Azure.Functions.OpenApi;

/// <summary>
/// Builds the <see cref="OpenApiDocument"/> that is served by the OpenAPI endpoints.
/// </summary>
/// <remarks>
/// Implementations are resolved from DI by the HTTP trigger. The default implementation is
/// registered by <see cref="OpenApiServiceCollectionExtensions.AddOpenApi(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{OpenApiOptions}?)"/>.
/// </remarks>
public interface IOpenApiDocumentProvider
{
    /// <summary>
    /// Builds (or returns a cached) OpenAPI document describing the consuming Functions app.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while building the document.</param>
    /// <returns>The fully populated <see cref="OpenApiDocument"/>.</returns>
    ValueTask<OpenApiDocument> GetDocumentAsync(CancellationToken cancellationToken = default);
}
