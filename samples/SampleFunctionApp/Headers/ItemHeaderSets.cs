using AzureExtensions.Functions.OpenApi;

namespace SampleFunctionApp.Headers;

internal sealed class HeaderDefinition : IOpenApiHeaderDefinition
{
    public required string Name { get; init; }

    public required Type Type { get; init; }

    public string? Description { get; init; }

    public bool Required { get; init; }

    public bool Deprecated { get; init; }
}

/// <summary>
/// Reusable tenant header that can be applied directly or included in a header set.
/// </summary>
public sealed class TenantIdHeader : IOpenApiHeaderDefinition
{
    public string Name => "X-Tenant-Id";

    public Type Type => typeof(Guid);

    public string Description => "Tenant identifier used to scope the catalog request.";

    public bool Required => true;

    public bool Deprecated => false;
}

/// <summary>
/// Reusable request headers that identify the caller and tenant for item operations.
/// </summary>
public sealed class CatalogRequestHeaders : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new HeaderDefinition
        {
            Name = "X-Correlation-Id",
            Type = typeof(Guid),
            Description = "Client-supplied correlation identifier for catalog write operations.",
            Required = true,
            Deprecated = false,
        },
        new TenantIdHeader(),
    ];
}

/// <summary>
/// Reusable response headers returned with throttled item reads.
/// </summary>
public sealed class CatalogRateLimitHeaders : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new HeaderDefinition
        {
            Name = "X-RateLimit-Limit",
            Type = typeof(int),
            Description = "Maximum number of catalog read requests allowed in the current window.",
            Required = true,
            Deprecated = false,
        },
        new HeaderDefinition
        {
            Name = "X-RateLimit-Remaining",
            Type = typeof(int),
            Description = "Catalog read requests remaining in the current window.",
            Required = true,
            Deprecated = false,
        },
    ];
}
