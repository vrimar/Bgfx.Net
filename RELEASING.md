# Releasing Bgfx.Net

How to ship a release of `Bgfx.Net` and `Bgfx.Net.Tools` to NuGet.org.

## Versioning policy

Bgfx.Net uses **independent [Semantic Versioning](https://semver.org/)** — the
package version tracks the *wrapper's* managed API, not the bgfx revision it embeds.
bgfx is a versionless, rolling-release library (no upstream tags), so embedding its
revision in our version number is meaningless; instead the bgfx pin travels as
**metadata** (see below). This is the same approach SkiaSharp (rolling Skia) and
`sharp` (libvips) take.

The wrapper owns all three version components:

| Change | Bump |
|---|---|
| Breaking managed API change (rename/remove/signature) | **MAJOR** |
| Binary-incompatible bgfx upgrade (ABI break), even if the managed API is unchanged | **MAJOR** |
| New binding feature — extension method, Span overload, helper | **MINOR** |
| Non-breaking bgfx upgrade bundled with no managed API change | **MINOR** |
| Bug fix, no API change | **PATCH** |

- **0.x.y** — pre-stable. While we're below 1.0 the managed API hasn't settled, so
  breaking changes ride a MINOR (standard SemVer pre-1.0 convention). Promote to
  `1.0.0` as a deliberate, one-time event once the surface stabilizes.
- The ABI-break rule is the *only* place bgfx vetoes our number: a bgfx upgrade that
  breaks binary compatibility forces a MAJOR even if our C# didn't change, because
  consumers' compiled code is affected.

Pre-release suffixes use the `-alpha.N` / `-beta.N` / `-rc.N` convention
(e.g. `v0.2.0-rc.1`). The NuGet workflow accepts any tag matching `v*` and fails the
build if the resolved version isn't valid SemVer.

### Recording the bgfx pin

Every release ships against exactly one bgfx commit. The pin is captured
**automatically** by `build/sync-bindings.ps1` from the submodule — no manual
`defines.h` lookup — and surfaced three ways:

- `AssemblyMetadata("BgfxRevision", "<sha>")` and `AssemblyMetadata("BgfxApiVersion", "<n>")`
  in [src/Bgfx.Net/AssemblyInfo.cs](src/Bgfx.Net/AssemblyInfo.cs).
- Public constants `Bgfx.Net.BgfxBuildInfo.Revision` / `.ApiVersion` (AOT-friendly,
  no reflection) in the generated `BgfxBuildInfo.g.cs`.
- The package's `<PackageReleaseNotes>`, auto-filled from `BGFX_API_VERSION` at pack
  time, so the gallery page always states the bundled API version.

When writing the GitHub Release notes, copy these already-captured values through
(SHA + `BGFX_API_VERSION`); you don't need to look them up by hand.

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
