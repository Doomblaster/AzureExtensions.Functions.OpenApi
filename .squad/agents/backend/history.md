# Project Context

- **Owner:** Espen Berglund
- **Project:** Azure.Functions.OpenApi — a .NET 10 library that injects an OpenAPI HttpTrigger into an Azure Functions isolated worker v4 (latest) host and serves an OpenAPI specification document.
- **Stack:** .NET 10, C#, Azure Functions .NET isolated worker v4 (Microsoft.Azure.Functions.Worker), Microsoft.OpenApi (document build + JSON/YAML serialization). Target OpenAPI Specification 3.x (per OAI/OpenAPI-Specification).
- **Created:** 2026-08-26T09:49:06+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-08-26 — Document provider + serialization helper

- Microsoft.OpenApi **3.x flattens namespaces**: `OpenApiDocument`, `OpenApiInfo`, `OpenApiPaths`, `OpenApiSpecVersion`, `OpenApiJsonWriter`/`OpenApiYamlWriter` are all in `Microsoft.OpenApi` (not `.Models`). Always verify against the restored assembly (D:\NugetCache\microsoft.openapi\3.10.2) — reflection over the DLL is the fastest source of truth.
- Sync serialization: `document.SerializeAs(specVersion, IOpenApiWriter)` with `new OpenApiJsonWriter(TextWriter)` / `new OpenApiYamlWriter(TextWriter)`. **Writers have no `Flush()`** — flush the underlying `TextWriter`/`StringWriter`.
- The `openapi` field emits the **exact patch version** (`3.1.2`, `3.0.4`), not `x.y.0`. Tests should assert the `3.1`/`3.0` prefix, not an exact `3.1.0`.
- A valid 3.x doc needs a present (empty-ok) `Paths` object. Provider caches the built doc (immutable per process); DI uses `TryAddSingleton` so consumers can override the provider.
- Helper for Functions: `internal static OpenApiDocumentSerializer.SerializeJson/SerializeYaml(OpenApiDocument, OpenApiSpecVersion) : string`. Trigger must inject `IOptions<OpenApiOptions>` to get `SpecVersion`.

📌 Team update (2026-08-26T09:49:06+02:00): HTTP endpoints consume the provider plus internal serializer seam and read SpecVersion from IOptions<OpenApiOptions>; tests cover JSON/YAML serialization and provider caching — decided by Backend, Functions, Tester

## 2026-08-26 — OpenApiSchemaGenerator (CLR → schema)

- Added `internal sealed OpenApiSchemaGenerator` (`Schema/OpenApiSchemaGenerator.cs`): `IOpenApiSchema GetOrCreateSchema(Type, OpenApiComponents)`. Complex types → `$ref` registered once in `components.Schemas`; primitives/scalars/collections inline.
- **3.10.2 gotchas confirmed by reflection + a throwaway console:** `IOpenApiSchema.Type` is a `[Flags] JsonSchemaType?` (Null=1,Boolean=2,Integer=4,Number=8,String=16,Object=32,Array=64). **Nullable = OR-in `JsonSchemaType.Null`** → serializes `"type":["null","integer"]`. No `Nullable` bool exists.
- `Properties`/`Items`/`AdditionalProperties` are `IOpenApiSchema`; `Components.Schemas` is `IDictionary<string,IOpenApiSchema>` (nullable, init before add); `Enum` is `IList<JsonNode>` (use `JsonValue.Create`). `$ref` = `new OpenApiSchemaReference(id)` (host/external optional) which implements `IOpenApiSchema`.
- **Recursion guard:** register the object shell in `components.Schemas` BEFORE filling properties → self-referencing types emit `$ref` without stack overflow. Verified with `Node.Parent : Node`.
- Decisions: decimal→number/`decimal`; enums→string with member names; one generator instance + one shared components per document build (holds the dedup registry). Build: 0 warnings/0 errors.
- Verify: never Add-Type the OpenApi DLL in PowerShell (STJ dep load fails) — spin up a scratch console `ProjectReference` instead, set `<AssemblyName>Azure.Functions.OpenApi.Tests</AssemblyName>` to satisfy the src `InternalsVisibleTo`, then delete it.

## 2026-08-26 — OpenApiPathsBuilder (paths-builder)

- Built `OpenApiPathsBuilder.Populate(document, endpoints, includeUnannotated)` mapping discovered
  endpoints + attributes → paths/components; delegates schema construction to a single
  `OpenApiSchemaGenerator` per build.
