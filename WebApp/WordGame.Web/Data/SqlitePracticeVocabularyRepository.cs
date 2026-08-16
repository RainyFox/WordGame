using Microsoft.Data.Sqlite;
using WordGame.Web.Models;

namespace WordGame.Web.Data;

public sealed class SqlitePracticeVocabularyRepository(
    IReadOnlyWordGameConnectionFactory connectionFactory)
    : IPracticeVocabularyRepository
{
    const string NumberRangeSql = """
        SELECT COALESCE(MIN(番号), 0), COALESCE(MAX(番号), 0)
        FROM Vocabulary
        """;
    const string TypesSql = """
        SELECT DISTINCT TRIM(タイプ)
        FROM Vocabulary
        WHERE タイプ IS NOT NULL
          AND TRIM(タイプ) <> ''
        ORDER BY TRIM(タイプ)
        """;
    const string CandidateNumbersSql = """
        SELECT 番号
        FROM Vocabulary
        WHERE 番号 BETWEEN $minNumber AND $maxNumber
          AND ($type IS NULL OR TRIM(タイプ) = $type)
        ORDER BY 番号
        """;
    const string VocabularyByNumberSql = """
        SELECT 番号,
               COALESCE(単語, ''),
               COALESCE(かな, ''),
               COALESCE(中国語, ''),
               COALESCE(例, ''),
               COALESCE(タイプ, '')
        FROM Vocabulary
        WHERE 番号 = $number
        """;
    const int RequiredDistractorCount = 3;

    public async Task<PracticeOptionsResponse> GetOptionsAsync(
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await connectionFactory.OpenAsync(cancellationToken);
        (int minNumber, int maxNumber) = await ReadNumberRangeAsync(
            connection,
            cancellationToken);
        IReadOnlyList<string> types = await ReadTypesAsync(
            connection,
            cancellationToken);
        return new PracticeOptionsResponse(minNumber, maxNumber, types);
    }

    public async Task<IReadOnlyList<int>> GetCandidateNumbersAsync(
        VocabularyFilter filter,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await connectionFactory.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = CandidateNumbersSql;
        command.Parameters.AddWithValue("$minNumber", filter.MinNumber);
        command.Parameters.AddWithValue("$maxNumber", filter.MaxNumber);
        command.Parameters.AddWithValue("$type", (object?)filter.Type ?? DBNull.Value);

        return await ReadNumbersAsync(command, cancellationToken);
    }

    public async Task<VocabularyEntry?> GetByNumberAsync(
        int number,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await connectionFactory.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = VocabularyByNumberSql;
        command.Parameters.AddWithValue("$number", number);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadVocabulary(reader)
            : null;
    }

    public async Task<IReadOnlyList<string>> GetDistractorAnswersAsync(
        VocabularyEntry question,
        PracticeDirection direction,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await connectionFactory.OpenAsync(cancellationToken);
        string correctAnswer = GetAnswer(direction, question);
        string? preferredType = string.IsNullOrWhiteSpace(question.Type)
            ? null
            : question.Type.Trim();
        IReadOnlyList<string> preferred = await ReadDistractorAnswersAsync(
            connection,
            question.Number,
            correctAnswer,
            direction,
            preferredType,
            cancellationToken);

        if (preferred.Count >= RequiredDistractorCount || preferredType is null)
            return preferred;

        IReadOnlyList<string> fallback = await ReadDistractorAnswersAsync(
            connection,
            question.Number,
            correctAnswer,
            direction,
            type: null,
            cancellationToken);
        return preferred
            .Concat(fallback)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    static async Task<(int MinNumber, int MaxNumber)> ReadNumberRangeAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = NumberRangeSql;
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    static async Task<IReadOnlyList<string>> ReadTypesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = TypesSql;
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var types = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            types.Add(reader.GetString(0));
        return types;
    }

    static async Task<IReadOnlyList<int>> ReadNumbersAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        var numbers = new List<int>();
        while (await reader.ReadAsync(cancellationToken))
            numbers.Add(reader.GetInt32(0));
        return numbers;
    }

    static async Task<IReadOnlyList<string>> ReadDistractorAnswersAsync(
        SqliteConnection connection,
        int questionNumber,
        string correctAnswer,
        PracticeDirection direction,
        string? type,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = CreateDistractorAnswersSql(direction);
        command.Parameters.AddWithValue("$number", questionNumber);
        command.Parameters.AddWithValue("$correctAnswer", correctAnswer);
        command.Parameters.AddWithValue("$type", (object?)type ?? DBNull.Value);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        var answers = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            answers.Add(reader.GetString(0));
        return answers;
    }

    static string CreateDistractorAnswersSql(PracticeDirection direction)
    {
        string answerColumn = direction == PracticeDirection.JpToCn
            ? "かな"
            : "単語";
        return $"""
            SELECT DISTINCT COALESCE({answerColumn}, '') AS Answer
            FROM Vocabulary
            WHERE 番号 <> $number
              AND COALESCE({answerColumn}, '') <> $correctAnswer
              AND TRIM(COALESCE({answerColumn}, '')) <> ''
              AND ($type IS NULL OR TRIM(COALESCE(タイプ, '')) = $type)
            ORDER BY Answer
            """;
    }

    static string GetAnswer(
        PracticeDirection direction,
        VocabularyEntry question)
    {
        return direction == PracticeDirection.JpToCn
            ? question.Kana
            : question.Word;
    }

    static VocabularyEntry ReadVocabulary(SqliteDataReader reader)
    {
        return new VocabularyEntry(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }
}
