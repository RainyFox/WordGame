using WordGame.Web.Services;

namespace WordGame.Web.Tests.Support;

internal sealed class SequenceRandomSource(
    IEnumerable<double>? doubleValues = null,
    IEnumerable<int>? intValues = null) : IRandomSource
{
    readonly Queue<double> doubleValues = new(doubleValues ?? []);
    readonly Queue<int> intValues = new(intValues ?? []);

    public double NextDouble()
    {
        return doubleValues.Count > 0 ? doubleValues.Dequeue() : 0;
    }

    public int NextInt(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        int value = intValues.Count > 0 ? intValues.Dequeue() : 0;
        return Math.Clamp(value, 0, exclusiveMaximum - 1);
    }
}
