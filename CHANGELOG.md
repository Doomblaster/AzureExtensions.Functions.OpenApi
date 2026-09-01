# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Until the first stable `0.1.0` release, prerelease builds are published to GitHub Packages
as `0.0.0-preview.*` from the `dev` branch.

## [Unreleased]

### Added
- OpenAPI 3.x document endpoint for Azure Functions isolated worker (v4) apps via a single
  `AddOpenApi()` registration call.
- Attribute-driven metadata for operations, parameters (query/header/path), request bodies,
  responses, and response headers.
- Reflection-based schema generation for primitives, enums, collections, and nested types,
  emitting reusable component schemas.
- Response definitions via `OpenApiResponseAttribute<T>` and `IOpenApiResponseDefinition`.
- RFC 9457 `ProblemDetails` / `HttpValidationProblemDetails` schema support.
- Swagger UI endpoint with `X-Forwarded-Host` support.
- Built with the `Microsoft.OpenApi` object model and serialized by that package.

[Unreleased]: https://github.com/Doomblaster/AzureExtensions.Functions.OpenApi/commits/dev
