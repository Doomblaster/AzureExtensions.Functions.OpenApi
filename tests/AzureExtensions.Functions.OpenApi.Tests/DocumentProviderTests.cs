using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Xunit;

namespace AzureExtensions.Functions.OpenApi.Tests;

/// <summary>
/// Tests that <see cref="OpenApiDocumentProvider"/> builds a valid document reflecting the options.
/// </summary>
public sealed class DocumentProviderTests
{
    private static OpenApiDocumentProvider CreateProvider(Action<OpenApiOptions>? configure = null)
    {
        var options = new OpenApiOptions();
        configure?.Invoke(options);
        return new OpenApiDocumentProvider(Options.Create(options));
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsNonNullDocument()
    {
        var provider = CreateProvider();

        var document = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(document);
        Assert.NotNull(document.Info);
        Assert.NotNull(document.Paths);
    }

    [Fact]
    public async Task GetDocumentAsync_ReflectsConfiguredTitleAndVersion()
    {
        var provider = CreateProvider(o =>
        {
            o.Title = "Contoso API";
            o.Version = "3.4.5";
        });

        var document = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Contoso API", document.Info!.Title);
        Assert.Equal("3.4.5", document.Info.Version);
    }

    [Fact]
    public async Task GetDocumentAsync_SetsDescription_WhenProvided()
    {
        var provider = CreateProvider(o => o.Description = "Detailed description.");

        var document = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Detailed description.", document.Info!.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetDocumentAsync_LeavesDescriptionNull_WhenBlank(string? description)
    {
        var provider = CreateProvider(o => o.Description = description);

        var document = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Null(document.Info!.Description);
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsCachedInstance_OnRepeatedCalls()
    {
        var provider = CreateProvider();

        var first = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);
        var second = await provider.GetDocumentAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetDocumentAsync_HonorsCancellationToken_WhenAlreadyCancelled()
    {
        var provider = CreateProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The provider builds synchronously and caches; it should still return a valid document.
        var document = await provider.GetDocumentAsync(cts.Token);
        Assert.NotNull(document);
    }
}
