using System.Globalization;
using System.Numerics;

namespace DotNetConf.NumericsDemos;

internal static class ComplexDemo
{
    public static void Run(TextWriter output)
    {
        Decimal128 scalar = Decimal128.Parse("1.25", CultureInfo.InvariantCulture);
        output.WriteLine($"Square Decimal128: {Square(scalar)}");

        Complex<float> binary = new(3, 4);
        Complex<float> binarySquare = Square(binary);
        output.WriteLine($"Square Complex<float>: real={binarySquare.Real:R}, imaginary={binarySquare.Imaginary:R}");
        output.WriteLine($"Float magnitude: {binary.GetMagnitude():R}");
        output.WriteLine($"Float phase: {binary.GetPhase():R}");

        Complex<Decimal128> decimalComplex = new(Decimal128.CreateChecked(3), Decimal128.CreateChecked(4));
        Complex<Decimal128> decimalSquare = Square(decimalComplex);
        output.WriteLine($"Square Complex<Decimal128>: real={decimalSquare.Real}, imaginary={decimalSquare.Imaginary}");
        output.WriteLine($"Decimal magnitude: {decimalComplex.GetMagnitude()}");
    }

    private static T Square<T>(T value) where T : INumberBase<T>
        => value * value;
}
