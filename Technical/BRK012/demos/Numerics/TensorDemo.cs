using System.Numerics.Tensors;

namespace DotNetConf.NumericsDemos;

internal static class TensorDemo
{
    public static void Run(TextWriter output)
    {
        int[] storage = [1, 2, -99, 3, 4, -99];
        ReadOnlyTensorSpan<int> gapped = new(storage, [2, 2], [3, 1]);
        Span<int> flattened = stackalloc int[4];
        gapped.FlattenTo(flattened);
        output.WriteLine($"Gapped storage: {Format(storage)}");
        output.WriteLine($"IsDense: {gapped.IsDense}");
        output.WriteLine($"Flattened view: {Format(flattened)}");

        int[] input = [1, 2];
        int[] resized = [99, 99, 99, 99, 99];
        Tensor.ResizeTo(new ReadOnlyTensorSpan<int>(input), new TensorSpan<int>(resized));
        output.WriteLine($"Resize: {Format(input)} -> {Format(resized)}");

        int[] denseOverlap = [1, 2, 3, 4, 5];
        ReadOnlyTensorSpan<int> denseSource = new(denseOverlap.AsSpan(0, 4));
        TensorSpan<int> denseDestination = new(denseOverlap.AsSpan(1, 4));
        denseSource.CopyTo(denseDestination);
        output.WriteLine($"Overlapping dense copy: {Format(denseOverlap)}");

        int[] overlap = [1, 2, 3, 4, 5, 6];
        int[] before = (int[])overlap.Clone();
        ReadOnlyTensorSpan<int> stridedSource = new(overlap, [3], [2]);
        TensorSpan<int> overlappingDestination = new(overlap.AsSpan(1, 3));
        try
        {
            stridedSource.CopyTo(overlappingDestination);
            throw new InvalidOperationException("The unsupported overlapping copy was not rejected.");
        }
        catch (ArgumentException)
        {
            if (!overlap.AsSpan().SequenceEqual(before))
            {
                throw new InvalidOperationException("The rejected copy modified its destination.");
            }

            output.WriteLine("Strided overlap: ArgumentException; buffer unchanged");
        }

        float[] withNaNs = [1, 4, float.NaN, 4, float.NaN];
        float[] withTies = [1, 4, 4];
        output.WriteLine($"First NaN index: {Tensor.IndexOfMax(new ReadOnlyTensorSpan<float>(withNaNs))}");
        output.WriteLine($"First maximum tie index: {Tensor.IndexOfMax(new ReadOnlyTensorSpan<float>(withTies))}");
    }

    private static string Format(ReadOnlySpan<int> values)
        => $"[{string.Join(", ", values.ToArray())}]";
}
