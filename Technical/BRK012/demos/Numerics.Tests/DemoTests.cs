using System.Globalization;
using System.Numerics;
using System.Numerics.Tensors;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using DotNetConf.NumericsDemos;

namespace Numerics.Tests;

public class DemoTests
{
    [Theory]
    [InlineData("decimal", "Double sum: 0.30000000000000004")]
    [InlineData("decimal", "Decimal64 sum: 0.3")]
    [InlineData("decimal", "Same value: True")]
    [InlineData("decimal", "HaveSameQuantum: False")]
    [InlineData("decimal", "Quantum of 1.0: 0.1")]
    [InlineData("decimal", "Quantum of 1.00: 0.01")]
    [InlineData("decimal", "Quantize 1.235: 1.24")]
    [InlineData("decimal", "Quantize 1.245: 1.24")]
    [InlineData("decimal", "IEEE divide by zero: Infinity")]
    [InlineData("decimal", "Double Acos: 0")]
    [InlineData("bfloat16", "Range: input=70000 | Half=Infinity | BFloat16=70144")]
    [InlineData("bfloat16", "Precision: input=1.0009765625 | Half=1.0009765625 | BFloat16=1")]
    [InlineData("bfloat16", "BFloat16 direct: 1.0078125")]
    [InlineData("bfloat16", "BFloat16 via double: 1")]
    [InlineData("bfloat16", "Half direct: 1.0009765625")]
    [InlineData("bfloat16", "Half via double: 1")]
    [InlineData("complex", "Square Decimal128: 1.5625")]
    [InlineData("complex", "Square Complex<float>: real=-7, imaginary=24")]
    [InlineData("complex", "Float magnitude: 5")]
    [InlineData("complex", "Square Complex<Decimal128>: real=-7, imaginary=24")]
    [InlineData("tensors", "Flattened view: [1, 2, 3, 4]")]
    [InlineData("tensors", "Resize: [1, 2] -> [1, 2, 0, 0, 0]")]
    [InlineData("tensors", "Overlapping dense copy: [1, 1, 2, 3, 4]")]
    [InlineData("tensors", "Strided overlap: ArgumentException; buffer unchanged")]
    [InlineData("tensors", "First NaN index: 2")]
    [InlineData("tensors", "First maximum tie index: 1")]
    [InlineData("random", "Zero maximum: 0")]
    [InlineData("random", "Equal bounds: 7")]
    [InlineData("text", "Numeric prefix: 12.5")]
    [InlineData("text", "Consumed characters: 4")]
    [InlineData("text", "Validated unit: ms")]
    [InlineData("text", "Whole-value TryParse: False")]
    [InlineData("text", "BigInteger UTF-8: 123456789012345678901234567890")]
    [InlineData("text", "Complex round-trip: True")]
    [InlineData("text", "Hexadecimal 0x1.8p+1: 3")]
    public void ScenarioProducesExpectedOutput(string name, string expected)
    {
        var result = RunDemo(name);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains(expected, result.Output.Split(Environment.NewLine));
        Assert.StartsWith($"Runtime: {RuntimeInformation.FrameworkDescription}", result.Output);
    }

    [Fact]
    public void DemoStillTargetsNet11()
    {
        TargetFrameworkAttribute? target = typeof(DemoApp).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
        Assert.NotNull(target);
        Assert.Equal(".NETCoreApp,Version=v11.0", target.FrameworkName);
    }

    [Fact]
    public void DemoUsesNet11GaRuntime()
    {
        Assert.Equal(11, Environment.Version.Major);
        Assert.Equal($".NET {Environment.Version}", RuntimeInformation.FrameworkDescription);
    }

    [Fact]
    public void TensorDemoUsesTheGaPackage()
    {
        var result = RunDemo("tensors");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("11.0.0", Value(result.Output, "Tensor build").Split('+')[0]);
        Assert.Equal(new Version(11, 0, 0, 0), typeof(Tensor).Assembly.GetName().Version);
    }

    [Fact]
    public void DecimalEndpointRetainsItsResidual()
    {
        var result = RunDemo("decimal");
        Assert.Equal(0, result.ExitCode);
        double acos = double.Parse(Value(result.Output, "Decimal128 Acos"), CultureInfo.InvariantCulture);
        double reference = Math.Sqrt(2e-30);
        Assert.InRange(Math.Abs(acos - reference) / reference, 0, 1e-14);
    }

    [Fact]
    public void ComplexMagnitudeAndPhaseHaveTheExpectedValues()
    {
        var result = RunDemo("complex");
        Assert.Equal(0, result.ExitCode);
        Decimal128 magnitude = Decimal128.Parse(Value(result.Output, "Decimal magnitude"), CultureInfo.InvariantCulture);
        Assert.Equal(Decimal128.CreateChecked(5), magnitude);
        double phase = double.Parse(Value(result.Output, "Float phase"), CultureInfo.InvariantCulture);
        Assert.InRange(Math.Abs(phase - Math.Atan2(4, 3)), 0, 1e-6);
    }

    [Fact]
    public void RandomSamplesRespectTheActualBounds()
    {
        var result = RunDemo("random");
        Assert.Equal(0, result.ExitCode);
        BigInteger limit = BigInteger.Parse(Value(result.Output, "Integer limit"), CultureInfo.InvariantCulture);
        Assert.Equal(BigInteger.One << 80, limit);

        string[] integers = Samples(result.Output, "Integer samples");
        string[] fractions = Samples(result.Output, "Half samples");
        Assert.Equal(4, integers.Length);
        Assert.Equal(4, fractions.Length);
        foreach (string text in integers)
        {
            BigInteger value = BigInteger.Parse(text, CultureInfo.InvariantCulture);
            Assert.True(value >= 0 && value < limit);
        }

        foreach (string text in fractions)
        {
            double value = double.Parse(text, CultureInfo.InvariantCulture);
            Assert.True(value >= 0 && value < 1);
            Assert.Equal(value, (double)(Half)value);
        }
    }

    [Theory]
    [InlineData("all")]
    [InlineData("")]
    public void AllRunsEveryScenarioInPresentationOrder(string selection)
    {
        var result = selection.Length == 0 ? RunDemo() : RunDemo(selection);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        string[] names = Regex.Matches(result.Output, @"^=== (.+) ===\r?$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(["decimal", "bfloat16", "complex", "tensors", "random", "text"], names);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void HelpListsTheDemosWithoutRunningThem(string selection)
    {
        var result = RunDemo(selection);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("decimal | bfloat16 | complex | tensors | random | text | all", result.Output);
        Assert.DoesNotContain("===", result.Output);
    }

    [Fact]
    public void InvalidSelectionFailsExplicitly()
    {
        var result = RunDemo("unknown");
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Unknown demo: unknown", result.Error);
    }

    [Fact]
    public void ExtraArgumentsFailExplicitly()
    {
        var result = RunDemo("decimal", "unexpected");
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Select one demo or 'all'.", result.Error);
    }

    [Fact]
    public void NumericOutputIsInvariantAndTheCallerCultureIsRestored()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = RunDemo("DECIMAL");
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Decimal64 sum: 0.3", result.Output);
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static (int ExitCode, string Output, string Error) RunDemo(params string[] args)
    {
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        int exitCode = DemoApp.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string Value(string output, string name)
    {
        string prefix = $"{name}: ";
        return output.Split(Environment.NewLine).Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
    }

    private static string[] Samples(string output, string name)
        => Value(output, name).Trim('[', ']').Split(", ");
}
