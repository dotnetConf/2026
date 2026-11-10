using System.Numerics;

namespace DotNetConf.NumericsDemos;

internal static class BFloat16Demo
{
    public static void Run(TextWriter output)
    {
        const double wide = 70000;
        output.WriteLine($"Range: input={wide:R} | Half={(double)(Half)wide:R} | BFloat16={(double)(BFloat16)wide:R}");

        const double nearOne = 1.0009765625;
        output.WriteLine($"Precision: input={nearOne:R} | Half={(double)(Half)nearOne:R} | BFloat16={(double)(BFloat16)nearOne:R}");

        // These exact decimal values sit just above destination-format midpoints.
        const decimal bfloatMidpointPlus = 1.0039062500000000000000000001m;
        const decimal halfMidpointPlus = 1.0004882812500000000000000001m;
        output.WriteLine($"BFloat16 input: {bfloatMidpointPlus}");
        output.WriteLine($"BFloat16 direct: {(double)(BFloat16)bfloatMidpointPlus:R}");
        output.WriteLine($"BFloat16 via double: {(double)(BFloat16)(double)bfloatMidpointPlus:R}");
        output.WriteLine($"Half direct: {(double)(Half)halfMidpointPlus:R}");
        output.WriteLine($"Half via double: {(double)(Half)(double)halfMidpointPlus:R}");
    }
}
