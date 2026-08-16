namespace WordGame.Web.Models;

public sealed record VocabularyFilter(
    int MinNumber,
    int MaxNumber,
    string? Type);
