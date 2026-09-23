using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Bgfx.Net;

public static partial class Bgfx
{
    /// <summary>
    /// Imports the <c>GPUDevice</c> the page stored in <c>Module.preinitializedWebGPUDevice</c> as a
    /// <c>WGPUDevice</c> for <see cref="PlatformData.Context"/>, or returns null when the page set none.
    /// </summary>
    /// <remarks>
    /// Without JSPI or Asyncify, bgfx cannot wait for its own adapter and device requests in the browser,
    /// so the page resolves the device before the runtime starts and bgfx renders on it. The page chooses
    /// the device's features and limits, and handles its loss; bgfx installs no callbacks on it.
    /// </remarks>
    [SupportedOSPlatform("browser")]
    [LibraryImport("bgfx", EntryPoint = "bgfx_net_webgpu_page_device")]
    public static unsafe partial void* ImportPageWebGpuDevice();
}
