using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Tests for <see cref="OpenApiServiceCollectionExtensions.AddOpenApi"/> DI registration.
/// </summary>
public sealed class DiRegistrationTests
{
    [Fact]
    public void AddOpenApi_RegistersDocumentProvider()
    {
        var services = new ServiceCollection();

        services.AddOpenApi();

        using var provider = services.BuildServiceProvider();
        var documentProvider = provider.GetService<IOpenApiDocumentProvider>();

        Assert.NotNull(documentProvider);
        Assert.IsType<OpenApiDocumentProvider>(documentProvider);
    }

    [Fact]
    public void AddOpenApi_WithoutConfigure_ResolvesDefaultOptions()
    {
        var services = new ServiceCollection();

        services.AddOpenApi();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenApiOptions>>().Value;

        Assert.Equal("OpenAPI Document", options.Title);
        Assert.Equal("1.0.0", options.Version);
        Assert.Equal("api", options.RoutePrefix);
        Assert.Equal("openapi.json", options.JsonRoute);
        Assert.Equal("openapi.yaml", options.YamlRoute);
        Assert.Equal(OpenApiSpecVersion.OpenApi3_1, options.SpecVersion);
    }

    [Fact]
    public void AddOpenApi_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddOpenApi(o =>
        {
            o.Title = "My API";
            o.Version = "2.5.0";
            o.Description = "A configured description.";
            o.SpecVersion = OpenApiSpecVersion.OpenApi3_0;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenApiOptions>>().Value;

        Assert.Equal("My API", options.Title);
        Assert.Equal("2.5.0", options.Version);
        Assert.Equal("A configured description.", options.Description);
        Assert.Equal(OpenApiSpecVersion.OpenApi3_0, options.SpecVersion);
    }

    [Fact]
    public void AddOpenApi_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddOpenApi();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddOpenApi_TryAddSingleton_DoesNotOverrideExistingProvider()
    {
        var services = new ServiceCollection();
        var custom = new StubDocumentProvider();
        services.AddSingleton<IOpenApiDocumentProvider>(custom);

        services.AddOpenApi();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<IOpenApiDocumentProvider>());
    }

    private sealed class StubDocumentProvider : IOpenApiDocumentProvider
    {
        public ValueTask<OpenApiDocument> GetDocumentAsync(CancellationToken cancellationToken = default)
            => new(new OpenApiDocument { Info = new OpenApiInfo { Title = "Stub", Version = "0.0.0" }, Paths = new OpenApiPaths() });
    }
}
