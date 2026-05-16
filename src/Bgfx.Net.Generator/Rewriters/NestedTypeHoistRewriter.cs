using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Lifts nested type declarations (enum / struct / class) out of the
/// <c>Bgfx</c> static class and re-places them as siblings in the
/// <c>Bgfx.Net</c> namespace. With nested types gone, callers no longer
/// need <c>using static Bgfx.Net.Bgfx;</c> to reach them; a plain
/// <c>using Bgfx.Net;</c> suffices, and methods on the <c>Bgfx</c> class
/// can share names with same-named types without collision.
/// </summary>
internal sealed class NestedTypeHoistRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var bgfxClass = node.Members.OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == "Bgfx");
        if (bgfxClass is null)
        {
            return base.VisitNamespaceDeclaration(node);
        }

        var nested = bgfxClass.Members.OfType<BaseTypeDeclarationSyntax>().ToList();
        if (nested.Count == 0)
        {
            return base.VisitNamespaceDeclaration(node);
        }

        var trimmedMembers = SyntaxFactory.List(
            bgfxClass.Members.Where(m => m is not BaseTypeDeclarationSyntax));
        var trimmedClass = bgfxClass.WithMembers(trimmedMembers);

        var hoisted = nested
            .Select(t => (MemberDeclarationSyntax)t.WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
                                                   .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));

        var newMembers = SyntaxFactory.List<MemberDeclarationSyntax>(
            hoisted.Append(trimmedClass));
        return node.WithMembers(newMembers);
    }
}
