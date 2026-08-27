namespace AzureExtensions.Functions.OpenApi.Schema;

/// <summary>
/// Identifies the ASP.NET Core <c>ProblemDetails</c> family (RFC 9457) by full type name so the
/// library can emit canonical, hand-authored component schemas for them instead of reflecting.
/// </summary>
/// <remarks>
/// <para>
/// Detection is performed purely on <see cref="System.Type.FullName"/>. This intentionally avoids
/// a compile-time dependency on <c>Microsoft.AspNetCore.Mvc</c> / <c>Microsoft.AspNetCore.Http</c>:
/// consumers reference those types, but this library only needs to recognise them by name.
/// </para>
/// <para>
/// Plain reflection is wrong for these types for two reasons: the RFC 9457 / ASP.NET Core JSON
/// contract serialises the members in lowercase (<c>type</c>, <c>title</c>, <c>status</c>,
/// <c>detail</c>, <c>instance</c>) whereas reflection would emit PascalCase, and
/// <c>ProblemDetails.Extensions</c> is <c>[JsonExtensionData]</c> — flattened to the top level on
/// the wire rather than nested under an <c>extensions</c> property.
/// </para>
/// </remarks>
internal static class ProblemDetailsTypes
{
    /// <summary>Full name of <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c>.</summary>
    public const string ProblemDetailsFullName = "Microsoft.AspNetCore.Mvc.ProblemDetails";

    /// <summary>Full name of <c>Microsoft.AspNetCore.Mvc.ValidationProblemDetails</c>.</summary>
    public const string ValidationProblemDetailsFullName = "Microsoft.AspNetCore.Mvc.ValidationProblemDetails";

    /// <summary>Full name of <c>Microsoft.AspNetCore.Http.HttpValidationProblemDetails</c>.</summary>
    public const string HttpValidationProblemDetailsFullName = "Microsoft.AspNetCore.Http.HttpValidationProblemDetails";

    /// <summary>
    /// Distinguishes the base problem type from the validation variants so callers can pick the
    /// correct canonical component schema.
    /// </summary>
    public enum ProblemKind
    {
        /// <summary>The type is not part of the <c>ProblemDetails</c> family.</summary>
        None = 0,

        /// <summary>The base <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c> type.</summary>
        ProblemDetails,

        /// <summary>The <c>Microsoft.AspNetCore.Mvc.ValidationProblemDetails</c> type.</summary>
        ValidationProblemDetails,

        /// <summary>The <c>Microsoft.AspNetCore.Http.HttpValidationProblemDetails</c> type.</summary>
        HttpValidationProblemDetails,
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is one of the recognised
    /// <c>ProblemDetails</c> family types (detected by full name).
    /// </summary>
    /// <param name="type">The CLR type to inspect.</param>
    /// <returns><see langword="true"/> if the type is a problem-details type; otherwise <see langword="false"/>.</returns>
    public static bool IsProblemDetails(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Classify(type) != ProblemKind.None;
    }

    /// <summary>
    /// Classifies <paramref name="type"/> as one of the <c>ProblemDetails</c> family kinds, or
    /// <see cref="ProblemKind.None"/> when it is unrelated.
    /// </summary>
    /// <param name="type">The CLR type to classify.</param>
    /// <returns>The matching <see cref="ProblemKind"/>.</returns>
    public static ProblemKind Classify(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.FullName switch
        {
            ProblemDetailsFullName => ProblemKind.ProblemDetails,
            ValidationProblemDetailsFullName => ProblemKind.ValidationProblemDetails,
            HttpValidationProblemDetailsFullName => ProblemKind.HttpValidationProblemDetails,
            _ => ProblemKind.None,
        };
    }
}
