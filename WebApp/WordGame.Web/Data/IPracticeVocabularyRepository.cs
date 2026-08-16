using WordGame.Web.Models;

namespace WordGame.Web.Data;

public interface IPracticeVocabularyRepository
{
    Task<PracticeOptionsResponse> GetOptionsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetCandidateNumbersAsync(
        VocabularyFilter filter,
        CancellationToken cancellationToken);

    Task<VocabularyEntry?> GetByNumberAsync(
        int number,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetDistractorAnswersAsync(
        VocabularyEntry question,
        PracticeDirection direction,
        CancellationToken cancellationToken);
}
