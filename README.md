# Bgfx.Net

Cross-platform .NET bindings for [bgfx](https://github.com/bkaradzic/bgfx), a graphics
rendering library that runs on Windows, Linux, macOS, and Android with Direct3D, Vulkan,
OpenGL, OpenGL ES, and Metal backends.

## Packages

| Package | Purpose |
|---|---|
| `Bgfx.Net` | Managed bindings + native `bgfx` shared library for all supported RIDs |
| `Bgfx.Net.Tools` | MSBuild integration that runs `shaderc` / `texturec` / `geometryc` at build time. Marked `developmentDependency`, so it doesn't propagate into consumer output. |

Supported RIDs: `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`, `android-arm64`,
`android-x64`. (`linux-arm64` is planned — blocked on upstream bx adding a native
`linux-arm64-gcc` action.)

On Android the native `libbgfx.so` is shipped under `runtimes/android-arm64/native`
and `runtimes/android-x64/native`; a `net*-android` app that references the package
gets it bundled into the APK automatically. Two consumer-side notes:

- The app's effective Android `<RuntimeIdentifiers>` must include the ABIs you want
  bundled — Release builds often resolve only `android-arm64`, so add `android-x64`
  if you also need the x86_64 emulator.
- Supply bgfx the native window: set `Init.SwapChain.Nwh` to the `ANativeWindow*`
  obtained from the Java `Surface` via JNI/NDK before `Init`.

## Quick start

The bindings mirror the bgfx C API, so entry points take pointers and the calling
code is `unsafe`.

```csharp
using Bgfx.Net;

var init = default(Init);
Bgfx.InitCtor(&init);
init.Type = RendererType.Vulkan;
init.SwapChain.Nwh = nativeWindowHandle;
init.SwapChain.Width = 1280;
init.SwapChain.Height = 720;
init.Reset = (uint)ResetFlags.Vsync;

if (!Bgfx.Init(&init))
{
    throw new InvalidOperationException("bgfx_init failed");
}

Bgfx.SetViewClear(0, (ushort)(ClearFlags.Color | ClearFlags.Depth), 0x303080ff, 1.0f, 0);
Bgfx.SetViewRect(0, 0, 0, 1280, 720);

while (running)
{
    Bgfx.Touch(0);
    Bgfx.Frame(0);
}

Bgfx.Shutdown();
```

See [samples/Bgfx.Net.Sdl2Sample](samples/Bgfx.Net.Sdl2Sample) for a runnable version
that obtains `nativeWindowHandle` from SDL2.

## Cloning

This repository uses git submodules for `bgfx`, `bx`, and `bimg`. `bgfx` points at
the `bgfx.net` branch of the `vrimar/bgfx` fork, which is upstream master plus the
fixes carried until they land upstream (see [RELEASING.md](RELEASING.md)):

```sh
git clone --recurse-submodules https://github.com/$USER/Bgfx.Net.git
# or, after a normal clone:
git submodule update --init --recursive
# or use the helper:
./build/bootstrap.sh
```

## Building locally

Prerequisites: .NET SDK 10, plus a C++ toolchain for your platform
(MSVC on Windows, gcc/clang on Linux, Xcode CLT on macOS). On Windows the native
build still uses PowerShell (`build-native-win.ps1`). To cross-build the Android
native lib, install an NDK and run `build-native-android.sh android-arm64`
(or `android-x64`) with `ANDROID_NDK_ROOT` set.

```sh
./build/bootstrap.sh             # init submodules, fetch genie
./build/build-native-unix.sh     # or build-native-win.ps1 on Windows
./build/sync-bindings.sh         # copies bgfx.cs into src/Bgfx.Net/Generated/
./build/run-generator.sh         # produces bgfx.g.cs
dotnet build Bgfx.Net.sln
```

## Caveats

- **Shaders on Linux/macOS**: `shaderc` can produce GLSL/ESSL/Metal/SPIR-V on all
  platforms, but Direct3D shader compilation (DXBC via `fxc`, DXIL via `dxc`) needs
  additional Windows-side tooling.
- **Threading**: bgfx uses an API thread + render thread model selected at `Init`.
  See bgfx's docs for the threading contract.
- **Pinned to a specific bgfx revision**: The `Bgfx.Net` assembly is built against
  exactly one bgfx commit. Read the pin at runtime via `Bgfx.Net.BgfxBuildInfo.ApiVersion`
  and `.Revision` (also embedded as `BgfxApiVersion` / `BgfxRevision` assembly
  metadata). The package version itself is independent wrapper SemVer, not the bgfx
  revision — see [RELEASING.md](RELEASING.md). Mixing the assembly with a different
  `bgfx.dll` at runtime is unsupported.

## License

BSD-2-Clause. See [LICENSE](LICENSE) for our code and
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for upstream and 3rdparty
attributions.
