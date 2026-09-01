# Contributing to AzureExtensions.Functions.OpenApi

Thanks for your interest in contributing! This project adds an OpenAPI 3.x document
endpoint to Azure Functions isolated worker (v4) apps. Contributions of all kinds —
bug reports, feature requests, docs, and code — are welcome.

By participating, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (see [`global.json`](global.json) for the pinned version)
- Git

## Getting started

```bash
git clone https://github.com/Doomblaster/AzureExtensions.Functions.OpenApi.git
cd AzureExtensions.Functions.OpenApi

dotnet restore AzureExtensions.Functions.OpenApi.slnx
dotnet build   AzureExtensions.Functions.OpenApi.slnx -c Release
dotnet test  --solution AzureExtensions.Functions.OpenApi.slnx -c Release
```

> The test step uses the Microsoft.Testing.Platform runner (configured in `global.json`),
> which is why it is invoked as `dotnet test --solution <solution>`.

## Project layout

- `src/AzureExtensions.Functions.OpenApi/` — the library (the published package)
- `tests/AzureExtensions.Functions.OpenApi.Tests/` — unit + end-to-end tests
- `samples/SampleFunctionApp/` — a sample isolated-worker Functions app used by the E2E tests
- `docs/` — maintainer documentation (e.g. releasing)

## Branching model

This repo uses a **dev-first** flow:

- `dev` is the active integration branch. **Open pull requests against `dev`.**
- `main` holds tagged, stable releases. It is promoted from `dev`, not committed to directly.
- Use short-lived feature branches, e.g. `feature/short-description` or `fix/short-description`.

## Making a change

1. Create a branch off `dev`.
2. Make your change with tests. New behavior should come with coverage; API changes must
   update the affected tests.
3. Run the full build + test locally (commands above) and make sure it's green.
4. Update the README/docs if you changed public behavior.
5. Open a pull request against `dev` and fill out the PR template.

## Pull request expectations

- Keep PRs focused — one logical change per PR.
- CI (`Build, test & pack`) must pass.
- Describe **what** changed and **why**. Reference any related issue (`Closes #123`).
- Be responsive to review feedback; maintainers may request changes before merge.

## Coding conventions

- Target framework and language settings follow the existing projects
  (`net10.0`, nullable enabled, implicit usings).
- Match the surrounding code style; keep changes surgical and avoid unrelated churn.
- Prefer attribute-driven metadata for OpenAPI surface, consistent with the current design.

## Reporting bugs and requesting features

Use the [issue templates](.github/ISSUE_TEMPLATE/) — they prompt for the details we need to
help quickly. For security issues, **do not** open a public issue; see [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
