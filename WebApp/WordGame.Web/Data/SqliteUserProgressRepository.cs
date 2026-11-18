using System.Data.Common;
using System.Globalization;
using WordGame.Web.Models;
using WordGame.Web.Services;

namespace WordGame.Web.Data;

public sealed class SqliteUserProgressRepository(
    IReadOnlyWordGameConnectionFactory readConnectionFactory,
    IReadWriteWordGameConnectionFactory writeConnectionFactory)
    : IUserProgressRepository
{
    const int MaximumProficiency = 5;
    const int IncorrectAnswerReviewDelayDays = 1;
    const string CreateHistoryTableSql = """
        CREATE TABLE IF NOT EXISTS PracticeHistory (
            Id INTEGER PRIMARY KEY,
            Number INTEGER NOT NULL,
            Mode TEXT NOT NULL,
            AnsweredAt TEXT NOT NULL,
            IsCorrect INTEGER NOT NULL
        )
        """;
    const string CreateHistoryIndexSql = """
        CREATE INDEX IF NOT EXISTS IX_PracticeHistory_AnsweredAt
            ON PracticeHistory (AnsweredAt)
        """;
    const string RecordHistorySql = """
        INSERT INTO PracticeHistory (Number, Mode, AnsweredAt, IsCorrect)
        VALUES ($number, $mode, $answeredAt, $isCorrect)
        RETURNING Id
        """;
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
        await using DbConnection connection =
            await writeConnectionFactory.OpenAsync(cancellationToken);
        await using DbTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken);

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
        long answerId = await RecordHistoryAsync(
            connection, transaction, updated, isCorrect, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated with { AnswerId = answerId };
    }

    public async Task<UserProgressRecord> MarkAnswerWrongAsync(
        UserProgressRecord correctAnswer,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await writeConnectionFactory.OpenAsync(cancellationToken);
        await using DbTransaction transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        StoredProgress current = await ReadProgressAsync(
            connection, transaction, correctAnswer.Number, correctAnswer.Direction,
            cancellationToken);
        if (current.Proficiency != correctAnswer.Proficiency ||
            current.TotalCorrect != correctAnswer.TotalCorrect ||
            current.TotalWrong != correctAnswer.TotalWrong)
        {
            throw new PracticeStateException("本題之後已有新的練習紀錄，無法修改。");
        }

        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE PracticeHistory SET IsCorrect = 0
            WHERE Id = $id AND Number = $number AND Mode = $mode AND IsCorrect = 1
            """;
        command.AddParameter("$id", correctAnswer.AnswerId);
        command.AddParameter("$number", correctAnswer.Number);
        command.AddParameter("$mode", correctAnswer.Direction.ToString());
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new PracticeStateException("本題已記錄為錯誤，或作答紀錄已變更。");

        UserProgressRecord corrected = correctAnswer with
        {
            Proficiency = Math.Max(correctAnswer.PreviousProficiency - 1, 0),
            NextReview = correctAnswer.LastAnswer.AddDays(IncorrectAnswerReviewDelayDays),
            TotalCorrect = correctAnswer.TotalCorrect - 1,
            TotalWrong = correctAnswer.TotalWrong + 1
        };
        await WriteProgressAsync(connection, transaction, corrected, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return corrected;
    }

    public async Task<int> CountAnswersAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await readConnectionFactory.OpenAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = 'PracticeHistory'
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
            return 0;

        command.CommandText = """
            SELECT COUNT(*) FROM PracticeHistory
            WHERE AnsweredAt >= $start AND AnsweredAt < $end
            """;
        command.AddParameter("$start", FormatTimestamp(startInclusive));
        command.AddParameter("$end", FormatTimestamp(endExclusive));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<ReviewCandidateProgress>> GetReviewCandidatesAsync(
        VocabularyFilter filter,
        PracticeDirection direction,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await readConnectionFactory.OpenAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = ReviewCandidatesSql;
        command.AddParameter("$mode", direction.ToString());
        command.AddParameter("$minNumber", filter.MinNumber);
        command.AddParameter("$maxNumber", filter.MaxNumber);
        command.AddParameter("$type", (object?)filter.Type ?? DBNull.Value);

        return await ReadReviewCandidatesAsync(command, cancellationToken);
    }

    static async Task<StoredProgress> ReadProgressAsync(
        DbConnection connection,
        DbTransaction transaction,
        int number,
        PracticeDirection direction,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ReadProgressSql;
        command.AddParameter("$number", number);
        command.AddParameter("$mode", direction.ToString());

        await using DbDataReader reader =
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
            totalWrong)
        {
            PreviousProficiency = current.Proficiency
        };
    }

    static async Task WriteProgressAsync(
        DbConnection connection,
        DbTransaction transaction,
        UserProgressRecord progress,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertProgressSql;
        command.AddParameter("$number", progress.Number);
        command.AddParameter("$proficiency", progress.Proficiency);
        command.AddParameter("$lastAnswer", FormatTimestamp(progress.LastAnswer));
        command.AddParameter("$nextReview", FormatTimestamp(progress.NextReview));
        command.AddParameter("$totalCorrect", progress.TotalCorrect);
        command.AddParameter("$totalWrong", progress.TotalWrong);
        command.AddParameter("$mode", progress.Direction.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    static async Task<long> RecordHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        UserProgressRecord progress,
        bool isCorrect,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = CreateHistoryTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = CreateHistoryIndexSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = RecordHistorySql;
        command.AddParameter("$number", progress.Number);
        command.AddParameter("$mode", progress.Direction.ToString());
        command.AddParameter("$answeredAt", FormatTimestamp(progress.LastAnswer));
        command.AddParameter("$isCorrect", isCorrect ? 1 : 0);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    static async Task<IReadOnlyList<ReviewCandidateProgress>> ReadReviewCandidatesAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        await using DbDataReader reader =
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
