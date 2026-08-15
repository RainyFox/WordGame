using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace WordGame.Web.Tests.Support;

internal sealed class TemporaryWordGameDatabase : IDisposable
{
    readonly string temporaryDirectory;

    public string DatabasePath { get; }

    public TemporaryWordGameDatabase()
    {
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"WordGameWebTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        DatabasePath = Path.Combine(temporaryDirectory, "WordGame.db");
        CreateDatabase();
    }

    public byte[] ComputeHash()
    {
        return SHA256.HashData(File.ReadAllBytes(DatabasePath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(temporaryDirectory, recursive: true);
    }

    void CreateDatabase()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false
        };
        using var connection = new SqliteConnection(connectionString.ToString());
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Vocabulary (
                番号 INTEGER PRIMARY KEY,
                単語 TEXT,
                かな TEXT,
                中国語 TEXT,
                例 TEXT,
                タイプ TEXT,
                備考 TEXT
            );

            CREATE TABLE UserProgress (
                番号 INTEGER,
                Proficiency INTEGER,
                LastAnswer TEXT,
                NextReview TEXT,
                TotalCorrect INTEGER,
                TotalWrong INTEGER,
                Mode TEXT,
                PRIMARY KEY (番号, Mode)
            );

            INSERT INTO Vocabulary VALUES
                (1, '整う', 'ととのう', '整理', '', '通常', ''),
                (2, '習得', 'しゅうとく', '學習並掌握', '', 'テキスト', ''),
                (3, '次第', 'しだい', '取決於', '', '文法', ''),
                (4, 'まとも', 'まとも', '正經', '', '通常', '');

            INSERT INTO UserProgress VALUES
                (1, 3, '2026-08-01T00:00:00Z', '2026-08-09T00:00:00Z', 4, 1, 'JpToCn');
            """;
        command.ExecuteNonQuery();
    }
}
