using System.Data.Common;

namespace WordGame.Web.Data;

public interface IReadOnlyWordGameConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}
