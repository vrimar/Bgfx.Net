using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Maps C99 <c>stdint</c> aliases that leak into upstream's binding (e.g. the video
/// decoder structs' <c>uint8_t*</c>) to their C# predefined equivalents (<c>byte*</c>).
/// bgfx's C# generator normally emits <c>byte</c>/<c>uint</c>/etc., but a new API
/// occasionally slips a raw <c>uint8_t</c> through, which isn't a C# type and won't compile.
/// </summary>
internal sealed class StdIntTypeRewriter : CSharpSyntaxRewriter
{
    private static readonly Dictionary<string, SyntaxKind> Map = new(StringComparer.Ordinal)
    {
        ["uint8_t"] = SyntaxKind.ByteKeyword,
        ["int8_t"] = SyntaxKind.SByteKeyword,
        ["uint16_t"] = SyntaxKind.UShortKeyword,
        ["int16_t"] = SyntaxKind.ShortKeyword,
        ["uint32_t"] = SyntaxKind.UIntKeyword,
        ["int32_t"] = SyntaxKind.IntKeyword,
        ["uint64_t"] = SyntaxKind.ULongKeyword,
        ["int64_t"] = SyntaxKind.LongKeyword,
    };

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (Map.TryGetValue(node.Identifier.ValueText, out var keyword))
        {
            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(keyword)).WithTriviaFrom(node);
        }
        return base.VisitIdentifierName(node);
    }
}