- **Learning:** In Microsoft.OpenApi 3.10.2 `OpenApiPathItem.Operations` is a
  `Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>` — there is NO `OperationType` verb
  enum like 1.x. Use `HttpMethod.Parse("GET")` as the key (case-insensitive equality dedups verbs)
  and initialize the dictionary yourself (it is nullable). `Operation.Tags` is
  `ISet<OpenApiTagReference>` (construct `new OpenApiTagReference(name)`), and request-body/response
  content are `IDictionary<string, IOpenApiMediaType>`. Verified members by reflecting the actual
  3.10.2 assembly at `D:\NugetCache\microsoft.openapi\3.10.2` before coding — faster than guessing.
- Resilience pattern: wrap each endpoint build in try/catch so one bad method can't abort the doc.

## 2026-08-26 — OpenApiDocumentProvider wired (provider-wire, final integration)

- `BuildDocument()` now runs discovery → paths-builder: resolve assemblies (`DocumentAssemblies`
  or `FunctionEndpointDiscovery.GetDefaultAssemblies()`), `Discover(assemblies, RoutePrefix)`,
  then `new OpenApiPathsBuilder(new OpenApiSchemaGenerator()).Populate(doc, endpoints, IncludeUnannotatedEndpoints)`.
  Document is created with `Info` + empty `OpenApiPaths` + `Components{ Schemas = new Dictionary<> }`.
- **Meta-endpoint exclusion learning:** the library's own assembly is named `Azure.Functions.OpenApi`
  which does NOT start with a framework prefix, so `GetDefaultAssemblies()` includes it and would
  discover `OpenApiHttpFunctions` (openapi.json/openapi.yaml). Cleanest guard = filter endpoints
  whose `Method.DeclaringType.Assembly == typeof(OpenApiDocumentProvider).Assembly`. This beats
  route-string matching: robust to consumers changing JsonRoute/YamlRoute and to the library being
  explicitly added to `DocumentAssemblies`.
- **Resilience:** discovery+populate wrapped in try/catch → on runtime reflection failure the
  provider still returns a valid path-less document rather than 500-ing the spec request.
- **DI-safe optional logger:** added `ILogger<OpenApiDocumentProvider>? logger = null` as a second
  ctor param. DI resolves it when logging is registered; `TryAddSingleton` registration unchanged,
  no breaking contract change.
- Verified with a throwaway xUnit test using auto-scan over loaded assemblies (library + fake
  `[Function]`+`[HttpTrigger]` methods): asserted `/api/items` + `/api/items/{id}` present with GET
  and openapi.json/yaml absent. Passed, then removed. Build: 0 warnings/0 errors.

📌 Team update (2026-08-26T11:30:00+02:00): Attribute-driven OpenAPI generation landed across public attributes/options, reflection discovery, CLR schema components, path/operation generation, sample CRUD endpoints, README docs, and 65/65 passing tests — decided by Lead, Functions, Backend, and Tester.


## 2026-08-26: Enum $ref hoisting + nullable-reference anyOf union

- **Enum reuse:** enums were emitted inline at every call site. Refactored to hoist them into
  `components.Schemas` once and return `OpenApiSchemaReference`, reusing the object registry
  (`_registeredIds`/`_idOwners`/`ReserveSchemaId`) — new `CreateOrReferenceEnum`. Kept
  `CreateEnumSchema` as the component-body factory; bare primitives stay inline.
- **Nullable refs:** old `MakeNullable` OR-ed `JsonSchemaType.Null` only into flag-carrying inline
  schemas and returned references UNCHANGED, silently dropping nullability. Fixed by wrapping
  non-flag schemas in `anyOf: [ <ref>, { type: "null" } ]`.
- **3.10.2 fact:** the union member is `OpenApiSchema.AnyOf` typed `IList<IOpenApiSchema>` — verified
  by reflecting the assembly (package resolved to `D:\NugetCache`, NOT the default `~/.nuget`;
  `packagesPath` came from `obj/project.assets.json` → `packageFolders`).
- **Reflection gotcha:** `MakeNullable` only fires for `Nullable<T>` VALUE types. A nullable
  reference type like `ItemDimensions?` (sealed class) is invisible to reflection and stays a plain
  `$ref` — my first throwaway test wrongly used a `class` for the nested-nullable case and failed;
  switching it to a `struct` (real `Nullable<Dims>`) confirmed the anyOf+null path. Test removed.
- Library build: 0 warnings / 0 errors. Existing inline-enum SchemaGeneratorTests will now fail by
  design; Tester owns updating them (todo `enum-refs-tests`).

