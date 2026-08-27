namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Declares a path (route) parameter for the OpenAPI operation produced by an HTTP-triggered
/// function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. Path parameters are
/// required by the OpenAPI specification, so <see cref="Required"/> defaults to
/// <see langword="true"/>. Apply it once per path parameter; multiple instances are allowed on a
/// single method.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiPathParameterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiPathParameterAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the path parameter.</param>
    /// <param name="type">The CLR type used to derive the parameter schema.</param>
    public OpenApiPathParameterAttribute(string name, Type type)
    {
        Name = name;
        Type = type;
    }

    /// <summary>
    /// The name of the path parameter as it appears in the route template.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The CLR type used to derive the parameter schema.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Whether the parameter is required. Defaults to <see langword="true"/> because path
    /// parameters are always required by the OpenAPI specification.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Optional description of the parameter.
    /// </summary>
    public string? Description { get; set; }
}
