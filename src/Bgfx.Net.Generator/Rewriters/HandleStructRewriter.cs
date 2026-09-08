using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Promotes handle structs to <c>public readonly partial struct</c>, marks every
/// instance field <c>readonly</c>, and adds a constructor taking one parameter per
/// field plus value-equality. IDisposable is deliberately not emitted: bgfx's
/// destroy_* functions are distinct per handle type and the matching is brittle to
/// automate, so callers destroy handles explicitly.
/// </summary>
internal sealed class HandleStructRewriter : CSharpSyntaxRewriter
{
    private readonly record struct HandleField(string Type, string Name);

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
        var fields = new List<HandleField>();

        foreach (var member in node.Members)
        {
            if (member is FieldDeclarationSyntax field && IsInstanceField(field))
            {
                fields.AddRange(ReadFields(field, name));

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

        if (fields.Count == 0)
        {
            return node.WithModifiers(modifiers).WithMembers(newMembers);
        }

        if (!node.Members.OfType<ConstructorDeclarationSyntax>().Any())
        {
            var parameters = string.Join(", ", fields.Select(f => $"{f.Type} {Camel(f.Name)}"));
            var assignments = string.Join(" ", fields.Select(f => $"this.{f.Name} = {Camel(f.Name)};"));
            newMembers = newMembers.Add(ParseMember(name, $"public {name}({parameters}) {{ {assignments} }}"));
        }

        if (!node.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == "Equals"))
        {
            var comparands = string.Join(" && ", fields.Select(f => $"{f.Name} == other.{f.Name}"));
            var hashed = string.Join(", ", fields.Select(f => f.Name));
            foreach (var source in new[]
                     {
                         $"public bool Equals({name} other) => {comparands};",
                         $"public override bool Equals(object? obj) => obj is {name} other && Equals(other);",
                         $"public override int GetHashCode() => HashCode.Combine({hashed});",
                         $"public static bool operator ==({name} left, {name} right) => left.Equals(right);",
                         $"public static bool operator !=({name} left, {name} right) => !left.Equals(right);",
                     })
            {
                newMembers = newMembers.Add(ParseMember(name, source));
            }
        }

        var rewritten = node.WithModifiers(modifiers).WithMembers(newMembers);
        return rewritten.BaseList is null ? rewritten.WithBaseList(EquatableBaseList(name)) : rewritten;
    }

    private static BaseListSyntax EquatableBaseList(string name)
    {
        return SyntaxFactory.BaseList(
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"IEquatable<{name}>"))))
            .WithTrailingTrivia(SyntaxFactory.Space);
    }

    private static bool IsInstanceField(FieldDeclarationSyntax field)
    {
        return !field.Modifiers.Any(SyntaxKind.StaticKeyword)
            && !field.Modifiers.Any(SyntaxKind.ConstKeyword);
    }

    private static IEnumerable<HandleField> ReadFields(FieldDeclarationSyntax field, string structName)
    {
        if (field.Modifiers.Any(SyntaxKind.FixedKeyword) ||
            field.Declaration.Type is not (PredefinedTypeSyntax or IdentifierNameSyntax))
        {
            throw new InvalidOperationException(
                $"Handle struct {structName} has an unsupported field type '{field.Declaration.Type}'.");
        }

        var type = field.Declaration.Type.ToString();
        foreach (var variable in field.Declaration.Variables)
        {
            if (variable.ArgumentList is not null)
            {
                throw new InvalidOperationException(
                    $"Handle struct {structName} has an unsupported field '{variable.Identifier.ValueText}'.");
            }
            yield return new HandleField(type, variable.Identifier.ValueText);
        }
    }

    private static MemberDeclarationSyntax ParseMember(string structName, string source)
    {
        // SyntaxFactory-built members lose keyword separators ("publicXHandle").
        var parsed = SyntaxFactory.ParseMemberDeclaration(source)
            ?? throw new InvalidOperationException($"Could not parse generated member for {structName}: {source}");
        return parsed
            .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
