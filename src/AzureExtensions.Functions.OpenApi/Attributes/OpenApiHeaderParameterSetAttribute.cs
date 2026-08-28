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
/// behaves the same as an individually-declared <see cref="OpenApiHeaderParameterAttribute"/>. On
/// a case-insensitive name collision with an individual
/// <see cref="OpenApiHeaderParameterAttribute"/> declared on the same method, the individual
/// attribute wins and the set member is suppressed.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiHeaderParameterSetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiHeaderParameterSetAttribute"/> class.
    /// </summary>
    /// <param name="collectionType">
    /// The reusable header collection type. This must be a concrete, non-abstract type with a
    /// public parameterless constructor that implements
    /// <see cref="IOpenApiHeaderDefinitionCollection"/>.
    /// </param>
    public OpenApiHeaderParameterSetAttribute(Type collectionType)
    {
        CollectionType = collectionType;
    }

    /// <summary>
    /// The reusable header collection type.
    /// </summary>
    public Type CollectionType { get; }
}
