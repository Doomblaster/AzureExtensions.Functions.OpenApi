using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Azure.Functions.OpenApi.Schema;

/// <summary>
/// Maps a CLR <see cref="Type"/> to a Microsoft.OpenApi (3.10.2) schema.
/// </summary>
/// <remarks>
/// <para>
/// Primitive and well-known scalar types are returned as inline <see cref="OpenApiSchema"/>
/// instances. Complex object types (classes and records) are registered exactly once into the
/// supplied <see cref="OpenApiComponents.Schemas"/> dictionary under a stable schema id and are
/// returned as an <see cref="OpenApiSchemaReference"/> (a <c>$ref</c> to
/// <c>#/components/schemas/{id}</c>).
/// </para>
/// <para>
/// In Microsoft.OpenApi 3.10.2 the schema type is expressed via the <see cref="JsonSchemaType"/>
/// <c>[Flags]</c> enum on <see cref="IOpenApiSchema.Type"/>. Nullability of an inline concrete
/// schema is modelled by OR-ing <see cref="JsonSchemaType.Null"/> into the type flags (JSON
/// Schema 2020-12 style, which the 3.1 wire format emits as e.g. <c>"type": ["string", "null"]</c>).
/// A reference (or any schema that cannot carry the flag, such as a hoisted enum or nested object)
/// is instead wrapped in an <c>anyOf: [ &lt;schema&gt;, { "type": "null" } ]</c> union.
/// </para>
/// <para>
/// The generator is deterministic and side-effect-free apart from populating the
/// <see cref="OpenApiComponents.Schemas"/> dictionary passed to
/// <see cref="GetOrCreateSchema(Type, OpenApiComponents)"/>. A per-instance registry guards
/// against infinite recursion on self-referencing types: the component shell is registered
/// before its properties are filled in.
/// </para>
/// </remarks>
internal sealed class OpenApiSchemaGenerator
{
    // CLR type -> registered component schema id. Also acts as the recursion guard: a type is
    // added here BEFORE its properties are populated so a self-reference resolves to the $ref.
    private readonly Dictionary<Type, string> _registeredIds = new();

    // Reverse lookup so schema-id disambiguation never assigns one id to two different types.
    private readonly Dictionary<string, Type> _idOwners = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns an OpenAPI schema for <paramref name="type"/>. Primitive and scalar types are
    /// returned inline; complex object types are registered into <paramref name="components"/>
    /// and returned as a <see cref="OpenApiSchemaReference"/>.
    /// </summary>
    /// <param name="type">The CLR type to map.</param>
    /// <param name="components">The shared components object whose <see cref="OpenApiComponents.Schemas"/> receives object schemas.</param>
    /// <returns>An inline schema or a reference schema, whichever is correct for <paramref name="type"/>.</returns>
    public IOpenApiSchema GetOrCreateSchema(Type type, OpenApiComponents components)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(components);

