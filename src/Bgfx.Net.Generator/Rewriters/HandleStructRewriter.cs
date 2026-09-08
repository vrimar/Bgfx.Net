using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Promotes handle structs to <c>public readonly partial struct</c>, marks every
/// instance field <c>readonly</c>, and adds a constructor taking one parameter per
/// field in declaration order. Callers consume handles returned from bgfx by value
/// and read the fields if they need them, but cannot mutate post-construction. The
/// constructor exists so tests and advanced users can synthesise a specific
/// handle value (e.g. <c>new ShaderHandle(ushort.MaxValue)</c>).
/// The existing <c>Valid</c> property emitted by upstream is preserved.
/// </summary>
internal sealed class HandleStructRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (!name.EndsWith("Handle", StringComparison.Ordinal))
        {
            return base.VisitStructDeclaration(node);
        }

        var modifiers = node.Modifiers;
        if (!modifiers.Any(SyntaxKind.PartialKeyword))
        {
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        }
        if (!modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            var publicIdx = modifiers.IndexOf(SyntaxKind.PublicKeyword);
            var insertAt = publicIdx >= 0 ? publicIdx + 1 : 0;
            modifiers = modifiers.Insert(insertAt,
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        }

        var newMembers = SyntaxFactory.List<MemberDeclarationSyntax>();
        var hasCtor = node.Members.OfType<ConstructorDeclarationSyntax>().Any();
        var fields = new List<(string Type, string Name)>();

        foreach (var member in node.Members)
        {
            if (member is FieldDeclarationSyntax field && IsInstanceField(field))
            {
                var type = field.Declaration.Type.ToString();
                fields.AddRange(field.Declaration.Variables.Select(v => (type, v.Identifier.ValueText)));

                var newModifiers = SyntaxFactory.TokenList(
                    field.Modifiers
                        .Where(m => !m.IsKind(SyntaxKind.ReadOnlyKeyword))
                        .Concat(new[] { SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space) }));
                newMembers = newMembers.Add(field.WithModifiers(newModifiers));
            }
            else
            {
                newMembers = newMembers.Add(member);
            }
        }

        if (!hasCtor && fields.Count > 0)
        {
            var parameters = string.Join(", ", fields.Select(f => $"{f.Type} {Camel(f.Name)}"));
            var assignments = string.Join(" ", fields.Select(f => $"this.{f.Name} = {Camel(f.Name)};"));
            // SyntaxFactory-built members lose keyword separators ("publicXHandle").
            var parsedCtor = (ConstructorDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
                $"public {name}({parameters}) {{ {assignments} }}")!;
            newMembers = newMembers.Add(parsedCtor
                .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")));
        }

        return node.WithModifiers(modifiers).WithMembers(newMembers);
    }

    private static bool IsInstanceField(FieldDeclarationSyntax field)
    {
        return !field.Modifiers.Any(SyntaxKind.StaticKeyword)
            && !field.Modifiers.Any(SyntaxKind.ConstKeyword);
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
