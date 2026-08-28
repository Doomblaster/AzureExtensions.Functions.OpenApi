# Squad Decisions

## Active Decisions

### 2026-08-26: Foundation scaffold for Azure.Functions.OpenApi

**By:** Lead
**Status:** Accepted

**What:** Scaffolded classic Azure.Functions.OpenApi.sln with net10.0 library and sample isolated-worker host. Public API contracts are IOpenApiDocumentProvider, OpenApiOptions, AddOpenApi, OpenApiHttpFunctions, and internal OpenApiDocumentProvider. Chose ASP.NET Core HTTP integration for isolated worker v4 and current stable packages: Microsoft.OpenApi 3.10.2, Worker 2.52.0, Worker.Sdk 2.1.0, Http 3.3.0, Http.AspNetCore 2.1.1.

**Why:** Classic .sln maximizes tooling compatibility. ASP.NET Core integration provides modern HttpRequest/IResult support. The stable public surface unblocked Backend, Functions, and Tester work.

### 2026-08-26: OpenAPI document provider and serializer

**By:** Backend
**Status:** Accepted

**What:** Implemented OpenApiDocumentProvider.GetDocumentAsync to build and cache a document from OpenApiOptions, with Info, optional Description, and empty OpenApiPaths. Added internal OpenApiDocumentSerializer.SerializeJson/SerializeYaml using Microsoft.OpenApi writers and OpenApiSpecVersion.

**Why:** Microsoft.OpenApi 3.10.2 has flattened namespaces and emits exact spec patch versions (for example 3.1.2/3.0.4). Using its writers avoids hand-rolled wire format and keeps endpoints aligned with the library.

### 2026-08-26: OpenAPI HTTP trigger endpoints

**By:** Functions
**Status:** Accepted

**What:** Implemented anonymous GET endpoints for openapi.json and openapi.yaml using async Task<IResult>, provider injection, IOptions<OpenApiOptions>, request-aborted cancellation, and Results.Text with application/json and application/yaml. Sample host uses ConfigureFunctionsWebApplication and AddOpenApi.

**Why:** HttpTrigger route values are compile-time constants. JsonRoute/YamlRoute/RoutePrefix remain advisory for documentation/link generation; host.json routePrefix controls the effective /api prefix.

### 2026-08-26: Test project and endpoint coverage

**By:** Tester
**Status:** Accepted

**What:** Added tests/Azure.Functions.OpenApi.Tests with 25 xUnit tests covering DI registration, default options, custom provider preservation, document provider behavior/caching, serializer JSON/YAML output and null handling, and direct OpenApiHttpFunctions IResult execution. Added InternalsVisibleTo via MSBuild item and FrameworkReference Microsoft.AspNetCore.App for endpoint tests.

**Why:** Public behavior is covered through public APIs where possible; internals are exposed only for the default provider and serializer seams. Runtime binding metadata was verified separately by Functions; tests stay deterministic without func host.

### 2026-08-26: Public OpenAPI attributes and generation options

**By:** Lead
**Status:** Accepted

**What:** Added the public attribute set for operations, query/header/path parameters, request bodies, and responses. Added `OpenApiOptions.IncludeUnannotatedEndpoints` and `DocumentAssemblies` so generation can include inferred endpoints and target explicit assemblies.

**Why:** This gives consumers a compact metadata surface while keeping discovery and generation configurable.

### 2026-08-26: Reflection discovery for HTTP-triggered functions

**By:** Functions
**Status:** Accepted

**What:** Added `FunctionEndpointDiscovery` to scan assemblies for `[Function]` methods with `HttpTrigger`, normalize routes and route parameters, apply route prefixes, uppercase verbs, auto-scan non-framework assemblies, and skip malformed methods.

**Why:** Paths generation needs a small, testable endpoint-fact layer independent of OpenAPI object construction.

### 2026-08-26: CLR schema generation with reusable components

**By:** Backend
**Status:** Accepted

**What:** Added `OpenApiSchemaGenerator` to map CLR primitives, nullable values, enums, collections, dictionaries, and complex types into Microsoft.OpenApi schemas, using component `$ref`s and recursion-safe registration.

**Why:** Request and response metadata can now produce reusable OpenAPI component schemas without hand-rolled JSON.

### 2026-08-26: Attribute-driven paths and operations builder

