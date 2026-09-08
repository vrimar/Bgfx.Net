using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Emits the tag enum and implicit conversions for handle structs carrying a
/// discriminator, so <c>BlitBuffer(vbh, ...)</c> works as it does in C++ instead of
/// forcing callers to guess <c>new BufferHandle(vbh.idx, 4)</c>.
/// </summary>
internal sealed class TaggedHandleRewriter(C99Facts facts) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var tagged = facts.TaggedHandles.FirstOrDefault(t => t.StructName == node.Identifier.ValueText);
        if (tagged is null)
        {
            return base.VisitStructDeclaration(node);
        }

        var tag = node.Members
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .Select(v => v.Identifier.ValueText)
            .FirstOrDefault(n => n.Equals("type", StringComparison.OrdinalIgnoreCase));
        if (tag is null)
        {
            return base.VisitStructDeclaration(node);
        }

        var members = node.Members;
        foreach (var conversion in tagged.Conversions)
        {
            var source =
                $"public static implicit operator {tagged.StructName}({conversion.FromHandle} handle) => " +
                $"new(handle.idx, (ushort){tagged.EnumName}.{conversion.Member});";
            members = members.Add(SyntaxFactory.ParseMemberDeclaration(source)!
                .WithLeadingTrivia(SyntaxFactory.Whitespace("    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")));
        }

        return node.WithMembers(members);
    }

    public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var visited = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node)!;

        foreach (var tagged in facts.TaggedHandles)
        {
            var anchor = visited.Members
                .OfType<StructDeclarationSyntax>()
                .FirstOrDefault(s => s.Identifier.ValueText == tagged.StructName);
            if (anchor is null)
            {
                continue;
            }

            var body = string.Join("\n", tagged.Members.Select(m => $"\t\t{m},"));
            var declaration = SyntaxFactory.ParseMemberDeclaration(
                $"public enum {tagged.EnumName} : ushort\n\t{{\n{body}\n\t}}")!
                .WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("\t"))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.EndOfLine("\n"));

            visited = visited.WithMembers(visited.Members.Insert(visited.Members.IndexOf(anchor), declaration));
        }

        return visited;
    }
}
