using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AzureExtensions.Functions.OpenApi;

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
        var json = Serialize(WithServers(document, request), "json", _options.SpecVersion);

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
        var yaml = Serialize(WithServers(document, request), "yaml", _options.SpecVersion);

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
    /// When the request arrives through a reverse proxy (e.g. the Aspire dev proxy) the
    /// <c>X-Forwarded-Host</c> / <c>X-Forwarded-Proto</c> headers are used so the emitted URL points at
    /// the public-facing endpoint the browser can actually reach, not the internal listener.
    /// </summary>
    private string BuildJsonUrl(HttpRequest request)
    {
        var prefix = _options.RoutePrefix.Trim('/');
        var jsonRoute = _options.JsonRoute.Trim('/');
        var suffix = string.Join('/', new[] { prefix, jsonRoute }.Where(static s => !string.IsNullOrEmpty(s)));

        var baseUrl = ResolveRequestBaseUrl(request);
        if (baseUrl is null)
        {
            return "/" + suffix;
        }

        return string.IsNullOrEmpty(suffix) ? baseUrl : $"{baseUrl}/{suffix}";
    }

    /// <summary>
    /// Produces the per-request <c>servers</c> list for the document. When
    /// <see cref="OpenApiOptions.Servers"/> is configured it is used verbatim; otherwise a single
    /// server is inferred from the request base URL (scheme/host/path base, honoring forwarded
    /// headers) combined with <see cref="OpenApiOptions.RoutePrefix"/>. When the host cannot be
    /// resolved the URL falls back to a relative base path so the document is still valid.
    /// </summary>
    private IList<OpenApiServer> BuildServers(HttpRequest request)
    {
        if (_options.Servers.Count > 0)
        {
            return _options.Servers.ToList();
        }

        var prefix = _options.RoutePrefix.Trim('/');
        var baseUrl = ResolveRequestBaseUrl(request);

        string url;
        if (baseUrl is null)
        {
            url = string.IsNullOrEmpty(prefix) ? "/" : "/" + prefix;
        }
        else
        {
            url = string.IsNullOrEmpty(prefix) ? baseUrl : $"{baseUrl}/{prefix}";
        }

        return new List<OpenApiServer> { new() { Url = url } };
    }

    /// <summary>
    /// Returns a per-request shallow copy of the cached document with its <c>servers</c> set. The
    /// cached document is shared across requests, so it is never mutated; the copy carries the
    /// request-specific server list while sharing the (read-only during serialization) paths and
    /// components.
    /// </summary>
    private OpenApiDocument WithServers(OpenApiDocument document, HttpRequest request) =>
        new(document) { Servers = BuildServers(request) };

    /// <summary>
    /// Resolves the request's public-facing base URL as <c>{scheme}://{host}</c> (plus the request
    /// path base when present), honoring <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>. Returns
    /// <see langword="null"/> when no host can be determined.
    /// </summary>
    private static string? ResolveRequestBaseUrl(HttpRequest request)
    {
        var scheme = ResolveForwardedValue(request, "X-Forwarded-Proto") ?? request.Scheme;
        var host = ResolveForwardedValue(request, "X-Forwarded-Host") ?? request.Host.Value;
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var pathBase = request.PathBase.HasValue ? request.PathBase.Value!.Trim('/') : string.Empty;
        var baseUrl = $"{scheme}://{host}";
        return string.IsNullOrEmpty(pathBase) ? baseUrl : $"{baseUrl}/{pathBase}";
    }

    /// <summary>
    /// Returns the first value of a forwarded header when it is present and non-empty; otherwise
    /// <see langword="null"/>. Proxies may append multiple comma-separated values (the client-facing
    /// hop is the first), so only the leading entry is used.
    /// </summary>
    private static string? ResolveForwardedValue(HttpRequest request, string headerName)
    {
        if (!request.Headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        var first = values.ToString().Split(',', 2)[0].Trim();
        return string.IsNullOrEmpty(first) ? null : first;
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
