using Bgfx.Net.Generator;
using Xunit;

namespace Bgfx.Net.Generator.Tests;

public class RewriterTests
{
    [Fact]
    public void SnakeToPascal_BasicCase()
    {
        Assert.Equal("CreateShader", MethodNameRewriter.SnakeToPascal("create_shader"));
    }

    [Fact]
    public void SnakeToPascal_PreservesDigitsInPlace()
    {
        Assert.Equal("Decode32b", MethodNameRewriter.SnakeToPascal("decode_32b"));
    }

    [Fact]
    public void NamespaceIsRenamedToBgfxNet()
    {
        var input = """
            namespace Bgfx
            {
                public static partial class bgfx { }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("namespace Bgfx.Net", output);
        Assert.Contains("public static partial class Bgfx", output);
    }

    [Fact]
    public void DllImportIsConvertedToLibraryImportWithLiteralLibName()
    {
        var input = """
            using System.Runtime.InteropServices;
            namespace Bgfx {
                public static partial class bgfx {
                    [DllImport(DllName, EntryPoint="bgfx_init", CallingConvention = CallingConvention.Cdecl)]
                    public static extern unsafe bool init();
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("[LibraryImport(\"bgfx\"", output);
        Assert.Contains("EntryPoint=\"bgfx_init\"", output.Replace(" ", ""));
        Assert.Contains("UnmanagedCallConv", output);
        Assert.Contains("CallConvCdecl", output);
        Assert.DoesNotContain("DllImport", output);
        Assert.DoesNotContain("DllName", output);
        Assert.DoesNotContain(" extern ", output);
    }

    [Fact]
    public void ExternMethodsAreRenamedSnakeToPascalAndMadePartial()
    {
        var input = """
            using System.Runtime.InteropServices;
            namespace Bgfx {
                public static partial class bgfx {
                    [DllImport(DllName, EntryPoint="bgfx_create_shader", CallingConvention = CallingConvention.Cdecl)]
                    public static extern unsafe int create_shader();
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("CreateShader", output);
        Assert.Contains("static partial", output);
        Assert.DoesNotContain(" create_shader(", output);
    }

    [Fact]
    public void NestedTypesAreHoistedToNamespaceLevel()
    {
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public enum RendererType { Vulkan }
                    public struct Init { public RendererType type; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        // The enum and struct must now live OUTSIDE the Bgfx class but inside the namespace.
        var classIdx = output.IndexOf("class Bgfx", StringComparison.Ordinal);
        var enumIdx = output.IndexOf("enum RendererType", StringComparison.Ordinal);
        var structIdx = output.IndexOf("struct Init", StringComparison.Ordinal);
        Assert.True(enumIdx >= 0 && structIdx >= 0 && classIdx >= 0);
        Assert.True(enumIdx < classIdx, "enum should be hoisted before the class");
        Assert.True(structIdx < classIdx, "struct should be hoisted before the class");
    }

    [Fact]
    public void MethodNamesNoLongerCollideWithHoistedTypes()
    {
        // After hoisting, the Init method and the (hoisted) Init struct can coexist
        // without the old "InitCall" workaround.
        var input = """
            using System.Runtime.InteropServices;
            namespace Bgfx {
                public static partial class bgfx {
                    public struct Init { public int x; }
                    [DllImport(DllName, EntryPoint="bgfx_init", CallingConvention = CallingConvention.Cdecl)]
                    public static extern unsafe bool init(Init* _init);
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("partial bool Init(", output);
        Assert.DoesNotContain("InitCall", output);
    }

    [Fact]
    public void BoolReturnAndParametersAreMarshalledAsU1()
    {
        var input = """
            using System.Runtime.InteropServices;
            namespace Bgfx {
                public static partial class bgfx {
                    [DllImport(DllName, EntryPoint="bgfx_x", CallingConvention = CallingConvention.Cdecl)]
                    public static extern unsafe bool x(bool _flag);
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("[return: MarshalAs(UnmanagedType.U1)]", output);
        Assert.Contains("[MarshalAs(UnmanagedType.U1)] bool _flag", output);
    }

    [Fact]
    public void HandleStructsBecomeReadOnlyWithConstructor()
    {
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public struct ShaderHandle { public ushort idx; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("readonly partial struct ShaderHandle", output);
        Assert.Contains("readonly ushort idx", output);
        Assert.Contains("public ShaderHandle(ushort idx)", output);
    }

    [Fact]
    public void NonHandleStructFieldsArePascalCased()
    {
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public struct Init { public int type; public byte debug; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("public int Type;", output);
        Assert.Contains("public byte Debug;", output);
        Assert.DoesNotContain("public int type;", output);
    }

    [Fact]
    public void HandleStructFieldsArePreservedLowercase()
    {
        // The handle's `idx` field must stay lowercase because the generator's emitted
        // `Valid` property references it as `idx`.
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public struct ShaderHandle { public ushort idx; public bool Valid => idx != UInt16.MaxValue; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("readonly ushort idx", output);
        Assert.DoesNotContain("readonly ushort Idx", output);
    }
}
