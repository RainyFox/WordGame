using WordGame.Web.Data;

namespace WordGame.Web.Tests.Support;

internal sealed class FixedDatabasePathResolver(string databasePath)
    : IWordGameDatabasePathResolver
{
    public string GetDatabasePath()
    {
        return databasePath;
    }
}
