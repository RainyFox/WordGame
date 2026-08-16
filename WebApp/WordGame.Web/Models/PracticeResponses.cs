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
    IReadOnlyList<string> Choices,
    PracticeMode Mode,
    int Round,
    int Position,
    int Total);

public sealed record PracticeAnswerResponse(
    bool IsCorrect,
    PracticeOutcome? RecordedOutcome,
    PracticeRevealResponse? Reveal,
    UserProgressResponse? Progress);

public sealed record PracticeRevealResponse(
    string Answer,
    string Translation,
    string Example);

public sealed record UserProgressResponse(
    int Proficiency,
    DateTimeOffset LastAnswer,
    DateTimeOffset NextReview,
    int TotalCorrect,
    int TotalWrong);
