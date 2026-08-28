namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Declares a header parameter for the OpenAPI operation produced by an HTTP-triggered
/// function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. Apply it once per
/// header parameter; multiple instances are allowed on a single method. When you already have a
/// reusable <see cref="IOpenApiHeaderDefinition"/> type, prefer
/// <see cref="OpenApiRequestHeaderParameterAttribute{T}"/> so the same definition can be reused
/// both as a standalone request header and as a member of an
/// <see cref="IOpenApiHeaderDefinitionCollection"/> consumed by
/// <see cref="OpenApiRequestHeaderParameterSetAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class OpenApiRequestHeaderParameterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiRequestHeaderParameterAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the header parameter.</param>
    /// <param name="type">The CLR type used to derive the parameter schema.</param>
    public OpenApiRequestHeaderParameterAttribute(string name, Type type)
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
    /// When <see langword="true"/>, the header is deprecated.
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// Optional description of the parameter.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Declares a request header parameter from a reusable
/// <see cref="IOpenApiHeaderDefinition"/> type.
/// </summary>
/// <typeparam name="T">
/// The reusable header definition type. Prefer this generic form when the definition is also
/// reused inside an <see cref="IOpenApiHeaderDefinitionCollection"/> for bundled header sets.
/// </typeparam>
public sealed class OpenApiRequestHeaderParameterAttribute<T> : OpenApiRequestHeaderParameterAttribute
    where T : IOpenApiHeaderDefinition, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiRequestHeaderParameterAttribute{T}"/>
    /// class.
    /// </summary>
    public OpenApiRequestHeaderParameterAttribute()
        : this(new T())
    {
    }

    private OpenApiRequestHeaderParameterAttribute(IOpenApiHeaderDefinition definition)
        : base(definition.Name, definition.Type)
    {
        Required = definition.Required;
        Deprecated = definition.Deprecated;
        Description = definition.Description;
    }
}
