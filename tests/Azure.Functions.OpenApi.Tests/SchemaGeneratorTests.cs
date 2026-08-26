using System.Linq;
using System.Text.Json.Nodes;
using Azure.Functions.OpenApi.Schema;
using Microsoft.OpenApi;
using Xunit;

namespace Azure.Functions.OpenApi.Tests;

/// <summary>
/// Tests for <see cref="OpenApiSchemaGenerator"/> CLR-to-OpenAPI schema mapping (Microsoft.OpenApi 3.10.2).
/// </summary>
public sealed class SchemaGeneratorTests
{
    private enum Color
    {
        Red,
        Green,
        Blue,
    }

    private sealed class Nested
    {
        public int Count { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class Node
    {
        public string Name { get; set; } = string.Empty;

        public Node? Parent { get; set; }
    }

    // Exercises the nullable-handling matrix: nullable enum ref, nullable nested-object ref,
    // nullable inline scalar, plus the non-nullable counterparts and inline primitives. The enum
    // is referenced twice (Favorite + Status) so a single-registration assertion has something to
    // prove.
    private sealed class NullableHost
    {
        public Color? Favorite { get; set; }

        public Color Status { get; set; }

        public Nested? Child { get; set; }

        public Nested Mandatory { get; set; } = new();

        public string? Nickname { get; set; }

        public string Required { get; set; } = string.Empty;

        public int Age { get; set; }

        public decimal Amount { get; set; }
    }

    private static (OpenApiSchema Schema, OpenApiComponents Components) RegisterObject(Type type)
    {
        var (generator, components) = NewGenerator();
        generator.GetOrCreateSchema(type, components);
        var schema = Assert.IsType<OpenApiSchema>(components.Schemas![type.Name]);
        return (schema, components);
    }

    private static (OpenApiSchemaGenerator Generator, OpenApiComponents Components) NewGenerator()
    {
        var components = new OpenApiComponents
        {
            Schemas = new Dictionary<string, IOpenApiSchema>(),
        };
        return (new OpenApiSchemaGenerator(), components);
    }

    private static OpenApiSchema Inline(Type type)
    {
        var (generator, components) = NewGenerator();
        var schema = generator.GetOrCreateSchema(type, components);
        return Assert.IsType<OpenApiSchema>(schema);
    }

    [Fact]
    public void Int_MapsToInteger_Int32()
    {
        var schema = Inline(typeof(int));

        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Equal("int32", schema.Format);
    }

    [Fact]
    public void Long_MapsToInteger_Int64()
    {
        var schema = Inline(typeof(long));

        Assert.Equal(JsonSchemaType.Integer, schema.Type);
        Assert.Equal("int64", schema.Format);
    }

    [Fact]
    public void Decimal_MapsToNumber_Decimal()
    {
        var schema = Inline(typeof(decimal));

        Assert.Equal(JsonSchemaType.Number, schema.Type);
        Assert.Equal("decimal", schema.Format);
    }

    [Fact]
    public void Bool_MapsToBoolean()
    {
        Assert.Equal(JsonSchemaType.Boolean, Inline(typeof(bool)).Type);
    }

    [Fact]
    public void String_MapsToString()
    {
        var schema = Inline(typeof(string));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Null(schema.Format);
    }

    [Fact]
    public void DateTimeOffset_MapsToStringDateTime()
    {
        var schema = Inline(typeof(DateTimeOffset));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("date-time", schema.Format);
    }

    [Fact]
    public void Guid_MapsToStringUuid()
    {
        var schema = Inline(typeof(Guid));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("uuid", schema.Format);
    }

    [Fact]
    public void DateTime_MapsToStringDateTime()
    {
        var schema = Inline(typeof(DateTime));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("date-time", schema.Format);
    }

    [Fact]
    public void DateOnly_MapsToStringDate()
    {
        var schema = Inline(typeof(DateOnly));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("date", schema.Format);
    }

    [Fact]
    public void TimeOnly_MapsToStringTime()
    {
        var schema = Inline(typeof(TimeOnly));

        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal("time", schema.Format);
    }

    [Theory]
    [InlineData(typeof(Guid?), "uuid")]
    [InlineData(typeof(DateTime?), "date-time")]
    [InlineData(typeof(DateTimeOffset?), "date-time")]
    [InlineData(typeof(DateOnly?), "date")]
    [InlineData(typeof(TimeOnly?), "time")]
    public void NullableTemporalOrGuid_CarriesStringAndNullFlags_AndPreservesFormat(Type type, string expectedFormat)
    {
        var schema = Inline(type);

        Assert.True(schema.Type!.Value.HasFlag(JsonSchemaType.String));
        Assert.True(schema.Type!.Value.HasFlag(JsonSchemaType.Null));
        Assert.Equal(expectedFormat, schema.Format);

        // Nullable inline scalars stay inline; they are not turned into an anyOf union.
        Assert.True(schema.AnyOf is null || schema.AnyOf.Count == 0);
    }

    [Fact]
    public void Enum_RegisteredInComponents_AndReturnedAsReference()
    {
        var (generator, components) = NewGenerator();

        var schema = generator.GetOrCreateSchema(typeof(Color), components);

        // Enums are now hoisted: the use site emits a $ref, the definition lives in components.
        var reference = Assert.IsType<OpenApiSchemaReference>(schema);
        Assert.Equal(nameof(Color), reference.Reference!.Id);
        Assert.True(components.Schemas!.ContainsKey(nameof(Color)));

        var registered = Assert.IsType<OpenApiSchema>(components.Schemas![nameof(Color)]);
        Assert.Equal(JsonSchemaType.String, registered.Type);
        Assert.NotNull(registered.Enum);
        var names = registered.Enum!.Select(n => n!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "Red", "Green", "Blue" }, names);
    }

