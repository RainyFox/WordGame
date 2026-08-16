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
