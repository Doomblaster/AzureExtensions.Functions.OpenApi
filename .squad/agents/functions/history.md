# Project Context

- **Owner:** Espen Berglund
- **Project:** Azure.Functions.OpenApi — a .NET 10 library that injects an OpenAPI HttpTrigger into an Azure Functions isolated worker v4 (latest) host and serves an OpenAPI specification document.
- **Stack:** .NET 10, C#, Azure Functions .NET isolated worker v4 (Microsoft.Azure.Functions.Worker), Microsoft.OpenApi (document build + JSON/YAML serialization). Target OpenAPI Specification 3.x (per OAI/OpenAPI-Specification).
- **Created:** 2026-08-26T09:49:06+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-08-26 — HTTP trigger bodies + serialization seam

- Implemented `OpenApiHttpFunctions.GetOpenApiJson` / `GetOpenApiYaml` as `async Task<IResult>`,
  returning `Results.Text(payload, contentType, 200)` at `AuthorizationLevel.Anonymous`.
- **Constructor injection works in the isolated worker function class**: injected
  `IOpenApiDocumentProvider` + `IOptions<OpenApiOptions>` + `ILogger<>`. `SpecVersion` must come
  from `IOptions<OpenApiOptions>` — the provider does not expose it.
- **`[HttpTrigger(Route=...)]` is a compile-time constant** — you cannot bind route segments to
  runtime `OpenApiOptions`. Route *prefix* (`api`) is host-owned via `host.json`
  (`extensions.http.routePrefix`); `OpenApiOptions.RoutePrefix/JsonRoute/YamlRoute` are advisory.