### 2026-08-26 — Nullable reference-type properties (NullabilityInfoContext)

- Nullable-reference annotations (`string?`, `ItemDimensions?`) are erased at runtime; recover them with `System.Reflection.NullabilityInfoContext`. It is **not thread-safe** — create a local instance per method call, not a shared field.
- Use `ctx.Create(property).ReadState == NullabilityState.Nullable` (ReadState, since schemas come from readable props).
- **Double-application guard is essential:** skip the reference-nullability step when `Nullable.GetUnderlyingType(property.PropertyType) is not null` — `int?`/`SomeEnum?` are already made nullable by the `Nullable<T>` unwrap in `GetOrCreateSchema`. Without the guard, nullable enums get wrapped twice.
- Net wire result via existing `MakeNullable`: nullable inline scalar → `type:["string","null"]`; nullable `$ref` (nested class or hoisted enum) → `anyOf:[{$ref},{type:null}]`. Non-nullable members stay plain.
- Real fixture confirmed: `SampleFunctionApp Item.Dimensions (ItemDimensions?)` → anyOf+null; non-nullable `Name`/`Status` unchanged.

📌 Team update (2025-06-02T12:00:00Z): Enum schemas are now reusable components referenced by `$ref`; nullable inline scalars use `type:["x","null"]`, while nullable refs/enums/objects use `anyOf:[{$ref},{type:null}]`. Full suite is green at 74/74 and README documents the behavior — decided by Backend, Tester, Lead.

📌 Team update (2026-08-26T12:41:00+02:00): Nullable `$ref` schema output was fact-checked: `anyOf:[{$ref},{type:null}]` is canonical for OpenAPI 3.1/JSON Schema 2020-12, and Microsoft.OpenApi 3.10.2 serializes it to valid OpenAPI 3.0 without illegal `type:null` — decided by Backend, Tester, Lead, Fact Checker.

## 2026-08-26 — RFC 9457 ProblemDetails support (todos problem-schemas, problem-contenttype)

- New `Schema/ProblemDetailsTypes.cs`: detects the family by FullName only (no ASP.NET Core Mvc
  reference) — `ProblemDetails`, `ValidationProblemDetails` (Mvc),
  `HttpValidationProblemDetails` (Http). `IsProblemDetails` + `Classify`/`ProblemKind`.
- `OpenApiSchemaGenerator`: check `IsProblemDetails` BEFORE dictionary/enumerable/complex-object
  branches (else they'd reflect into wrong object schemas). `CreateOrReferenceProblemDetails`
  emits CANONICAL hand-authored components, register-once-return-`$ref` like enums.
- **Why not reflection**: reflection emits PascalCase (Type/Title/...) but the wire is lowercase
  (type/title/status/detail/instance); and `Extensions` is `[JsonExtensionData]` (flattened to
  top level) — reflection would add a spurious nested `extensions` object.
- Components: single `ProblemDetails` (lowercase, all-nullable; status integer/int32; type &
  instance format `uri`); `ValidationProblemDetails` & `HttpValidationProblemDetails` each
  `allOf:[{$ref:ProblemDetails}, {errors: object<string,string[]>}]`. Base deduped via a
  `_problemDetailsBaseId` field so both variants reuse one base component and the `$ref` resolves.
- **GOTCHA — additionalProperties**: Microsoft.OpenApi 3.10.2 canNOT emit boolean
  `additionalProperties: true`. Its XML docs say boolean `true` == an EMPTY schema.
  `AdditionalPropertiesAllowed = true` is the default and serializes to NOTHING. So set
  `AdditionalProperties = new OpenApiSchema()` → emits `additionalProperties: { }` (the documented
  equivalent of true). Verified via throwaway probe using `SerializeAsV31`.
- `OpenApiPathsBuilder.ResolveContentType(bodyType, declaredContentType)`: if body is a problem
  type AND ContentType is still the default `application/json`, use `application/problem+json`;
  respect any explicitly-set content type. Applied to BuildResponses + BuildRequestBody.
- Build: `dotnet build -warnaserror` → 0/0. Probe confirmed all wire assertions, then removed
  (never committed). Did not touch sample/tests/README (owned by Functions/Tester/Lead).

📌 Team update (2026-08-26T12:59:00+02:00): RFC 9457 ProblemDetails support added canonical reusable schemas, validation allOf/errors maps, and default application/problem+json content for problem-family bodies — decided by Backend, Functions, Tester, Lead.
