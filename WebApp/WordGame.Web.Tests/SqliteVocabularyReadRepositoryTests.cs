using WordGame.Web.Data;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class SqliteVocabularyReadRepositoryTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsCountAndDistinctTypesWithoutWritingDatabase()
    {
        using var database = new TemporaryWordGameDatabase();
        byte[] hashBeforeRead = database.ComputeHash();
        var repository = new SqliteVocabularyReadRepository(
            new FixedDatabasePathResolver(database.DatabasePath));

        VocabularySummary summary = await repository.GetSummaryAsync(
            CancellationToken.None);

        Assert.Equal(4, summary.Count);
        Assert.Equal(new[] { "テキスト", "文法", "通常" }, summary.Types);
        Assert.Equal(hashBeforeRead, database.ComputeHash());
    }

    [Fact]
    public async Task GetSummaryAsync_ThrowsWhenDatabaseDoesNotExist()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var repository = new SqliteVocabularyReadRepository(
            new FixedDatabasePathResolver(missingPath));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            repository.GetSummaryAsync(CancellationToken.None));
    }
}
