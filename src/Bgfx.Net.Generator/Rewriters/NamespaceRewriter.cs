using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Rewrites <c>namespace Bgfx</c> to <c>namespace Bgfx.Net</c> so the wrapper
/// occupies a distinct namespace from upstream's auto-generated file.
/// </summary>
internal sealed class NamespaceRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        if (node.Name is IdentifierNameSyntax id && id.Identifier.ValueText == "Bgfx")
        {
            var newName = SyntaxFactory.ParseName("Bgfx.Net")
                .WithLeadingTrivia(node.Name.GetLeadingTrivia())
                .WithTrailingTrivia(node.Name.GetTrailingTrivia());
            return node.WithName(newName);
        }
        return base.VisitNamespaceDeclaration(node);
    }
}
