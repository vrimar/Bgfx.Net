using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// PascalCases public fields on namespace-level structs (excluding handle structs,
/// whose <c>idx</c> field is handled separately). Maps <c>init.type</c> to
/// <c>init.Type</c>, <c>resolution.numBackBuffers</c> to
/// <c>Resolution.NumBackBuffers</c>, etc.
/// </summary>
internal sealed class StructFieldPascalCaseRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var visited = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;
        if (visited.Identifier.ValueText.EndsWith("Handle", StringComparison.Ordinal))
        {
            return visited;
        }

        // C# member-name namespace: a field cannot share its name with a sibling
        // nested type. The bgfx binding has structs like Init { struct Limits; Limits limits; },
        // so a naive `limits` -> `Limits` rename collides. Collect sibling type names
        // and skip those renames.
        var siblingTypeNames = new HashSet<string>(
            visited.Members.OfType<BaseTypeDeclarationSyntax>().Select(t => t.Identifier.ValueText),
            StringComparer.Ordinal);

        var newMembers = SyntaxFactory.List<MemberDeclarationSyntax>(
            visited.Members.Select(m => PascalCaseFieldNames(m, siblingTypeNames)));
        return visited.WithMembers(newMembers);
    }

    private static MemberDeclarationSyntax PascalCaseFieldNames(MemberDeclarationSyntax member, HashSet<string> siblingTypeNames)
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
            if (siblingTypeNames.Contains(pascal))
            {
                return v;
            }
            return v.WithIdentifier(SyntaxFactory.Identifier(pascal).WithTriviaFrom(v.Identifier));
        });

        var newDecl = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(newVariables));
        return field.WithDeclaration(newDecl);
    }
}
