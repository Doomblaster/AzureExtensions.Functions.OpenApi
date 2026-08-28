using AzureExtensions.Functions.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace AzureExtensions.Functions.OpenApi.Tests;

internal sealed class TestHeaderDefinition : IOpenApiHeaderDefinition
{
    public required string Name { get; init; }

    public required Type Type { get; init; }

    public string? Description { get; init; }

    public bool Required { get; init; }

    public bool Deprecated { get; init; }
}

internal sealed class RequestHeaderSetFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new TestHeaderDefinition
        {
            Name = "X-Tenant-Id",
            Type = typeof(Guid),
            Description = "Tenant identifier.",
            Required = true,
            Deprecated = false,
        },
        new TestHeaderDefinition
        {
            Name = "X-Trace-Id",
            Type = typeof(string),
            Description = "Trace identifier.",
            Required = false,
            Deprecated = false,
        },
    ];
}

internal sealed class RequestHeaderCollisionFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new TestHeaderDefinition
        {
            Name = "x-trace-id",
            Type = typeof(string),
            Description = "Trace identifier from set.",
            Required = false,
            Deprecated = false,
        },
        new TestHeaderDefinition
        {
            Name = "X-Tenant-Id",
            Type = typeof(Guid),
            Description = "Tenant identifier.",
            Required = true,
            Deprecated = false,
        },
    ];
}

internal sealed class DeprecatedRequestHeaderSetFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new TestHeaderDefinition
        {
            Name = "X-Deprecated-Request",
            Type = typeof(string),
            Description = "Deprecated request header.",
            Required = false,
            Deprecated = true,
        },
    ];
}

internal sealed class ResponseHeaderSetFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new TestHeaderDefinition
        {
            Name = "X-Request-Id",
            Type = typeof(Guid),
            Description = "Correlation identifier.",
            Required = true,
            Deprecated = false,
        },
        new TestHeaderDefinition
        {
            Name = "X-Served-By",
            Type = typeof(string),
            Description = "Serving node identifier.",
            Required = false,
            Deprecated = false,
        },
    ];
}

internal sealed class ResponseHeaderCollisionFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new TestHeaderDefinition
        {
            Name = "x-request-id",
            Type = typeof(string),
            Description = "Correlation identifier from set.",
            Required = false,
            Deprecated = false,
        },
    ];
}

internal sealed class InvalidRequestHeaderSetWithoutPublicParameterlessConstructor : IOpenApiHeaderDefinitionCollection
{
    private InvalidRequestHeaderSetWithoutPublicParameterlessConstructor()
    {
    }

    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } = Array.Empty<IOpenApiHeaderDefinition>();
}

internal sealed class DocumentHeaderSetFunctions
{
    [Function("DocumentHeaderSetFunction")]
    [OpenApiOperation(OperationId = "documentHeaderSet", Summary = "Document header-set coverage")]
    [OpenApiHeaderParameterSet(typeof(RequestHeaderCollisionFixture))]
    [OpenApiHeaderParameter("X-Trace-Id", typeof(Guid), Required = true, Description = "Trace identifier override.")]
    [OpenApiResponse(200, Type = typeof(string), Description = "Ok.")]
    [OpenApiResponse(201, Type = typeof(string), Description = "Created.")]
    [OpenApiResponseHeaderSet(typeof(ResponseHeaderSetFixture), 200, 201)]
    public IResult DocumentHeaderSetFunction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header-set-docs")] HttpRequest req)
        => Results.Ok("ok");

    [Function("DocumentMalformedHeaderSetFunction")]
    [OpenApiOperation(OperationId = "documentMalformedHeaderSet", Summary = "Malformed header-set coverage")]
    [OpenApiHeaderParameterSet(typeof(InvalidRequestHeaderSetWithoutPublicParameterlessConstructor))]
    [OpenApiResponse(200, Type = typeof(string), Description = "Ok.")]
    public IResult DocumentMalformedHeaderSetFunction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header-set-docs/malformed")] HttpRequest req)
        => Results.Ok("bad");
}
