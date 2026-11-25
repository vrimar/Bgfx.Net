# Changelog

All notable changes to this project are documented here; each version's section is its GitHub Release's notes.
Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html) as [RELEASING.md](RELEASING.md)
describes.

## [0.12.0] - 2026-11-25

Breaking release: bgfx moves to API 172, where a view's stats are split by the kind of work submitted to it.

Breaking managed API changes:
- `Stats.NumViews` is renamed `NumViewStats`. A view has one `ViewStats` entry for each kind of work submitted to it, told apart by the new `ViewStats.Type` (`ViewStatsType.Blit`, `Compute` or `Draw`), so the same `ViewStats.View` can appear up to three times.

New:
- `PlatformData.Queue` takes a Metal command queue as well as a Direct3D 12 one, so bgfx submits on the app's queue. `InternalData.Queue` returns the queue bgfx uses on Direct3D 12 and Metal.
- Metal: `FrameFlags.DebugCapture` writes a `.gputrace` file when Xcode isn't attached and `MTL_CAPTURE_ENABLED=1` is set.
- Compute shaders can use two-channel 16- and 32-bit float, integer and unsigned images, and Metal reports `RG11B10F` as writable from compute.

Shader tools (Bgfx.Net.Tools):
- shaderc records which `i_dataN` each instance input is in SPIR-V and WGSL shaders, so Vulkan and WebGPU bind it by index rather than by position. Shaders built by earlier shaderc still load.
- texturec reads and writes PFM images, writes EXR as `RGBA32F` as well as `RGBA16F`, and decodes an EXR without an alpha channel as opaque.
- geometryc builds with meshoptimizer 1.3.

bgfx changes:
- Instanced draws on Vulkan, OpenGL, Metal and WebGPU feed each `i_dataN` to the input the shader declares for it, rather than assuming instance inputs follow the vertex attributes.
- OpenGL maps depth to 0..1 with `ARB_clip_control` where the driver has it, instead of patching every vertex shader.
- Render passes and MSAA resolves cover only the rect a view renders into, leaving the rest of the frame buffer as it was.
- Uploading to and reading back a 3D texture's slices work on every renderer.
- Metal: updating an index buffer no longer writes past its end, buffers still alive at shutdown are freed, a buffer at the last stage is bound, and a compute view that follows a blit gets a compute encoder.
- Direct3D 11: a blit between a buffer and a texture no longer conflicts with the bound frame buffer, a clear covers every layer of a layered attachment, and `Init.Capabilities` without `CapsFlags.TextureDirectAccess` turns direct texture access off.

bgfx fixes carried on the fork:
- Dropped: VK/WebGPU compute-only views in the view stats, which upstream's per-type view stats cover.
- Kept: the two shaderc fixes from v0.9.0, and the Windows ARM64 shaderc build from v0.11.0.

bgfx pin: 225dfd041c3174b87a5674f91c93c8549448c7be (bgfx.net branch: upstream 3fe177f61 plus the three fixes above), BGFX_API_VERSION 172. bx and bimg move to upstream b8ee10a and 38ceda5.

## [0.11.0] - 2026-11-20

Breaking release: `Bgfx.Net` targets .NET 11 only, and the package adds iOS, Linux arm64 and Windows arm64.

Breaking:
- `Bgfx.Net` targets `net11.0` instead of `net10.0`. A project on .NET 10 stays on 0.10.x.

New:
- iOS: `ios-arm64` and `iossimulator-arm64`, with the Metal renderer, from iOS 16. bgfx ships as a static library that the app links directly, which only NativeAOT can do: set `PublishAot` and publish the app. Any other build warns with `BGFXNET002`, and its first bgfx call throws `DllNotFoundException`. Set `Init.SwapChain.Nwh` to a `CAMetalLayer`.
- `linux-arm64` and `win-arm64` natives, and `Bgfx.Net.Tools` ships shaderc, texturec and geometryc for both hosts. A linux-arm64 host cannot compile DXBC shaders.
- `DiagnosticFlags.DrawValidation`: a debug build of bgfx with `Init.Debug` set asserts when a fragment shader output's type doesn't match the frame buffer attachment it writes.

Shader tools (Bgfx.Net.Tools):
- `varying.def.sc` can give `gl_FragData[N]` a type other than `vec4`, such as `uvec4 gl_FragData[1];` for an integer attachment.
- shaderc's preprocessor expands named variadic macro parameters correctly.
- texturec writes EXR files with a correct offset table and orientation, and decodes images with stb_image 2.32.

bgfx changes:
- Integer color attachments clear correctly, and Direct3D 11 and 12 no longer blend them.
- Metal times compute views.
- Resources still alive at shutdown are freed.

bgfx fixes carried on the fork:
- New: shaderc builds for Windows ARM64.
- Kept: VK/WebGPU compute-only views in the view stats, and the two shaderc fixes from v0.9.0.

