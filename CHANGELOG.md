# Changelog

All notable changes to this project are documented here; each version's section is its GitHub Release's notes.
Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html) as [RELEASING.md](RELEASING.md)
describes.

## [0.9.2] - 2026-11-04

Fix release for the shader tools; `Bgfx.Net` is unchanged apart from its version.

Shader tools (Bgfx.Net.Tools):
- A project that only references the package and adds `BgfxShader` items now compiles on Windows, Linux and macOS. Two bugs stopped it on every OS:
  - On .NET SDK 10 the package's props ran before the SDK defined its RID, so the tool paths had no RID in them and shaderc was "not found".
  - On Linux and macOS each tool sits in a folder of its own name, `native/shaderc/shaderc`, but the props pointed at the folder.
- `BgfxShaderC`, `BgfxTextureC` and `BgfxGeometryC` now get their defaults in the package's targets, not its props. Setting them in your project still overrides them; reading them in your project's own properties now finds them empty.
- Every release first builds a consumer project against the packed tools on Windows, Linux and macOS.

bgfx pin: unchanged from 0.9.0, 028ed92929fd123fec638f9325ba14dfaf163ad2, BGFX_API_VERSION 167.

## [0.9.1] - 2026-11-04

Fix release for the shader tools; `Bgfx.Net` is unchanged apart from its version.

Shader tools (Bgfx.Net.Tools):
- `BgfxCompileShaders` compiles on a Windows host. The default `BgfxShaderIncludePath` ends in a backslash, which escaped the closing quote of shaderc's `-i`, so every shader failed with "Could not open include file 'bgfx_shader.sh'". A path you set yourself may end in a backslash too.

bgfx pin: unchanged from 0.9.0, 028ed92929fd123fec638f9325ba14dfaf163ad2, BGFX_API_VERSION 167.

## [0.9.0] - 2026-10-30

Breaking release: bgfx moves to API 167, which reports MSAA support per sample count. On Linux, shaderc's Direct3D shaders now declare only the uniforms they read, and shaderc rejects instance data on the wrong semantic.

Breaking managed API changes:
- `CapsFormatFlags` gains `TextureFramebufferMsaaX2`, `X4`, `X8` and `X16`, one per sample count a format can render at. `TextureFramebufferMsaa` is now the mask of all four, so `(caps & TextureFramebufferMsaa) == TextureFramebufferMsaa` asks for every count: test the count you render at instead.
- `TextureMsaa`, `TextureMipAutogen`, `TextureBackbuffer` and `TextureVideoDecodeDst` each move down one bit; anything that stored them as numbers must be rebuilt.

Shader tools (Bgfx.Net.Tools):
- shaderc fails a `varying.def.sc` that puts `i_data<N>` on any semantic but `TEXCOORD<31-N>`, on every backend. Direct3D binds instance data by that semantic alone, so a mismatch drew from the mesh's own vertex stream on Windows and correctly everywhere else.
- On Linux, shaderc's Direct3D shaders declare only the uniforms they read, as a Windows-hosted build's do. d3d4linux reported every uniform as used, so each shader carried bgfx's whole predefined block, about 700 bytes of constant buffer per stage on every draw.
- shaderc's default Metal profile is MSL 3.0, and profiles older than MSL 2.0 are gone.

bgfx changes:
- MSAA requests are clamped to what each format supports.
- Lower submit and upload overhead on D3D11, D3D12, Vulkan and WebGPU.
- Transient buffers no longer misreport space after wrapping, and D3D12 releases resources sooner.
- Vulkan: descriptor pool exhaustion and occlusion query stalls are fixed, and GPU memory and viewport layer arrays are reported on devices without the EXT extensions.
- GL and GLES: window swap chains pace to vsync, with MSAA and sRGB back buffers fixed.
- WebGPU: the main swap chain follows a new window on reset, and an expired canvas texture is no longer rendered into on Emscripten.
- `gl_NumWorkGroups` is predefined in non-GLSL compute shaders.

bgfx fixes carried on the fork:
- New: the two shaderc fixes above.
- Kept: VK/WebGPU compute-only views in the view stats, and the WGPU external device through `PlatformData::context`.
- The other three WGPU/WebGL fixes carried in v0.8.0 have landed upstream.

bgfx pin: 028ed92929fd123fec638f9325ba14dfaf163ad2 (bgfx.net branch: upstream 376ed1b10 plus the four fixes above), BGFX_API_VERSION 167. bx and bimg move to upstream 0e5bbca and 9bfbd64.

## [0.8.0] - 2026-10-20

Breaking release: bgfx moves to API 166, the Vulkan swap chain now uses a format and composite alpha the surface supports, which fixes black frames on Android, and the Android libraries are 16 KB page-aligned.

Breaking managed API changes:
- `CapsFlags` from `PrimitiveId` up each move one bit to make room for `LowLatency`; anything that stored them as numbers must be rebuilt.
- `Stats` gains seven `Latency*` fields after `WaitSubmit`, so every field after them moves.

New:
- `ResetFlags.LowLatencyOn`/`LowLatencyBoost` and `CapsFlags.LowLatency`: NVIDIA Reflex on Windows, with its timings in `Stats.LatencySleep`, `LatencyTotal`, `LatencySimulation`, `LatencyRenderSubmit`, `LatencyPresent`, `LatencyQueue` and `LatencyGpu`.
- Vulkan falls back to a swap-chain format and composite alpha the surface lists. Android surfaces offer neither bgfx's default BGRA8 nor opaque composite alpha, so every frame there rendered black; pass `TextureFormat.Count` as `SwapChain.FormatColor` on reset to keep the format bgfx chose.
- The main window follows a new native window handle on every renderer, and Android recreates its main EGL surface.
- Vulkan HDR10, and HDR10 colour space fixes on D3D11 and D3D12.
- Fixes for draws and dispatches whose dynamic buffer grows later in the same frame, sampling MSAA depth, 3D texture conversion, D3D11 multi-window vsync, Vulkan GPU selection, Metal occlusion queries, and resource creation after device removal.

