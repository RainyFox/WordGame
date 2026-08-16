namespace WordGame.Web.Services;

public sealed class SystemRandomSource : IRandomSource
{
    public double NextDouble()
    {
        return Random.Shared.NextDouble();
    }

    public int NextInt(int exclusiveMaximum)
    {
        return Random.Shared.Next(exclusiveMaximum);
    }
}
