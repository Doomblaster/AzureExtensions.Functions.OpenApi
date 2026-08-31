using AzureExtensions.Functions.OpenApi;

namespace SampleFunctionApp.Responses;

/// <summary>
/// Reusable 404 response for item operations that return no body.
/// </summary>
public sealed class NotFoundResponseDefinition : IOpenApiResponseDefinition
{
    public int StatusCode => 404;

    public Type? Type => null;

    // ContentType is ignored by OpenAPI generation because Type is null (no response body).
    public string ContentType => "application/json";

    public string Description => "No item exists with the given identifier.";
}
