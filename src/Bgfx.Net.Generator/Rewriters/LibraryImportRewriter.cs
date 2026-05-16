using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Converts <c>[DllImport(DllName, EntryPoint="x", CallingConvention=CallingConvention.Cdecl)]
/// public static extern unsafe T Name(...)</c> into the source-generated
/// <c>[LibraryImport("bgfx", EntryPoint="x")] [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
/// public static partial T Name(...)</c> form. LibraryImport is AOT-friendly
/// (no runtime marshaller required) and the analyzer flags unmarshallable
/// types at compile time. Bool parameters and returns are explicitly marked
/// <c>[MarshalAs(UnmanagedType.U1)]</c>; LibraryImport rejects unmarshalled
/// bools, and U1 (1-byte) matches bgfx's C99 <c>_Bool</c> ABI on every
/// supported platform (DllImport's default 4-byte Windows BOOL was incorrect).
/// </summary>
internal sealed class LibraryImportRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        var dllImport = visited.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "DllImport" or "System.Runtime.InteropServices.DllImport");
        if (dllImport is null)
        {
            return visited;
        }

        var entryPointArg = dllImport.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "EntryPoint");
        var entryPointLiteral = entryPointArg?.Expression?.ToString() ?? "\"\"";

        // Parse the new attribute lists from source text so spacing (comma, equals signs)
        // matches what a human would type. Building these with SyntaxFactory.Attribute(...)
        // produces "LibraryImport(\"bgfx\",EntryPoint=..." with no spaces.
        var libraryImportAL = ParseAttributeList($"[LibraryImport(\"bgfx\", EntryPoint = {entryPointLiteral})]");
        var unmanagedCallConvAL = ParseAttributeList("[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]");

        var newAttributeLists = SyntaxFactory.List<AttributeListSyntax>();
        foreach (var al in visited.AttributeLists)
        {
            var kept = al.Attributes.Where(a => a != dllImport).ToList();
            if (kept.Count > 0)
            {
                newAttributeLists = newAttributeLists.Add(
                    al.WithAttributes(SyntaxFactory.SeparatedList(kept)));
            }
        }

        var leading = visited.AttributeLists.FirstOrDefault()?.GetLeadingTrivia() ?? visited.GetLeadingTrivia();
        newAttributeLists = newAttributeLists.Insert(0,
            libraryImportAL
                .WithLeadingTrivia(leading)
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("\t")));
        newAttributeLists = newAttributeLists.Insert(1,
            unmanagedCallConvAL
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("\t")));

        var isBoolReturn = visited.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.BoolKeyword);
        var hasExistingReturnMarshal = newAttributeLists.Any(al =>
            al.Target is { } target &&
            target.Identifier.IsKind(SyntaxKind.ReturnKeyword) &&
            al.Attributes.Any(a => a.Name.ToString().Contains("MarshalAs")));
        if (isBoolReturn && !hasExistingReturnMarshal)
        {
            var marshalReturn = ParseAttributeList("[return: MarshalAs(UnmanagedType.U1)]")
                .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("\t"));
            newAttributeLists = newAttributeLists.Add(marshalReturn);
        }

        var newParams = visited.ParameterList.Parameters.Select(MarshalBoolParameter);
        var newParamList = visited.ParameterList.WithParameters(SyntaxFactory.SeparatedList(newParams));

        var newModifiers = SyntaxFactory.TokenList(
            visited.Modifiers
                .Where(m => !m.IsKind(SyntaxKind.ExternKeyword))
                .Concat(new[] { SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space) }));

        return visited
            .WithAttributeLists(newAttributeLists)
            .WithModifiers(newModifiers)
            .WithParameterList(newParamList);
    }

    private static ParameterSyntax MarshalBoolParameter(ParameterSyntax param)
    {
        if (param.Type is not PredefinedTypeSyntax pts || !pts.Keyword.IsKind(SyntaxKind.BoolKeyword))
        {
            return param;
        }
        if (param.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString().Contains("MarshalAs"))))
        {
            return param;
        }

        var marshal = ParseAttributeList("[MarshalAs(UnmanagedType.U1)]")
            .WithTrailingTrivia(SyntaxFactory.Space);
        return param.WithAttributeLists(param.AttributeLists.Add(marshal));
    }

    internal static AttributeListSyntax ParseAttributeList(string text)
    {
        // Parse a single bracketed attribute list (e.g. "[MarshalAs(...)]") as if it
        // were attached to a placeholder member, then extract the list. This keeps
        // Roslyn's parser in charge of separator spacing instead of hand-composing
        // SyntaxFactory.Attribute calls.
        var unit = SyntaxFactory.ParseCompilationUnit(text + "\nclass __Placeholder {}");
        return unit.DescendantNodes().OfType<AttributeListSyntax>().First();
    }
}
