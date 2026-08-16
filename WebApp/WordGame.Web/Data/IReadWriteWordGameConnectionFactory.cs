using Microsoft.Data.Sqlite;

namespace WordGame.Web.Data;

public interface IReadWriteWordGameConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
}
