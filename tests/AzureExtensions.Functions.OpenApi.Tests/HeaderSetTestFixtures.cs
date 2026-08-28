using AzureExtensions.Functions.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace AzureExtensions.Functions.OpenApi.Tests;

internal sealed class TestRequestHeaderDefinition : IOpenApiRequestHeaderDefinition
{
    public required string Name { get; init; }

    public required Type Type { get; init; }

    public string? Description { get; init; }

    public bool Required { get; init; }
}

internal sealed class TestResponseHeaderDefinition : IOpenApiResponseHeaderDefinition
{
    public required string Name { get; init; }

    public required Type Type { get; init; }

    public string? Description { get; init; }

    public bool Required { get; init; }

    public bool Deprecated { get; init; }
}

internal sealed class RequestHeaderSetFixture : IOpenApiRequestHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiRequestHeaderDefinition> Headers { get; } =
    [
        new TestRequestHeaderDefinition
        {
            Name = "X-Tenant-Id",
            Type = typeof(Guid),
            Description = "Tenant identifier.",
            Required = true,
        },
        new TestRequestHeaderDefinition
        {
            Name = "X-Trace-Id",
            Type = typeof(string),
            Description = "Trace identifier.",
            Required = false,
        },
    ];
}

internal sealed class RequestHeaderCollisionFixture : IOpenApiRequestHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiRequestHeaderDefinition> Headers { get; } =
    [
        new TestRequestHeaderDefinition
        {
            Name = "x-trace-id",
            Type = typeof(string),
            Description = "Trace identifier from set.",
            Required = false,
        },
        new TestRequestHeaderDefinition
        {
            Name = "X-Tenant-Id",
            Type = typeof(Guid),
            Description = "Tenant identifier.",
            Required = true,
        },
    ];
}

internal sealed class ResponseHeaderSetFixture : IOpenApiResponseHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiResponseHeaderDefinition> Headers { get; } =
    [
        new TestResponseHeaderDefinition
        {
            Name = "X-Request-Id",
            Type = typeof(Guid),
            Description = "Correlation identifier.",
            Required = true,
            Deprecated = false,
        },
        new TestResponseHeaderDefinition
        {
            Name = "X-Served-By",
            Type = typeof(string),
            Description = "Serving node identifier.",
            Required = false,
            Deprecated = false,
        },
    ];
}

internal sealed class ResponseHeaderCollisionFixture : IOpenApiResponseHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiResponseHeaderDefinition> Headers { get; } =
    [
        new TestResponseHeaderDefinition
        {
            Name = "x-request-id",
            Type = typeof(string),
            Description = "Correlation identifier from set.",
            Required = false,
            Deprecated = false,
        },
    ];
}

internal sealed class InvalidRequestHeaderSetWithoutPublicParameterlessConstructor : IOpenApiRequestHeaderDefinitionCollection
{
    private InvalidRequestHeaderSetWithoutPublicParameterlessConstructor()
    {
    }

    public IReadOnlyList<IOpenApiRequestHeaderDefinition> Headers { get; } = Array.Empty<IOpenApiRequestHeaderDefinition>();
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