Packaging:
- The android-arm64 and android-x64 libraries are linked with 16 KB page alignment, which Android 15 devices with 16 KB pages need, whatever NDK builds them; the build fails if a segment comes out below 16 KB.

bgfx fixes carried on the fork:
- The five fixes from v0.7.0: VK/WebGPU compute-only views in the view stats, and the four WGPU/WebGL fixes. None has landed upstream.

bgfx pin: 1f5b2c84a2c755d8103da97dbbeca1b6de703238 (bgfx.net branch: upstream 10a105be8 plus the five fixes above), BGFX_API_VERSION 166. bx and bimg move to upstream dd261e2 and 34cb73b.

## [0.7.1] - 2026-10-17

- Bgfx.Net.Tools ships Microsoft's d3dcompiler beside the linux-x64 shaderc, so DXBC shaders build under Wine.

## [0.7.0] - 2026-10-16

Breaking release: bgfx moves to API 165, and the fork carries a fix that times compute-only views on Vulkan and WebGPU.

Breaking managed API changes:
- `CapsFlags` from `SwapChain` up each move one bit to make room for `ShaderF16`; anything that stored them as numbers must be rebuilt.
- `SetTextureView` and its encoder form take `lodMin` and `lodMax`, in quarter-mip steps relative to `firstMip`; pass `0, byte.MaxValue` for the previous behaviour.
- `Init.Limits` gains `MinViews` and `MinUniformCacheSize`, and `Caps.Limits` gains `MinUniformCacheSize`, so both structs change layout.

New:
- `CapsFlags.ShaderF16`: 16-bit floats are supported in shaders.
- `TextureFlags.MipCountShift`/`MipCountMask`: an explicit mip count, which overrides `hasMips` when set.
- bgfx 165 sizes its view storage, handle tables, sort keys and uniform cache by use, up to 4096 view ids; `Init.Limits.MinViews` and `MinUniformCacheSize` reserve ahead.

bgfx fixes carried on the fork:
- VK/WebGPU: a view that only runs compute shows in the view stats, so its GPU time is measured.
- The four WGPU/WebGL fixes from v0.6.0. The WGPU 32-bit build fix landed upstream and is no longer carried.

bgfx pin: 8da274db00b8b959c65f974a690b0f6247241674 (bgfx.net branch: upstream 73183bea9 plus the five fixes above), BGFX_API_VERSION 165.

Third-party notices now list nanosvg (Zlib), which the bimg update brings in.

## [0.6.2] - 2026-10-10

- The linux-x64 natives build on Ubuntu 22.04, so they load on glibc 2.35.

## [0.6.1] - 2026-10-01

- The Windows binaries no longer carry build-machine paths.

## [0.6.0] - 2026-09-23

Breaking release: bgfx moves to API 161, and the browser build now hands bgfx the page's WebGPU device.

Breaking managed API changes:
- `SetViewRect` takes `minDepth`/`maxDepth`; pass `0.0f, 1.0f` for the previous behaviour.
- `ResetFlags.DepthClamp` is gone; depth clamp is per draw via `SetDepthControl(..., depthClamp)`.
- `ResolveFlags` is now `AttachmentFlags`, and `Attachment.Resolve` is now `Attachment.Flags`.
- `SetCompute{Index,Vertex,DynamicIndex,DynamicVertex}Buffer` and their encoder forms take `offset` and `size`.
- `DbgTextPrintf(x, y, attr, format, args)` is now `DbgTextPrint(x, y, attr, text)`: the text is printed as is, so format it in C#. The old binding crashed on browser-wasm and read garbage on Apple arm64 whenever the format used an argument.
- `DbgTextVprintf` is removed; a `va_list` cannot be built portably from C#.

New:
- `Bgfx.ImportPageWebGpuDevice()` (browser-wasm): returns the `GPUDevice` the page stored in `Module.preinitializedWebGPUDevice` as a `WGPUDevice` for `Init.PlatformData.Context`. The page resolves the device before the .NET runtime starts, and chooses its features and limits. See the README.
- bgfx API 161 additions: `SetViewDepthBias`, `SetViewSampleMask`, `SetSampleMask`, `SetDepthControl`, `SetUniformRef`, sRGB-mutable textures and read-only depth/stencil attachments.

bgfx fixes carried on the fork (each verified against upstream on Linux desktop and in Chrome/Firefox):
- WGPU: 32-bit build fix (wasm32 did not compile with WebGPU on).
- WebGL: instance data attributes are no longer left enabled across frames (a non-instanced draw after an instanced frame was dropped).
- WGPU: a shader input missing from the vertex layout no longer invalidates the pipeline when the stride is under 12 bytes.
- WGPU: the canvas texture is acquired on first use in the browser, so frames that draw nothing keep the last image instead of erroring or going black.
- WGPU: an app-supplied device through `PlatformData.Context` (all platforms).

bgfx pin: aca8642f72520b17b3f25cbf631b8a3dc726fcb8 (bgfx.net branch: upstream 81d81fba7 plus the five fixes above), BGFX_API_VERSION 161.

Known issues, present upstream too:
- Desktop WebGPU: recreating a swap chain on a window whose swap chain was just destroyed can fail with `VK_ERROR_NATIVE_WINDOW_IN_USE_KHR`, which can happen when bgfx is shut down and started again on the same window with an app-supplied device.
- Browser: `RequestScreenShot` does nothing on WebGPU.

Versions before 0.6.0 are in the git history.
