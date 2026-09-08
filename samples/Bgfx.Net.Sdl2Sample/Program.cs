using System.Runtime.InteropServices;
using Bgfx.Net;
using Silk.NET.SDL;
using BgfxApi = Bgfx.Net.Bgfx;

// Minimal SDL2 + bgfx integration sample.
//
// Opens an OS window via SDL2, hands its native handle to bgfx, and runs a
// clear-color frame loop until the window is closed. Defaults to bgfx's
// auto-selected renderer (D3D11 on Windows, Metal on macOS, OpenGL on Linux);
// pass `--renderer <name>` to force one — e.g. Noop, Vulkan, OpenGL,
// Direct3D11, Direct3D12, Metal. Pass `--frames N` to exit automatically
// after N frames (useful for smoke runs in CI).

const ushort Width = 1280;
const ushort Height = 720;

var (rendererOverride, maxFrames) = ParseArgs(args);

using var sdl = Sdl.GetApi();

if (sdl.Init(Sdl.InitVideo) != 0)
{
    throw new InvalidOperationException($"SDL_Init failed: {sdl.GetErrorS()}");
}

return RunUnsafe(sdl, rendererOverride, maxFrames);

static unsafe int RunUnsafe(Sdl sdl, RendererType? rendererOverride, int maxFrames)
{
    var title = "BgfxApi.Net + SDL2"u8;
    Window* window;
    fixed (byte* titlePtr = title)
    {
        window = sdl.CreateWindow(
            titlePtr,
            Sdl.WindowposCentered, Sdl.WindowposCentered,
            Width, Height,
            (uint)(WindowFlags.Shown | WindowFlags.Resizable));
    }
    if (window is null)
    {
        sdl.Quit();
        throw new InvalidOperationException($"SDL_CreateWindow failed: {sdl.GetErrorS()}");
    }

    try
    {
        var init = default(Init);
        BgfxApi.InitCtor(&init);
        if (rendererOverride is { } t) init.Type = t;
        FillNativeHandles(sdl, window, ref init);
        init.SwapChain.Width = Width;
        init.SwapChain.Height = Height;
        init.Reset = (uint)ResetFlags.Vsync;

        if (!BgfxApi.Init(&init))
        {
            throw new InvalidOperationException("bgfx_init failed");
        }

        try
        {
            Console.WriteLine($"bgfx renderer: {BgfxApi.GetRendererType()}");

            BgfxApi.SetDebug((uint)DebugFlags.Text, new FrameBufferHandle(ushort.MaxValue), 0);
            BgfxApi.SetViewClear(0, (ushort)(ClearFlags.Color | ClearFlags.Depth), 0x303080ff, 1.0f, 0);
            BgfxApi.SetViewRect(0, 0, 0, Width, Height);

            var frames = 0;
            var running = true;
            var ev = default(Event);
            var currentW = (uint)Width;
            var currentH = (uint)Height;

            while (running)
            {
                while (sdl.PollEvent(ref ev) != 0)
                {
                    if ((EventType)ev.Type == EventType.Quit)
                    {
                        running = false;
                    }
                    else if ((EventType)ev.Type == EventType.Windowevent
                             && (WindowEventID)ev.Window.Event == WindowEventID.SizeChanged)
                    {
                        currentW = (uint)ev.Window.Data1;
                        currentH = (uint)ev.Window.Data2;
                        // default(SwapChain) is not neutral; InitCtor's values are.
                        var swapChain = init.SwapChain;
                        swapChain.Width = currentW;
                        swapChain.Height = currentH;
                        BgfxApi.Reset((uint)ResetFlags.Vsync, &swapChain);
                        BgfxApi.SetViewRect(0, 0, 0, (ushort)currentW, (ushort)currentH);
                    }
                }

                BgfxApi.Touch(0);
                BgfxApi.DbgTextClear(0, false);
                BgfxApi.DbgTextPrintf(1, 1, 0x4f, "BgfxApi.Net + SDL2 sample", string.Empty);
                BgfxApi.DbgTextPrintf(1, 2, 0x6f, $"Renderer: {BgfxApi.GetRendererType()}  ({currentW}x{currentH})", string.Empty);
                BgfxApi.Frame(0);

                frames++;
                if (maxFrames > 0 && frames >= maxFrames) running = false;
            }
        }
        finally
        {
            BgfxApi.Shutdown();
        }
    }
    finally
    {
        sdl.DestroyWindow(window);
        sdl.Quit();
    }

    return 0;
}

static (RendererType? Renderer, int MaxFrames) ParseArgs(string[] args)
{
    RendererType? renderer = null;
    var maxFrames = 0;
    for (var i = 0; i < args.Length - 1; i++)
    {
        switch (args[i])
        {
            case "--renderer" or "-r" when Enum.TryParse<RendererType>(args[i + 1], ignoreCase: true, out var r):
                renderer = r;
                break;
            case "--frames" or "-f" when int.TryParse(args[i + 1], out var n) && n > 0:
                maxFrames = n;
                break;
        }
    }
    return (renderer, maxFrames);
}

static unsafe void FillNativeHandles(Sdl sdl, Window* window, ref Init init)
{
    var info = default(SysWMInfo);
    sdl.GetVersion(&info.Version);
    if (!sdl.GetWindowWMInfo(window, &info))
    {
        throw new InvalidOperationException($"SDL_GetWindowWMInfo failed: {sdl.GetErrorS()}");
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        init.SwapChain.Nwh = (void*)info.Info.Win.Hwnd;
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        init.SwapChain.Nwh = (void*)info.Info.Cocoa.Window;
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        // bgfx picks X11 vs Wayland from PlatformData.Type. SDL2 reports which
        // backend it actually used via info.Subsystem.
        if (info.Subsystem == SysWMType.Wayland)
        {
            init.SwapChain.Ndt = (void*)info.Info.Wayland.Display;
            init.SwapChain.Nwh = (void*)info.Info.Wayland.Surface;
            init.PlatformData.Type = NativeWindowHandleType.Wayland;
        }
        else
        {
            init.SwapChain.Ndt = (void*)info.Info.X11.Display;
            init.SwapChain.Nwh = (void*)info.Info.X11.Window;
            init.PlatformData.Type = NativeWindowHandleType.Default;
        }
    }
    else
    {
        throw new PlatformNotSupportedException();
    }
}