**By:** Backend
**Status:** Accepted

**What:** Added `OpenApiPathsBuilder` to combine discovered endpoints with OpenAPI attributes into paths, operations, parameters, request bodies, responses, tags, and schemas. Unannotated endpoints are included only when configured.

**Why:** This is the central translation layer from Azure Functions metadata to OpenAPI operations.

### 2026-08-26: Document provider wired to generated paths

**By:** Backend
**Status:** Accepted

**What:** `OpenApiDocumentProvider.BuildDocument` now resolves document assemblies, discovers endpoints, excludes library meta-endpoints, populates paths/components, logs generation failures when possible, and returns a valid empty-path document on reflection failures.

**Why:** Served `openapi.json` and `openapi.yaml` now describe the consumer app rather than only returning Info metadata.

### 2026-08-26: Sample CRUD API for generator validation

**By:** Functions
**Status:** Accepted

**What:** Added sample `Item`/DTO models and CRUD endpoints for list, get, create, update, and delete, exercising path/query/header parameters, request bodies, bodyless responses, response schemas, enums, collections, nested objects, and nullable members.

**Why:** The sample app now demonstrates and validates the intended attribute-driven authoring model.

### 2026-08-26: Generation coverage expanded to 65 passing tests

**By:** Tester
**Status:** Accepted

**What:** Added tests for discovery, schema generation, paths building, and end-to-end document generation over the real sample app. Verified build with 0 warnings/errors and `dotnet test` with 65/65 passing.

**Why:** The new generation pipeline is covered from isolated components through serialized OpenAPI output.

### 2026-08-26T12:41:00+02:00: Enum components and nullable schema handling (consolidated)

**By:** Backend, Tester, Lead, Fact Checker
**Status:** Accepted

**What:** `OpenApiSchemaGenerator` hoists enum schemas into `components.schemas` once and reuses them via `$ref`; bare primitives remain inline. Nullable value types use `Nullable.GetUnderlyingType`; nullable reference-type properties use a per-call `System.Reflection.NullabilityInfoContext` with a guard to avoid double-wrapping `Nullable<T>`. Nullable inline scalars emit `type:["x","null"]`; nullable `$ref` schemas, including nullable enums and nullable object references, emit `anyOf:[{$ref},{type:null}]`. Fact Checker verified the 3.1.2 output is the canonical JSON Schema 2020-12 idiom and that Microsoft.OpenApi 3.10.2 down-translates the same construct to valid OpenAPI 3.0 output without leaking illegal `type:null`.

**Why:** Reusable enum components reduce duplicated schemas and align enum handling with complex-object component reuse. Nullable refs cannot carry a `type:null` flag directly, so OpenAPI 3.1 null unions preserve correct wire semantics. The feature is validated by `dotnet build` with 0 warnings/errors and the full test suite passing 74/74; no nullable-schema code change is required for 3.0 correctness, though the generated 3.0 `anyOf`/`enum:[null]` form may be less idiomatic for strict linters.

### 2026-08-26T12:41:00+02:00: CDN-hosted Swagger UI endpoint (consolidated)

**By:** Functions, Tester, Lead
**Status:** Accepted

**What:** Added an anonymous GET `/api/swagger` HttpTrigger that returns a minimal HTML page loading Swagger UI assets from jsDelivr using pinned `swagger-ui-dist` version `5.32.14` (not embedded). The page points Swagger UI at the existing `openapi.json` endpoint using an absolute URL derived from the incoming request and safely serialized into JavaScript; the page title is HTML-encoded. Added public options `EnableSwaggerUi` (default true), `SwaggerUiRoute` (advisory only; actual trigger route remains the compile-time constant `swagger`), `SwaggerUiCdnBaseUrl`, `SwaggerUiVersion`, and `SwaggerUiPageTitle`. README documents endpoint behavior, CDN/CSP considerations, and configuration.

**Why:** A built-in Swagger UI page gives consumers an interactive documentation surface without bundling client assets into the library. Pinning the CDN package keeps output deterministic while preserving configurability. The compile-time route limitation matches existing Azure Functions trigger constraints. Validation passed with `dotnet build` at 0 warnings/0 errors and the full test suite at 84/84 passing, including HTML generation, endpoint behavior, disabled mode, custom route-prefix/json-route URL derivation, pinned CDN URLs, and JS/HTML escaping.

