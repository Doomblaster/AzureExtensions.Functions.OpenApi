using System.Linq;
using System.Reflection;
using Azure.Functions.OpenApi.Discovery;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Fake HTTP-triggered functions defined in the test assembly so <see cref="FunctionEndpointDiscovery"/>
/// can be exercised against real <c>[Function]</c> + <c>[HttpTrigger]</c> metadata.
/// </summary>
public sealed class DiscoveryFakeFunctions
{
    [Function("GetThing")]
    public IResult GetThing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "things/{id:int}")] HttpRequest req,
        int id) => Results.Ok();

    [Function("MultiVerbThing")]
    public IResult MultiVerbThing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "things")] HttpRequest req)
        => Results.Ok();

    [Function("NoRouteThing")]
    public IResult NoRouteThing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req) => Results.Ok();

    [Function("OptionalTokenThing")]
    public IResult OptionalTokenThing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "widgets/{key:guid?}")] HttpRequest req)
        => Results.Ok();

    // Not an HTTP trigger — must be ignored by discovery.
    [Function("TimerThing")]
    public void TimerThing() { }
}

/// <summary>
/// Tests for <see cref="FunctionEndpointDiscovery"/> reflection-based endpoint discovery.
/// </summary>
public sealed class DiscoveryTests
{
    private static IReadOnlyList<DiscoveredEndpoint> DiscoverFakes(string routePrefix = "api")
    {
        var discovery = new FunctionEndpointDiscovery();
        return discovery.Discover(new[] { typeof(DiscoveryFakeFunctions).Assembly }, routePrefix);
    }

    private static DiscoveredEndpoint Find(IReadOnlyList<DiscoveredEndpoint> endpoints, string function) =>
        endpoints.Single(e => e.Method.Name == function);

    [Fact]
    public void Discover_FindsHttpTriggeredFunctions()
    {
        var endpoints = DiscoverFakes();

        var names = endpoints.Select(e => e.Method.Name).ToHashSet();
        Assert.Contains(nameof(DiscoveryFakeFunctions.GetThing), names);
        Assert.Contains(nameof(DiscoveryFakeFunctions.MultiVerbThing), names);
        Assert.Contains(nameof(DiscoveryFakeFunctions.NoRouteThing), names);
    }

    [Fact]
    public void Discover_IgnoresNonHttpTriggeredFunctions()
    {
        var endpoints = DiscoverFakes();

        Assert.DoesNotContain(endpoints, e => e.Method.Name == nameof(DiscoveryFakeFunctions.TimerThing));
    }

    [Fact]
    public void Discover_BuildsPrefixedPath_AndStripsRouteConstraints()
    {
        var endpoint = Find(DiscoverFakes(), nameof(DiscoveryFakeFunctions.GetThing));

        Assert.Equal("/api/things/{id}", endpoint.Path);
    }

    [Fact]
    public void Discover_ExtractsUpperCasedVerb()
    {
        var endpoint = Find(DiscoverFakes(), nameof(DiscoveryFakeFunctions.GetThing));

        Assert.Equal(new[] { "GET" }, endpoint.HttpMethods);
    }

    [Fact]
    public void Discover_CollectsRouteParameters()
    {
        var endpoint = Find(DiscoverFakes(), nameof(DiscoveryFakeFunctions.GetThing));

        Assert.Equal(new[] { "id" }, endpoint.RouteParameters);
    }

    [Fact]
    public void Discover_HandlesMultipleVerbs()
    {
        var endpoint = Find(DiscoverFakes(), nameof(DiscoveryFakeFunctions.MultiVerbThing));

        Assert.Equal(new[] { "GET", "POST" }, endpoint.HttpMethods);
        Assert.Equal("/api/things", endpoint.Path);
    }

    [Fact]
    public void Discover_FallsBackToFunctionName_WhenNoRoute()
    {
        var endpoint = Find(DiscoverFakes(), nameof(DiscoveryFakeFunctions.NoRouteThing));

        Assert.Equal("/api/NoRouteThing", endpoint.Path);
        Assert.Empty(endpoint.RouteParameters);
    }

    [Fact]
    public void Discover_StripsConstraintAndOptionalMarker_FromToken()
    {
        var endpoint = Find(DiscoverFakes(), nameof(DiscoveryFakeFunctions.OptionalTokenThing));

        Assert.Equal("/api/widgets/{key}", endpoint.Path);
        Assert.Equal(new[] { "key" }, endpoint.RouteParameters);
    }

    [Fact]
    public void Discover_HonorsCustomRoutePrefix()
    {
        var endpoint = Find(DiscoverFakes("v2"), nameof(DiscoveryFakeFunctions.GetThing));

        Assert.Equal("/v2/things/{id}", endpoint.Path);
    }

    [Fact]
    public void Discover_WithEmptyPrefix_ServesFromRoot()
    {
        var endpoint = Find(DiscoverFakes(""), nameof(DiscoveryFakeFunctions.MultiVerbThing));

        Assert.Equal("/things", endpoint.Path);
    }

    [Fact]
    public void GetDefaultAssemblies_ExcludesFrameworkAssemblies()
    {
        var assemblies = new FunctionEndpointDiscovery().GetDefaultAssemblies().ToList();

        Assert.DoesNotContain(assemblies, a =>
        {
            var name = a.GetName().Name ?? string.Empty;
            return name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
        });
    }
}
