using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Azure.Functions.OpenApi;

/// <summary>
/// Registration entry point for the Azure.Functions.OpenApi library.
/// </summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAPI document provider and options so that the OpenAPI HTTP endpoints
    /// (for example <c>GET /api/openapi.json</c> and <c>GET /api/openapi.yaml</c>) are available
    /// in an Azure Functions isolated worker app.
    /// </summary>
    /// <param name="services">The DI service collection to add registrations to.</param>
    /// <param name="configure">An optional delegate to configure <see cref="OpenApiOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddOpenApi(this IServiceCollection services, Action<OpenApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            // Ensure OpenApiOptions is bindable/resolvable even without explicit configuration.
            services.AddOptions<OpenApiOptions>();
        }

        services.TryAddSingleton<IOpenApiDocumentProvider, OpenApiDocumentProvider>();

        return services;
    }
}
