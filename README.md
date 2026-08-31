# AzureExtensions.Functions.OpenApi

[![CI](https://github.com/Doomblaster/AzureExtensions.Functions.OpenApi/actions/workflows/ci.yml/badge.svg)](https://github.com/Doomblaster/AzureExtensions.Functions.OpenApi/actions/workflows/ci.yml)

A reusable .NET 10 class library that adds an **OpenAPI 3.x document endpoint** to an
[Azure Functions isolated worker (v4)](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
app. Reference the package, call one registration method, and your app exposes an OpenAPI
specification over HTTP that reflects its **real API**.

The document is built in code using the [Microsoft.OpenApi](https://www.nuget.org/packages/Microsoft.OpenApi)
object model and serialized by that package — no hand-rolled JSON.

## What it does

When you reference the library and call `AddOpenApi()`, the library **auto-scans the Functions
application assembly** and generates OpenAPI paths and operations for every `[Function]` method
backed by an `HttpTrigger`. For each endpoint it produces:

- **Query, header, and path parameters** — declared with attributes, or inferred from the route.
- **Request bodies** — with schemas derived from your CLR types.
- **Response schemas** — per documented status code.
- **`components/schemas`** — reusable schemas for your models, referenced with `$ref`.

The served documents at `GET /api/openapi.json` and `GET /api/openapi.yaml` therefore describe
the app's actual endpoints, not a static placeholder.

## Endpoints

Once registered, the app serves (using the default `api` route prefix):

- `GET /api/openapi.json` — the OpenAPI document as JSON
- `GET /api/openapi.yaml` — the OpenAPI document as YAML
- `GET /api/swagger` — an interactive [Swagger UI](#swagger-ui) page (enabled by default)

## Install

```bash
dotnet add package AzureExtensions.Functions.OpenApi
```

## Quick start

In your isolated worker `Program.cs`:

```csharp
using AzureExtensions.Functions.OpenApi;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// One call contributes GET /api/openapi.json and GET /api/openapi.yaml
builder.Services.AddOpenApi(options =>
{
    options.Title = "My API";
    options.Version = "1.0.0";
});

builder.Build().Run();
```

`AddOpenApi()` also works with no arguments to use the defaults. **Your HTTP endpoints are
discovered automatically** — you don't register them with the library. Annotate them (see below)
to enrich the document, or leave them bare and rely on best-effort inference.

## Annotating endpoints

Apply the following attributes to the method that backs an HTTP trigger. All are optional; use as
many as you need. Parameter attributes may be repeated on a single method.

| Attribute | Constructor | Notable properties |
| --- | --- | --- |
| `[OpenApiOperation]` | *(none)* | `OperationId`, `Summary`, `Description`, `Tags`, `Deprecated` |
| `[OpenApiQueryParameter]` | `(string name, Type type)` | `Required`, `Description` |
| `[OpenApiRequestHeaderParameter]` | `(string name, Type type)` | `Required`, `Deprecated`, `Description`, generic `T : IOpenApiHeaderDefinition, new()` |
| `[OpenApiRequestHeaderParameterSet]` | `(Type collectionType)` | `CollectionType`, generic `T : IOpenApiHeaderDefinitionCollection, new()` |
| `[OpenApiPathParameter]` | `(string name, Type type)` | `Required` (default `true`), `Description` |
| `[OpenApiRequestBody]` | `(Type type)` | `Required` (default `true`), `ContentType`, `Description` |
| `[OpenApiResponse]` | `(int statusCode)` | `Type` (omit for no body), `ContentType`, `Description`, generic `T : IOpenApiResponseDefinition, new()` |
| `[OpenApiResponseHeader]` | `(string name, Type type, params int[] statusCodes)` | `Required`, `Deprecated`, `Description`, generic `T : IOpenApiHeaderDefinition, new()` |
| `[OpenApiResponseHeaderSet]` | `(Type collectionType, params int[] statusCodes)` | `CollectionType`, `StatusCodes`, generic `T : IOpenApiHeaderDefinitionCollection, new()` |

### Annotated CRUD example

```csharp
public sealed class ItemsFunctions
{
    private const string ItemsTag = "Items";

    // GET /api/items — list with a query parameter
    [Function("ListItems")]
    [OpenApiOperation(
        OperationId = "listItems",
        Summary = "List items",
        Description = "Returns a paged list of catalog items, optionally filtered by status.",
        Tags = new[] { ItemsTag })]
    [OpenApiQueryParameter("status", typeof(ItemStatus), Required = false, Description = "Filter items by lifecycle status.")]
    [OpenApiQueryParameter("page", typeof(int), Required = false, Description = "1-based page number.")]
    [OpenApiResponse(200, Type = typeof(List<Item>), Description = "The matching items.")]
    public IResult ListItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items")] HttpRequest req)
        => Results.Ok(/* ... */);

    // GET /api/items/{id} — a path parameter
    [Function("GetItem")]
    [OpenApiOperation(OperationId = "getItem", Summary = "Get an item", Tags = new[] { ItemsTag })]
    [OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
    [OpenApiResponse(200, Type = typeof(Item), Description = "The requested item.")]
    [OpenApiResponse(404, Description = "No item exists with the given identifier.")]
    public IResult GetItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}")] HttpRequest req,
        int id)
        => Results.Ok(/* ... */);

    // POST /api/items — request body, header parameter, 201 response
    [Function("CreateItem")]
    [OpenApiOperation(OperationId = "createItem", Summary = "Create an item", Tags = new[] { ItemsTag })]
    [OpenApiRequestHeaderParameter("X-Correlation-Id", typeof(Guid), Required = false, Description = "Optional client correlation identifier.")]
    [OpenApiRequestBody(typeof(CreateItemRequest), Description = "The item to create.")]
    [OpenApiResponse(201, Type = typeof(Item), Description = "The created item.")]
    public Task<IResult> CreateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items")] HttpRequest req)
        => /* ... */;

    // PUT /api/items/{id} — update
    [Function("UpdateItem")]
    [OpenApiOperation(OperationId = "updateItem", Summary = "Update an item", Tags = new[] { ItemsTag })]
    [OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
    [OpenApiRequestBody(typeof(UpdateItemRequest), Description = "The updated item values.")]
    [OpenApiResponse(200, Type = typeof(Item), Description = "The updated item.")]
    [OpenApiResponse<NotFoundResponseDefinition>]
    public Task<IResult> UpdateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "items/{id}")] HttpRequest req,
        int id)
        => /* ... */;

    // DELETE /api/items/{id} — delete
    [Function("DeleteItem")]
    [OpenApiOperation(OperationId = "deleteItem", Summary = "Delete an item", Tags = new[] { ItemsTag })]
    [OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
    [OpenApiResponse(204, Description = "The item was deleted.")]
    [OpenApiResponse<NotFoundResponseDefinition>]
    public IResult DeleteItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "items/{id}")] HttpRequest req,
        int id)
        => Results.NoContent();
}
```

A complete, runnable version of this app lives in `samples/SampleFunctionApp/`.

### Reusable response definitions

When several endpoints share the same response metadata, implement
`IOpenApiResponseDefinition` once and reuse it through the generic
`[OpenApiResponse<T>]` form:

```csharp
public sealed class NotFoundResponseDefinition : IOpenApiResponseDefinition
{
    public int StatusCode => 404;
    public Type? Type => null;

    // ContentType is ignored by OpenAPI generation because Type is null (no response body).
    public string ContentType => "application/json";
    public string Description => "No item exists with the given identifier.";
}

[OpenApiResponse<NotFoundResponseDefinition>]
```

Prefer the generic form when you already have a reusable response definition because the compiler
enforces `new()` plus the correct interface contract. The original non-generic overload remains
available for one-off responses:

```csharp
[OpenApiResponse(404, Description = "No item exists with the given identifier.")]
```

### Response headers

Use `[OpenApiResponseHeader]` to document a header returned on a response. Each header is emitted as
an OpenAPI Header Object under `responses.{statusCode}.headers.{name}`, with an inline schema derived
from the supplied CLR type. The attribute may be repeated on a single method.

```csharp
[OpenApiResponse(201, Type = typeof(Item), Description = "The created item.")]
[OpenApiResponseHeader("Location", typeof(Uri), 201, Description = "URL of the newly created item.")]
public Task<IResult> CreateItem(/* ... */) => /* ... */;
```

The trailing `params int[] statusCodes` controls which responses the header attaches to:

- **One or more status codes** — the header is attached to each listed response. If a listed status
  code has no `[OpenApiResponse]`, a bare response is created so the header is not lost. A single
  attribute can therefore span several codes:

  ```csharp
  [OpenApiResponse(200, Type = typeof(List<Item>), Description = "Matching items.")]
  [OpenApiResponse(400, Type = typeof(HttpValidationProblemDetails), Description = "Invalid request.")]
  [OpenApiResponseHeader("X-Request-Id", typeof(Guid), 200, 400, Description = "Correlation id.")]
  public IResult SearchItems(/* ... */) => /* ... */;
  ```

- **No status codes** — the header is attached to **every** response documented for the method (or to
  the synthetic `200` when the method documents none). Already-present responses are targeted; no new
  responses are invented.

  ```csharp
  [OpenApiResponseHeader("X-Trace-Id", typeof(string), Description = "Trace id on all responses.")]
  ```

Response headers are distinct from **request** header parameters (`[OpenApiRequestHeaderParameter]`), which
document inbound headers as operation parameters.

### Header sets (reusable header groups)

If the same headers always travel together, you can define them once and reuse them with
`[OpenApiRequestHeaderParameterSet<TCollection>]` or `[OpenApiResponseHeaderSet<TCollection>]`
instead of repeating several single-header attributes on every method. The original non-generic
`Type` overloads still exist for simple inline usage and for runtime-only types.

Define a concrete collection type with a public parameterless constructor that implements
`IOpenApiHeaderDefinitionCollection`:

```csharp
using AzureExtensions.Functions.OpenApi;

namespace SampleFunctionApp.Headers;

internal sealed class HeaderDefinition : IOpenApiHeaderDefinition
{
    public required string Name { get; init; }
    public required Type Type { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public bool Deprecated { get; init; }
}

public sealed class TenantIdHeader : IOpenApiHeaderDefinition
{
    public string Name => "X-Tenant-Id";
    public Type Type => typeof(Guid);
    public string Description => "Tenant identifier used to scope the catalog request.";
    public bool Required => true;
    public bool Deprecated => false;
}

public sealed class CatalogRequestHeaders : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new HeaderDefinition
        {
            Name = "X-Correlation-Id",
            Type = typeof(Guid),
            Description = "Client-supplied correlation identifier for catalog write operations.",
            Required = true,
            Deprecated = false,
        },
        new TenantIdHeader(),
    ];
}

public sealed class CatalogRateLimitHeaders : IOpenApiHeaderDefinitionCollection
{
    public IReadOnlyList<IOpenApiHeaderDefinition> Headers { get; } =
    [
        new HeaderDefinition
        {
            Name = "X-RateLimit-Limit",
            Type = typeof(int),
            Description = "Maximum number of catalog read requests allowed in the current window.",
            Required = true,
            Deprecated = false,
        },
        new HeaderDefinition
        {
            Name = "X-RateLimit-Remaining",
            Type = typeof(int),
            Description = "Catalog read requests remaining in the current window.",
            Required = true,
            Deprecated = false,
        },
    ];
}
```

Each reusable header entry implements `IOpenApiHeaderDefinition`, so the same definition type can be
reused both as a standalone generic attribute and as a member inside a reusable collection.
`Deprecated` is available in both places for OpenAPI parity: on individual
`[OpenApiRequestHeaderParameter]` / `[OpenApiResponseHeader]` attributes and on members inside a
reusable header set, matching the spec's `deprecated` support on both Parameter Objects and Header
Objects.

Apply reusable definitions and sets like this:

```csharp
[Function("GetItem")]
[OpenApiOperation(OperationId = "getItem", Summary = "Get an item", Tags = new[] { ItemsTag })]
[OpenApiPathParameter("id", typeof(int), Description = "The item identifier.")]
[OpenApiRequestHeaderParameter<TenantIdHeader>]
[OpenApiResponse(200, Type = typeof(Item), Description = "The requested item.")]
public IResult GetItem(/* ... */) => /* ... */;

[Function("CreateItem")]
[OpenApiOperation(OperationId = "createItem", Summary = "Create an item", Tags = new[] { ItemsTag })]
[OpenApiRequestHeaderParameterSet<CatalogRequestHeaders>]
[OpenApiRequestHeaderParameter("X-Correlation-Id", typeof(Guid), Required = false, Description = "Optional client correlation identifier.")]
[OpenApiRequestBody(typeof(CreateItemRequest), Description = "The item to create.")]
[OpenApiResponse(201, Type = typeof(Item), Description = "The created item.")]
public Task<IResult> CreateItem(/* ... */) => /* ... */;

[Function("SearchItems")]
[OpenApiOperation(OperationId = "searchItems", Summary = "Search items", Tags = new[] { ItemsTag })]
[OpenApiResponse(200, Type = typeof(List<Item>), Description = "Matching items.")]
[OpenApiResponse(400, Type = typeof(HttpValidationProblemDetails), Description = "The request was invalid.")]
[OpenApiResponseHeaderSet<CatalogRateLimitHeaders>(200, 400)]
[OpenApiResponseHeader("X-Request-Id", typeof(Guid), 200, 400, Description = "Correlation id echoed on success and validation failure.")]
public IResult SearchItems(/* ... */) => /* ... */;
```

Prefer the generic forms when you have a reusable definition or collection type because the
compiler enforces `new()` plus the correct interface contract. The original non-generic overloads
remain available:

```csharp
[OpenApiRequestHeaderParameter("X-Correlation-Id", typeof(Guid), Required = false)]
[OpenApiRequestHeaderParameterSet(typeof(CatalogRequestHeaders))]
[OpenApiResponseHeader("Location", typeof(Uri), 201)]
[OpenApiResponseHeaderSet(typeof(CatalogRateLimitHeaders), 200, 400)]
```

For response header sets, the trailing `params int[] statusCodes` uses the same rules as
`[OpenApiResponseHeader]`:

- **One or more status codes** — apply the set to each listed response.
- **No status codes** — apply the set to every response already documented on the method (via
  `[OpenApiResponse]`); it does not invent new responses.

On a case-insensitive name collision, an individual attribute on the same method always wins over
the matching set member. In the `CreateItem` example above, the method reuses
`CatalogRequestHeaders`, but its individual `[OpenApiRequestHeaderParameter("X-Correlation-Id", ...)]`
overrides the set's `X-Correlation-Id` declaration for that method. The same precedence rule
applies to `[OpenApiResponseHeader]` versus `[OpenApiResponseHeaderSet]`.

## Unannotated endpoints

Endpoints without OpenAPI attributes are still documented on a **best-effort** basis. Discovery
infers:

- the **path** from the trigger's route template (or the function name when no route is set),
- the **HTTP verb(s)** from the trigger's method list,
- **path parameters** from `{...}` tokens in the route template.

This behavior is controlled by `IncludeUnannotatedEndpoints` (default `true`). Set it to `false`
to document **only** endpoints that carry OpenAPI attributes.

## Schema generation

Model types referenced by request bodies, responses, and typed parameters are converted to JSON
Schema and placed in `components/schemas`, referenced by `$ref`. The generator supports:

- **Primitives** — `int`, `long`, `bool`, `double`, `string`, etc.
  Bare primitives stay **inline** wherever they are used; scalars are never hoisted into
  `components/schemas`.
- **`decimal`** — emitted as `type: number` with `format: decimal` (also inline).
- **Temporal & identifier types** — emitted as `type: string` with an OpenAPI/JSON-Schema
  `format`: `Guid` → `uuid`, `DateTime`/`DateTimeOffset` → `date-time`, `DateOnly` → `date`,
  `TimeOnly` → `time`, `TimeSpan` → `duration`, `Uri` → `uri`. Nullable value-type variants
  (e.g. `DateTime?`, `Guid?`) stay inline as `type: ["string", "null"]` with the `format`
  preserved.
- **Enums** — emitted as `type: string` using the member names, and registered as **reusable
  components** (see below).
- **Collections** — arrays for `IEnumerable<T>`/`List<T>` element types.
- **Dictionaries** — objects with `additionalProperties` set to the value type's schema.
- **Nested classes** — recursively generated and referenced by `$ref`.
- **Nullability** — nullable reference types, nullable enums, and `Nullable<T>` value types are
  modelled as nullable schemas (see below).

### Enums as reusable components

Enums are **not inlined**. Each distinct enum type is registered **once** under
`components/schemas/{EnumTypeName}`, and every use in the document is a `$ref` to that single
component. This keeps the spec compact and lets client generators emit one shared type per enum.

In the sample app, `ItemStatus` is defined once:

```jsonc
"components": {
  "schemas": {
    "ItemStatus": {
      "type": "string",
      "enum": [ "Active", "Discontinued", "Backordered" ]
    }
  }
}
```

and referenced everywhere it appears — the `status` query parameter on `ListItems`, and the
`Status` property on the `Item`, `CreateItemRequest`, and `UpdateItemRequest` schemas:

```jsonc
"status": { "$ref": "#/components/schemas/ItemStatus" }
```

Hoisting scalars to components is a deliberate **non-goal**: bare primitives (`int`, `string`,
`decimal`, …) stay inline. Only **enums** and **complex object types** become components.

### Nullable handling

Nullability is detected from the CLR type: nullable **reference** types via
`System.Reflection.NullabilityInfoContext` (honouring C# nullable-reference annotations), and
nullable **value** types (`int?`) via `Nullable.GetUnderlyingType`. Non-nullable properties stay
plain, with no null modelling. How null is expressed depends on the shape of the schema:

- **Nullable reference types and nullable enums** — because a `$ref` cannot carry a `type`
  keyword, the null is expressed with an `anyOf` null-union. For example, `Item.Dimensions` is
  `ItemDimensions?`:

  ```jsonc
  "Dimensions": {
    "anyOf": [
      { "$ref": "#/components/schemas/ItemDimensions" },
      { "type": "null" }
    ]
  }
  ```

- **Nullable inline scalars** — expressed as a type array. A `string?` property emits:

  ```jsonc
  "type": [ "string", "null" ]
  ```

- **Non-nullable properties** — emitted plainly, e.g. `"type": "string"` or a bare `$ref`.

## Error responses (ProblemDetails / RFC 9457)

The library recognizes the ASP.NET Core **ProblemDetails family** as error-response body types and
emits [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) *"Problem Details for HTTP APIs"* schemas
for them. The recognized types are:

- `Microsoft.AspNetCore.Mvc.ProblemDetails`
- `Microsoft.AspNetCore.Http.HttpValidationProblemDetails`
- `Microsoft.AspNetCore.Mvc.ValidationProblemDetails`

### Canonical schemas, not reflection

Rather than reflecting over these framework types, the generator emits **canonical RFC 9457
schemas** registered **once** as reusable components under `components/schemas` and referenced with
`$ref` wherever a problem type appears.

- `ProblemDetails` uses the lowercase members defined by RFC 9457 — `type` (uri), `title`,
  `status` (integer), `detail`, and `instance` (uri) — all **nullable**. It also sets
  `additionalProperties` so that RFC 9457 **extension members** are allowed. (ASP.NET Core
  serializes extension members flattened at the top level of the object via `[JsonExtensionData]`.)

  ```jsonc
  "ProblemDetails": {
    "type": "object",
    "properties": {
      "type":     { "type": [ "string", "null" ], "format": "uri" },
      "title":    { "type": [ "string", "null" ] },
      "status":   { "type": [ "integer", "null" ] },
      "detail":   { "type": [ "string", "null" ] },
      "instance": { "type": [ "string", "null" ], "format": "uri" }
    },
    "additionalProperties": true
  }
  ```

- The validation variants (`HttpValidationProblemDetails`, `ValidationProblemDetails`) add an
  `errors` object — a map of **field name → array of error strings** — and are composed with
  `allOf` referencing the shared `ProblemDetails` component:

  ```jsonc
  "HttpValidationProblemDetails": {
    "allOf": [ { "$ref": "#/components/schemas/ProblemDetails" } ],
    "type": "object",
    "properties": {
      "errors": {
        "type": "object",
        "additionalProperties": { "type": "array", "items": { "type": "string" } }
      }
    }
  }
  ```

### `application/problem+json`

Responses (and request bodies) whose type is a problem-family type are served as
**`application/problem+json`** automatically, per RFC 9457. An explicitly-set `ContentType` on the
attribute is respected and takes precedence.

### Annotating an endpoint

Declare the problem type on `[OpenApiResponse]` (or `[OpenApiRequestBody]`) just like any other
model, and return it from the handler with `Results.Problem(...)` /
`Results.ValidationProblem(...)`:

```csharp
[OpenApiResponse(404, Type = typeof(ProblemDetails), Description = "Not found.")]
[OpenApiResponse(400, Type = typeof(HttpValidationProblemDetails), Description = "Validation failed.")]
```

The sample app demonstrates both. `GetItem` (`GET /api/items/{id}`) returns a `404` `ProblemDetails`
via `Results.Problem(...)` when no item matches, and `SearchItems` (`GET /api/items/search`) returns
a `400` `HttpValidationProblemDetails` via `Results.ValidationProblem(...)` when the required `name`
query parameter is missing. Both live in
`samples/SampleFunctionApp/Functions/ItemsFunctions.cs`.

## Swagger UI

Alongside the raw documents, the library serves an interactive **Swagger UI** page at
`GET /api/swagger`. The endpoint is **anonymous** and returns a minimal HTML shell that renders
Swagger UI and points it at your `openapi.json` endpoint. The JSON URL is derived from the incoming
request, so it honours the host's `RoutePrefix` and the configured `JsonRoute` automatically.

When the app runs behind a reverse proxy (for example the **.NET Aspire** dev proxy), the internal
listener host/port differs from the public-facing address the browser actually used. The JSON URL
therefore honours the standard `X-Forwarded-Host` and `X-Forwarded-Proto` headers when they are
present — falling back to `request.Host` / `request.Scheme` otherwise — so the Swagger UI loads the
spec from a URL the browser can reach. If a proxy chain supplies multiple comma-separated values,
the first (client-facing) entry is used.

The Swagger UI assets — `swagger-ui.css`, `swagger-ui-bundle.js`, and
`swagger-ui-standalone-preset.js` — are loaded from a **CDN (jsDelivr)** and are **not embedded or
bundled** in the library, so the package stays free of vendored JavaScript. The asset version is
**pinned** (default `swagger-ui-dist@5.32.14`) and fully configurable via `SwaggerUiCdnBaseUrl` and
`SwaggerUiVersion`.

Set `EnableSwaggerUi = false` to disable the page — the endpoint then returns `404 Not Found`.

```csharp
builder.Services.AddOpenApi(options =>
{
    options.Title = "My API";
    options.Version = "1.0.0";
    options.EnableSwaggerUi = true;              // default; set false to return 404
    options.SwaggerUiPageTitle = "My API — Docs"; // optional; falls back to Title
});
```

Browse to `http://localhost:7071/api/swagger` when running the sample app locally.

> **Route nuance:** `SwaggerUiRoute` (default `"swagger"`) is **advisory only** — it is used when
> advertising/documenting the page's URL. The actual route bound to the `[HttpTrigger]` is a
> compile-time constant (`"swagger"`), so changing `SwaggerUiRoute` does **not** move the endpoint.

> **Content-Security-Policy:** because the assets load from jsDelivr, any strict CSP applied to the
> page must allow the `https://cdn.jsdelivr.net` origin in both `script-src` and `style-src` (and
> `connect-src`/`img-src` as needed), otherwise the UI will fail to load.

## Options

Configure via the `AddOpenApi(options => { ... })` callback (`OpenApiOptions`):

| Option | Default | Purpose |
| --- | --- | --- |
| `Title` | `"OpenAPI Document"` | `info.title` of the document |
| `Version` | `"1.0.0"` | `info.version` (your API version) |
| `Description` | `null` | `info.description` |
| `RoutePrefix` | `"api"` | Functions host route prefix, used to advertise endpoint URLs |
| `JsonRoute` | `"openapi.json"` | Route (relative to `RoutePrefix`) serving JSON |
| `YamlRoute` | `"openapi.yaml"` | Route (relative to `RoutePrefix`) serving YAML |
| `SpecVersion` | `OpenApi3_1` | OpenAPI Specification version to serialize against |
| `IncludeUnannotatedEndpoints` | `true` | Document HTTP functions with no OpenAPI attributes |
| `EnableSwaggerUi` | `true` | Serve the interactive Swagger UI page; `false` returns `404` |
| `SwaggerUiRoute` | `"swagger"` | **Advisory** route used to advertise the UI URL (the actual trigger route is fixed at `swagger`) |
| `SwaggerUiCdnBaseUrl` | `"https://cdn.jsdelivr.net/npm/swagger-ui-dist"` | CDN base URL hosting the Swagger UI assets |
| `SwaggerUiVersion` | `"5.32.14"` | Pinned `swagger-ui-dist` version loaded from the CDN |
| `SwaggerUiPageTitle` | `null` | Browser-tab title for the UI page; falls back to `Title` |
| `DocumentAssemblies` | *(empty)* | Assemblies to scan; empty auto-scans the Functions app assembly |

## Reflection and AOT/trimming

Discovery and schema generation are **reflection-based**: the library scans assemblies for
`[Function]`/`HttpTrigger` methods and walks CLR types to build schemas. This works out of the box
for the standard isolated worker, but is **not trim- or Native-AOT-safe** — aggressive trimming may
remove types or metadata the generator relies on. Avoid trimming the Functions app assembly and the
model types you expose, or keep them with trimming roots/`DynamicDependency` if you enable AOT.

## Repository layout

```
AzureExtensions.Functions.OpenApi.slnx
src/AzureExtensions.Functions.OpenApi/      # the library
samples/SampleFunctionApp/        # isolated worker v4 sample consumer
```

## Build

```bash
dotnet build AzureExtensions.Functions.OpenApi.slnx
```

Requires the .NET 10 SDK.

## License

Licensed under the [MIT License](https://github.com/Doomblaster/AzureExtensions.Functions.OpenApi/blob/main/LICENSE).

## Releasing

Maintainer release/publishing details (versioning, CI/CD workflows, NuGet.org Trusted Publishing
setup) live in [`docs/RELEASING.md`](https://github.com/Doomblaster/AzureExtensions.Functions.OpenApi/blob/main/docs/RELEASING.md).