Packaging:
- Every release first builds an iOS app against the packed `Bgfx.Net` with NativeAOT, links it for a device, and renders with Noop and Metal on the iOS simulator. The shader tools are also checked on Windows and Linux arm64.

bgfx pin: 7068c26229e7be611ac0572c0ed41074daaab4eb (bgfx.net branch: upstream f3bd091b4 plus the four fixes above), BGFX_API_VERSION 170. bx and bimg move to upstream fcaf6fd and b58f8e5.

## [0.10.0] - 2026-11-16

Breaking release: bgfx moves to API 169, where transient buffers grow on demand and Direct3D 12 is the default renderer on Windows. Shaders compiled by this release's shaderc load only in this release's `Bgfx.Net` or later.

Breaking managed API changes:
- `Init.Limits` and `Caps.Limits` rename `MaxTransientVbSize` and `MaxTransientIbSize` to `MinTransientVbSize` and `MinTransientIbSize`. Transient buffers start at that size and grow on demand.

Behaviour changes:
- On Windows, an `Init` that leaves `Type` at `RendererType.Count` gets Direct3D 12 instead of Direct3D 11, and Windows 10 is the minimum. Set `RendererType.Direct3D11` to keep the old renderer.
- Blits between a buffer and a multisampled texture are rejected, and a depth texture or one created with `TextureFlags.MsaaSample` can only be blitted a whole mip at a time.

New:
- `Init.Diagnostics` takes `DiagnosticFlags`: `Verbose`, `GpuValidation` and `SyncValidation` turn on more of the debug layer and need `Init.Debug`; `Serialize` waits for the GPU after each command buffer.
- `Init.BackendType` picks the native API a layered renderer (WebGPU) runs on.
- `PciIdFlags.Google`.
- `ResetFlags.TransparentBackbuffer` works on Direct3D 11 and 12 (through DirectComposition), Metal and WebGPU.

Shader tools (Bgfx.Net.Tools):
- shaderc writes shader binary version 14, which stores uniform array sizes in 16 bits, so arrays of more than 255 elements compile. This release's `Bgfx.Net` still loads shaders built by earlier shaderc; earlier `Bgfx.Net` cannot load shaders built by this one.
- shaderc's preprocessor is faster.
- texturec decodes PNGs under 8 bits per channel, grayscale images decoded to a color format, HDR images built with GCC 15, and compressed images that aren't a multiple of the block size correctly, and no longer overflows on very large images.

bgfx changes:
- Direct3D 12, Vulkan and WebGPU: mip generation, blit and swap chain fixes, and cube and MSAA cube render targets resolve correctly.
- Integer color attachments blend and clear correctly, including partial clears on Metal and WebGPU.
- Vulkan: vsync turns off on drivers without the IMMEDIATE present mode, aliased lines use Bresenham rasterization, and zero-area swap chains are deferred.
- Direct3D 12: wireframe and a scratch descriptor heap overflow are fixed.
- Metal: anisotropy without a main swap chain, depth texture sampling and user uniforms are fixed, and float32 and RGB9E5 formats generate mips.
- GL and GLES: texture to buffer blits, image layer binding, the ASTC probe and EGL swap chain contexts are fixed.
- WebGPU: bind group, callback and browser fixes.
- Texture copies and sampling on Mali are fixed.

bgfx fixes carried on the fork:
- Kept: VK/WebGPU compute-only views in the view stats, and the two shaderc fixes from v0.9.0.
- The WGPU external device through `PlatformData::context` has landed upstream and is no longer carried.

Packaging:
- The Android libraries are still built for API 24 (Android 7.0), though bx now defaults to API 34.

bgfx pin: c1340445ea150112f52393ef1cd586e735fa60fb (bgfx.net branch: upstream 3aab3201a plus the three fixes above), BGFX_API_VERSION 169. bx and bimg move to upstream 93ab098 and f091e14.

## [0.9.3] - 2026-11-04

Fix release for the shader tools; `Bgfx.Net` is unchanged apart from its version.

Shader tools (Bgfx.Net.Tools):
- shaderc, texturec and geometryc move from `runtimes/<rid>/native/` to `tools/<rid>/` in the package, so they no longer get copied into the build and publish output of the app that references it. A project that hard-codes a path into the package must use `$(BgfxShaderC)`, `$(BgfxTextureC)` and `$(BgfxGeometryC)` instead.

Packaging:
- When the bgfx pin, native build scripts and compilers are unchanged, a release reuses the earlier build's win-x64, linux-x64 and osx binaries (bgfx and the shader tools) instead of rebuilding them, so they stay byte-identical across those releases. Android and browser-wasm still rebuild every release.

bgfx pin: unchanged from 0.9.0, 028ed92929fd123fec638f9325ba14dfaf163ad2, BGFX_API_VERSION 167.

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
