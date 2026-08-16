using WordGame.Web.Models;

namespace WordGame.Web.Services;

public interface IMultipleChoiceService
{
    Task<IReadOnlyList<string>> CreateChoicesAsync(
        VocabularyEntry question,
        PracticeDirection direction,
        CancellationToken cancellationToken);
}
