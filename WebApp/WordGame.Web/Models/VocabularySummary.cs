namespace WordGame.Web.Models;

public sealed record VocabularySummary(int Count, IReadOnlyList<string> Types);