- **Microsoft.OpenApi 3.10.2 writers have no sync `Flush()`** — only `FlushAsync(ct)`; flush the
  underlying `TextWriter`/`StringWriter` instead. (Caught a sibling's build break from this.)
- Kept serialization behind a single private `Serialize(...)` seam that delegates to Backend's
  `OpenApiDocumentSerializer.SerializeJson/SerializeYaml` — one swap point, no hand-rolled JSON/YAML.
- **Verify triggers via `functions.metadata`** in `bin/.../` after build — confirms the source
  generator discovered them with the expected route/method/authLevel without running the host.
- Build: `dotnet build Azure.Functions.OpenApi.sln` → 0 errors / 0 warnings.

📌 Team update (2026-08-26T09:49:06+02:00): Endpoint routes are fixed HttpTrigger constants openapi.json/openapi.yaml with host.json controlling routePrefix; options route fields are advisory for links/docs — decided by Lead, Functions

### 2026-08-26 — Reflection-based function endpoint discovery

- Added `Discovery/FunctionEndpointDiscovery.cs`: `internal record DiscoveredEndpoint(Path,
  HttpMethods, MethodInfo, RouteParameters)` + `internal class FunctionEndpointDiscovery` with
  `Discover(IEnumerable<Assembly>, string routePrefix)` and `GetDefaultAssemblies()`.
- **Design decoupled from options on purpose**: takes `assemblies` + `routePrefix` as params so it
  compiles regardless of Lead's in-flight `IncludeUnannotatedEndpoints`/`DocumentAssemblies`.
- **`HttpTriggerAttribute.Route` keeps ASP.NET route constraints** (`{id:int}`, `{id?}`). The
  emitted OpenAPI-style `Path` must strip them → normalize tokens to bare `{name}` with a single
  regex; reuse the same regex to collect `RouteParameters`. (Initial pass left `{id:int}` in the
  path — caught by the throwaway verification test.)
- **Path combine trick**: split both prefix and route on `/` with `RemoveEmptyEntries`, rejoin with
  a single leading `/` — kills double slashes and handles empty prefix for free.
- **Resilience matters for reflection**: wrap `GetTypes()` (catch `ReflectionTypeLoadException`,
  keep non-null types) and per-method discovery in try/catch so one bad type/method never aborts the
  whole scan.
- Framework filter: exclude assembly simple-names starting with `System`/`Microsoft`/`netstandard`/
  `mscorlib`; always include `Assembly.GetEntryAssembly()`.
- Verified `[Function("GetItem")]`+`[HttpTrigger(...,"get",Route="items/{id:int}")]` →
  `/api/items/{id}`, `["GET"]`, `["id"]`. Build: `dotnet build Azure.Functions.OpenApi.sln` → 0/0.

## 2026-08-26 — Sample CRUD app (todo sample-crud)

- Added realistic CRUD to `samples/SampleFunctionApp` exercising every generator feature:
  `Models/Item.cs` (enum, decimal, `List<string>`, nullable nested `ItemDimensions`,
  `DateTimeOffset`, + Create/Update DTOs) and `Functions/ItemsFunctions.cs`.
- 5 endpoints, all Tag `Items`, Anonymous, ASP.NET Core `HttpRequest`/`IResult` model:
  ListItems GET items (query status/page, 200 List<Item>); GetItem GET items/{id} (path id, 200/404);
  CreateItem POST items (header X-Correlation-Id, body CreateItemRequest, 201); UpdateItem PUT
  items/{id} (path + body UpdateItemRequest, 200/404); DeleteItem DELETE items/{id} (204/404).
- **Attribute gotcha**: path/query/header/response attrs are `[AttributeUsage(Method)]` — they
  target the METHOD, not the `HttpRequest` parameter. Route path params (`{id}`) still need the
  method to accept an `int id` arg alongside `HttpRequest` for the worker binding.
- Build `dotnet build Azure.Functions.OpenApi.sln` → 0 warnings / 0 errors. Verified
  `functions.metadata` in sample bin lists all 5 item functions with routes `items`/`items/{id}`.

📌 Team update (2026-08-26T11:30:00+02:00): Attribute-driven OpenAPI generation landed across public attributes/options, reflection discovery, CLR schema components, path/operation generation, sample CRUD endpoints, README docs, and 65/65 passing tests — decided by Lead, Functions, Backend, and Tester.


## Swagger UI endpoint (2026-08-26)
- Added anonymous GET trigger `GetSwaggerUi` (route `swagger` → `/api/swagger`) serving a minimal
  CDN-loaded Swagger UI page pointed at `openapi.json`. New file `SwaggerUiHtml.cs` (internal static,
  `Build(...)` returns the shell using a C# raw string literal `\\\` interpolation).
- Pinned `swagger-ui-dist` **5.32.14** (latest stable 5.x, verified via npm registry `/latest`).
  Never use `latest` — pin for deterministic output. Configurable via new `OpenApiOptions`:
  EnableSwaggerUi/SwaggerUiRoute/SwaggerUiCdnBaseUrl/SwaggerUiVersion/SwaggerUiPageTitle.
- **Route is a compile-time const** (Functions requirement) — `SwaggerUiRoute` option is advisory only,
  same pattern as JsonRoute/YamlRoute. Documented that in XML docs.
- JSON URL built ABSOLUTE from request (`{scheme}://{host}/{pathBase}/{RoutePrefix}/{JsonRoute}`),
  segments trimmed + empties dropped to avoid doubled slashes. Injected via `JsonSerializer.Serialize`
  for safe JS-string escaping; page title via `WebUtility.HtmlEncode`.
- `Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status200OK)` compiles.
- Build 0/0; sample `functions.metadata` now lists GetSwaggerUi. Tests/README left to Tester/Lead.

📌 Team update (2026-08-26T12:41:00+02:00): Swagger UI shipped as anonymous GET `/api/swagger` with CDN-hosted pinned `swagger-ui-dist@5.32.14`, absolute request-derived `openapi.json` URL, safe escaping, configurable options, README coverage, and 84/84 passing tests — decided by Functions, Tester, Lead.

## 2026-08-26 — problem-sample
- Wired RFC 9457 ProblemDetails into SampleFunctionApp: GetItem 404 -> Results.Problem + [OpenApiResponse(404, typeof(Mvc.ProblemDetails))]; new SearchItems (GET items/search) with required 
ame query param, 200 List<Item>, 400 Results.ValidationProblem + [OpenApiResponse(400, typeof(Http.HttpValidationProblemDetails))].
- Learning: HttpValidationProblemDetails lives in Microsoft.AspNetCore.Http (not Mvc); ProblemDetails/ValidationProblemDetails in Microsoft.AspNetCore.Mvc. Results.Problem/Results.ValidationProblem/StatusCodes come from the already-imported Microsoft.AspNetCore.Http. Backend auto-selects application/problem+json when the response Type is a problem-family type and ContentType is default. Build clean 0/0.

📌 Team update (2026-08-26T12:59:00+02:00): Sample app now exercises RFC 9457 ProblemDetails with GetItem 404 Results.Problem and SearchItems 400 Results.ValidationProblem plus required name query — decided by Backend, Functions, Tester, Lead.

📌 Team update (2026-08-28T16:01:40.777+02:00): Functions added the sample ItemHeaderSets.cs, applied request/response header-set attributes in ItemsFunctions.cs, and updated README.md so the new reusable header collections ship with end-to-end sample and docs coverage.
