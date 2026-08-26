# Project Context

- **Owner:** Espen Berglund
- **Project:** Azure.Functions.OpenApi — a .NET 10 library that injects an OpenAPI HttpTrigger into an Azure Functions isolated worker v4 (latest) host and serves an OpenAPI specification document.
- **Stack:** .NET 10, C#, Azure Functions .NET isolated worker v4 (Microsoft.Azure.Functions.Worker), Microsoft.OpenApi (document build + JSON/YAML serialization). Target OpenAPI Specification 3.x (per OAI/OpenAPI-Specification).
- **Created:** 2026-08-26T09:49:06+02:00

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-08-26T11:30:00+02:00): Attribute-driven OpenAPI generation landed across public attributes/options, reflection discovery, CLR schema components, path/operation generation, sample CRUD endpoints, README docs, and 65/65 passing tests — decided by Lead, Functions, Backend, and Tester.


📌 Team update (2026-08-26T12:41:00+02:00): Nullable `$ref` schema output verification merged into shared decisions; OpenAPI 3.1 form is canonical and OpenAPI 3.0 serialization remains valid under Microsoft.OpenApi 3.10.2 — decided by Backend, Tester, Lead, Fact Checker.
