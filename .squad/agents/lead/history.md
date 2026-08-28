# Project Context

- **Owner:** Espen Berglund
- **Project:** Azure.Functions.OpenApi — a .NET 10 library that injects an OpenAPI HttpTrigger into an Azure Functions isolated worker v4 (latest) host and serves an OpenAPI specification document.
- **Stack:** .NET 10, C#, Azure Functions .NET isolated worker v4 (Microsoft.Azure.Functions.Worker), Microsoft.OpenApi (document build + JSON/YAML serialization). Target OpenAPI Specification 3.x (per OAI/OpenAPI-Specification).
- **Created:** 2026-08-26T09:49:06+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- 2026-08-26: Scaffolded net10.0 solution + froze public API (IOpenApiDocumentProvider, OpenApiOptions, AddOpenApi, OpenApiHttpFunctions). Chose ASP.NET Core HTTP integration model. Microsoft.OpenApi 3.10.2 (namespaces flattened to Microsoft.OpenApi, not .Models). Worker 2.52.0. Solution + sample build clean.

📌 Team update (2026-08-26T09:49:06+02:00): Public API scaffold is now implemented end-to-end with provider, serializer, HTTP endpoints, sample host wiring, and 25 passing tests — decided by Lead, Backend, Functions, Tester
- 2026-08-26: Froze the public OpenAPI attribute contract (6 method-level attributes in Attributes/: Operation, QueryParameter, HeaderParameter, PathParameter, RequestBody, Response) — pure metadata carriers, ctor args carry required identity (name/type/status), optional data are init-set props. PathParameter.Required defaults true; Response.Type null = bodyless. Added OpenApiOptions.IncludeUnannotatedEndpoints (default true) and DocumentAssemblies (empty = auto-scan) as additive hooks discovery reads. AddOpenApi needed no change. System.Reflection.Assembly needs an explicit using in OpenApiOptions.cs (not in ImplicitUsings). Build 0/0.

## 2026-08-26 — README: attribute-driven OpenAPI generation

Updated repo-root README to document the shipped feature. Verified all attribute signatures against src/Azure.Functions.OpenApi/Attributes/* before writing snippets (Query/Header/Path ctor = (string name, Type type); RequestBody = (Type type); Response = (int statusCode) + optional Type; Operation has no ctor, props only). Options table sourced from OpenApiOptions.cs; schema behavior (decimal→number/format decimal, enums→string names, dictionaries→additionalProperties, 3.1 nullable arrays) confirmed from Schema/OpenApiSchemaGenerator.cs. Note: the .squad/decisions/inbox/* notes referenced in the spawn were already merged/absent — read live source instead of trusting the prompt's file list. Kept README scope to root README.md only; did not touch src/tests (Tester owns tests).

📌 Team update (2026-08-26T11:30:00+02:00): Attribute-driven OpenAPI generation landed across public attributes/options, reflection discovery, CLR schema components, path/operation generation, sample CRUD endpoints, README docs, and 65/65 passing tests — decided by Lead, Functions, Backend, and Tester.

📌 Team update (2026-08-28T13:57:21.332+02:00): Approved the schema-id collision rule that keeps the first plain base id, then prefers `lastNamespaceSegment + baseName` before numeric suffixes so same-named CLR types across namespaces remain readable and unique; shipped in PR #2


📌 Team update (2025-06-02T12:00:00Z): README now documents enum components and nullable schema handling: enums/complex objects are components reused by `$ref`; nullable refs/enums/objects use `anyOf` null unions; nullable inline scalars use type arrays; non-nullable members stay plain — decided by Backend, Tester, Lead.

📌 Team update (2026-08-26T12:41:00+02:00): Swagger UI shipped as anonymous GET `/api/swagger` with CDN-hosted pinned `swagger-ui-dist@5.32.14`, absolute request-derived `openapi.json` URL, safe escaping, configurable options, README coverage, and 84/84 passing tests — decided by Functions, Tester, Lead.

📌 Team update (2026-08-26T12:41:00+02:00): Nullable `$ref` schema output was fact-checked: `anyOf:[{$ref},{type:null}]` is canonical for OpenAPI 3.1/JSON Schema 2020-12, and Microsoft.OpenApi 3.10.2 serializes it to valid OpenAPI 3.0 without illegal `type:null` — decided by Backend, Tester, Lead, Fact Checker.

📌 Team update (2026-08-26T12:59:00+02:00): README now documents RFC 9457 ProblemDetails support, canonical reusable schemas, application/problem+json, explicit overrides, and sample references — decided by Backend, Functions, Tester, Lead.

📌 Team update (2026-08-28T16:01:40.777+02:00): Header-set delivery is complete: Lead defined the interface-only request/response header-set contracts, review #1 rejected over-broad collision suppression and malformed-endpoint stray path-item behavior, Lead then owned the corrective patch under the Reviewer Rejection Protocol because Backend/Tester were locked out from revising rejected artifacts, and review #2 approved with 132/132 tests passing.
