using Microsoft.Data.Sqlite;
using WordGame.Web.Data;
using WordGame.Web.Models;
using WordGame.Web.Tests.Support;

namespace WordGame.Web.Tests;

public sealed class SqliteUserProgressRepositoryTests
{
    static readonly DateTimeOffset AnsweredAt =
        new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CountAnswersAsync_ExistingDatabaseWithoutHistoryReturnsZeroWithoutWriting()
    {
        using var database = new TemporaryWordGameDatabase();
        byte[] before = database.ComputeHash();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);

        int count = await repository.CountAnswersAsync(
            AnsweredAt.AddHours(-12), AnsweredAt.AddHours(12), CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Equal(before, database.ComputeHash());
    }

    [Fact]
    public async Task CountAnswersAsync_CountsRepeatedWordsAndBothDirectionsAfterRepositoryRestart()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);
        await repository.RecordAnswerAsync(
            2, PracticeDirection.JpToCn, true, AnsweredAt, CancellationToken.None);
        await repository.RecordAnswerAsync(
            2, PracticeDirection.JpToCn, false, AnsweredAt, CancellationToken.None);
        await repository.RecordAnswerAsync(
            2, PracticeDirection.CnToJp, true, AnsweredAt, CancellationToken.None);

        SqliteUserProgressRepository restarted = CreateRepository(database.DatabasePath);
        int count = await restarted.CountAnswersAsync(
            AnsweredAt.AddHours(-12), AnsweredAt.AddHours(12), CancellationToken.None);

        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData("2026-08-16T00:00:00+09:00", "2026-08-17T00:00:00+09:00")]
    [InlineData("2026-03-08T00:00:00-05:00", "2026-03-09T00:00:00-04:00")]
    [InlineData("2026-11-01T00:00:00-04:00", "2026-11-02T00:00:00-05:00")]
    public async Task CountAnswersAsync_UsesLocalMidnightIncludingDaylightSavingBoundaries(
        string startTimestamp,
        string endTimestamp)
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);
        DateTimeOffset start = DateTimeOffset.Parse(startTimestamp);
        DateTimeOffset end = DateTimeOffset.Parse(endTimestamp);
        foreach (DateTimeOffset timestamp in new[]
                 { start.AddTicks(-1), start, end.AddTicks(-1), end })
        {
            await repository.RecordAnswerAsync(
                2, PracticeDirection.JpToCn, true, timestamp, CancellationToken.None);
        }

        int count = await repository.CountAnswersAsync(start, end, CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task RecordAnswerAsync_HistoryFailureRollsBackProgressAndCountTogether()
    {
        using var database = new TemporaryWordGameDatabase();
        SqliteUserProgressRepository repository = CreateRepository(database.DatabasePath);
        await repository.RecordAnswerAsync(
            2, PracticeDirection.JpToCn, true, AnsweredAt, CancellationToken.None);
        StoredProgressRow? before = database.ReadProgress(2, PracticeDirection.JpToCn);
        using (var connection = new SqliteConnection($"Data Source={database.DatabasePath};Pooling=False"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER FailHistoryInsert BEFORE INSERT ON PracticeHistory
                BEGIN SELECT RAISE(ABORT, 'test history failure'); END;
                """;
            command.ExecuteNonQuery();
        }

        await Assert.ThrowsAsync<SqliteException>(() => repository.RecordAnswerAsync(
            2, PracticeDirection.JpToCn, false, AnsweredAt.AddMinutes(1), CancellationToken.None));

        Assert.Equal(before, database.ReadProgress(2, PracticeDirection.JpToCn));
        Assert.Equal(1, await repository.CountAnswersAsync(
            AnsweredAt.AddHours(-12), AnsweredAt.AddHours(12), CancellationToken.None));
    }

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
