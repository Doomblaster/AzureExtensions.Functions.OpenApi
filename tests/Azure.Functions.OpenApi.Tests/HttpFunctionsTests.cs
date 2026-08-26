using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Endpoint-level tests for <see cref="OpenApiHttpFunctions"/>. The trigger class is constructed
/// directly with a real <see cref="IOpenApiDocumentProvider"/> and <see cref="IOptions{TOptions}"/>,
/// then its <see cref="IResult"/> is executed against an in-memory <see cref="DefaultHttpContext"/>.
/// This exercises the full document-build → serialize → HTTP-result path without a running Functions
/// host (no <c>func host start</c>, no Azure, no network).
/// </summary>
public sealed class HttpFunctionsTests
{
    private static OpenApiHttpFunctions CreateFunctions(Action<OpenApiOptions>? configure = null)
    {
        var options = new OpenApiOptions { Title = "Endpoint API", Version = "1.0.0" };
        configure?.Invoke(options);
        var wrapped = Options.Create(options);
        var provider = new OpenApiDocumentProvider(wrapped);
        return new OpenApiHttpFunctions(provider, wrapped, NullLogger<OpenApiHttpFunctions>.Instance);
    }

    private static HttpRequest CreateHttpRequest(HttpContext context) => context.Request;

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
    public void Constructor_NullProvider_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new OpenApiHttpFunctions(null!, Options.Create(new OpenApiOptions()), NullLogger<OpenApiHttpFunctions>.Instance));

    [Fact]
    public async Task GetOpenApiJson_ReturnsJsonResult_WithExpectedContentTypeAndBody()
    {
        var functions = CreateFunctions(o => o.Title = "Json Endpoint API");
        var context = new DefaultHttpContext();

        var result = await functions.GetOpenApiJson(CreateHttpRequest(context));
        var (status, contentType, body) = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(contentType);
        Assert.StartsWith("application/json", contentType);

        using var doc = JsonDocument.Parse(body);
        Assert.StartsWith("3.1", doc.RootElement.GetProperty("openapi").GetString());
        Assert.Equal("Json Endpoint API", doc.RootElement.GetProperty("info").GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetOpenApiYaml_ReturnsYamlResult_WithExpectedContentTypeAndBody()
    {
        var functions = CreateFunctions(o => o.Title = "Yaml Endpoint API");
        var context = new DefaultHttpContext();

        var result = await functions.GetOpenApiYaml(CreateHttpRequest(context));
        var (status, contentType, body) = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(contentType);
        Assert.StartsWith("application/yaml", contentType);
        Assert.Contains("openapi:", body);
        Assert.Contains("Yaml Endpoint API", body);
    }

    [Fact]
    public async Task GetOpenApiJson_HonorsConfiguredSpecVersion()
    {
        var functions = CreateFunctions(o => o.SpecVersion = OpenApiSpecVersion.OpenApi3_0);
        var context = new DefaultHttpContext();

        var result = await functions.GetOpenApiJson(CreateHttpRequest(context));
        var (_, _, body) = await ExecuteAsync(result);

        using var doc = JsonDocument.Parse(body);
        Assert.StartsWith("3.0", doc.RootElement.GetProperty("openapi").GetString());
    }
}
