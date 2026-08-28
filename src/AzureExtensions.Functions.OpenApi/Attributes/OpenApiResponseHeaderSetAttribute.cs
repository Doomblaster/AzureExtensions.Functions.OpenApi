namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Declares a reusable set of response headers for the OpenAPI operation produced by an
/// HTTP-triggered function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery. The
/// <c>collectionType</c> must be a concrete, non-abstract type with a public
/// parameterless constructor that implements
/// <see cref="IOpenApiHeaderDefinitionCollection"/>. The <c>statusCodes</c>
/// semantics mirror <see cref="OpenApiResponseHeaderAttribute"/>: when empty, the header set
/// applies to every response documented for the method. When reusable
/// <see cref="IOpenApiHeaderDefinition"/> types exist, prefer
/// <see cref="OpenApiResponseHeaderAttribute{T}"/> for standalone usage and this attribute for
/// bundled reuse through an <see cref="IOpenApiHeaderDefinitionCollection"/>. On a
/// case-insensitive name collision with an individual
/// <see cref="OpenApiResponseHeaderAttribute"/> targeting the same status code, the individual
/// attribute wins for that status code.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class OpenApiResponseHeaderSetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiResponseHeaderSetAttribute"/> class.
    /// </summary>
    /// <param name="collectionType">
    /// The reusable header collection type. This must be a concrete, non-abstract type with a
    /// public parameterless constructor that implements
    /// <see cref="IOpenApiHeaderDefinitionCollection"/>.
    /// </param>
    /// <param name="statusCodes">
    /// The HTTP status codes this header set is attached to. When empty, the header set is
    /// attached to every response documented for the method.
    /// </param>
    public OpenApiResponseHeaderSetAttribute(Type collectionType, params int[] statusCodes)
    {
        CollectionType = collectionType;
        StatusCodes = statusCodes;
    }

    /// <summary>
    /// The reusable header collection type.
    /// </summary>
    public Type CollectionType { get; }

    /// <summary>
    /// The HTTP status codes this header set is attached to. When empty, the header set is
    /// attached to every response documented for the method.
    /// </summary>
    public int[] StatusCodes { get; }
}

/// <summary>
/// Declares a reusable response-header set from a strongly typed
/// <see cref="IOpenApiHeaderDefinitionCollection"/>.
/// </summary>
/// <typeparam name="T">
/// The reusable header collection type. Prefer this generic form when the collection should be
/// parameterless-constructible at compile time and its member definitions are also reused through
/// <see cref="OpenApiResponseHeaderAttribute{T}"/>.
/// </typeparam>
public sealed class OpenApiResponseHeaderSetAttribute<T> : OpenApiResponseHeaderSetAttribute
    where T : IOpenApiHeaderDefinitionCollection, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiResponseHeaderSetAttribute{T}"/> class.
    /// </summary>
    /// <param name="statusCodes">
    /// The HTTP status codes this header set is attached to. When empty, the header set is
    /// attached to every response documented for the method.
    /// </param>
    public OpenApiResponseHeaderSetAttribute(params int[] statusCodes)
        : base(typeof(T), statusCodes)
    {
    }
}
