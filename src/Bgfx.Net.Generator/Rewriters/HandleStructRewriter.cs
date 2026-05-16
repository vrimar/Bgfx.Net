using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Promotes handle structs to <c>public readonly partial struct</c> with a
/// <c>public readonly ushort idx</c> field and a <c>public XHandle(ushort idx)</c>
/// constructor. Callers consume handles returned from bgfx by value and read
/// <c>idx</c> if they need it, but cannot mutate post-construction. The
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

        foreach (var member in node.Members)
        {
            if (member is FieldDeclarationSyntax field &&
                field.Declaration.Variables.Any(v => v.Identifier.ValueText == "idx"))
            {
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

        if (!hasCtor)
        {
            // Parse the ctor as source text so trivia (spaces) come out right; building it
            // token-by-token with SyntaxFactory leaves "publicXHandle" missing the keyword
            // separator.
            var ctorSource = $"public {name}(ushort idx) => this.idx = idx;";
            var parsedCtor = (ConstructorDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(ctorSource)!;
            parsedCtor = parsedCtor
                .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
            newMembers = newMembers.Add(parsedCtor);
        }

        return node.WithModifiers(modifiers).WithMembers(newMembers);
    }
}
