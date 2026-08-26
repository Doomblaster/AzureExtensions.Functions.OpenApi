using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Endpoint-level tests for the <c>GetSwaggerUi</c> function on <see cref="OpenApiHttpFunctions"/>.
/// The trigger class is constructed directly (real provider, <see cref="IOptions{TOptions}"/> and a
/// null logger) and its <see cref="IResult"/> is executed against an in-memory
/// <see cref="DefaultHttpContext"/>, mirroring <see cref="HttpFunctionsTests"/>. No Functions host,
/// no Azure, no network.
/// </summary>
public sealed class SwaggerUiFunctionTests
{
    private static OpenApiHttpFunctions CreateFunctions(Action<OpenApiOptions>? configure = null)
    {
        var options = new OpenApiOptions { Title = "Swagger Endpoint API", Version = "1.0.0" };
        configure?.Invoke(options);
        var wrapped = Options.Create(options);
        var provider = new OpenApiDocumentProvider(wrapped);
        return new OpenApiHttpFunctions(provider, wrapped, NullLogger<OpenApiHttpFunctions>.Instance);
    }

    private static HttpRequest CreateHttpRequest(
        string scheme = "https",
        string host = "localhost",
        string pathBase = "",
        string? forwardedHost = null,
        string? forwardedProto = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        if (!string.IsNullOrEmpty(pathBase))
        {
            context.Request.PathBase = new PathString(pathBase);
        }

        if (forwardedHost is not null)
        {
            context.Request.Headers["X-Forwarded-Host"] = forwardedHost;
        }

        if (forwardedProto is not null)
        {
            context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        }

        return context.Request;
    }

    private static async Task<(int StatusCode, string? ContentType, string Body)> ExecuteAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var serviceProvider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await result.ExecuteAsync(context);

        responseBody.Position = 0;
        var body = await new StreamReader(responseBody, Encoding.UTF8).ReadToEndAsync();
        return (context.Response.StatusCode, context.Response.ContentType, body);
    }

    [Fact]
    public async Task GetSwaggerUi_WhenEnabled_ReturnsHtmlPage()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        var result = functions.GetSwaggerUi(CreateHttpRequest());
        var (status, contentType, body) = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("text/html; charset=utf-8", contentType);
        Assert.Contains("<!DOCTYPE html>", body);
        Assert.Contains("id=\"swagger-ui\"", body);
        Assert.Contains("SwaggerUIBundle({", body);
        Assert.Contains("StandaloneLayout", body);
    }

    [Fact]
    public async Task GetSwaggerUi_WhenEnabled_DerivesJsonUrlFromDefaultRouting()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        var result = functions.GetSwaggerUi(CreateHttpRequest(scheme: "https", host: "example.com"));
        var (_, _, body) = await ExecuteAsync(result);

        // Default RoutePrefix "api" + default JsonRoute "openapi.json".
        Assert.Contains("url: \"https://example.com/api/openapi.json\"", body);
    }

    [Fact]
    public async Task GetSwaggerUi_DerivedJsonUrl_ReflectsConfiguredRoutePrefixAndJsonRoute()
    {
        var functions = CreateFunctions(o =>
        {
            o.EnableSwaggerUi = true;
            o.RoutePrefix = "gateway";
            o.JsonRoute = "spec/openapi.json";
        });

        var result = functions.GetSwaggerUi(CreateHttpRequest(scheme: "https", host: "api.contoso.com"));
        var (_, _, body) = await ExecuteAsync(result);

        Assert.Contains("url: \"https://api.contoso.com/gateway/spec/openapi.json\"", body);
    }

    [Fact]
    public async Task GetSwaggerUi_UsesPinnedSwaggerUiVersionInAssetUrls()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        var result = functions.GetSwaggerUi(CreateHttpRequest());
        var (_, _, body) = await ExecuteAsync(result);

        Assert.Contains("swagger-ui-dist@5.32.14/swagger-ui.css", body);
        Assert.Contains("swagger-ui-dist@5.32.14/swagger-ui-bundle.js", body);
    }

    [Fact]
    public async Task GetSwaggerUi_WhenDisabled_ReturnsNotFound()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = false);

        var result = functions.GetSwaggerUi(CreateHttpRequest());
        var (status, _, _) = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task GetSwaggerUi_UsesForwardedHost_WhenXForwardedHostPresent()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        // Mirrors the Aspire dev proxy: the internal listener is localhost:51516, but the browser
        // reached the app via the public-facing localhost:7071 carried in X-Forwarded-Host.
        var result = functions.GetSwaggerUi(CreateHttpRequest(
            scheme: "http",
            host: "localhost:51516",
            forwardedHost: "localhost:7071"));
        var (_, _, body) = await ExecuteAsync(result);

        Assert.Contains("url: \"http://localhost:7071/api/openapi.json\"", body);
        Assert.DoesNotContain("localhost:51516", body);
    }

    [Fact]
    public async Task GetSwaggerUi_UsesForwardedProto_WhenXForwardedProtoPresent()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        // TLS-terminating proxy: the internal hop is http, but the client-facing scheme is https.
        var result = functions.GetSwaggerUi(CreateHttpRequest(
            scheme: "http",
            host: "internal:8080",
            forwardedHost: "api.contoso.com",
            forwardedProto: "https"));
        var (_, _, body) = await ExecuteAsync(result);

        Assert.Contains("url: \"https://api.contoso.com/api/openapi.json\"", body);
    }

    [Fact]
    public async Task GetSwaggerUi_UsesFirstForwardedHost_WhenMultipleCommaSeparated()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        // A chain of proxies appends values; the client-facing hop is the first entry.
        var result = functions.GetSwaggerUi(CreateHttpRequest(
            scheme: "https",
            host: "internal:8080",
            forwardedHost: "public.example.com, gateway.internal"));
        var (_, _, body) = await ExecuteAsync(result);

        Assert.Contains("url: \"https://public.example.com/api/openapi.json\"", body);
    }

    [Fact]
    public async Task GetSwaggerUi_FallsBackToHost_WhenNoForwardedHeaders()
    {
        var functions = CreateFunctions(o => o.EnableSwaggerUi = true);

        var result = functions.GetSwaggerUi(CreateHttpRequest(scheme: "https", host: "direct.example.com"));
        var (_, _, body) = await ExecuteAsync(result);

        Assert.Contains("url: \"https://direct.example.com/api/openapi.json\"", body);
    }
}
