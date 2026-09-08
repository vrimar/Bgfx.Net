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
    public void MultiFieldHandleStructsGetAConstructorCoveringEveryField()
    {
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public struct BufferHandle { public ushort idx; public ushort type; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("readonly ushort idx", output);
        Assert.Contains("readonly ushort Type", output);
        Assert.Contains("public BufferHandle(ushort idx, ushort type) { this.idx = idx; this.Type = type; }", output);
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
    public void StdIntAliasesAreMappedToCSharpPredefinedTypes()
    {
        // Upstream's video decoder structs leak raw C99 `uint8_t*`, which isn't a C# type.
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public unsafe struct VideoDecoderInit { public uint8_t* parameterSets; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("byte* ParameterSets;", output);
        Assert.DoesNotContain("uint8_t", output);
    }

    [Fact]
    public void HandleIdxFieldIsPreservedLowercase()
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

    [Fact]
    public void ArrayParametersBecomePointers()
    {
        var facts = C99Facts.Parse(
            "BGFX_C_API void bgfx_set_palette_color(uint8_t _index, const float _rgba[4]);\n" +
            "BGFX_C_API void bgfx_set_palette_color_rgba32f(uint8_t _index, float _r, float _g);\n");
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    [DllImport(DllName, EntryPoint="bgfx_set_palette_color", CallingConvention = CallingConvention.Cdecl)]
                    public static extern unsafe void set_palette_color(byte _index, float _rgba);

                    [DllImport(DllName, EntryPoint="bgfx_set_palette_color_rgba32f", CallingConvention = CallingConvention.Cdecl)]
                    public static extern unsafe void set_palette_color_rgba32f(byte _index, float _r, float _g);
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input, facts);
        Assert.Contains("SetPaletteColor(byte _index,float* _rgba)", output);
        Assert.Contains("SetPaletteColorRgba32f(byte _index,float _r,float _g)", output);
    }

    [Fact]
    public void TaggedHandlesGetATagEnumAndImplicitConversions()
    {
        var facts = C99Facts.Parse("""
            typedef struct bgfx_buffer_handle_s { uint16_t idx; uint16_t type; } bgfx_buffer_handle_t;

            typedef enum bgfx_buffer_handle_type
            {
                BGFX_BUFFER_HANDLE_TYPE_INDEX_BUFFER,
                BGFX_BUFFER_HANDLE_TYPE_VERTEX_BUFFER,

                BGFX_BUFFER_HANDLE_TYPE_COUNT

            } bgfx_buffer_handle_type_t;

            static inline bgfx_buffer_handle_t bgfx_buffer_from_vertex_buffer(bgfx_vertex_buffer_handle_t _handle)
            {
                bgfx_buffer_handle_t handle;
                handle.idx  = _handle.idx;
                handle.type = BGFX_BUFFER_HANDLE_TYPE_VERTEX_BUFFER;
                return handle;
            }
            """);
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public struct BufferHandle { public ushort idx; public ushort type; }
                    public struct VertexBufferHandle { public ushort idx; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input, facts);
        Assert.Contains("public enum BufferHandleType : ushort", output);
        Assert.Contains("IndexBuffer,", output);
        Assert.Contains("VertexBuffer,", output);
        Assert.Contains("Count,", output);
        Assert.Contains(
            "public static implicit operator BufferHandle(VertexBufferHandle handle) => new(handle.idx, (ushort)BufferHandleType.VertexBuffer);",
            output);
    }
}
