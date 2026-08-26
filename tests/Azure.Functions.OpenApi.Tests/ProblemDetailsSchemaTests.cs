using System.Linq;
using Azure.Functions.OpenApi.Schema;
using Microsoft.OpenApi;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;
using MvcValidationProblemDetails = Microsoft.AspNetCore.Mvc.ValidationProblemDetails;
using HttpValidationProblemDetails = Microsoft.AspNetCore.Http.HttpValidationProblemDetails;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Tests for the canonical RFC 9457 <c>ProblemDetails</c> family component schemas emitted by
/// <see cref="OpenApiSchemaGenerator"/> (Microsoft.OpenApi 3.10.2).
/// </summary>
public sealed class ProblemDetailsSchemaTests
{
    private const string ProblemDetailsId = "ProblemDetails";

    private static (OpenApiSchemaGenerator Generator, OpenApiComponents Components) NewGenerator()
    {
        var components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, IOpenApiSchema>(),
        };
        return (new OpenApiSchemaGenerator(), components);
    }

    [Fact]
    public void ProblemDetails_RegistersComponent_AndReturnsReference()
    {
        var (generator, components) = NewGenerator();

        var schema = generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);

        var reference = Assert.IsType<OpenApiSchemaReference>(schema);
        Assert.Equal(ProblemDetailsId, reference.Reference!.Id);
        Assert.True(components.Schemas!.ContainsKey(ProblemDetailsId));
    }

    [Fact]
    public void ProblemDetails_Component_HasExactlyLowercaseRfc9457Members()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);
        var component = Assert.IsType<OpenApiSchema>(components.Schemas![ProblemDetailsId]);

        Assert.Equal(JsonSchemaType.Object, component.Type);
        Assert.NotNull(component.Properties);

        // Exactly the five RFC 9457 members, all lowercase, in the canonical order.
        Assert.Equal(
            new[] { "type", "title", "status", "detail", "instance" },
            component.Properties!.Keys.ToArray());
    }

    [Fact]
    public void ProblemDetails_Component_HasNoExtensionsProperty()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);
        var component = Assert.IsType<OpenApiSchema>(components.Schemas![ProblemDetailsId]);

        Assert.False(component.Properties!.ContainsKey("extensions"));
        Assert.False(component.Properties!.ContainsKey("Extensions"));
    }

    [Fact]
    public void ProblemDetails_Component_StatusIsInteger()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);
        var component = Assert.IsType<OpenApiSchema>(components.Schemas![ProblemDetailsId]);

        var status = Assert.IsType<OpenApiSchema>(component.Properties!["status"]);
        Assert.True(status.Type!.Value.HasFlag(JsonSchemaType.Integer));
    }

    [Fact]
    public void ProblemDetails_Component_TypeAndInstanceCarryUriFormat()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);
        var component = Assert.IsType<OpenApiSchema>(components.Schemas![ProblemDetailsId]);

        var type = Assert.IsType<OpenApiSchema>(component.Properties!["type"]);
        var instance = Assert.IsType<OpenApiSchema>(component.Properties!["instance"]);

        Assert.Equal("uri", type.Format);
        Assert.Equal("uri", instance.Format);
    }

    [Fact]
    public void ProblemDetails_Component_MembersAreNullable()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);
        var component = Assert.IsType<OpenApiSchema>(components.Schemas![ProblemDetailsId]);

        foreach (var member in new[] { "type", "title", "status", "detail", "instance" })
        {
            var schema = Assert.IsType<OpenApiSchema>(component.Properties![member]);
            Assert.True(
                schema.Type!.Value.HasFlag(JsonSchemaType.Null),
                $"Member '{member}' should be nullable.");
        }
    }

    [Fact]
    public void ProblemDetails_Component_AllowsExtensionMembers_ViaAdditionalProperties()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);
        var component = Assert.IsType<OpenApiSchema>(components.Schemas![ProblemDetailsId]);

        // RFC 9457 extension members are permitted: additionalProperties must be present (emitted
        // as "additionalProperties: {}" by Microsoft.OpenApi 3.10.2).
        Assert.NotNull(component.AdditionalProperties);
    }

    [Fact]
    public void HttpValidationProblemDetails_ComposesProblemDetailsWithErrorsMap()
    {
        var (generator, components) = NewGenerator();

        var schema = generator.GetOrCreateSchema(typeof(HttpValidationProblemDetails), components);

        AssertValidationVariant(schema, components, nameof(HttpValidationProblemDetails));
    }

    [Fact]
    public void ValidationProblemDetails_ComposesProblemDetailsWithErrorsMap()
    {
        var (generator, components) = NewGenerator();

        var schema = generator.GetOrCreateSchema(typeof(MvcValidationProblemDetails), components);

        AssertValidationVariant(schema, components, "ValidationProblemDetails");
    }

    [Fact]
    public void ValidationVariants_RegisterProblemDetailsComponent_ExactlyOnce()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(HttpValidationProblemDetails), components);
        generator.GetOrCreateSchema(typeof(MvcValidationProblemDetails), components);
        generator.GetOrCreateSchema(typeof(MvcProblemDetails), components);

        Assert.Single(components.Schemas!.Keys, k => k == ProblemDetailsId);
    }

    private static void AssertValidationVariant(
        IOpenApiSchema schema,
        OpenApiComponents components,
        string componentId)
    {
        var reference = Assert.IsType<OpenApiSchemaReference>(schema);
        Assert.Equal(componentId, reference.Reference!.Id);

        var component = Assert.IsType<OpenApiSchema>(components.Schemas![componentId]);
        Assert.NotNull(component.AllOf);
        Assert.Equal(2, component.AllOf!.Count);

        // First allOf member references the single canonical ProblemDetails base component.
        var baseRef = Assert.IsType<OpenApiSchemaReference>(component.AllOf[0]);
        Assert.Equal(ProblemDetailsId, baseRef.Reference!.Id);

        // Second allOf member adds the errors map: object<string, string[]>.
        var errorsHost = Assert.IsType<OpenApiSchema>(component.AllOf[1]);
        var errors = Assert.IsType<OpenApiSchema>(errorsHost.Properties!["errors"]);
        Assert.Equal(JsonSchemaType.Object, errors.Type);

        var errorValue = Assert.IsType<OpenApiSchema>(errors.AdditionalProperties);
        Assert.Equal(JsonSchemaType.Array, errorValue.Type);

        var errorItem = Assert.IsType<OpenApiSchema>(errorValue.Items);
        Assert.Equal(JsonSchemaType.String, errorItem.Type);
    }
}
