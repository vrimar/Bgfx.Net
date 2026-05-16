using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Renames extern static method declarations from snake_case to PascalCase.
/// With nested types hoisted out, methods and same-named types coexist in
/// different scopes, so the previous collision-rename map (InitCall,
/// ConvertTopology, RenderNextFrame) is no longer needed.
/// The native EntryPoint attribute is preserved unchanged.
/// </summary>
internal sealed class MethodNameRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (string.IsNullOrEmpty(name) || !IsExternStatic(node))
        {
            return base.VisitMethodDeclaration(node);
        }

        if (char.IsUpper(name[0]))
        {
            return base.VisitMethodDeclaration(node);
        }

        var newName = SnakeToPascal(name);
        if (newName == name)
        {
            return base.VisitMethodDeclaration(node);
        }

        return node.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(node.Identifier));
    }

    private static bool IsExternStatic(MethodDeclarationSyntax node)
    {
        var hasExtern = false;
        var hasStatic = false;
        foreach (var mod in node.Modifiers)
        {
            if (mod.IsKind(SyntaxKind.ExternKeyword)) hasExtern = true;
            if (mod.IsKind(SyntaxKind.StaticKeyword)) hasStatic = true;
        }
        return hasExtern && hasStatic;
    }

    internal static string SnakeToPascal(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return snake;
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
