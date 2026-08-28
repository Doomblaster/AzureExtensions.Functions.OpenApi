namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Declares a response header for the OpenAPI operation produced by an HTTP-triggered function
/// method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. A response header is
/// emitted as an OpenAPI Header Object under <c>responses.{statusCode}.headers.{name}</c>. Apply it
/// once per header; multiple instances are allowed on a single method. A single instance fans out
/// to every status code it lists. When you already have a reusable
/// <see cref="IOpenApiHeaderDefinition"/> type, prefer
/// <see cref="OpenApiResponseHeaderAttribute{T}"/> so the same definition can be reused both as a
/// standalone response header and as a member of an
/// <see cref="IOpenApiHeaderDefinitionCollection"/> consumed by
/// <see cref="OpenApiResponseHeaderSetAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class OpenApiResponseHeaderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiResponseHeaderAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the response header.</param>
    /// <param name="type">The CLR type used to derive the header schema.</param>
    /// <param name="statusCodes">
    /// The HTTP status codes this header is attached to. When empty, the header is attached to
    /// every response documented for the method.
    /// </param>
    public OpenApiResponseHeaderAttribute(string name, Type type, params int[] statusCodes)
    {
        Name = name;
        Type = type;
        StatusCodes = statusCodes;
    }

    /// <summary>
    /// The name of the response header as it appears in the response.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The CLR type used to derive the header schema.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The HTTP status codes this header is attached to. When empty, the header is attached to
    /// every response documented for the method.
    /// </summary>
    public int[] StatusCodes { get; }

    /// <summary>
    /// When <see langword="true"/>, the header is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the header is deprecated.
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// Optional description of the header.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Declares a response header from a reusable <see cref="IOpenApiHeaderDefinition"/> type.
/// </summary>
/// <typeparam name="T">
/// The reusable header definition type. Prefer this generic form when the definition is also
/// reused inside an <see cref="IOpenApiHeaderDefinitionCollection"/> for bundled header sets.
/// </typeparam>
public sealed class OpenApiResponseHeaderAttribute<T> : OpenApiResponseHeaderAttribute
    where T : IOpenApiHeaderDefinition, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiResponseHeaderAttribute{T}"/> class.
    /// </summary>
    /// <param name="statusCodes">
    /// The HTTP status codes this header is attached to. When empty, the header is attached to
    /// every response documented for the method.
    /// </param>
    public OpenApiResponseHeaderAttribute(params int[] statusCodes)
        : this(new T(), statusCodes)
    {
    }

    private OpenApiResponseHeaderAttribute(IOpenApiHeaderDefinition definition, int[] statusCodes)
        : base(definition.Name, definition.Type, statusCodes)
    {
        Required = definition.Required;
        Deprecated = definition.Deprecated;
        Description = definition.Description;
    }
}
