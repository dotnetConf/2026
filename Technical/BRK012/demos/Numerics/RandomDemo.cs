using System.Numerics;

namespace DotNetConf.NumericsDemos;

internal static class RandomDemo
{
    public static void Run(TextWriter output)
    {
        Random rng = new(42);
        BigInteger limit = BigInteger.One << 80;
        BigInteger[] integers = new BigInteger[4];
        Half[] fractions = new Half[4];
        for (int i = 0; i < integers.Length; i++)
        {
            integers[i] = rng.NextInteger<BigInteger>(limit);
            fractions[i] = rng.NextBinaryFloat<Half>();
        }

        output.WriteLine($"Integer limit: {limit}");
        output.WriteLine($"Integer samples: [{string.Join(", ", integers)}]");
        output.WriteLine($"Half samples: [{string.Join(", ", fractions.Select(value => ((double)value).ToString("R")))}]");
        output.WriteLine($"Zero maximum: {rng.NextInteger<int>(0)}");
        output.WriteLine($"Equal bounds: {rng.NextInteger<int>(7, 7)}");
        output.WriteLine("Bounded maxima are exclusive. These APIs are not cryptographic.");
    }
}
