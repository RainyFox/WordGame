using Microsoft.Data.Sqlite;

namespace WordGame.Web.Data;

public interface IReadOnlyWordGameConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken);
}
