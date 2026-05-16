using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Renames the upstream <c>bgfx</c> class to <c>Bgfx</c> (PascalCase) so callers
/// write <c>Bgfx.Init(...)</c> instead of <c>bgfx.init(...)</c>.
/// </summary>
internal sealed class ClassNameRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (visited.Identifier.ValueText == "bgfx")
        {
            return visited.WithIdentifier(SyntaxFactory.Identifier("Bgfx").WithTriviaFrom(visited.Identifier));
        }
        return visited;
    }
}
