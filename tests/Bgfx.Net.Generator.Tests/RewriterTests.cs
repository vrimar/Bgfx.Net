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
        Assert.DoesNotContain("UnmanagedCallConv", output);
        Assert.DoesNotContain("CallingConvention", output);
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
        Assert.Contains("public ShaderHandle(ushort idx) { this.idx = idx; }", output);
    }

    [Fact]
    public void HandleStructsGetValueEquality()
    {
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public struct ShaderHandle { public ushort idx; }
                }
            }
            """;
        var output = BindingRewriter.Rewrite(input);
        Assert.Contains("struct ShaderHandle : IEquatable<ShaderHandle>", output);
        Assert.Contains("public bool Equals(ShaderHandle other) => idx == other.idx;", output);
        Assert.Contains("public override bool Equals(object? obj) => obj is ShaderHandle other && Equals(other);", output);
        Assert.Contains("public override int GetHashCode() => HashCode.Combine(idx);", output);
        Assert.Contains("public static bool operator ==(ShaderHandle left, ShaderHandle right)", output);
        Assert.Contains("public static bool operator !=(ShaderHandle left, ShaderHandle right)", output);
    }

    [Fact]
    public void MultiFieldHandleStructsCoverEveryFieldInConstructorAndEquality()
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
        Assert.Contains("public bool Equals(BufferHandle other) => idx == other.idx && Type == other.Type;", output);
        Assert.Contains("public override int GetHashCode() => HashCode.Combine(idx, Type);", output);
    }

    [Fact]
    public void HandleStructsWithAnUnsupportedFieldShapeFailLoudly()
    {
        var input = """
            namespace Bgfx {
                public static partial class bgfx {
                    public unsafe struct WeirdHandle { public ushort idx; public fixed uint tags[4]; }
                }
            }
            """;
        var ex = Assert.Throws<InvalidOperationException>(() => BindingRewriter.Rewrite(input));
        Assert.Contains("WeirdHandle", ex.Message);
    }

    private const string PrintfHeader =
        "BGFX_C_API void bgfx_dbg_text_printf(uint16_t _x, uint16_t _y, uint8_t _attr, const char* _format, ... );\n";

    private const string VprintfHeader =
        "BGFX_C_API void bgfx_dbg_text_vprintf(uint16_t _x, uint16_t _y, uint8_t _attr, const char* _format, va_list _argList);\n";

    private const string PrintfBinding = """
        /// <summary>
        /// Print formatted data to internal debug text character-buffer.
        /// </summary>
        /// <param name="_format">`printf` style
        /// format.</param>
        [DllImport(DllName, EntryPoint="bgfx_dbg_text_printf", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void dbg_text_printf(ushort _x, ushort _y, byte _attr, [MarshalAs(UnmanagedType.LPStr)] string _format, [MarshalAs(UnmanagedType.LPStr)] string args );
        """;

    private const string VprintfBinding = """
        [DllImport(DllName, EntryPoint="bgfx_dbg_text_vprintf", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void dbg_text_vprintf(ushort _x, ushort _y, byte _attr, [MarshalAs(UnmanagedType.LPStr)] string _format, IntPtr _argList);
        """;

    private static string InBgfxClass(params string[] members) =>
        "namespace Bgfx {\npublic static partial class bgfx {\n" + string.Join("\n", members) + "\n}\n}\n";

    [Fact]
    public void VariadicFunctionsPrintVerbatimTextAndKeepTheVarargsSlot()
    {
        var output = BindingRewriter.Rewrite(
            InBgfxClass(PrintfBinding, VprintfBinding), C99Facts.Parse(PrintfHeader + VprintfHeader));

        Assert.Contains("public static unsafe void DbgTextPrint(ushort _x, ushort _y, byte _attr, string _text)", output);
        Assert.Contains("DbgTextPrintfNative(_x, _y, _attr, (_text ?? string.Empty).Replace(\"%\", \"%%\", StringComparison.Ordinal), null);", output);
        Assert.Contains("private static unsafe partial void DbgTextPrintfNative(", output);
        Assert.Contains("string _format,void* _varargs)", output);
        Assert.Contains("<param name=\"_text\">", output);
        Assert.Contains("Print data to internal debug text character-buffer.", output);
        Assert.DoesNotContain("<param name=\"_format\">", output);
        Assert.DoesNotContain("formatted", output);
        Assert.DoesNotContain("string args", output);
        Assert.DoesNotContain("public static unsafe void DbgTextPrintf(", output);
    }

    [Fact]
    public void VaListSiblingsAreDropped()
    {
        var output = BindingRewriter.Rewrite(
            InBgfxClass(PrintfBinding, VprintfBinding), C99Facts.Parse(PrintfHeader + VprintfHeader));

        Assert.DoesNotContain("bgfx_dbg_text_vprintf", output);
        Assert.DoesNotContain("DbgTextVprintf", output);
    }

    [Fact]
    public void VariadicWrappersForwardRefModifiers()
    {
        var facts = C99Facts.Parse("BGFX_C_API void bgfx_log(int32_t* _count, const char* _format, ... );\n");
        var input = InBgfxClass("""
            [DllImport(DllName, EntryPoint="bgfx_log", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe void log(ref int _count, [MarshalAs(UnmanagedType.LPStr)] string _format, [MarshalAs(UnmanagedType.LPStr)] string args );
            """);

        var output = BindingRewriter.Rewrite(input, facts);

        Assert.Contains("public static unsafe void Log(ref int _count, string _text)", output);
        Assert.Contains("LogNative(ref _count, (_text ?? string.Empty)", output);
    }

    [Fact]
    public void VariadicFunctionsWithAnUnexpectedPlaceholderFailLoudly()
    {
        var input = InBgfxClass("""
            [DllImport(DllName, EntryPoint="bgfx_dbg_text_printf", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe void dbg_text_printf(ushort _x, ushort _y, byte _attr, [MarshalAs(UnmanagedType.LPStr)] string _format, IntPtr _argList);
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => BindingRewriter.Rewrite(input, C99Facts.Parse(PrintfHeader)));
        Assert.Contains("DbgTextPrintf", ex.Message);
    }

    [Fact]
    public void VariadicFunctionsWithAMismatchedFormatParameterFailLoudly()
    {
        var facts = C99Facts.Parse(
            "BGFX_C_API void bgfx_dbg_text_printf(uint16_t _x, uint16_t _y, uint8_t _attr, const char* _fmt, ... );\n");

        var ex = Assert.Throws<InvalidOperationException>(() => BindingRewriter.Rewrite(InBgfxClass(PrintfBinding), facts));
        Assert.Contains("_fmt", ex.Message);
    }

    [Fact]
    public void VariadicFunctionsWithANonStringFormatFailLoudly()
    {
        var input = InBgfxClass("""
            [DllImport(DllName, EntryPoint="bgfx_dbg_text_printf", CallingConvention = CallingConvention.Cdecl)]
            public static extern unsafe void dbg_text_printf(ushort _x, ushort _y, byte _attr, IntPtr _format, [MarshalAs(UnmanagedType.LPStr)] string args );
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => BindingRewriter.Rewrite(input, C99Facts.Parse(PrintfHeader)));
        Assert.Contains("string _format", ex.Message);
    }

    [Fact]
    public void VariadicFunctionsLeftUnrewrittenFailLoudly()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BindingRewriter.Rewrite(InBgfxClass(PrintfBinding), C99Facts.Parse(PrintfHeader + VprintfHeader)));
        Assert.Contains("bgfx_dbg_text_vprintf", ex.Message);
    }

    [Fact]
    public void VarargsPlaceholdersWithoutAHeaderFactFailLoudly()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => BindingRewriter.Rewrite(InBgfxClass(PrintfBinding)));
        Assert.Contains("bgfx_dbg_text_printf", ex.Message);
    }

    [Fact]
    public void C99FactsReadVariadicAndVaListExportsOnly()
    {
        var facts = C99Facts.Parse(PrintfHeader + VprintfHeader + """
            typedef struct bgfx_interface_vtbl
            {
                void (*dbg_text_printf)(uint16_t _x, uint16_t _y, uint8_t _attr, const char* _format, ... );
            } bgfx_interface_vtbl_t;
            BGFX_C_API void bgfx_frame(void);
            """);

        Assert.Equal("_format", Assert.Single(facts.VariadicFormatParameters).Value);
        Assert.Equal("bgfx_dbg_text_vprintf", Assert.Single(facts.VaListFunctions));
    }

    [Fact]
    public void C99FactsRejectAnUnreadableFormatParameter()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => C99Facts.Parse(
            "BGFX_C_API void bgfx_dbg_text_printf(uint16_t _x, const char* _format /* fmt */, ... );\n"));
        Assert.Contains("bgfx_dbg_text_printf", ex.Message);
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
