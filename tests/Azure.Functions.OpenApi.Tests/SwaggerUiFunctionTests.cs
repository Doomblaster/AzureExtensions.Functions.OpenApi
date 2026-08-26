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

    private static HttpRequest CreateHttpRequest(string scheme = "https", string host = "localhost", string pathBase = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        if (!string.IsNullOrEmpty(pathBase))
        {
            context.Request.PathBase = new PathString(pathBase);
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
}
