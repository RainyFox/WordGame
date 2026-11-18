using System.Data.Common;
using Microsoft.Extensions.Options;
using Turso;

namespace WordGame.Web.Data;

public sealed class TursoWordGameConnectionFactory(IOptions<WordGameDatabaseOptions> options)
    : IReadOnlyWordGameConnectionFactory, IReadWriteWordGameConnectionFactory
{
    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = options.Value.TursoUrl,
            ["Auth Token"] = options.Value.TursoAuthToken,
            ["Default Timeout"] = 15
        };
        var connection = new TursoConnection(connectionString.ConnectionString);
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
}
