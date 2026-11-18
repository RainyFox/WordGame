namespace WordGame.Web.Data;

public sealed class WordGameDatabaseOptions
{
    public const string SectionName = "Database";

    public string? Path { get; set; }
    public bool UseTurso { get; set; }
    public string? TursoUrl { get; set; }
    public string? TursoAuthToken { get; set; }

    public bool IsTursoConfigured()
    {
        return !string.IsNullOrWhiteSpace(TursoAuthToken)
            && Uri.TryCreate(TursoUrl, UriKind.Absolute, out Uri? url)
            && url.Scheme is "libsql" or "https"
            && url.Host.Length > 0
            && url.UserInfo.Length == 0
            && url.AbsolutePath is "" or "/"
            && url.Query.Length == 0
            && url.Fragment.Length == 0;
    }
}
