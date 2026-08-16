using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace WordGame.Web.Tests.Support;

internal sealed class CopiedWordGameDatabase : IDisposable
{
    readonly string temporaryDirectory;

    public CopiedWordGameDatabase()
    {
        SourcePath = FindSourceDatabase();
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"WordGameWebCopyTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        DatabasePath = Path.Combine(temporaryDirectory, "WordGame.db");
        File.Copy(SourcePath, DatabasePath);
    }

    public string SourcePath { get; }

    public string DatabasePath { get; }

    public byte[] ComputeSourceHash()
    {
        return SHA256.HashData(File.ReadAllBytes(SourcePath));
    }

    public SqliteConnection OpenCopyReadOnly()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        var connection = new SqliteConnection(connectionString.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    static string FindSourceDatabase()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "WordGame.db");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("找不到專案根目錄的 WordGame.db。");
    }
}
