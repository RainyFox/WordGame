using WordGame.Web.Data;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class SqlitePracticeVocabularyRepositoryTests
{
    [Fact]
    public async Task ReadMethods_ReturnOptionsFilteredCandidatesAndVocabulary()
    {
        using var database = new TemporaryWordGameDatabase();
        byte[] hashBeforeRead = database.ComputeHash();
        SqlitePracticeVocabularyRepository repository = CreateRepository(database.DatabasePath);

        PracticeOptionsResponse options = await repository.GetOptionsAsync(
            CancellationToken.None);
        IReadOnlyList<int> candidates = await repository.GetCandidateNumbersAsync(
            new VocabularyFilter(1, 4, "通常"),
            CancellationToken.None);
        VocabularyEntry? vocabulary = await repository.GetByNumberAsync(
            1,
            CancellationToken.None);

        Assert.Equal(1, options.MinNumber);
        Assert.Equal(4, options.MaxNumber);
        Assert.Equal(new[] { "テキスト", "文法", "通常" }, options.Types);
        Assert.Equal(new[] { 1, 4 }, candidates);
        Assert.NotNull(vocabulary);
        Assert.Equal("整う", vocabulary.Word);
        Assert.Equal("ととのう", vocabulary.Kana);
        Assert.Equal(hashBeforeRead, database.ComputeHash());
    }

    [Fact]
    public async Task GetCandidateNumbersAsync_UsesActualVocabularyNumbers()
    {
        using var database = new TemporaryWordGameDatabase();
        SqlitePracticeVocabularyRepository repository = CreateRepository(database.DatabasePath);

        IReadOnlyList<int> candidates = await repository.GetCandidateNumbersAsync(
            new VocabularyFilter(2, 3, null),
            CancellationToken.None);

        Assert.Equal(new[] { 2, 3 }, candidates);
    }

    static SqlitePracticeVocabularyRepository CreateRepository(string databasePath)
    {
        var connectionFactory = new SqliteReadOnlyWordGameConnectionFactory(
            new FixedDatabasePathResolver(databasePath));
        return new SqlitePracticeVocabularyRepository(connectionFactory);
    }
}