    [Fact]
    public void Enum_RegisteredExactlyOnce_WhenUsedAtMultipleSites()
    {
        // NullableHost references Color twice (Favorite + Status); it must be registered once.
        var (_, components) = RegisterObject(typeof(NullableHost));

        Assert.Single(components.Schemas!.Keys, k => k == nameof(Color));
    }

    [Fact]
    public void NullableEnumProperty_MapsToAnyOfRefPlusNull()
    {
        var (schema, _) = RegisterObject(typeof(NullableHost));

        var favorite = Assert.IsType<OpenApiSchema>(schema.Properties![nameof(NullableHost.Favorite)]);
        Assert.NotNull(favorite.AnyOf);
        Assert.Equal(2, favorite.AnyOf!.Count);

        var enumRef = Assert.IsType<OpenApiSchemaReference>(favorite.AnyOf[0]);
        Assert.Equal(nameof(Color), enumRef.Reference!.Id);

        var nullMember = Assert.IsType<OpenApiSchema>(favorite.AnyOf[1]);
        Assert.Equal(JsonSchemaType.Null, nullMember.Type);
    }

    [Fact]
    public void NullableNestedObjectProperty_MapsToAnyOfRefPlusNull()
    {
        var (schema, _) = RegisterObject(typeof(NullableHost));

        var child = Assert.IsType<OpenApiSchema>(schema.Properties![nameof(NullableHost.Child)]);
        Assert.NotNull(child.AnyOf);
        Assert.Equal(2, child.AnyOf!.Count);

        var objectRef = Assert.IsType<OpenApiSchemaReference>(child.AnyOf[0]);
        Assert.Equal(nameof(Nested), objectRef.Reference!.Id);

        var nullMember = Assert.IsType<OpenApiSchema>(child.AnyOf[1]);
        Assert.Equal(JsonSchemaType.Null, nullMember.Type);
    }

    [Fact]
    public void NullableStringProperty_CarriesStringAndNullTypeFlags()
    {
        var (schema, _) = RegisterObject(typeof(NullableHost));

        var nickname = Assert.IsType<OpenApiSchema>(schema.Properties![nameof(NullableHost.Nickname)]);
        Assert.True(nickname.Type!.Value.HasFlag(JsonSchemaType.String));
        Assert.True(nickname.Type!.Value.HasFlag(JsonSchemaType.Null));

        // A nullable inline scalar stays inline; it is not turned into an anyOf union.
        Assert.True(nickname.AnyOf is null || nickname.AnyOf.Count == 0);
    }

    [Fact]
    public void NonNullableStringProperty_StaysPlainString()
    {
        var (schema, _) = RegisterObject(typeof(NullableHost));

        var required = Assert.IsType<OpenApiSchema>(schema.Properties![nameof(NullableHost.Required)]);
        Assert.Equal(JsonSchemaType.String, required.Type);
        Assert.False(required.Type!.Value.HasFlag(JsonSchemaType.Null));
    }

    [Fact]
    public void NonNullableReferenceProperty_StaysPlainReference()
    {
        var (schema, _) = RegisterObject(typeof(NullableHost));

        // A non-nullable object reference is a bare $ref, not wrapped in an anyOf+null union.
        var mandatory = Assert.IsType<OpenApiSchemaReference>(schema.Properties![nameof(NullableHost.Mandatory)]);
        Assert.Equal(nameof(Nested), mandatory.Reference!.Id);
    }

