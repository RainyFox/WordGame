namespace WordGame.Web.Models;

public sealed record VocabularyEntry(
    int Number,
    string Word,
    string Kana,
    string Chinese,
    string Example,
    string Type);
