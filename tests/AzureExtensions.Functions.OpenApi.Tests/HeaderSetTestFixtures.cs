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

internal sealed class GenericRequestHeaderDefinition : IOpenApiHeaderDefinition
{
    public string Name => "X-Correlation-Id";

    public Type Type => typeof(Guid);

    public string? Description => "Correlation identifier from generic request header.";

    public bool Required => true;

    public bool Deprecated => false;
}

internal sealed class GenericRequestHeaderSetFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new GenericRequestHeaderDefinition(),
        new TestHeaderDefinition
        {
            Name = "X-Generic-Trace-Id",
            Type = typeof(string),
            Description = "Trace identifier from generic request header set.",
            Required = false,
            Deprecated = true,
        },
    ];
}

internal sealed class GenericResponseHeaderDefinition : IOpenApiHeaderDefinition
{
    public string Name => "X-Processed-By";

    public Type Type => typeof(string);

    public string? Description => "Processing node from generic response header.";

    public bool Required => false;

    public bool Deprecated => true;
}

internal sealed class GenericResponseHeaderSetFixture : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new GenericResponseHeaderDefinition(),
        new TestHeaderDefinition
        {
            Name = "X-Generic-Request-Id",
            Type = typeof(Guid),
            Description = "Correlation identifier from generic response header set.",
            Required = true,
            Deprecated = false,
        },
    ];
}

internal sealed class NotFoundResponseBodyFixture
{
    public string Message { get; set; } = string.Empty;
}

internal sealed class NotFoundResponseDefinitionFixture : IOpenApiResponseDefinition
{
    public int StatusCode => 404;

    public Type? Type => typeof(NotFoundResponseBodyFixture);

    public string ContentType => "application/json";

    public string? Description => "Not found.";
}

internal sealed class DocumentHeaderSetFunctions
{
    [Function("DocumentHeaderSetFunction")]
    [OpenApiOperation(OperationId = "documentHeaderSet", Summary = "Document header-set coverage")]
    [OpenApiRequestHeaderParameterSet(typeof(RequestHeaderCollisionFixture))]
    [OpenApiRequestHeaderParameter("X-Trace-Id", typeof(Guid), Required = true, Description = "Trace identifier override.")]
    [OpenApiResponse(200, Type = typeof(string), Description = "Ok.")]
    [OpenApiResponse(201, Type = typeof(string), Description = "Created.")]
    [OpenApiResponseHeaderSet(typeof(ResponseHeaderSetFixture), 200, 201)]
    public IResult DocumentHeaderSetFunction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header-set-docs")] HttpRequest req)
        => Results.Ok("ok");

    [Function("DocumentGenericHeaderAttributeFunction")]
    [OpenApiOperation(OperationId = "documentGenericHeaderAttribute", Summary = "Generic header attribute coverage")]
    [OpenApiRequestHeaderParameter<GenericRequestHeaderDefinition>]
    [OpenApiRequestHeaderParameterSet<GenericRequestHeaderSetFixture>]
    [OpenApiResponse(200, Type = typeof(string), Description = "Ok.")]
    [OpenApiResponse(202, Type = typeof(string), Description = "Accepted.")]
    [OpenApiResponseHeader<GenericResponseHeaderDefinition>(202)]
    [OpenApiResponseHeaderSet<GenericResponseHeaderSetFixture>(200, 202)]
    public IResult DocumentGenericHeaderAttributeFunction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header-set-docs/generic")] HttpRequest req)
        => Results.Ok("generic");

    [Function("DocumentMalformedHeaderSetFunction")]
    [OpenApiOperation(OperationId = "documentMalformedHeaderSet", Summary = "Malformed header-set coverage")]
    [OpenApiRequestHeaderParameterSet(typeof(InvalidRequestHeaderSetWithoutPublicParameterlessConstructor))]
    [OpenApiResponse(200, Type = typeof(string), Description = "Ok.")]
    public IResult DocumentMalformedHeaderSetFunction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header-set-docs/malformed")] HttpRequest req)
        => Results.Ok("bad");
}