### 2026-08-26T12:59:00+02:00: RFC 9457 ProblemDetails error response support (consolidated)

**By:** Backend, Functions, Tester, Lead
**Status:** Accepted

**What:** Added first-class ProblemDetails-family support without adding an MVC compile dependency. `ProblemDetailsTypes` detects `Microsoft.AspNetCore.Mvc.ProblemDetails`, `Microsoft.AspNetCore.Mvc.ValidationProblemDetails`, and `Microsoft.AspNetCore.Http.HttpValidationProblemDetails` by full type name, classifies them, and lets schema/content generation identify the family. `OpenApiSchemaGenerator` now emits canonical reusable components: `ProblemDetails` with lowercase nullable RFC 9457 members (`type`, `title`, `status`, `detail`, `instance`), URI formats for `type`/`instance`, integer `status`, no `Extensions` property, and visible `additionalProperties: {}` for extension members; validation variants compose `allOf` with the base `ProblemDetails` component plus an `errors: object<string,string[]>` map. `OpenApiPathsBuilder` emits default ProblemDetails-family bodies as `application/problem+json` while respecting explicit `ContentType` overrides. The sample app now documents and returns `ProblemDetails` for `GetItem` 404 and adds `SearchItems` (`GET items/search`) with a required `name` query, `200 List<Item>`, and `400 HttpValidationProblemDetails` via `Results.ValidationProblem`. README documents recognized types, canonical schemas, `application/problem+json`, explicit override behavior, and sample references without claiming general camelCase conversion.

**Why:** RFC 9457 errors need stable wire-shaped schemas that differ from normal CLR property casing and from MVC's extension-data implementation details. Name-only detection preserves ASP.NET Core integration support without taking a direct MVC package dependency. Register-once components and `allOf` keep generated OpenAPI concise and interoperable, while automatic `application/problem+json` matches standards-based error responses unless users explicitly choose another content type. The feature is verified by targeted and end-to-end coverage: 17 new tests for schemas, paths, and sample document generation; coordinator final verification reported `dotnet build` at 0 warnings/0 errors and `dotnet test` at 101/101 passing.


### 2026-08-26T21:39:52+02:00: Minimal xUnit v3 Microsoft.Testing.Platform test setup (consolidated)

**By:** Hockney (Tester), Copilot (coordinator)
**Status:** Accepted

**What:** The test project uses a minimal pure-MTP setup: `tests/Azure.Functions.OpenApi.Tests` references only `xunit.v3` 4.0.0 for test infrastructure. `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` were removed after the earlier xUnit v3 migration. The repo-root `global.json` keeps `dotnet test` opted into `Microsoft.Testing.Platform`; use `dotnet test --project <path>` for a single project and `dotnet test --solution <path.slnx>` for an explicit solution.

**Why:** `xunit.v3` 4.0.0 has native MTP support built in and brings the required platform/runner/analyzers transitively, so VSTest infrastructure packages are unnecessary. Dropping `xunit.runner.visualstudio` accepts loss of legacy VSTest-protocol Test Explorer discovery; modern VS/VS Code C# Dev Kit, CLI, and CI use MTP. Verified with 110/110 tests passing and 0 warnings before later feature work.

### 2026-08-26: OpenAPI response headers via `[OpenApiResponseHeader]`

**By:** Fenster
**Status:** Accepted

**What:** Added a new public attribute `OpenApiResponseHeaderAttribute(string name, Type type, params int[] statusCodes)` with settable `Description`, `Required`, and `Deprecated`. Wired it through `OpenApiPathsBuilder.BuildResponses` via `ApplyResponseHeaders` so OpenAPI Header Objects with inline schemas are emitted under `responses.{statusCode}.headers.{name}`. Non-empty status lists fan out to each code and create a bare `OpenApiResponse { Description = string.Empty }` when needed; an empty status list targets already-present responses, including the synthetic `200`.

**Why:** Keeps response headers separate from request-header parameters (`[OpenApiHeaderParameter]`) and preserves byte-identical behavior when no response-header attributes are present. Sample, unit, end-to-end, and README coverage were updated. Build: 0 warnings / 0 errors. Tests: 117/117 passing.

### 2026-08-26T21:39:52+02:00: Separate response-header attribute is canonical

