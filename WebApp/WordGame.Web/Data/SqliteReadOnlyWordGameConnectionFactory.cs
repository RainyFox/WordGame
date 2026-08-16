using Microsoft.Data.Sqlite;

namespace WordGame.Web.Data;

public sealed class SqliteReadOnlyWordGameConnectionFactory(
    IWordGameDatabasePathResolver pathResolver) : IReadOnlyWordGameConnectionFactory
{
    public async Task<SqliteConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        string databasePath = pathResolver.GetDatabasePath();
        EnsureDatabaseExists(databasePath);

        SqliteConnection connection = CreateConnection(databasePath);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    static void EnsureDatabaseExists(string databasePath)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("找不到 WordGame.db。", databasePath);
    }

    static SqliteConnection CreateConnection(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        return new SqliteConnection(connectionString.ToString());
    }
}
