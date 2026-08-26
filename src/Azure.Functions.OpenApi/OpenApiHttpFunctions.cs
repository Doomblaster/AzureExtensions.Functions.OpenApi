using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Azure.Functions.OpenApi;

/// <summary>
/// HTTP-triggered functions that serve the OpenAPI document as JSON and YAML.
/// </summary>
/// <remarks>
/// This type lives in the library so that merely referencing the package and calling
/// <see cref="OpenApiServiceCollectionExtensions.AddOpenApi(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{OpenApiOptions}?)"/>
/// contributes the endpoints. The document is built by <see cref="IOpenApiDocumentProvider"/>
/// (Backend) and serialized here at the configured <see cref="OpenApiOptions.SpecVersion"/>.
/// </remarks>
public sealed class OpenApiHttpFunctions
{
    private const string JsonContentType = "application/json";
    private const string YamlContentType = "application/yaml";

    private readonly IOpenApiDocumentProvider _documentProvider;
    private readonly OpenApiOptions _options;
    private readonly ILogger<OpenApiHttpFunctions> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiHttpFunctions"/> class.
    /// </summary>
    /// <param name="documentProvider">The provider that builds the OpenAPI document.</param>
    /// <param name="options">The OpenAPI options (controls spec version and routing).</param>
    /// <param name="logger">The logger.</param>
    public OpenApiHttpFunctions(
        IOpenApiDocumentProvider documentProvider,
        IOptions<OpenApiOptions> options,
        ILogger<OpenApiHttpFunctions> logger)
    {
        ArgumentNullException.ThrowIfNull(documentProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _documentProvider = documentProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Serves the OpenAPI document as JSON at <c>GET /api/openapi.json</c>.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>An HTTP result containing the serialized JSON document.</returns>
    [Function("GetOpenApiJson")]
    public async Task<IResult> GetOpenApiJson(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi.json")] HttpRequest request)
    {
        _logger.LogDebug("Serving OpenAPI document as JSON at spec version {SpecVersion}.", _options.SpecVersion);

        var document = await _documentProvider.GetDocumentAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
        var json = Serialize(document, "json", _options.SpecVersion);

        return Results.Text(json, JsonContentType, statusCode: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Serves the OpenAPI document as YAML at <c>GET /api/openapi.yaml</c>.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>An HTTP result containing the serialized YAML document.</returns>
    [Function("GetOpenApiYaml")]
    public async Task<IResult> GetOpenApiYaml(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi.yaml")] HttpRequest request)
    {
        _logger.LogDebug("Serving OpenAPI document as YAML at spec version {SpecVersion}.", _options.SpecVersion);

        var document = await _documentProvider.GetDocumentAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
        var yaml = Serialize(document, "yaml", _options.SpecVersion);

        return Results.Text(yaml, YamlContentType, statusCode: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Serves an interactive Swagger UI page at <c>GET /api/swagger</c>. The Swagger UI assets are
    /// loaded from the configured CDN (not embedded); the page is pointed at the JSON document
    /// endpoint. Responds with <c>404 Not Found</c> when <see cref="OpenApiOptions.EnableSwaggerUi"/>
    /// is <see langword="false"/>.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <returns>An HTTP result containing the Swagger UI HTML page, or a not-found result.</returns>
    [Function("GetSwaggerUi")]
    public IResult GetSwaggerUi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger")] HttpRequest request)
    {
        if (!_options.EnableSwaggerUi)
        {
            return Results.NotFound();
        }

        _logger.LogDebug("Serving Swagger UI page loading swagger-ui-dist {SwaggerUiVersion} from CDN.", _options.SwaggerUiVersion);

        var jsonUrl = BuildJsonUrl(request);
        var pageTitle = _options.SwaggerUiPageTitle ?? _options.Title;
        var html = SwaggerUiHtml.Build(jsonUrl, _options.SwaggerUiCdnBaseUrl, _options.SwaggerUiVersion, pageTitle);

        return Results.Content(html, "text/html; charset=utf-8", System.Text.Encoding.UTF8, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Builds an absolute URL to the JSON document endpoint from the incoming request, honoring the
    /// configured <see cref="OpenApiOptions.RoutePrefix"/> and <see cref="OpenApiOptions.JsonRoute"/>.
    /// Segments are trimmed of stray slashes so the result never contains empty or doubled path parts.
    /// </summary>
    private string BuildJsonUrl(HttpRequest request)
    {
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value!.Trim('/') : string.Empty;
        var prefix = _options.RoutePrefix.Trim('/');
        var jsonRoute = _options.JsonRoute.Trim('/');

        var path = string.Join('/', new[] { pathBase, prefix, jsonRoute }.Where(s => !string.IsNullOrEmpty(s)));

        return $"{request.Scheme}://{request.Host}/{path}";
    }

    /// <summary>
    /// Single serialization entry point. Delegates to Backend's <see cref="OpenApiDocumentSerializer"/>
    /// helper, which serializes an <see cref="OpenApiDocument"/> to the requested <paramref name="format"/>
    /// ("json" or "yaml") at the given <paramref name="specVersion"/> using Microsoft.OpenApi's own writers.
    /// Keeping this in one place means the triggers never hand-roll JSON/YAML and the serialization
    /// implementation can change without touching the endpoints.
    /// </summary>
    private static string Serialize(OpenApiDocument document, string format, OpenApiSpecVersion specVersion)
    {
        return format switch
        {
            "yaml" => OpenApiDocumentSerializer.SerializeYaml(document, specVersion),
            _ => OpenApiDocumentSerializer.SerializeJson(document, specVersion),
        };
    }
}
