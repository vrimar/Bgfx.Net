using System.Reflection;
using Xunit;
using Bgfx.Net;

namespace Bgfx.Net.Tests;

public unsafe class SmokeTests
{
    [Fact]
    public void AssemblyEmbedsBgfxRevision()
    {
        var attr = typeof(ShaderHandle).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BgfxRevision");

        Assert.NotNull(attr);
        Assert.False(string.IsNullOrEmpty(attr!.Value), "BgfxRevision metadata should be populated by sync-bindings.ps1");
    }

    [Fact]
    public void HandleStructsExposeValid()
    {
        var invalid = new ShaderHandle(ushort.MaxValue);
        Assert.False(invalid.Valid);

        var valid = new ShaderHandle(0);
        Assert.True(valid.Valid);
    }

    [Fact]
    public void HandleEqualityIsValueBased()
    {
        var a = new TextureHandle(42);
        var b = new TextureHandle(42);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [SkippableFact]
    public void NoopRendererInitAndShutdown()
    {
        Skip.IfNot(NativeLibraryPresent(), "bgfx native library not deployed; per-RID smoke job (package.yml) runs this with the lib staged.");

        var init = default(Init);
        Bgfx.InitCtor(&init);
        init.Type = RendererType.Noop;

        Assert.True(Bgfx.Init(&init), "bgfx_init should succeed with the noop renderer");
        try
        {
            for (var i = 0; i < 10; i++)
            {
                Bgfx.Frame((byte)0);
            }
            Assert.Equal(RendererType.Noop, Bgfx.GetRendererType());
        }
        finally
        {
            Bgfx.Shutdown();
        }
    }

    private static bool NativeLibraryPresent()
    {
        var dir = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(dir, "bgfx.dll"))
            || File.Exists(Path.Combine(dir, "libbgfx.so"))
            || File.Exists(Path.Combine(dir, "libbgfx.dylib"));
    }
}
