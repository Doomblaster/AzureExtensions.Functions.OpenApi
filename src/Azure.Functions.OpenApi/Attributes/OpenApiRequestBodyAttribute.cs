namespace Azure.Functions.OpenApi;

/// <summary>
/// Declares the request body for the OpenAPI operation produced by an HTTP-triggered function
/// method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. Apply it to the
/// method that backs an HTTP trigger to document its <c>requestBody</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequestBodyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiRequestBodyAttribute"/> class.
    /// </summary>
    /// <param name="type">The CLR type used to derive the request body schema.</param>
    public OpenApiRequestBodyAttribute(Type type)
    {
        Type = type;
    }

    /// <summary>
    /// The CLR type used to derive the request body schema.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The media type of the request body. Defaults to <c>application/json</c>.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// When <see langword="true"/>, the request body is required. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Optional description of the request body.
    /// </summary>
    public string? Description { get; set; }
}
