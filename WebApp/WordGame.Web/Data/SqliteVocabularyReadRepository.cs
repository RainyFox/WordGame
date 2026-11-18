using System.Data.Common;
using WordGame.Web.Models;

namespace WordGame.Web.Data;

public sealed class SqliteVocabularyReadRepository(
    IReadOnlyWordGameConnectionFactory connectionFactory) : IVocabularyReadRepository
{
    const string VocabularyCountSql = "SELECT COUNT(*) FROM Vocabulary";
    const string VocabularyTypesSql = """
        SELECT DISTINCT TRIM(タイプ)
        FROM Vocabulary
        WHERE タイプ IS NOT NULL
          AND TRIM(タイプ) <> ''
        ORDER BY TRIM(タイプ)
        """;

    public async Task<VocabularySummary> GetSummaryAsync(
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await connectionFactory.OpenAsync(cancellationToken);

        int count = await ReadVocabularyCountAsync(connection, cancellationToken);
        IReadOnlyList<string> types = await ReadVocabularyTypesAsync(
            connection,
            cancellationToken);
        return new VocabularySummary(count, types);
    }

    static async Task<int> ReadVocabularyCountAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = VocabularyCountSql;
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    static async Task<IReadOnlyList<string>> ReadVocabularyTypesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = VocabularyTypesSql;
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        var types = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            types.Add(reader.GetString(0));
        }

        return types;
    }
}
