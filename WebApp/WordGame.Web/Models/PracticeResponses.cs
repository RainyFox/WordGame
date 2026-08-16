namespace WordGame.Web.Models;

public sealed record PracticeOptionsResponse(
    int MinNumber,
    int MaxNumber,
    IReadOnlyList<string> Types);

public sealed record StartPracticeResponse(
    Guid SessionId,
    PracticeQuestionResponse Question);

public sealed record PracticeQuestionResponse(
    int Number,
    string Prompt,
    int Round,
    int Position,
    int Total);

public sealed record PracticeAnswerResponse(
    bool IsCorrect,
    PracticeRevealResponse? Reveal);

public sealed record PracticeRevealResponse(
    string Answer,
    string Translation,
    string Example);