    [Fact]
    public void BarePrimitiveProperties_StayInline_AndAreNotHoisted()
    {
        var (schema, components) = RegisterObject(typeof(NullableHost));

        var age = Assert.IsType<OpenApiSchema>(schema.Properties![nameof(NullableHost.Age)]);
        Assert.Equal(JsonSchemaType.Integer, age.Type);
        Assert.Equal("int32", age.Format);

        var amount = Assert.IsType<OpenApiSchema>(schema.Properties![nameof(NullableHost.Amount)]);
        Assert.Equal(JsonSchemaType.Number, amount.Type);
        Assert.Equal("decimal", amount.Format);

        // Primitives are never registered as components (no int/decimal/string hoisting).
        Assert.False(components.Schemas!.ContainsKey("Int32"));
        Assert.False(components.Schemas!.ContainsKey("Decimal"));
        Assert.False(components.Schemas!.ContainsKey("String"));
    }

    [Fact]
    public void List_MapsToArrayWithItems()
    {
        var schema = Inline(typeof(List<int>));

        Assert.Equal(JsonSchemaType.Array, schema.Type);
        var items = Assert.IsType<OpenApiSchema>(schema.Items);
        Assert.Equal(JsonSchemaType.Integer, items.Type);
    }

    [Fact]
    public void Dictionary_MapsToObjectWithAdditionalProperties()
    {
        var schema = Inline(typeof(Dictionary<string, bool>));

        Assert.Equal(JsonSchemaType.Object, schema.Type);
        var additional = Assert.IsType<OpenApiSchema>(schema.AdditionalProperties);
        Assert.Equal(JsonSchemaType.Boolean, additional.Type);
    }

    [Fact]
    public void NullableInt_CarriesNullFlag()
    {
        var schema = Inline(typeof(int?));

        Assert.True(schema.Type!.Value.HasFlag(JsonSchemaType.Integer));
        Assert.True(schema.Type!.Value.HasFlag(JsonSchemaType.Null));
    }

    [Fact]
    public void ComplexType_RegisteredInComponents_AndReturnedAsReference()
    {
        var (generator, components) = NewGenerator();

        var schema = generator.GetOrCreateSchema(typeof(Nested), components);

        Assert.IsType<OpenApiSchemaReference>(schema);
        Assert.True(components.Schemas!.ContainsKey(nameof(Nested)));

        var registered = Assert.IsType<OpenApiSchema>(components.Schemas![nameof(Nested)]);
        Assert.Equal(JsonSchemaType.Object, registered.Type);
        Assert.True(registered.Properties!.ContainsKey(nameof(Nested.Count)));
        Assert.True(registered.Properties!.ContainsKey(nameof(Nested.Label)));
    }

    [Fact]
    public void ComplexType_RegisteredOnce_WhenRequestedTwice()
    {
        var (generator, components) = NewGenerator();

        generator.GetOrCreateSchema(typeof(Nested), components);
        generator.GetOrCreateSchema(typeof(Nested), components);

        Assert.Single(components.Schemas!.Keys, k => k == nameof(Nested));
    }

    [Fact]
    public void SelfReferencingType_DoesNotInfiniteLoop()
    {
        var (generator, components) = NewGenerator();

        var schema = generator.GetOrCreateSchema(typeof(Node), components);

        Assert.IsType<OpenApiSchemaReference>(schema);
        Assert.True(components.Schemas!.ContainsKey(nameof(Node)));

        var node = Assert.IsType<OpenApiSchema>(components.Schemas![nameof(Node)]);

        // Parent is `Node?` (nullable self-reference): it resolves to an anyOf[ $ref, null ] union
        // rather than looping into another inline object. The $ref member proves the recursion
        // guard reused the in-progress registration.
        var parent = Assert.IsType<OpenApiSchema>(node.Properties![nameof(Node.Parent)]);
        Assert.NotNull(parent.AnyOf);
        Assert.Equal(2, parent.AnyOf!.Count);
        var parentRef = Assert.IsType<OpenApiSchemaReference>(parent.AnyOf[0]);
        Assert.Equal(nameof(Node), parentRef.Reference!.Id);
        Assert.Equal(JsonSchemaType.Null, Assert.IsType<OpenApiSchema>(parent.AnyOf[1]).Type);
    }
}
