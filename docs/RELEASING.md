# Releasing AzureExtensions.Functions.OpenApi

This document is for maintainers cutting a release. It is not required reading for consumers of
the NuGet package — see the main [README](../README.md) for usage documentation.

## Packaging & releasing

The library is published as a NuGet package. Versioning is **tag-driven** via
[MinVer](https://github.com/adamralph/minver): the package version is derived from the latest
`v*` git tag, and commits after a tag produce `-preview.N` prereleases automatically. There is no
version number to bump by hand.

| Trigger | Workflow | Result |
|---------|----------|--------|
| Pull request / push to `dev` or `main` | `ci.yml` | Build, test, and pack; the `.nupkg`/`.snupkg` are uploaded as build artifacts (no publish) |
| Push to `dev` | `publish.yml` | Prerelease published to **GitHub Packages** (uses the built-in `GITHUB_TOKEN`) |
| Push a `v*` tag (e.g. `v1.2.0`) | `publish.yml` | Stable release published to **NuGet.org** via Trusted Publishing |

## NuGet.org Trusted Publishing (keyless)

Releases publish to NuGet.org with [**Trusted Publishing**](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
— there is **no long-lived API key** to store or rotate. At publish time the workflow requests a
short-lived GitHub **OIDC** token, exchanges it with nuget.org (via
[`NuGet/login`](https://github.com/NuGet/login)) for a temporary key valid ~1 hour, and pushes.

One-time setup to enable publishing:

1. **Register a Trusted Publishing policy on nuget.org** — sign in → your username →
   *Trusted Publishing* → add a policy with:
   - **Repository Owner:** `Doomblaster`
   - **Repository:** `AzureExtensions.Functions.OpenApi`
   - **Workflow File:** `publish.yml` (file name only)
   - **Environment:** `release`
2. **Create the `release` environment** in the GitHub repo (*Settings → Environments*). Optionally
   add **required reviewers** so each release pauses for manual approval before publishing.
3. **Set the `NUGET_USER` repository variable** (*Settings → Secrets and variables → Actions →
   Variables*) to your nuget.org profile name. The release job stays a no-op until this is set.

The GitHub Packages prerelease channel needs no extra configuration.

To cut a release:

```bash
git switch main && git merge --ff-only dev   # promote dev → main when ready
git tag v1.2.0
git push origin main --tags
```

The `v1.2.0` tag makes MinVer stamp the package `1.2.0`, and `publish.yml` publishes it to NuGet.org
(after the `release` environment approval, if configured).

Packages are built deterministically with embedded [SourceLink](https://github.com/dotnet/sourcelink)
metadata and a separate `.snupkg` symbol package for source-linked debugging.
