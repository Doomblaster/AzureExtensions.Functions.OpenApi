using System.Reflection;
using Microsoft.OpenApi;

namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Configuration for the generated OpenAPI document and the HTTP endpoints that expose it.
/// </summary>
/// <remarks>
/// These options are the public contract consumers use to shape the document. Behavior that
/// reads these values (document building, serialization, routing) is implemented by later
/// components; the shape defined here is final.
/// </remarks>
public sealed class OpenApiOptions
{
    /// <summary>
    /// The <c>info.title</c> of the generated OpenAPI document.
    /// </summary>
    public string Title { get; set; } = "OpenAPI Document";

    /// <summary>
    /// The <c>info.version</c> of the generated OpenAPI document (the API version, not the spec version).
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Optional <c>info.description</c> of the generated OpenAPI document.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The route prefix the Functions host uses for HTTP triggers. Defaults to <c>api</c>,
    /// matching the Azure Functions default. This is the base path for the API and is advertised
    /// via the document's <c>servers</c> entry rather than repeated on every path key. It also
    /// drives the effective URLs of the served document/Swagger UI endpoints.
    /// </summary>
    public string RoutePrefix { get; set; } = "api";

    /// <summary>
    /// Optional explicit <c>servers</c> for the generated document. When empty (the default), a
    /// single server is inferred from the incoming request (scheme/host, honoring
    /// <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c> and the request path base) combined with
    /// <see cref="RoutePrefix"/>, so the advertised base URL matches wherever the document is served
    /// from. Add entries here to advertise fixed URLs instead (for example a public API gateway),
    /// in which case they are used verbatim and request inference is skipped.
    /// </summary>
    public IList<OpenApiServer> Servers { get; } = new List<OpenApiServer>();

    /// <summary>
    /// The route (relative to <see cref="RoutePrefix"/>) that serves the JSON document.
    /// </summary>
    public string JsonRoute { get; set; } = "openapi.json";

    /// <summary>
    /// The route (relative to <see cref="RoutePrefix"/>) that serves the YAML document.
    /// </summary>
    public string YamlRoute { get; set; } = "openapi.yaml";

    /// <summary>
    /// The OpenAPI Specification version the document is serialized against. Defaults to 3.1.
    /// </summary>
    public OpenApiSpecVersion SpecVersion { get; set; } = OpenApiSpecVersion.OpenApi3_1;

    /// <summary>
    /// When <see langword="true"/> (the default), an anonymous HTTP-triggered function serves an
    /// interactive Swagger UI page that loads the assets from a CDN and points at the JSON document.
    /// When <see langword="false"/>, that endpoint responds with <c>404 Not Found</c>.
    /// </summary>
    public bool EnableSwaggerUi { get; set; } = true;

    /// <summary>
    /// The advertised route (relative to <see cref="RoutePrefix"/>) that serves the Swagger UI page.
    /// This value is advisory only: like <see cref="JsonRoute"/> and <see cref="YamlRoute"/>, the
    /// actual route bound to the HTTP trigger is a compile-time constant (<c>swagger</c>) because
    /// Azure Functions route templates must be constant expressions. Change this only if you also
    /// change the trigger's <c>Route</c> attribute.
    /// </summary>
    public string SwaggerUiRoute { get; set; } = "swagger";

    /// <summary>
    /// The base URL of the CDN that hosts the Swagger UI assets (CSS and JavaScript bundles).
    /// Defaults to jsDelivr's <c>swagger-ui-dist</c> package. The pinned <see cref="SwaggerUiVersion"/>
    /// is appended to this base when building asset URLs.
    /// </summary>
    public string SwaggerUiCdnBaseUrl { get; set; } = "https://cdn.jsdelivr.net/npm/swagger-ui-dist";

    /// <summary>
    /// The pinned <c>swagger-ui-dist</c> version loaded from <see cref="SwaggerUiCdnBaseUrl"/>. A
    /// specific version is pinned (rather than <c>latest</c>) so the served page is deterministic and
    /// not affected by upstream releases.
    /// </summary>
    public string SwaggerUiVersion { get; set; } = "5.32.14";

    /// <summary>
    /// Optional title for the Swagger UI HTML page (the browser tab text). When <see langword="null"/>
    /// (the default), the page falls back to <see cref="Title"/>.
    /// </summary>
    public string? SwaggerUiPageTitle { get; set; }

    /// <summary>
    /// When <see langword="true"/> (the default), HTTP-triggered functions that carry no
    /// OpenAPI attributes still appear in the document via best-effort inference. When
    /// <see langword="false"/>, only endpoints annotated with OpenAPI attributes are documented.
    /// </summary>
    public bool IncludeUnannotatedEndpoints { get; set; } = true;

    /// <summary>
    /// Explicit assemblies to scan for HTTP-triggered functions when building the document.
    /// When empty (the default), discovery auto-scans the Functions application assembly.
    /// Add assemblies here to point discovery at additional or alternative locations.
    /// </summary>
    public IList<Assembly> DocumentAssemblies { get; } = new List<Assembly>();
}
