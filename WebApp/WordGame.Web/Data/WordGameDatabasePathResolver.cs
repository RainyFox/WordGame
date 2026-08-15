using Microsoft.Extensions.Options;

namespace WordGame.Web.Data;

public sealed class WordGameDatabasePathResolver(
    IHostEnvironment environment,
    IOptions<WordGameDatabaseOptions> options) : IWordGameDatabasePathResolver
{
    public string GetDatabasePath()
    {
        string? configuredPath = options.Value.Path;
        string databasePath = string.IsNullOrWhiteSpace(configuredPath)
            ? GetDefaultDatabasePath(environment.ContentRootPath)
            : ResolveConfiguredPath(environment.ContentRootPath, configuredPath);

        return Path.GetFullPath(databasePath);
    }

    static string GetDefaultDatabasePath(string contentRootPath)
    {
        return Path.Combine(contentRootPath, "..", "..", "WordGame.db");
    }

    static string ResolveConfiguredPath(string contentRootPath, string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(contentRootPath, configuredPath);
    }
}
