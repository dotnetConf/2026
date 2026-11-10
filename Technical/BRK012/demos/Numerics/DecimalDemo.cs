using System.Globalization;
using System.Numerics;

namespace DotNetConf.NumericsDemos;

internal static class DecimalDemo
{
    public static void Run(TextWriter output)
    {
        Decimal64 a = Decimal64.Parse("0.1", CultureInfo.InvariantCulture);
        Decimal64 b = Decimal64.Parse("0.2", CultureInfo.InvariantCulture);
        output.WriteLine($"Double sum: {0.1 + 0.2:R}");
        output.WriteLine($"Decimal64 sum: {a + b}");

        Decimal64 oneDecimalPlace = Decimal64.Parse("1.0", CultureInfo.InvariantCulture);
        Decimal64 twoDecimalPlaces = Decimal64.Parse("1.00", CultureInfo.InvariantCulture);
        output.WriteLine($"Same value: {oneDecimalPlace == twoDecimalPlaces}");
        output.WriteLine($"HaveSameQuantum: {Decimal64.HaveSameQuantum(oneDecimalPlace, twoDecimalPlaces)}");
        output.WriteLine($"Quantum of 1.0: {Decimal64.GetQuantum(oneDecimalPlace)}");
        output.WriteLine($"Quantum of 1.00: {Decimal64.GetQuantum(twoDecimalPlaces)}");

        Decimal64 cents = Decimal64.Parse("0.01", CultureInfo.InvariantCulture);
        foreach (string text in new[] { "1.235", "1.245" })
        {
            Decimal64 value = Decimal64.Parse(text, CultureInfo.InvariantCulture);
            output.WriteLine($"Quantize {text}: {Decimal64.Quantize(value, cents)}");
        }

        output.WriteLine($"IEEE divide by zero: {Decimal64.One / Decimal64.Zero}");

        Decimal128 nearOne = Decimal128.One - Decimal128.Parse("1e-30", CultureInfo.InvariantCulture);
        output.WriteLine($"Double input after conversion: {(double)nearOne:R}");
        output.WriteLine($"Double Acos: {Math.Acos((double)nearOne):R}");
        output.WriteLine($"Decimal128 Acos: {Decimal128.Acos(nearOne)}");
    }
}
