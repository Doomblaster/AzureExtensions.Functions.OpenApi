namespace Azure.Functions.OpenApi;

/// <summary>
/// Declares a header parameter for the OpenAPI operation produced by an HTTP-triggered
/// function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. Apply it once per
/// header parameter; multiple instances are allowed on a single method.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiHeaderParameterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiHeaderParameterAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the header parameter.</param>
    /// <param name="type">The CLR type used to derive the parameter schema.</param>
    public OpenApiHeaderParameterAttribute(string name, Type type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>
    /// The name of the header parameter as it appears in the request.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The CLR type used to derive the parameter schema.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// When <see langword="true"/>, the parameter is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Optional description of the parameter.
    /// </summary>
    public string? Description { get; set; }
}
