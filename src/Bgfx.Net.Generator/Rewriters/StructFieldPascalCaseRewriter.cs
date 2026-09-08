using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// PascalCases public fields on namespace-level structs: <c>init.type</c> to
/// <c>Init.Type</c>, <c>swapChain.numBackBuffers</c> to <c>SwapChain.NumBackBuffers</c>.
/// Handle structs keep <c>idx</c> lowercase because the <c>Valid</c> property upstream
/// emits refers to it by that name.
/// </summary>
internal sealed class StructFieldPascalCaseRewriter : CSharpSyntaxRewriter
{
    private const string HandleIndexField = "idx";

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var visited = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;

        // A field cannot share its name with a sibling nested type: Init { struct Limits; Limits limits; }.
        var keep = new HashSet<string>(
            visited.Members.OfType<BaseTypeDeclarationSyntax>().Select(t => t.Identifier.ValueText),
            StringComparer.Ordinal);
        if (visited.Identifier.ValueText.EndsWith("Handle", StringComparison.Ordinal))
        {
            keep.Add(char.ToUpperInvariant(HandleIndexField[0]) + HandleIndexField[1..]);
        }

        var newMembers = SyntaxFactory.List<MemberDeclarationSyntax>(
            visited.Members.Select(m => PascalCaseFieldNames(m, keep)));
        return visited.WithMembers(newMembers);
    }

    private static MemberDeclarationSyntax PascalCaseFieldNames(MemberDeclarationSyntax member, HashSet<string> keep)
    {
        if (member is not FieldDeclarationSyntax field)
        {
            return member;
        }
        if (!field.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return field;
        }

        var newVariables = field.Declaration.Variables.Select(v =>
        {
            var name = v.Identifier.ValueText;
            if (string.IsNullOrEmpty(name) || char.IsUpper(name[0]))
            {
                return v;
            }
            var pascal = char.ToUpperInvariant(name[0]) + name[1..];
            if (keep.Contains(pascal))
            {
                return v;
            }
            return v.WithIdentifier(SyntaxFactory.Identifier(pascal).WithTriviaFrom(v.Identifier));
        });

        var newDecl = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(newVariables));
        return field.WithDeclaration(newDecl);
    }
}