**By:** Scribe
**Status:** Accepted

**What:** The canonical way to declare response headers in this library is a separate `OpenApiResponseHeaderAttribute`, not a `Direction` flag on the request-header attribute. Multi-status targeting uses `params int[] statusCodes`; an empty status-code list applies to all documented responses.

**Why:** OpenAPI response headers are status-code-scoped Header Objects, distinct from request Parameter Objects. A dedicated attribute preserves OpenAPI semantic fidelity while keeping multi-status response-header declarations concise.

### 2026-08-28T13:57:21.332+02:00: Namespace-segment schema-id collision disambiguation (consolidated)

**By:** Backend, Lead
**Status:** Accepted

**What:** `OpenApiSchemaGenerator.ReserveSchemaId` keeps the first claimant's plain base schema id unchanged. When a different CLR type collides on the same base name, the generator now first tries `lastNamespaceSegment + baseName` (for example `MyApp.Models.Item` -> `ModelsItem`) and only falls back to numeric suffixes on that candidate (`ModelsItem2`, `ModelsItem3`, ...) if needed.

**Why:** This preserves backward-compatible ids for the common non-colliding case, gives same-named types across namespaces more readable component names anchored to their CLR origin than immediate numeric suffixes, and still guarantees uniqueness for pathological namespace collisions or reserved-name edge cases.


### 2026-08-28: Header-set wiring preserves individual-attribute overrides
**By:** Backend
**What:** `OpenApiPathsBuilder` now expands request and response header-set attributes by instantiating their collection types through guarded `Activator.CreateInstance` checks, applies request-set members before individual request-header attributes with case-insensitive de-duplication, and applies response-set members before individual response-header attributes with case-insensitive header overwrites per targeted status code.
**Why:** This keeps reusable header collections symmetric with existing single-header attributes, preserves the documented "individual attribute wins" escape hatch, and lets malformed collection types fail clearly per-endpoint without aborting the overall document build.

### 2026-08-28: Header-set contracts stay interface-only in this slice
**By:** Lead
**What:** Added request and response header-set contracts as public interfaces plus two pure
metadata attributes. The reusable collection type is carried as <c>Type</c> and documented to be
concrete, non-abstract, and publicly parameterless-constructible; no helper base classes or
runtime validation were added in this slice.
**Why:** This keeps the public API minimal and reversible while giving Backend a clear reflection
contract to wire into <c>OpenApiPathsBuilder</c> next without coupling consumers to framework
inheritance or premature convenience types.

### 2026-08-28: Header-set final pre-merge review requests changes
**By:** Lead
**What:** Final review does not approve merge yet. The header-set API shape matches the PRD (two attributes, four request/response-qualified interfaces, response status codes on the attribute), and the set-vs-individual collision precedence is implemented case-insensitively. However, `OpenApiPathsBuilder` currently broadens that precedence logic into a behavior change for pre-existing single-header attributes, and the malformed-collection resilience tests permit an empty path item even though the class remarks say a malformed endpoint is skipped.
**Why:** The PRD explicitly requires no breaking change to existing single-header attribute behavior and asks the malformed-collection path to honor the documented per-endpoint try/catch contract. Before merge, narrow collision suppression to set-member vs individual-attribute collisions only, and align the malformed-endpoint behavior/tests with the documented "endpoint is skipped" contract.

### 2026-08-28: Header-set reviewer-requested corrections
**By:** Lead
**What:** Narrowed `OpenApiPathsBuilder` header-set collision handling to the approved scope only. `AddHeaderParameters` now suppresses set members only when a same-named individual `[OpenApiHeaderParameter]` exists on the method (case-insensitive), then appends every individual header attribute unchanged and in original order. `ApplyResponseHeaders` now restores case-sensitive response-header storage and skips a set member only for the specific response status keys already claimed by a same-named individual `[OpenApiResponseHeader]`. `AddEndpoint` now stages all operations locally before touching `document.Paths`, so malformed endpoints contribute nothing unless every operation builds successfully. Tightened the malformed-header-set regression test to require that the malformed endpoint path is absent.
**Why:** These are reviewer-requested corrections under the Reviewer Rejection Protocol, not new scope. The PRD approved only set-member-vs-individual collision precedence and the class remarks already promised malformed endpoints are skipped without leaving stray empty path items.

