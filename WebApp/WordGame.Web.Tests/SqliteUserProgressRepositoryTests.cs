using WordGame.Web.Data;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class SqliteUserProgressRepositoryTests
{
    static readonly DateTimeOffset AnsweredAt =
        new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordAnswerAsync_CorrectAnswerRaisesProficiencyAndUsesNewDelay()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);

        UserProgressRecord result = await repository.RecordAnswerAsync(
            1,
            PracticeDirection.JpToCn,
            true,
            AnsweredAt,
            CancellationToken.None);

        Assert.Equal(4, result.Proficiency);
        Assert.Equal(5, result.TotalCorrect);
        Assert.Equal(1, result.TotalWrong);
        Assert.Equal(AnsweredAt.AddDays(16), result.NextReview);
        Assert.Equal(4, database.ReadProgress(1, PracticeDirection.JpToCn)!.Proficiency);
    }

    [Fact]
    public async Task RecordAnswerAsync_WrongAnswerCreatesOneDayReview()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);

        UserProgressRecord result = await repository.RecordAnswerAsync(
            2,
            PracticeDirection.JpToCn,
            false,
            AnsweredAt,
            CancellationToken.None);

        Assert.Equal(0, result.Proficiency);
        Assert.Equal(0, result.TotalCorrect);
        Assert.Equal(1, result.TotalWrong);
        Assert.Equal(AnsweredAt.AddDays(1), result.NextReview);
    }

    [Fact]
    public async Task RecordAnswerAsync_CorrectAnswerStopsAtMaximumProficiency()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);

        await repository.RecordAnswerAsync(
            1,
            PracticeDirection.JpToCn,
            true,
            AnsweredAt,
            CancellationToken.None);
        await repository.RecordAnswerAsync(
            1,
            PracticeDirection.JpToCn,
            true,
            AnsweredAt,
            CancellationToken.None);
        UserProgressRecord result = await repository.RecordAnswerAsync(
            1,
            PracticeDirection.JpToCn,
            true,
            AnsweredAt,
            CancellationToken.None);

        Assert.Equal(5, result.Proficiency);
        Assert.Equal(7, result.TotalCorrect);
        Assert.Equal(AnsweredAt.AddDays(32), result.NextReview);
    }

    [Fact]
    public async Task RecordAnswerAsync_WrongAnswerReducesPositiveProficiency()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);

        UserProgressRecord result = await repository.RecordAnswerAsync(
            1,
            PracticeDirection.JpToCn,
            false,
            AnsweredAt,
            CancellationToken.None);

        Assert.Equal(2, result.Proficiency);
        Assert.Equal(4, result.TotalCorrect);
        Assert.Equal(2, result.TotalWrong);
        Assert.Equal(AnsweredAt.AddDays(1), result.NextReview);
    }

    [Fact]
    public async Task RecordAnswerAsync_KeepsDirectionsIndependent()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);

        UserProgressRecord result = await repository.RecordAnswerAsync(
            1,
            PracticeDirection.CnToJp,
            true,
            AnsweredAt,
            CancellationToken.None);

        Assert.Equal(1, result.Proficiency);
        Assert.Equal(AnsweredAt.AddDays(2), result.NextReview);
        Assert.Equal(3, database.ReadProgress(1, PracticeDirection.JpToCn)!.Proficiency);
        Assert.Equal(1, database.ReadProgress(1, PracticeDirection.CnToJp)!.Proficiency);
    }

    static SqliteUserProgressRepository CreateRepository(string databasePath)
    {
        var pathResolver = new FixedDatabasePathResolver(databasePath);
        return new SqliteUserProgressRepository(
            new SqliteReadOnlyWordGameConnectionFactory(pathResolver),
            new SqliteReadWriteWordGameConnectionFactory(pathResolver));
    }
}
