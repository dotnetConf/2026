using System.Globalization;
using System.Numerics.Tensors;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DotNetConf.NumericsDemos;

public static class DemoApp
{
    private static readonly (string Name, Action<TextWriter> Run)[] Demos =
    [
        ("decimal", DecimalDemo.Run),
        ("bfloat16", BFloat16Demo.Run),
        ("complex", ComplexDemo.Run),
        ("tensors", TensorDemo.Run),
        ("random", RandomDemo.Run),
        ("text", TextDemo.Run),
    ];

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length > 1)
        {
            error.WriteLine("Select one demo or 'all'.");
            WriteUsage(error);
            return 2;
        }

        string selected = args.Length == 0 ? "all" : args[0];
        if (selected is "--help" or "-h" or "help")
        {
            WriteUsage(output);
            return 0;
        }

        bool runAll = selected.Equals("all", StringComparison.OrdinalIgnoreCase);
        if (!runAll && !Array.Exists(Demos, demo => demo.Name.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            error.WriteLine($"Unknown demo: {selected}");
            WriteUsage(error);
            return 2;
        }

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            output.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
            foreach (var demo in Demos)
            {
                if (runAll || demo.Name.Equals(selected, StringComparison.OrdinalIgnoreCase))
                {
                    output.WriteLine();
                    output.WriteLine($"=== {demo.Name} ===");
                    if (demo.Name == "tensors")
                    {
                        string build = typeof(Tensor).Assembly
                            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                            ?? throw new InvalidOperationException("The tensor assembly has no build identity.");
                        output.WriteLine($"Tensor build: {build}");
                    }

                    demo.Run(output);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        return 0;
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("dotnet run --project Numerics -c Release --no-build -- <demo>");
        output.WriteLine("Demos: decimal | bfloat16 | complex | tensors | random | text | all");
    }
}
