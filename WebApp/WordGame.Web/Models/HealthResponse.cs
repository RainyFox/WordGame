namespace WordGame.Web.Models;

public sealed record HealthResponse(
    string Status,
    string DatabasePath,
    int VocabularyCount);
