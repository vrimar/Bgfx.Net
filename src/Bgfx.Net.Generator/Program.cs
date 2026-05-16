namespace Bgfx.Net.Generator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: Bgfx.Net.Generator <input bgfx.raw.cs> <output bgfx.g.cs>");
            return 1;
        }

        var input = args[0];
        var output = args[1];

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Input file not found: {input}");
            return 1;
        }

        var source = File.ReadAllText(input);
        var rewritten = BindingRewriter.Rewrite(source);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

        if (File.Exists(output) && File.ReadAllText(output) == rewritten)
        {
            Console.WriteLine($"[Bgfx.Net.Generator] {output} is up-to-date.");
            return 0;
        }

        File.WriteAllText(output, rewritten);
        Console.WriteLine($"[Bgfx.Net.Generator] Wrote {output} ({rewritten.Length:N0} chars).");
        return 0;
    }
}
