namespace WordGame.Web.Models;

public sealed record UserProgressRecord(
    int Number,
    PracticeDirection Direction,
    int Proficiency,
    DateTimeOffset LastAnswer,
    DateTimeOffset NextReview,
    int TotalCorrect,
    int TotalWrong);

public sealed record ReviewCandidateProgress(
    int Number,
    string? LastAnswer,
    string? NextReview);

public sealed record ReviewCandidate(
    int Number,
    bool IsNew,
    DateTimeOffset? NextReview,
    double SelectionWeight);
