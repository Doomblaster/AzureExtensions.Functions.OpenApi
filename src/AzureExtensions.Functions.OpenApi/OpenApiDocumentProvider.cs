using System.Reflection;
using AzureExtensions.Functions.OpenApi.Discovery;
using AzureExtensions.Functions.OpenApi.Generation;
using AzureExtensions.Functions.OpenApi.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Default <see cref="IOpenApiDocumentProvider"/> that builds an <see cref="OpenApiDocument"/>
/// in code using the Microsoft.OpenApi object model.
/// </summary>
internal sealed class OpenApiDocumentProvider : IOpenApiDocumentProvider
{
    private readonly OpenApiOptions _options;
    private readonly ILogger<OpenApiDocumentProvider>? _logger;
    private OpenApiDocument? _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiDocumentProvider"/> class.
    /// </summary>
    /// <param name="options">The configured OpenAPI options.</param>
    /// <param name="logger">
    /// An optional logger. When resolved from DI it captures discovery/build failures; the
    /// provider degrades gracefully (returns a path-less document) when it is <see langword="null"/>.
    /// </param>
    public OpenApiDocumentProvider(
        IOptions<OpenApiOptions> options,
        ILogger<OpenApiDocumentProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<OpenApiDocument> GetDocumentAsync(CancellationToken cancellationToken = default)
    {
        // The document is effectively immutable per process, so build it once and cache it.
        // Consumers who need dynamic content can replace this provider in DI.
        var document = _document ??= BuildDocument();
        return new ValueTask<OpenApiDocument>(document);
    }

    private OpenApiDocument BuildDocument()
    {
        var info = new OpenApiInfo
        {
            Title = _options.Title,
            Version = _options.Version,
        };

        if (!string.IsNullOrWhiteSpace(_options.Description))
        {
            info.Description = _options.Description;
        }

        // An empty-but-present Paths object is required for a valid OpenAPI 3.x document, and the
        // 3.10.2 model expects Components.Schemas to be a non-null dictionary before the schema
        // generator registers component references into it.
        var document = new OpenApiDocument
        {
            Info = info,
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(),
            },
        };

        // Discovery + population reflect over consumer assemblies at runtime. If anything throws
        // (e.g. an assembly fails to load its types), we still return a valid, path-less document
        // rather than turning an HTTP request for the spec into a 500.
        try
        {
            var discovery = new FunctionEndpointDiscovery();

            IEnumerable<Assembly> assemblies = _options.DocumentAssemblies.Count > 0
                ? _options.DocumentAssemblies
                : discovery.GetDefaultAssemblies();

            var endpoints = discovery.Discover(assemblies);

            // The default assembly scan can include this library's own assembly, which declares the
            // openapi.json/openapi.yaml meta-endpoints. The served spec must describe the CONSUMER's
            // API, not the endpoints that emit the spec, so drop anything declared in our assembly.
            var libraryAssembly = typeof(OpenApiDocumentProvider).Assembly;
            var consumerEndpoints = endpoints
                .Where(e => e.Method.DeclaringType?.Assembly != libraryAssembly)
                .ToList();

            var builder = new OpenApiPathsBuilder(new OpenApiSchemaGenerator());
            builder.Populate(document, consumerEndpoints, _options.IncludeUnannotatedEndpoints);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to discover or populate OpenAPI endpoints; serving a document without paths.");
        }

        return document;
    }
}
