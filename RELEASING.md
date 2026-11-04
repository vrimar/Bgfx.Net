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
**automatically** by `build/sync-bindings.sh` from the submodule — no manual
`defines.h` lookup — and surfaced three ways:

- `AssemblyMetadata("BgfxRevision", "<sha>")` and `AssemblyMetadata("BgfxApiVersion", "<n>")`
  in [src/Bgfx.Net/AssemblyInfo.cs](src/Bgfx.Net/AssemblyInfo.cs).
- Public constants `Bgfx.Net.BgfxBuildInfo.Revision` / `.ApiVersion` (AOT-friendly,
  no reflection) in the generated `BgfxBuildInfo.g.cs`.
- The package's `<PackageReleaseNotes>`, auto-filled from `BGFX_API_VERSION` at pack
  time, so the gallery page always states the bundled API version.

When writing the version's `CHANGELOG.md` section, copy these already-captured values
through (SHA + `BGFX_API_VERSION`); you don't need to look them up by hand.

## Release flow (tag-driven)

The [`package` workflow](.github/workflows/package.yml) fires on any tag matching
`v*`. It checks the version, runs the [`ci-pr` workflow](.github/workflows/ci-pr.yml),
builds natives across all RIDs, regenerates bindings, packs both packages, pushes to
NuGet.org with `--skip-duplicate`, and creates the GitHub Release.

### Prerequisites

- A trusted publishing policy on nuget.org (account → Trusted Publishing) for owner
  `vrimar`, repository `Bgfx.Net`, workflow `package.yml` and environment `nuget`.
  `NuGet/login` trades the run's OIDC token for a short-lived API key, so there is no
  key to store or rotate.
- `CHANGELOG.md` has a `## [x.y.z] - YYYY-MM-DD` section for the version, on `main`.
- `main` is green on CI.
- Generated bindings (`src/Bgfx.Net/Generated/bgfx.g.cs`, `bgfx.raw.cs`) are
  committed and in sync with the submodule. The workflow fails the build if
  they've drifted — re-run `build/sync-bindings.sh` + `build/run-generator.sh`
  and commit if so.

### Steps

1. Decide the version (see policy above).
2. Add its section to `CHANGELOG.md`, which becomes the GitHub Release's notes,
   covering:
   - What changed in the wrapper.
   - bgfx pin: SHA + `BGFX_API_VERSION`.
   - Any caveats (RID coverage, known issues).

   Commit it to `main`, and confirm `main` is the commit you want to ship.
3. Create and push an annotated tag:
   ```pwsh
   git tag -a v0.1.0 -m "v0.1.0"
   git push origin v0.1.0
   ```
4. Watch the workflow run under Actions → **package**. It will:
   - Fail unless the version is SemVer, `CHANGELOG.md` has its section, and the tag
     is on `main`.
   - Run the `ci-pr` checks and tests.
   - Build native `bgfx` for all RIDs (plus shader tools for the desktop RIDs). A desktop RID reuses
     an earlier run's build, up to 90 days old, when its pins, build scripts and compilers match.
   - Verify generated bindings match the submodule.
   - Pack `Bgfx.Net` and `Bgfx.Net.Tools`.
   - Build `tests/Bgfx.Net.Tools.Smoke` against the packed `Bgfx.Net.Tools` on Windows, Linux and macOS, as a
     consumer would, so a tool path or command line that breaks on one OS stops the release.
   - Push to NuGet.org.
   - Create the GitHub Release, marked as a prerelease when the version has a `-`.

### Dry run

To produce nupkgs without publishing (useful for verifying the build before the
real tag):

- GitHub UI → Actions → **package** → *Run workflow* → enter version, leave
  **publish** unchecked. The nupkgs land as a workflow artifact. A dry run needs no
  changelog section; checking **publish** requires one and pushes without creating a
  GitHub Release.

Locally (no native binaries — package will warn `BGFXNET001` and ship empty
`runtimes/` folders, so only useful for sanity-checking the managed assembly):

```pwsh
dotnet build Bgfx.Net.sln -c Release -p:Version=0.1.0 -p:SkipNativeWarning=true
dotnet pack src/Bgfx.Net/Bgfx.Net.csproj             -c Release -p:Version=0.1.0 -o packages
dotnet pack src/Bgfx.Net.Tools/Bgfx.Net.Tools.csproj -c Release -p:Version=0.1.0 -o packages
```

## Bumping the bgfx submodule

`external/bgfx` tracks the `bgfx.net` branch of the `vrimar/bgfx` fork: upstream
master plus the fixes carried until they land upstream, rebased so that
`git -C external/bgfx log upstream/master..bgfx.net` is exactly the carried set.
A bgfx update is a release-worthy event. Procedure:

1. `build/sync-bgfx-fork.sh` — rebases `bgfx.net` onto upstream master and pushes it.
   Resolve rebase conflicts there; a fix merged upstream drops out on its own.
2. `git -C external/bgfx checkout bgfx.net` (now at the rebased head).
3. Run `build/sync-bindings.sh` and `build/run-generator.sh`.
4. Build natives locally and run the test suite.
5. Commit submodule bump + regenerated bindings together.
6. Release a new MINOR (or MAJOR if the bgfx API broke binary compat).

To carry a new fix: commit it on `bgfx.net`, push, bump the pointer, and open the
upstream PR from a branch on the fork holding the same commit.

## If a release fails mid-flight

- **Pack succeeded, push failed**: re-run the workflow. `--skip-duplicate` makes
  the push idempotent for nupkgs that already landed.
- **Push succeeded, GitHub Release failed**: re-run failed jobs, which reruns the
  release job alone.
- **Pushed a bad package**: NuGet does not allow deletion, only *unlisting*.
  Unlist the bad version on nuget.org and ship a `+1` patch with the fix. Do not
  reuse the version number.
- **Pushed with wrong tag**: delete the tag locally and on origin
  (`git push origin :v0.1.0`) only *before* the workflow has published.
  After publish, the tag is part of the public record — fix forward.
