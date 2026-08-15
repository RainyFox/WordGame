namespace WordGame.Web.Data;

public sealed class WordGameDatabaseOptions
{
    public const string SectionName = "Database";

    public string? Path { get; set; }
}
