using CoreAnimation;
using UIKit;

namespace Bgfx.Net.IosSmoke;

internal static unsafe class Smoke
{
    private static readonly string ResultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "smoke.txt"
    );

    public static void Run(UIView view)
    {
        File.WriteAllText(ResultPath, "");
        try
        {
            Log($"noop={Render(RendererType.Noop, null, 64, 64)}");

            var scale = view.ContentScaleFactor;
            var layer = new CAMetalLayer { Frame = view.Bounds, ContentsScale = scale };
            view.Layer.AddSublayer(layer);
            var width = (uint)(view.Bounds.Width * scale);
            var height = (uint)(view.Bounds.Height * scale);
            Log($"metal={Render(RendererType.Metal, (void*)layer.Handle.Handle, width, height)}");
        }
        catch (Exception e)
        {
            Log($"error={e}");
        }

        Log("done");
        Environment.Exit(0);
    }

    private static string Render(RendererType type, void* nwh, uint width, uint height)
    {
        Bgfx.RenderFrame(-1);

        var init = default(Init);
        Bgfx.InitCtor(&init);
        init.Type = type;
        init.SwapChain.Nwh = nwh;
        init.SwapChain.Width = width;
        init.SwapChain.Height = height;

        if (!Bgfx.Init(&init))
        {
            return "init-failed";
        }

        var actual = Bgfx.GetRendererType();
        Bgfx.SetViewClear(0, (ushort)(ClearFlags.Color | ClearFlags.Depth), 0x303080ff, 1.0f, 0);
        Bgfx.SetViewRect(0, 0, 0, (ushort)width, (ushort)height, 0.0f, 1.0f);
        for (var i = 0; i < 3; i++)
        {
            Bgfx.Touch(0);
            Bgfx.Frame(0);
        }

        Bgfx.Shutdown();
        return $"ok renderer={actual}";
    }

    private static void Log(string line)
    {
        Console.WriteLine("SMOKE " + line);
        File.AppendAllText(ResultPath, line + "\n");
    }
}