        // Unwrap Nullable<T> and mark the resulting schema nullable.
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return MakeNullable(GetOrCreateSchema(underlying, components));
        }

        // Well-known scalar / primitive mappings (all inline).
        if (TryCreatePrimitive(type, out var primitive))
        {
            return primitive;
        }

        // Enums: registered once as a component and returned as a reference.
        if (type.IsEnum)
        {
            return CreateOrReferenceEnum(type, components);
        }

        // ProblemDetails family (RFC 9457): emit canonical hand-authored components. This must run
        // before the dictionary/enumerable/complex-object branches because these types would
        // otherwise be reflected into structurally-wrong object schemas.
        if (ProblemDetailsTypes.IsProblemDetails(type))
        {
            return CreateOrReferenceProblemDetails(type, components);
        }

        // Dictionaries: object with additionalProperties = schema of the value type.
        if (TryGetDictionaryValueType(type, out var valueType))
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = GetOrCreateSchema(valueType, components),
            };
        }

        // Collections / arrays: array with items = schema of the element type.
        if (TryGetEnumerableElementType(type, out var elementType))
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = GetOrCreateSchema(elementType, components),
            };
        }

        // Anything else that carries readable properties is treated as a complex object and
        // registered as a reusable component. Types with no usable shape fall back to an
        // untyped (any) schema.
        if (IsComplexObject(type))
        {
            return CreateOrReferenceObject(type, components);
        }

        // Fallback: an empty schema means "any value is allowed".
        return new OpenApiSchema();
    }

    private IOpenApiSchema CreateOrReferenceObject(Type type, OpenApiComponents components)
    {
        // Already registered (or currently being registered): return a reference and stop.
        if (_registeredIds.TryGetValue(type, out var existingId))
        {
            return new OpenApiSchemaReference(existingId);
        }

        var id = ReserveSchemaId(type);

        // Register the shell BEFORE recursing into properties so self-references resolve to the
        // $ref instead of looping forever.
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
        };

        components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        components.Schemas[id] = schema;

        // NullabilityInfoContext reads C# nullable-reference-type annotations (e.g. string? or
        // ItemDimensions?) that are otherwise invisible to reflection. It is not thread-safe, so
        // a fresh local instance is created per CreateOrReferenceObject call.
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var property in GetReadableProperties(type))
        {
            var propertySchema = GetOrCreateSchema(property.PropertyType, components);

            // Nullable<T> value types (int?, SomeEnum?, ...) are already made nullable inside
            // GetOrCreateSchema via Nullable.GetUnderlyingType, so only reference-type nullability
            // is applied here to avoid double-wrapping.
            if (Nullable.GetUnderlyingType(property.PropertyType) is null &&
                nullabilityContext.Create(property).ReadState == NullabilityState.Nullable)
            {
                propertySchema = MakeNullable(propertySchema);
            }

            schema.Properties[property.Name] = propertySchema;
        }

        return new OpenApiSchemaReference(id);
    }

    private IOpenApiSchema CreateOrReferenceEnum(Type enumType, OpenApiComponents components)
    {
        // Already registered: return a reference so an enum used N times is emitted once.
        if (_registeredIds.TryGetValue(enumType, out var existingId))
        {
            return new OpenApiSchemaReference(existingId);
        }

        var id = ReserveSchemaId(enumType);

        components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        components.Schemas[id] = CreateEnumSchema(enumType);

        return new OpenApiSchemaReference(id);
    }

    // The registered component id of the single canonical ProblemDetails base schema, shared by
    // both validation variants via allOf. Null until the base is first registered.
    private string? _problemDetailsBaseId;

    private IOpenApiSchema CreateOrReferenceProblemDetails(Type type, OpenApiComponents components)
    {
        // Already registered (or currently being registered): return a reference and stop.
        if (_registeredIds.TryGetValue(type, out var existingId))
        {
            return new OpenApiSchemaReference(existingId);
        }

        var kind = ProblemDetailsTypes.Classify(type);

        // The base ProblemDetails type maps straight to the canonical base component.
        if (kind == ProblemDetailsTypes.ProblemKind.ProblemDetails)
        {
            return new OpenApiSchemaReference(RegisterBaseProblemDetails(type, components));
        }

        // Validation variants compose the base component with an errors map:
        // allOf: [ { $ref: ProblemDetails }, { errors: object<string, string[]> } ]. Registering
        // the variant first ensures the base component is also registered so the $ref resolves.
        var baseType = FindBaseProblemDetailsType(type);
        var baseId = RegisterBaseProblemDetails(baseType, components);

        var validationId = ReserveSchemaId(type);

        components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        components.Schemas[validationId] = new OpenApiSchema
        {
            AllOf = new List<IOpenApiSchema>
            {
                new OpenApiSchemaReference(baseId),
                CreateValidationErrorsSchema(),
            },
        };

        return new OpenApiSchemaReference(validationId);
    }

    private string RegisterBaseProblemDetails(Type? baseType, OpenApiComponents components)
    {
        if (_problemDetailsBaseId is not null)
        {
            return _problemDetailsBaseId;
        }

        // Prefer keying the registry by the concrete base Type so a later direct ProblemDetails
        // reference dedupes to the same component; fall back to a name-only reservation when the
        // base type cannot be resolved by reflection.
        var id = baseType is not null ? ReserveSchemaId(baseType) : ReserveNameId("ProblemDetails");

        components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        components.Schemas[id] = CreateProblemDetailsSchema();

        _problemDetailsBaseId = id;
        return id;
    }

    private static Type? FindBaseProblemDetailsType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.FullName == ProblemDetailsTypes.ProblemDetailsFullName)
            {
                return current;
            }
        }

        return null;
    }

    private static OpenApiSchema CreateProblemDetailsSchema() =>
        new()
        {
            Type = JsonSchemaType.Object,
            // RFC 9457 members, lowercase per the ASP.NET Core / RFC JSON contract. All optional
            // and nullable. Extension members ([JsonExtensionData]) are flattened to the top level
            // on the wire, so they are modelled as free-form additional properties rather than a
            // nested "extensions" object.
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["type"] = NullableScalar(JsonSchemaType.String, "uri"),
                ["title"] = NullableScalar(JsonSchemaType.String),
                ["status"] = NullableScalar(JsonSchemaType.Integer, "int32"),
                ["detail"] = NullableScalar(JsonSchemaType.String),
                ["instance"] = NullableScalar(JsonSchemaType.String, "uri"),
            },
            // RFC 9457 extension members ([JsonExtensionData]) are flattened to the top level, so
            // arbitrary additional properties are permitted. Microsoft.OpenApi 3.10.2 models the
            // JSON Schema boolean "additionalProperties: true" as an empty schema (it cannot emit
            // the boolean literal), so an explicit empty schema is used to make the allowance
            // visible on the wire as "additionalProperties: {}".
            AdditionalProperties = new OpenApiSchema(),
        };

    private static OpenApiSchema CreateValidationErrorsSchema() =>
        new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["errors"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    AdditionalProperties = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchema { Type = JsonSchemaType.String },
                    },
                },
            },
        };

    private static OpenApiSchema NullableScalar(JsonSchemaType type, string? format = null) =>
        new() { Type = type | JsonSchemaType.Null, Format = format };

    private string ReserveNameId(string baseName)
    {
        var candidate = baseName;
        var suffix = 2;

        while (_idOwners.ContainsKey(candidate))
        {
            candidate = $"{baseName}{suffix}";
            suffix++;
        }

        // Reserve the name against a sentinel owner so no real type is ever assigned this id.
        _idOwners[candidate] = typeof(void);
        return candidate;
    }

    private string ReserveSchemaId(Type type)
    {
        var baseName = GetSchemaBaseName(type);
        var candidate = baseName;
        var suffix = 2;

        // Disambiguate collisions with a different type that already owns the base name.
        while (_idOwners.TryGetValue(candidate, out var owner) && owner != type)
        {
            candidate = $"{baseName}{suffix}";
            suffix++;
        }

        _registeredIds[type] = candidate;
        _idOwners[candidate] = type;
        return candidate;
    }

    private static string GetSchemaBaseName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        // Strip the arity marker (e.g. "Wrapper`1") and append the argument names.
        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            name = name[..tick];
        }

        var args = type.GetGenericArguments();
        return name + "Of" + string.Concat(Array.ConvertAll(args, GetSchemaBaseName));
    }

    private static IEnumerable<PropertyInfo> GetReadableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

    private static bool IsComplexObject(Type type)
    {
        if (type == typeof(object) || type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        // Abstractions with no concrete shape are not emitted as object schemas.
        if (type.IsInterface || typeof(IEnumerable).IsAssignableFrom(type))
        {
            return false;
        }

        return type.IsClass || (type.IsValueType && !type.IsPrimitive);
    }

    private static IOpenApiSchema MakeNullable(IOpenApiSchema schema)
    {
        // Inline concrete schemas that carry a type flag: OR in Null (JSON Schema 2020-12 style,
        // emitted on the wire as e.g. "type": ["string", "null"]).
        if (schema is OpenApiSchema concrete && concrete.Type is { } type)
        {
            concrete.Type = type | JsonSchemaType.Null;
            return schema;
        }

        // References (and anything else that can't carry the flag) can't be OR-ed directly, so
        // wrap them in an OpenAPI 3.1 nullable union: anyOf: [ <schema>, { type: "null" } ].
        return new OpenApiSchema
        {
            AnyOf = new List<IOpenApiSchema>
            {
                schema,
                new OpenApiSchema { Type = JsonSchemaType.Null },
            },
        };
    }

    private static bool TryCreatePrimitive(Type type, out IOpenApiSchema schema)
    {
        schema = type switch
        {
            _ when type == typeof(string) => Scalar(JsonSchemaType.String),
            _ when type == typeof(char) => Scalar(JsonSchemaType.String),
            _ when type == typeof(bool) => Scalar(JsonSchemaType.Boolean),

            _ when type == typeof(byte) => Scalar(JsonSchemaType.Integer),
            _ when type == typeof(sbyte) => Scalar(JsonSchemaType.Integer),
            _ when type == typeof(short) => Scalar(JsonSchemaType.Integer, "int32"),
            _ when type == typeof(ushort) => Scalar(JsonSchemaType.Integer, "int32"),
            _ when type == typeof(int) => Scalar(JsonSchemaType.Integer, "int32"),
            _ when type == typeof(uint) => Scalar(JsonSchemaType.Integer, "int32"),
            _ when type == typeof(long) => Scalar(JsonSchemaType.Integer, "int64"),
            _ when type == typeof(ulong) => Scalar(JsonSchemaType.Integer, "int64"),

            _ when type == typeof(float) => Scalar(JsonSchemaType.Number, "float"),
            _ when type == typeof(double) => Scalar(JsonSchemaType.Number, "double"),
            // decimal has no standard OpenAPI format; "decimal" preserves the CLR intent.
            _ when type == typeof(decimal) => Scalar(JsonSchemaType.Number, "decimal"),

            _ when type == typeof(DateTime) => Scalar(JsonSchemaType.String, "date-time"),
            _ when type == typeof(DateTimeOffset) => Scalar(JsonSchemaType.String, "date-time"),
            _ when type == typeof(DateOnly) => Scalar(JsonSchemaType.String, "date"),
            _ when type == typeof(TimeOnly) => Scalar(JsonSchemaType.String, "time"),
            _ when type == typeof(TimeSpan) => Scalar(JsonSchemaType.String, "duration"),
            _ when type == typeof(Guid) => Scalar(JsonSchemaType.String, "uuid"),
            _ when type == typeof(Uri) => Scalar(JsonSchemaType.String, "uri"),

            _ => null!,
        };

        return schema is not null;
    }

    private static OpenApiSchema Scalar(JsonSchemaType type, string? format = null) =>
        new() { Type = type, Format = format };

    private static IOpenApiSchema CreateEnumSchema(Type enumType)
    {
        // Emit enums as strings using the member names. This is the most consumer-friendly
        // representation; an int-backed representation would require the numeric values instead.
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = new List<JsonNode>(),
        };

        foreach (var name in Enum.GetNames(enumType))
        {
            schema.Enum.Add(JsonValue.Create(name));
        }

        return schema;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        foreach (var candidate in EnumerateTypeAndInterfaces(type))
        {
            if (candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                valueType = candidate.GetGenericArguments()[1];
                return true;
            }
        }

        valueType = null!;
        return false;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        foreach (var candidate in EnumerateTypeAndInterfaces(type))
        {
            if (candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    private static IEnumerable<Type> EnumerateTypeAndInterfaces(Type type)
    {
        yield return type;
        foreach (var iface in type.GetInterfaces())
        {
            yield return iface;
        }
    }
}
