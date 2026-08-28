namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Declares a reusable set of header parameters for the OpenAPI operation produced by an
/// HTTP-triggered function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. The
/// <c>collectionType</c> must be a concrete, non-abstract type with a public
/// parameterless constructor that implements
/// <see cref="IOpenApiHeaderDefinitionCollection"/>. Each header declared by the collection
/// behaves the same as an individually-declared
/// <see cref="OpenApiRequestHeaderParameterAttribute"/>. When a reusable
/// <see cref="IOpenApiHeaderDefinition"/> type exists, prefer
/// <see cref="OpenApiRequestHeaderParameterAttribute{T}"/> for standalone usage and this
/// attribute for bundled reuse through an <see cref="IOpenApiHeaderDefinitionCollection"/>. On a
/// case-insensitive name collision with an individual
/// <see cref="OpenApiRequestHeaderParameterAttribute"/> declared on the same method, the
/// individual attribute wins and the set member is suppressed.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class OpenApiRequestHeaderParameterSetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiRequestHeaderParameterSetAttribute"/> class.
    /// </summary>
    /// <param name="collectionType">
    /// The reusable header collection type. This must be a concrete, non-abstract type with a
    /// public parameterless constructor that implements
    /// <see cref="IOpenApiHeaderDefinitionCollection"/>.
    /// </param>
    public OpenApiRequestHeaderParameterSetAttribute(Type collectionType)
    {
        CollectionType = collectionType;
    }

    /// <summary>
    /// The reusable header collection type.
    /// </summary>
    public Type CollectionType { get; }
}

/// <summary>
/// Declares a reusable request-header set from a strongly typed
/// <see cref="IOpenApiHeaderDefinitionCollection"/>.
/// </summary>
/// <typeparam name="T">
/// The reusable header collection type. Prefer this generic form when the collection should be
/// parameterless-constructible at compile time and its member definitions are also reused through
/// <see cref="OpenApiRequestHeaderParameterAttribute{T}"/>.
/// </typeparam>
public sealed class OpenApiRequestHeaderParameterSetAttribute<T> : OpenApiRequestHeaderParameterSetAttribute
    where T : IOpenApiHeaderDefinitionCollection, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiRequestHeaderParameterSetAttribute{T}"/>
    /// class.
    /// </summary>
    public OpenApiRequestHeaderParameterSetAttribute()
        : base(typeof(T))
    {
    }
}
