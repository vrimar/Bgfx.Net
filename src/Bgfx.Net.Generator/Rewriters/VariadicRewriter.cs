using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bgfx.Net.Generator;

internal sealed class VariadicRewriter(C99Facts facts) : CSharpSyntaxRewriter
{
    private const string TextParameter = "_text";

    private readonly HashSet<string> _rewritten = new(StringComparer.Ordinal);

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;

        var members = new List<MemberDeclarationSyntax>(visited.Members.Count);
        var changed = false;
        foreach (var member in visited.Members)
        {
            if (member is not MethodDeclarationSyntax method ||
                EntryPoints.Of(method, "LibraryImport") is not { } entryPoint)
            {
                members.Add(member);
                continue;
            }

            if (facts.VariadicFormatParameters.TryGetValue(entryPoint, out var format))
            {
                members.Add(Wrapper(method, format));
                members.Add(Import(method));
            }
            else if (facts.VaListFunctions.Contains(entryPoint))
            {
                // C# cannot build a va_list portably, so the binding could only be called wrongly.
            }
            else
            {
                if (IsVarargsPlaceholder(method.ParameterList.Parameters.LastOrDefault()))
                {
                    throw new InvalidOperationException(
                        $"{method.Identifier.ValueText}: upstream binds C varargs as 'string args', but the C99 header declares no '...' for {entryPoint}.");
                }
                members.Add(member);
                continue;
            }

            _rewritten.Add(entryPoint);
            changed = true;
        }

        return changed ? visited.WithMembers(SyntaxFactory.List(members)) : visited;
    }

    public void EnsureComplete()
    {
        var missed = facts.VariadicFormatParameters.Keys
            .Concat(facts.VaListFunctions)
            .Where(entryPoint => !_rewritten.Contains(entryPoint))
            .ToList();
        if (missed.Count > 0)
        {
            throw new InvalidOperationException(
                $"No LibraryImport binding was rewritten for the C varargs function(s) {string.Join(", ", missed)}.");
        }
    }

    private static bool IsVarargsPlaceholder(ParameterSyntax? parameter) =>
        parameter is not null &&
        parameter.Identifier.ValueText == "args" &&
        parameter.Type?.ToString().Trim() == "string";

    private static MemberDeclarationSyntax Wrapper(MethodDeclarationSyntax method, string format)
    {
        var name = method.Identifier.ValueText;
        var fixedParameters = method.ParameterList.Parameters.SkipLast(1).ToList();
        if (fixedParameters.Count == 0 ||
            fixedParameters[^1].Identifier.ValueText != format ||
            fixedParameters[^1].Type?.ToString().Trim() != "string")
        {
            throw new InvalidOperationException(
                $"{name}: expected 'string {format}' right before the varargs placeholder.");
        }

        var passed = fixedParameters.SkipLast(1).ToList();
        var parameters = passed
            .Select(p => p.WithAttributeLists(default).ToString().Trim())
            .Append($"string {TextParameter}");
        var arguments = passed
            .Select(Argument)
            .Append($"({TextParameter} ?? string.Empty).Replace(\"%\", \"%%\", StringComparison.Ordinal)")
            .Append("null");

        var docs = Regex.Replace(
            method.AttributeLists[0].GetLeadingTrivia().ToFullString(),
            $"<param name=\"{Regex.Escape(format)}\">.*?</param>",
            $"<param name=\"{TextParameter}\">Text printed as is, with no printf formatting; null prints nothing.</param>",
            RegexOptions.Singleline);
        docs = Regex.Replace(docs, @"(?<=<summary>.*?)\bformatted\s+(?=.*?</summary>)", "", RegexOptions.Singleline);

        var publicName = name.EndsWith("Printf", StringComparison.Ordinal) ? name[..^1] : name;
        return SyntaxFactory.ParseMemberDeclaration(
            $"{docs}public static unsafe {method.ReturnType.ToString().Trim()} {publicName}({string.Join(", ", parameters)}) =>\n" +
            $"\t\t{name}Native({string.Join(", ", arguments)});\n")!;
    }

    private static string Argument(ParameterSyntax parameter) =>
        string.Join(" ", parameter.Modifiers
            .Where(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword) || m.IsKind(SyntaxKind.InKeyword))
            .Select(m => m.Text)
            .Append(parameter.Identifier.ValueText));

    private static MethodDeclarationSyntax Import(MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters;
        var placeholder = parameters.LastOrDefault();
        if (!IsVarargsPlaceholder(placeholder))
        {
            throw new InvalidOperationException(
                $"{method.Identifier.ValueText}: expected upstream's trailing 'string args' in place of '...', found '{placeholder}'.");
        }

        // wasm32 passes '...' as one trailing pointer, so the slot has to exist even when nothing is in it.
        var varargs = SyntaxFactory.ParseParameterList("(void* _varargs)").Parameters[0];

        var modifiers = SyntaxFactory.TokenList(method.Modifiers.Select(m =>
            m.IsKind(SyntaxKind.PublicKeyword)
                ? SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTriviaFrom(m)
                : m));

        var attributes = method.AttributeLists.Replace(
            method.AttributeLists[0],
            method.AttributeLists[0].WithLeadingTrivia(SyntaxFactory.EndOfLine("\n"), SyntaxFactory.Whitespace("\t")));

        return method
            .WithAttributeLists(attributes)
            .WithModifiers(modifiers)
            .WithIdentifier(SyntaxFactory.Identifier(method.Identifier.ValueText + "Native"))
            .WithParameterList(method.ParameterList.WithParameters(parameters.Replace(placeholder!, varargs)));
    }
}
