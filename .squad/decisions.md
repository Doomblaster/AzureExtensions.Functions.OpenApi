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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
