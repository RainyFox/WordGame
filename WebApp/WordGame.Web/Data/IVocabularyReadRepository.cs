using WordGame.Web.Models;

namespace WordGame.Web.Data;

public interface IVocabularyReadRepository
{
    Task<VocabularySummary> GetSummaryAsync(CancellationToken cancellationToken);
}
