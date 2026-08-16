using System.Globalization;
using Microsoft.Data.Sqlite;
using WordGame.Web.Models;

namespace WordGame.Web.Data;

public sealed class SqliteUserProgressRepository(
    IReadOnlyWordGameConnectionFactory readConnectionFactory,
    IReadWriteWordGameConnectionFactory writeConnectionFactory)
    : IUserProgressRepository
{
    const int MaximumProficiency = 5;
    const int IncorrectAnswerReviewDelayDays = 1;
    const string ReadProgressSql = """
        SELECT COALESCE(Proficiency, 0),
               COALESCE(TotalCorrect, 0),
               COALESCE(TotalWrong, 0)
        FROM UserProgress
        WHERE 番号 = $number AND Mode = $mode
        """;
    const string UpsertProgressSql = """
        INSERT INTO UserProgress
            (番号, Proficiency, LastAnswer, NextReview, TotalCorrect, TotalWrong, Mode)
        VALUES
            ($number, $proficiency, $lastAnswer, $nextReview,
             $totalCorrect, $totalWrong, $mode)
        ON CONFLICT(番号, Mode) DO UPDATE SET
            Proficiency = excluded.Proficiency,
            LastAnswer = excluded.LastAnswer,
            NextReview = excluded.NextReview,
            TotalCorrect = excluded.TotalCorrect,
            TotalWrong = excluded.TotalWrong
        """;
    const string ReviewCandidatesSql = """
        SELECT V.番号, U.LastAnswer, U.NextReview
        FROM Vocabulary AS V
        LEFT JOIN UserProgress AS U
          ON V.番号 = U.番号 AND U.Mode = $mode
        WHERE V.番号 BETWEEN $minNumber AND $maxNumber
          AND ($type IS NULL OR TRIM(V.タイプ) = $type)
        ORDER BY V.番号
        """;

    public async Task<UserProgressRecord> RecordAnswerAsync(
        int number,
        PracticeDirection direction,
        bool isCorrect,
        DateTimeOffset answeredAt,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await writeConnectionFactory.OpenAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        StoredProgress current = await ReadProgressAsync(
            connection,
            transaction,
            number,
            direction,
            cancellationToken);
        UserProgressRecord updated = CalculateUpdatedProgress(
            number,
            direction,
            current,
            isCorrect,
            answeredAt);
        await WriteProgressAsync(connection, transaction, updated, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<IReadOnlyList<ReviewCandidateProgress>> GetReviewCandidatesAsync(
        VocabularyFilter filter,
        PracticeDirection direction,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await readConnectionFactory.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = ReviewCandidatesSql;
        command.Parameters.AddWithValue("$mode", direction.ToString());
        command.Parameters.AddWithValue("$minNumber", filter.MinNumber);
        command.Parameters.AddWithValue("$maxNumber", filter.MaxNumber);
        command.Parameters.AddWithValue("$type", (object?)filter.Type ?? DBNull.Value);

        return await ReadReviewCandidatesAsync(command, cancellationToken);
    }

    static async Task<StoredProgress> ReadProgressAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int number,
        PracticeDirection direction,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ReadProgressSql;
        command.Parameters.AddWithValue("$number", number);
        command.Parameters.AddWithValue("$mode", direction.ToString());

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredProgress(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : new StoredProgress(0, 0, 0);
    }

    static UserProgressRecord CalculateUpdatedProgress(
        int number,
        PracticeDirection direction,
        StoredProgress current,
        bool isCorrect,
        DateTimeOffset answeredAt)
    {
        int proficiency = isCorrect
            ? Math.Min(current.Proficiency + 1, MaximumProficiency)
            : Math.Max(current.Proficiency - 1, 0);
        int totalCorrect = current.TotalCorrect + (isCorrect ? 1 : 0);
        int totalWrong = current.TotalWrong + (isCorrect ? 0 : 1);
        double reviewDelayDays = isCorrect
            ? Math.Pow(2, proficiency)
            : IncorrectAnswerReviewDelayDays;
        DateTimeOffset normalizedAnswerTime = answeredAt.ToUniversalTime();

        return new UserProgressRecord(
            number,
            direction,
            proficiency,
            normalizedAnswerTime,
            normalizedAnswerTime.AddDays(reviewDelayDays),
            totalCorrect,
            totalWrong);
    }

    static async Task WriteProgressAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UserProgressRecord progress,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertProgressSql;
        command.Parameters.AddWithValue("$number", progress.Number);
        command.Parameters.AddWithValue("$proficiency", progress.Proficiency);
        command.Parameters.AddWithValue("$lastAnswer", FormatTimestamp(progress.LastAnswer));
        command.Parameters.AddWithValue("$nextReview", FormatTimestamp(progress.NextReview));
        command.Parameters.AddWithValue("$totalCorrect", progress.TotalCorrect);
        command.Parameters.AddWithValue("$totalWrong", progress.TotalWrong);
        command.Parameters.AddWithValue("$mode", progress.Direction.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    static async Task<IReadOnlyList<ReviewCandidateProgress>> ReadReviewCandidatesAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        var candidates = new List<ReviewCandidateProgress>();
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ReviewCandidateProgress(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return candidates;
    }

    static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    sealed record StoredProgress(
        int Proficiency,
        int TotalCorrect,
        int TotalWrong);
}
