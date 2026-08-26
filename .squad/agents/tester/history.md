# Project Context

- **Owner:** Espen Berglund
- **Project:** Azure.Functions.OpenApi — a .NET 10 library that injects an OpenAPI HttpTrigger into an Azure Functions isolated worker v4 (latest) host and serves an OpenAPI specification document.
- **Stack:** .NET 10, C#, Azure Functions .NET isolated worker v4 (Microsoft.Azure.Functions.Worker), Microsoft.OpenApi (document build + JSON/YAML serialization). Target OpenAPI Specification 3.x (per OAI/OpenAPI-Specification).
- **Created:** 2026-08-26T09:49:06+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-08-26 — Test project & internal access.** Tests live in `tests/Azure.Functions.OpenApi.Tests` (net10.0, xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.14.1 + xunit.runner.visualstudio 3.1.5). The default provider and serializer are `internal`, so the library exposes them via a csproj MSBuild item `<InternalsVisibleTo Include="Azure.Functions.OpenApi.Tests" />` (preferred over a hand-written AssemblyInfo.cs). Keep the test assembly name in sync with that item.
- **2026-08-26 — Microsoft.OpenApi 3.10.2 emits exact patch version.** The `openapi` field is `"3.1.2"` (OpenApi3_1) / `"3.0.4"` (OpenApi3_0). Always assert the `"3.1"`/`"3.0"` PREFIX, never an exact `3.1.0`.
- **2026-08-26 — Endpoint testing without a Functions host.** `OpenApiHttpFunctions` triggers take an ASP.NET Core `HttpRequest` (AspNetCore integration model). Test them by constructing the class directly (real provider + `IOptions` + `NullLogger`), calling with a `DefaultHttpContext.Request`, then executing the returned `IResult` against an in-memory `DefaultHttpContext` (MemoryStream body, `RequestServices` with `AddLogging()`). Needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in the test csproj. This does NOT cover runtime route/authLevel binding (compile-time attributes) — that's verified via `functions.metadata`, not unit tests. No `func host start`, no Azure, no network.

📌 Team update (2026-08-26T09:49:06+02:00): Runtime binding metadata was verified by Functions while unit tests execute OpenApiHttpFunctions IResult directly for deterministic endpoint coverage — decided by Functions, Tester

- **2026-08-26 - Generator coverage (65 tests total).** Added DiscoveryTests, SchemaGeneratorTests, PathsBuilderTests, DocumentGenerationTests. Reflection-only internals (FunctionEndpointDiscovery, OpenApiSchemaGenerator, OpenApiPathsBuilder, DiscoveredEndpoint) are reachable via the existing InternalsVisibleTo. Two fake-function styles: DiscoveryTests use REAL [Function]+[HttpTrigger] fakes (verify path/verb/route-param extraction and {id:int}->{id} stripping); PathsBuilderTests use attribute-only fakes fed through hand-built DiscoveredEndpoint records so schema/attribute mapping is tested without the worker trigger machinery. E2E DocumentGenerationTests reference the real SampleFunctionApp via ProjectReference and pin DocumentAssemblies=typeof(Item).Assembly for determinism - no Functions-host build friction. 3.10.2 gotchas confirmed in assertions: JsonSchemaType is a [Flags] enum (nullable = OR Null, use HasFlag), OpenApiPathItem.Operations keyed by System.Net.Http.HttpMethod, OpenApiParameter.In is ParameterLocation?, OpenApiTagReference exposes .Reference.Id, complex types come back as OpenApiSchemaReference and land in Components.Schemas keyed by type.Name (enums stay inline, NOT registered).

📌 Team update (2026-08-26T11:30:00+02:00): Attribute-driven OpenAPI generation landed across public attributes/options, reflection discovery, CLR schema components, path/operation generation, sample CRUD endpoints, README docs, and 65/65 passing tests — decided by Lead, Functions, Backend, and Tester.


📌 Team update (2025-06-02T12:00:00Z): Enum schema assertions should expect a single component plus `$ref` reuse; nullable refs/enums/objects should assert `anyOf:[{$ref},{type:null}]`, nullable strings `type:["string","null"]`, and primitive scalars remain inline. Full suite is green at 74/74 — decided by Backend, Tester, Lead.

📌 Team update (2026-08-26T12:41:00+02:00): Swagger UI shipped as anonymous GET `/api/swagger` with CDN-hosted pinned `swagger-ui-dist@5.32.14`, absolute request-derived `openapi.json` URL, safe escaping, configurable options, README coverage, and 84/84 passing tests — decided by Functions, Tester, Lead.

📌 Team update (2026-08-26T12:41:00+02:00): Nullable `$ref` schema output was fact-checked: `anyOf:[{$ref},{type:null}]` is canonical for OpenAPI 3.1/JSON Schema 2020-12, and Microsoft.OpenApi 3.10.2 serializes it to valid OpenAPI 3.0 without illegal `type:null` — decided by Backend, Tester, Lead, Fact Checker.

📌 Team update (2026-08-26T12:59:00+02:00): ProblemDetails schema/content-type behavior is covered by 17 new tests and full suite verification at 101/101 passing — decided by Backend, Functions, Tester, Lead.
