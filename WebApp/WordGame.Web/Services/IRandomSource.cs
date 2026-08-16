namespace WordGame.Web.Services;

public interface IRandomSource
{
    double NextDouble();

    int NextInt(int exclusiveMaximum);
}
