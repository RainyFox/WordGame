using Microsoft.Extensions.Options;

namespace WordGame.Web.Data;

public enum WordGameDatabaseSource
{
    Local,
    Turso
}

public sealed class WordGameDatabaseSelection
{
    public WordGameDatabaseSelection(IOptions<WordGameDatabaseOptions> options)
    {
        DefaultSource = options.Value.UseTurso
            ? WordGameDatabaseSource.Turso
            : WordGameDatabaseSource.Local;
        Source = DefaultSource;
        TursoConfigured = options.Value.IsTursoConfigured();
    }

    public WordGameDatabaseSource DefaultSource { get; }
    public WordGameDatabaseSource Source { get; set; }
    public bool TursoConfigured { get; }
}
