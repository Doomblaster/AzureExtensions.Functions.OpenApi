namespace AzureExtensions.Functions.OpenApi;

/// <summary>
/// Describes the OpenAPI operation produced by an HTTP-triggered function method.
/// </summary>
/// <remarks>
/// This attribute is a pure metadata carrier consumed by OpenAPI discovery to populate the
/// operation's <c>operationId</c>, <c>summary</c>, <c>description</c>, <c>tags</c>, and
/// <c>deprecated</c> fields. Apply it to the method that backs an HTTP trigger.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiOperationAttribute : Attribute
{
    /// <summary>
    /// Optional stable identifier for the operation (<c>operationId</c>). When omitted,
    /// discovery infers one from the method or function name.
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Optional short summary of what the operation does (<c>summary</c>).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Optional longer description of the operation (<c>description</c>).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional tags used to group the operation in the document (<c>tags</c>).
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// When <see langword="true"/>, marks the operation as deprecated (<c>deprecated</c>).
    /// </summary>
    public bool Deprecated { get; set; }
}
