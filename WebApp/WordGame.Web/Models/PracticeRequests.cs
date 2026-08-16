namespace WordGame.Web.Models;

public sealed record StartPracticeRequest(
    int MinNumber,
    int MaxNumber,
    string? Type,
    PracticeDirection Direction);

public sealed record SubmitPracticeAnswerRequest(string? Answer);
