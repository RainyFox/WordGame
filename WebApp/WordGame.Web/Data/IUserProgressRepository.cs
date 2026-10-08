using WordGame.Web.Models;

namespace WordGame.Web.Data;

public interface IUserProgressRepository
{
    Task<int> CountAnswersAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);

    Task<UserProgressRecord> RecordAnswerAsync(
        int number,
        PracticeDirection direction,
        bool isCorrect,
        DateTimeOffset answeredAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewCandidateProgress>> GetReviewCandidatesAsync(
        VocabularyFilter filter,
        PracticeDirection direction,
        CancellationToken cancellationToken);
}
