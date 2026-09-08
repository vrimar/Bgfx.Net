namespace Bgfx.Net.Generator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: Bgfx.Net.Generator <input bgfx.raw.cs> <output bgfx.g.cs> <c99 bgfx.h>");
            return 1;
        }

        var input = args[0];
        var output = args[1];
        var header = args[2];

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Input file not found: {input}");
            return 1;
        }

        if (!File.Exists(header))
        {
            Console.Error.WriteLine($"C99 header not found: {header}");
            return 1;
        }

        var source = File.ReadAllText(input);
        var rewritten = BindingRewriter.Rewrite(source, C99Facts.Parse(File.ReadAllText(header)));

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
