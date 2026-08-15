using Microsoft.Data.Sqlite;
using WordGame.Web.Models;

namespace WordGame.Web.Data;

public sealed class SqliteVocabularyReadRepository(
    IWordGameDatabasePathResolver pathResolver) : IVocabularyReadRepository
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
        string databasePath = pathResolver.GetDatabasePath();
        EnsureDatabaseExists(databasePath);

        await using SqliteConnection connection = CreateReadOnlyConnection(databasePath);
        await connection.OpenAsync(cancellationToken);

        int count = await ReadVocabularyCountAsync(connection, cancellationToken);
        IReadOnlyList<string> types = await ReadVocabularyTypesAsync(
            connection,
            cancellationToken);
        return new VocabularySummary(count, types);
    }

    static void EnsureDatabaseExists(string databasePath)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("找不到 WordGame.db。", databasePath);
    }

    static SqliteConnection CreateReadOnlyConnection(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        return new SqliteConnection(connectionString.ToString());
    }

    static async Task<int> ReadVocabularyCountAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = VocabularyCountSql;
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    static async Task<IReadOnlyList<string>> ReadVocabularyTypesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = VocabularyTypesSql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        var types = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            types.Add(reader.GetString(0));
        }

        return types;
    }
}
