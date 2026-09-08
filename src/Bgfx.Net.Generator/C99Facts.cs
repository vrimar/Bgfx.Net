using System.Text;
using System.Text.RegularExpressions;

namespace Bgfx.Net.Generator;

internal sealed record TaggedHandle(
    string StructName,
    string EnumName,
    IReadOnlyList<string> Members,
    IReadOnlyList<TagConversion> Conversions);

internal sealed record TagConversion(string FromHandle, string Member);

/// <summary>
/// Recovers from bgfx's C99 header what upstream's C# binding drops: parameters
/// declared as C arrays (pointers in the ABI, by-value scalars in <c>bgfx.cs</c>),
/// and handle types carrying a tag discriminator.
/// </summary>
internal sealed class C99Facts
{
    private static readonly Regex ExportedFunction = new(
        @"BGFX_C_API\s+[^;{]*?\b(bgfx_\w+)\s*\(([^;]*?)\)\s*;",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ArrayParameter = new(
        @"\b(\w+)\s*\[\s*\d*\s*\]\s*$",
        RegexOptions.Compiled);

    private static readonly Regex TagEnum = new(
        @"typedef\s+enum\s+bgfx_(\w+)_type\s*\{(?<body>[^}]*)\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagConverter = new(
        @"static\s+inline\s+bgfx_(?<target>\w+)_t\s+bgfx_\w+\s*\(\s*(?:const\s+)?bgfx_(?<source>\w+)_handle_t\s+\w+\s*\)\s*\{(?<body>[^}]*)\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static C99Facts Empty { get; } = new(
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
        Array.Empty<TaggedHandle>());

    private C99Facts(
        IReadOnlyDictionary<string, IReadOnlySet<string>> arrayParameters,
        IReadOnlyList<TaggedHandle> taggedHandles)
    {
        ArrayParameters = arrayParameters;
        TaggedHandles = taggedHandles;
    }

    public IReadOnlyDictionary<string, IReadOnlySet<string>> ArrayParameters { get; }

    public IReadOnlyList<TaggedHandle> TaggedHandles { get; }

    public static C99Facts Parse(string header)
    {
        return new C99Facts(ParseArrayParameters(header), ParseTaggedHandles(header));
    }

    private static Dictionary<string, IReadOnlySet<string>> ParseArrayParameters(string header)
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (Match function in ExportedFunction.Matches(header))
        {
            var arrays = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in SplitTopLevel(function.Groups[2].Value))
            {
                var array = ArrayParameter.Match(parameter.Trim());
                if (array.Success)
                {
                    arrays.Add(array.Groups[1].Value);
                }
            }
            if (arrays.Count > 0)
            {
                result[function.Groups[1].Value] = arrays;
            }
        }

        return result;
    }

    private static List<TaggedHandle> ParseTaggedHandles(string header)
    {
        var result = new List<TaggedHandle>();

        foreach (Match tagEnum in TagEnum.Matches(header))
        {
            var cName = $"bgfx_{tagEnum.Groups[1].Value}";
            if (!header.Contains($"typedef struct {cName}_s", StringComparison.Ordinal))
            {
                continue;
            }

            var prefix = $"BGFX_{tagEnum.Groups[1].Value.ToUpperInvariant()}_TYPE_";
            var members = Regex.Matches(tagEnum.Groups["body"].Value, Regex.Escape(prefix) + @"(\w+)")
                .Select(m => Pascal(m.Groups[1].Value))
                .ToList();
            if (members.Count == 0)
            {
                continue;
            }

            var structName = Pascal(tagEnum.Groups[1].Value);
            var conversions = new List<TagConversion>();
            foreach (Match converter in TagConverter.Matches(header))
            {
                if (converter.Groups["target"].Value != tagEnum.Groups[1].Value)
                {
                    continue;
                }
                var assigned = Regex.Match(converter.Groups["body"].Value, Regex.Escape(prefix) + @"(\w+)");
                if (assigned.Success)
                {
                    conversions.Add(new TagConversion(
                        Pascal(converter.Groups["source"].Value) + "Handle",
                        Pascal(assigned.Groups[1].Value)));
                }
            }

            result.Add(new TaggedHandle(structName, structName + "Type", members, conversions));
        }

        return result;
    }

    private static IEnumerable<string> SplitTopLevel(string parameters)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < parameters.Length; i++)
        {
            switch (parameters[i])
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return parameters[start..i];
                    start = i + 1;
                    break;
            }
        }
        if (start < parameters.Length)
        {
            yield return parameters[start..];
        }
    }

    private static string Pascal(string snake)
    {
        var sb = new StringBuilder(snake.Length);
        foreach (var word in snake.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            sb.Append(char.ToUpperInvariant(word[0]));
            sb.Append(word.AsSpan(1).ToString().ToLowerInvariant());
        }
        return sb.ToString();
    }
}
