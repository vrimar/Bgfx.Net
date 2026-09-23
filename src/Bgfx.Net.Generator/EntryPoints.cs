using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

internal static class EntryPoints
{
    public static string? Of(MethodDeclarationSyntax method, string attribute) =>
        (method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name.ToString() is var name &&
                        (name == attribute || name.EndsWith("." + attribute, StringComparison.Ordinal)))
            .SelectMany(a => a.ArgumentList?.Arguments ?? default)
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "EntryPoint")
            ?.Expression as LiteralExpressionSyntax)?.Token.ValueText;
}
