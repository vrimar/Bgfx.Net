# Releasing Bgfx.Net

How to ship a release of `Bgfx.Net` and `Bgfx.Net.Tools` to NuGet.org.

## Versioning policy

Bgfx.Net uses **independent [Semantic Versioning](https://semver.org/)** — the
package version reflects the *wrapper's* API contract, not the bgfx revision it
embeds. Each release records the bgfx pin in the release notes (see below).

- **0.x.y** — pre-stable. Anything may break between minors. We're here until
  the API surface settles.
- **MAJOR** bumps signal a breaking change to managed API or a binary-incompatible
  bgfx upgrade.
- **MINOR** bumps add features or non-breaking bgfx upgrades.
- **PATCH** bumps are bug fixes only — no API changes, no bgfx revision change.

Pre-release suffixes use the `-alpha.N` / `-beta.N` / `-rc.N` convention
(e.g. `v0.2.0-rc.1`). The NuGet workflow accepts any tag matching `v*`.

### Recording the bgfx pin

Every release ships against exactly one bgfx commit. The release notes for each
tag must state:

- bgfx submodule SHA (from `git -C external/bgfx rev-parse HEAD`)
- bgfx `BGFX_API_VERSION` (from `external/bgfx/include/bgfx/defines.h`)

This is what consumers use to map "which bgfx is in this package".

## Release flow (tag-driven)

The [`package` workflow](.github/workflows/package.yml) fires on any tag matching
`v*`. It builds natives across all RIDs, regenerates bindings, packs both
packages, and pushes to NuGet.org with `--skip-duplicate`.

### Prerequisites

- `NUGET_API_KEY` repo secret set with push rights for `Bgfx.Net*` (Settings →
  Secrets and variables → Actions). First push reserves the IDs.
- `main` is green on CI.
- Generated bindings (`src/Bgfx.Net/Generated/bgfx.g.cs`, `bgfx.raw.cs`) are
  committed and in sync with the submodule. The workflow fails the build if
  they've drifted — re-run `build/sync-bindings.ps1` + `build/run-generator.ps1`
  and commit if so.

### Steps

1. Decide the version (see policy above) and confirm `main` is the commit you
   want to ship.
2. Create and push an annotated tag:
   ```pwsh
   git tag -a v0.1.0 -m "v0.1.0"
   git push origin v0.1.0
   ```
3. Watch the workflow run under Actions → **package**. It will:
   - Build native `bgfx` + tools for all five RIDs.
   - Verify generated bindings match the submodule.
   - Pack `Bgfx.Net` and `Bgfx.Net.Tools`.
   - Push to NuGet.org.
4. Once live, create a GitHub Release on the tag with notes covering:
   - What changed in the wrapper.
   - bgfx pin: SHA + `BGFX_API_VERSION`.
   - Any caveats (RID coverage, known issues).

### Dry run

To produce nupkgs without publishing (useful for verifying the build before the
real tag):

- GitHub UI → Actions → **package** → *Run workflow* → enter version, leave
  **publish** unchecked. The nupkgs land as a workflow artifact.

Locally (no native binaries — package will warn `BGFXNET001` and ship empty
`runtimes/` folders, so only useful for sanity-checking the managed assembly):

```pwsh
dotnet build Bgfx.Net.sln -c Release -p:Version=0.1.0 -p:SkipNativeWarning=true
dotnet pack src/Bgfx.Net/Bgfx.Net.csproj             -c Release -p:Version=0.1.0 -o packages
dotnet pack src/Bgfx.Net.Tools/Bgfx.Net.Tools.csproj -c Release -p:Version=0.1.0 -o packages
```

## Bumping the bgfx submodule

A bgfx update is a release-worthy event. Procedure:

1. `git -C external/bgfx fetch && git -C external/bgfx checkout <new-sha>`
2. Run `build/sync-bindings.ps1` and `build/run-generator.ps1`.
3. Build natives locally and run the test suite.
4. Commit submodule bump + regenerated bindings together.
5. Release a new MINOR (or MAJOR if the bgfx API broke binary compat).

## If a release fails mid-flight

- **Pack succeeded, push failed**: re-run the workflow. `--skip-duplicate` makes
  the push idempotent for nupkgs that already landed.
- **Pushed a bad package**: NuGet does not allow deletion, only *unlisting*.
  Unlist the bad version on nuget.org and ship a `+1` patch with the fix. Do not
  reuse the version number.
- **Pushed with wrong tag**: delete the tag locally and on origin
  (`git push origin :v0.1.0`) only *before* the workflow has published.
  After publish, the tag is part of the public record — fix forward.
