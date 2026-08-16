using Microsoft.Data.Sqlite;

namespace WordGame.Web.Data;

public static class SqliteDatabaseErrors
{
    const int BusyErrorCode = 5;
    const int LockedErrorCode = 6;

    public static bool IsBusyOrLocked(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqliteException
                && sqliteException.SqliteErrorCode is BusyErrorCode or LockedErrorCode)
            {
                return true;
            }
        }

        return false;
    }
}
