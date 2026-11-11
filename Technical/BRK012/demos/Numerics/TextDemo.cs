using System.Globalization;
using System.Numerics;
using System.Text;

namespace DotNetConf.NumericsDemos;

internal static class TextDemo
{
    public static void Run(TextWriter output)
    {
        const string input = "12.5ms";
        if (!double.TryParsePartial(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value, out int consumed))
        {
            throw new FormatException("The measurement has no numeric prefix.");
        }

        string unit = input[consumed..];
        if (unit != "ms")
        {
            throw new FormatException($"Unsupported measurement unit: {unit}");
        }

        output.WriteLine($"Numeric prefix: {value:R}");
        output.WriteLine($"Consumed characters: {consumed}");
        output.WriteLine($"Validated unit: {unit}");
        output.WriteLine($"Whole-value TryParse: {double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out _)}");

        BigInteger big = BigInteger.Parse("123456789012345678901234567890"u8, CultureInfo.InvariantCulture);
        Span<byte> utf8 = stackalloc byte[64];
        if (!big.TryFormat(utf8, out int written, default, CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("The BigInteger UTF-8 buffer is too small.");
        }

        output.WriteLine($"BigInteger UTF-8: {Encoding.UTF8.GetString(utf8[..written])}");

        Complex complex = new(3, 4);
        if (!complex.TryFormat(utf8, out written, default, CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("The Complex UTF-8 buffer is too small.");
        }

        Complex parsed = Complex.Parse(utf8[..written], CultureInfo.InvariantCulture);
        output.WriteLine($"Complex UTF-8: {Encoding.UTF8.GetString(utf8[..written])}");
        output.WriteLine($"Complex round-trip: {parsed == complex}");

        double hex = double.Parse("0x1.8p+1", NumberStyles.HexFloat, CultureInfo.InvariantCulture);
        output.WriteLine($"Hexadecimal 0x1.8p+1: {hex:R}");
    }
}
