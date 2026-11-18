using System.Data.Common;

namespace WordGame.Web.Data;

public interface IReadWriteWordGameConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}