### 2026-08-28: Header-set coverage lives in builder regressions plus provider e2e smoke
**By:** Tester
**What:** Added focused `PathsBuilderTests` coverage for request-header sets, response-header sets, status-code targeting, empty-status fan-out, case-insensitive collision precedence, and malformed collection resilience; added `DocumentGenerationTests` coverage that runs the full provider/discovery pipeline against real `[Function]` fixtures carrying the new header-set attributes.
**Why:** Most semantics are owned by `OpenApiPathsBuilder`, so unit coverage there is the fastest regression net. A smaller provider-level test confirms discovery + document generation still surface the new attributes end-to-end without depending on sample-app changes from a separate work item.

### 2026-08-28: Header-set sample and README adoption
**By:** Functions
**What:** Added `samples/SampleFunctionApp/Models/ItemHeaderSets.cs`, applied the new request/response header-set attributes in `samples/SampleFunctionApp/Functions/ItemsFunctions.cs`, and updated `README.md` to document reusable header collections and sample usage.
**Why:** The feature needs a concrete end-to-end sample plus documentation so consumers can copy the intended authoring model and see header sets exercised in a real Azure Functions app.

### 2026-08-28T16:51:06.599+02:00: Unified header-definition contracts and request-header deprecation support (consolidated)

**By:** Lead, Functions, Tester
**Status:** Accepted

**What:** Consolidated the request/response-specific reusable header contracts into a single `IOpenApiHeaderDefinition` plus `IOpenApiHeaderDefinitionCollection`, and extended request-side metadata so `Deprecated` is available both on direct request-header attributes and on reusable header-definition types used by header sets. The sample app, README, and test fixtures were updated to adopt the unified contracts and verify request-side deprecation propagation.

**Why:** The split contracts were nearly identical and created avoidable public API surface. A unified reusable-header model keeps request and response metadata aligned with OpenAPI 3.x capabilities while ensuring the sample, docs, and regression suite match the shipped contract.

### 2026-08-28T16:51:06.599+02:00: Request-header rename and generic reusable header attributes (consolidated)

**By:** Lead, Functions, Tester
**Status:** Accepted

**What:** Renamed `OpenApiHeaderParameterAttribute` to `OpenApiRequestHeaderParameterAttribute` and `OpenApiHeaderParameterSetAttribute` to `OpenApiRequestHeaderParameterSetAttribute` for request/response symmetry, and added generic `Attribute<T>` siblings for all four reusable header attributes: `OpenApiRequestHeaderParameterAttribute<T>`, `OpenApiRequestHeaderParameterSetAttribute<T>`, `OpenApiResponseHeaderAttribute<T>`, and `OpenApiResponseHeaderSetAttribute<T>`. The generic forms preserve the existing non-generic overloads while allowing reusable definitions and collections to be declared with compile-time generic syntax instead of `typeof(...)`. The sample app and README now demonstrate the pattern with `TenantIdHeader` used both standalone and inside `CatalogRequestHeaders`, and the test suite covers all four generic paths with 139/139 passing.

**Why:** Explicit request-side naming removes ambiguity with response headers, and generic attribute sugar makes reusable-header authoring more ergonomic without breaking existing inline or `Type`-based declarations. Sample and test coverage ensure the new API shape is discoverable, documented, and behaviorally verified.

### 2026-08-28: Naming correction note for earlier header-set decision entries

**By:** Squad (Coordinator)
**Status:** Accepted

**What:** Flagged by Copilot's PR #3 review as append-only-history nits, not code issues: the "Header-set reviewer-requested corrections" entry above refers to `[OpenApiHeaderParameter]`, and the "Header-set sample and README adoption" entry references `samples/SampleFunctionApp/Models/ItemHeaderSets.cs`. Both were accurate at the time they were written. The subsequent "Request-header rename..." and "Unified header-definition contracts..." entries later renamed that attribute to `OpenApiRequestHeaderParameterAttribute` and the sample file was placed at `samples/SampleFunctionApp/Headers/ItemHeaderSets.cs`. This note is appended (not a retroactive edit) to point future readers to the current names/paths without altering the historical record.

**Why:** `.squad/decisions.md` is append-only; the fix is a forward-pointing clarification rather than rewriting prior entries.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
