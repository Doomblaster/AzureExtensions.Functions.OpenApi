namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Declares a response for the OpenAPI operation produced by an HTTP-triggered function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. Apply it once per
/// documented status code; multiple instances are allowed on a single method.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiResponseAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiResponseAttribute"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code this response documents.</param>
    public OpenApiResponseAttribute(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// The HTTP status code this response documents.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The CLR type used to derive the response body schema. A <see langword="null"/> value
    /// indicates a response with no body.
    /// </summary>
    public Type? Type { get; set; }

    /// <summary>
    /// The media type of the response body. Defaults to <c>application/json</c>.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Optional description of the response.
    /// </summary>
    public string? Description { get; set; }
}
