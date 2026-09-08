using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

/// <summary>
/// Retypes parameters C declares as arrays (<c>const float _rgba[4]</c>) to pointers.
/// Upstream emits them as by-value scalars, putting a float in an SSE register where
/// the callee expects an address; <c>bgfx_vertex_unpack</c> then writes 16 bytes through it.
/// </summary>
internal sealed class ArrayParamPointerRewriter(C99Facts facts) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

        var entryPoint = visited.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name.ToString().EndsWith("DllImport", StringComparison.Ordinal))
            .SelectMany(a => a.ArgumentList?.Arguments ?? default)
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "EntryPoint")
            ?.Expression as LiteralExpressionSyntax;

        if (entryPoint is null ||
            !facts.ArrayParameters.TryGetValue(entryPoint.Token.ValueText, out var arrays))
        {
            return visited;
        }

        var parameters = visited.ParameterList.Parameters.Select(p =>
            arrays.Contains(p.Identifier.ValueText) && p.Type is not null
                ? p.WithType(SyntaxFactory.PointerType(p.Type.WithoutTrailingTrivia())
                    .WithTriviaFrom(p.Type))
                : p);

        return visited.WithParameterList(
            visited.ParameterList.WithParameters(SyntaxFactory.SeparatedList(parameters)));
    }
}
