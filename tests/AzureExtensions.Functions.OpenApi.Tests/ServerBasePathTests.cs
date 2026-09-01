using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using SampleFunctionApp.Models;
using Xunit;

namespace AzureExtensions.Functions.OpenApi.Tests;

/// <summary>
/// Tests that the base path (the Functions <c>routePrefix</c>) is advertised via the document's
/// <c>servers</c> entry — inferred from the request by default — rather than repeated on every path
/// key. Exercises the full serve path (<see cref="OpenApiHttpFunctions.GetOpenApiJson"/>) against an
/// in-memory <see cref="DefaultHttpContext"/>; no Functions host, no network.
/// </summary>
public sealed class ServerBasePathTests
{
    private static OpenApiHttpFunctions CreateFunctions(Action<OpenApiOptions>? configure = null)
    {
        var options = new OpenApiOptions { Title = "Server Base API", Version = "1.0.0" };
        options.DocumentAssemblies.Add(typeof(Item).Assembly);
        configure?.Invoke(options);
        var wrapped = Options.Create(options);
        var provider = new OpenApiDocumentProvider(wrapped);
        return new OpenApiHttpFunctions(provider, wrapped, NullLogger<OpenApiHttpFunctions>.Instance);
    }

    private static HttpRequest CreateHttpRequest(
        string? scheme = "https",
        string? host = "example.com",
        string pathBase = "",
        string? forwardedHost = null,
        string? forwardedProto = null)
    {
        var context = new DefaultHttpContext();
        if (scheme is not null)
        {
            context.Request.Scheme = scheme;
        }

        if (!string.IsNullOrEmpty(host))
        {
            context.Request.Host = new HostString(host);
        }

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

    private static async Task<JsonDocument> ExecuteJsonAsync(IResult result)
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
        return JsonDocument.Parse(body);
    }

    private static string FirstServerUrl(JsonDocument doc) =>
        doc.RootElement.GetProperty("servers")[0].GetProperty("url").GetString()!;

    [Fact]
    public async Task Servers_InferredFromRequestHost_AndPathsAreRelative()
    {
        var functions = CreateFunctions();

        using var doc = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(scheme: "https", host: "example.com")));

        Assert.Equal("https://example.com/api", FirstServerUrl(doc));

        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/items", out _));
        Assert.False(paths.TryGetProperty("/api/items", out _));
    }

    [Fact]
    public async Task Servers_HonorForwardedHeadersAndPathBase()
    {
        var functions = CreateFunctions();

        using var doc = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(
                scheme: "http",
                host: "internal:8080",
                pathBase: "/base",
                forwardedHost: "api.contoso.com",
                forwardedProto: "https")));

        Assert.Equal("https://api.contoso.com/base/api", FirstServerUrl(doc));
    }

    [Fact]
    public async Task Servers_FallBackToRelative_WhenHostUnresolved()
    {
        var functions = CreateFunctions();

        using var doc = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(scheme: null, host: null)));

        Assert.Equal("/api", FirstServerUrl(doc));
    }

    [Fact]
    public async Task Servers_EmptyRoutePrefix_YieldsHostOnlyServer()
    {
        var functions = CreateFunctions(o => o.RoutePrefix = string.Empty);

        using var doc = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(scheme: "https", host: "example.com")));

        Assert.Equal("https://example.com", FirstServerUrl(doc));
    }

    [Fact]
    public async Task Servers_ExplicitOption_OverridesRequestInference()
    {
        var functions = CreateFunctions(o =>
            o.Servers.Add(new OpenApiServer { Url = "https://fixed.example/api" }));

        using var doc = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(scheme: "https", host: "example.com")));

        Assert.Equal("https://fixed.example/api", FirstServerUrl(doc));
    }

    [Fact]
    public async Task Servers_DifferentHosts_ProduceDifferentServers_WithoutMutatingCachedPaths()
    {
        // Same functions instance -> shared, cached document. Per-request servers must not leak
        // across requests, and the cached paths must remain intact.
        var functions = CreateFunctions();

        using var first = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(host: "first.example")));
        using var second = await ExecuteJsonAsync(
            await functions.GetOpenApiJson(CreateHttpRequest(host: "second.example")));

        Assert.Equal("https://first.example/api", FirstServerUrl(first));
        Assert.Equal("https://second.example/api", FirstServerUrl(second));

        Assert.True(first.RootElement.GetProperty("paths").TryGetProperty("/items", out _));
        Assert.True(second.RootElement.GetProperty("paths").TryGetProperty("/items", out _));
    }
}
