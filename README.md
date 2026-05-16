# Bgfx.Net

Cross-platform .NET bindings for [bgfx](https://github.com/bkaradzic/bgfx), a graphics
rendering library that runs on Windows, Linux, and macOS with Direct3D, Vulkan, OpenGL,
and Metal backends.

## Packages

| Package | Purpose |
|---|---|
| `Bgfx.Net` | Managed bindings + native `bgfx` shared library for all supported RIDs |
| `Bgfx.Net.Tools` | MSBuild integration that runs `shaderc` / `texturec` / `geometryc` at build time. Marked `developmentDependency`, so it doesn't propagate into consumer output. |

Supported RIDs in v1: `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`. (`linux-arm64`
is planned — blocked on upstream bx adding a native `linux-arm64-gcc` action.)

## Quick start

```csharp
using Bgfx.Net;

var init = new InitDescription { Type = RendererType.Vulkan };
Bgfx.Init(in init);

while (running)
{
    Bgfx.Frame();
}

Bgfx.Shutdown();
```

## Cloning

This repository uses git submodules for `bgfx`, `bx`, and `bimg`:

```pwsh
git clone --recurse-submodules https://github.com/$USER/Bgfx.Net.git
# or, after a normal clone:
git submodule update --init --recursive
# or use the helper:
./build/bootstrap.ps1
```

## Building locally

Prerequisites: .NET SDK 10, PowerShell 7, plus a C++ toolchain for your platform
(MSVC on Windows, gcc/clang on Linux, Xcode CLT on macOS).

```pwsh
./build/bootstrap.ps1            # init submodules, fetch genie
./build/build-native-win.ps1     # or build-native-unix.sh on Linux/macOS
./build/sync-bindings.ps1        # copies bgfx.cs into src/Bgfx.Net/Generated/
./build/run-generator.ps1        # produces bgfx.g.cs
dotnet build Bgfx.Net.sln
```

## Caveats

- **Shaders on Linux/macOS**: `shaderc` can produce GLSL/ESSL/Metal/SPIR-V on all
  platforms, but Direct3D shader compilation (DXBC via `fxc`, DXIL via `dxc`) needs
  additional Windows-side tooling.
- **Threading**: bgfx uses an API thread + render thread model selected at `Init`.
  See bgfx's docs for the threading contract.
- **Pinned to a specific bgfx revision**: The `Bgfx.Net` assembly is built against
  exactly one bgfx commit (recorded as assembly metadata). Mixing it with a
  different `bgfx.dll` at runtime is unsupported.

## License

BSD-2-Clause. See [LICENSE](LICENSE) for our code and
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for upstream and 3rdparty
attributions.
