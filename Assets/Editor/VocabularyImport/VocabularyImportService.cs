using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace WordGame.Editor.VocabularyImport
{
    internal sealed class VocabularyImportService
    {
        const string DatabaseFileName = "WordGame.db";
        const string TestDatabaseDisplayName = "TestMode 資料庫";
        const string TemplateDatabaseDisplayName = "StreamingAssets 範本";
        const string UpsertVocabularySql = @"
            INSERT INTO Vocabulary
                (番号, 単語, かな, 中国語, 例, タイプ, 備考)
            VALUES
                (@number, @word, @kana, @chinese, @example, @type, @note)
            ON CONFLICT(番号) DO UPDATE SET
                単語 = excluded.単語,
                かな = excluded.かな,
                中国語 = excluded.中国語,
                例 = excluded.例,
                タイプ = excluded.タイプ,
                備考 = excluded.備考";
        const string SelectVocabularySql = @"
            SELECT 番号, 単語, かな, 中国語, 例, タイプ, 備考
            FROM Vocabulary
            ORDER BY 番号";
        const string SelectVocabularyByNumberSql = @"
            SELECT 番号, 単語, かな, 中国語, 例, タイプ, 備考
            FROM Vocabulary
            WHERE 番号 = @number";

        readonly string projectRoot;
        readonly string testDatabasePath;
        readonly string templateDatabasePath;

        public VocabularyImportService(string projectRoot)
        {
            this.projectRoot = projectRoot;
            testDatabasePath = Path.Combine(projectRoot, DatabaseFileName);
            templateDatabasePath = Path.Combine(
                projectRoot,
                "Assets",
                "StreamingAssets",
                DatabaseFileName);
            SQLitePCL.Batteries_V2.Init();
        }

        public VocabularyImportPlan CreatePlan(
            IReadOnlyList<VocabularyRecord> csvRecords)
        {
            ValidateDatabaseFiles();

            Dictionary<int, VocabularyRecord> testRecords =
                ReadVocabulary(testDatabasePath);
            Dictionary<int, VocabularyRecord> targetRecords =
                BuildTargetRecords(testRecords, csvRecords);

            var databasePlans = new[]
            {
                BuildDatabasePlan(
                    TestDatabaseDisplayName,
                    testDatabasePath,
                    testRecords,
                    targetRecords),
                BuildDatabasePlan(
                    TemplateDatabaseDisplayName,
                    templateDatabasePath,
                    ReadVocabulary(templateDatabasePath),
                    targetRecords)
            };

            return new VocabularyImportPlan(csvRecords.Count, databasePlans);
        }

        public VocabularyImportResult Apply(VocabularyImportPlan importPlan)
        {
            if (!importPlan.HasChanges)
                throw new InvalidOperationException("匯入計畫沒有需要套用的變更。");

            string backupDirectory = CreateBackupDirectory();
            IReadOnlyList<DatabaseBackup> backups = CreateBackups(
                importPlan,
                backupDirectory);

            try
            {
                ApplyDatabasePlans(importPlan.DatabasePlans);
            }
            catch (Exception importException)
            {
                RestoreBackups(backups, importException);
            }

            return new VocabularyImportResult(backupDirectory);
        }

        void ValidateDatabaseFiles()
        {
            ValidateDatabaseFile(testDatabasePath, TestDatabaseDisplayName);
            ValidateDatabaseFile(templateDatabasePath, TemplateDatabaseDisplayName);
        }

        static void ValidateDatabaseFile(string databasePath, string displayName)
        {
            if (!File.Exists(databasePath))
                throw new FileNotFoundException($"找不到 {displayName}：{databasePath}");
        }

        static Dictionary<int, VocabularyRecord> BuildTargetRecords(
            IReadOnlyDictionary<int, VocabularyRecord> existingRecords,
            IReadOnlyList<VocabularyRecord> csvRecords)
        {
            var targetRecords = new Dictionary<int, VocabularyRecord>(existingRecords);
            foreach (VocabularyRecord csvRecord in csvRecords)
            {
                targetRecords[csvRecord.Number] = csvRecord;
            }

            return targetRecords;
        }

        static DatabaseImportPlan BuildDatabasePlan(
            string displayName,
            string databasePath,
            IReadOnlyDictionary<int, VocabularyRecord> existingRecords,
            IReadOnlyDictionary<int, VocabularyRecord> targetRecords)
        {
            var recordsToWrite = new List<VocabularyRecord>();
            int addedCount = 0;
            int updatedCount = 0;
            int unchangedCount = 0;

            foreach (VocabularyRecord targetRecord in targetRecords.Values.OrderBy(x => x.Number))
            {
                ClassifyTargetRecord(
                    targetRecord,
                    existingRecords,
                    recordsToWrite,
                    ref addedCount,
                    ref updatedCount,
                    ref unchangedCount);
            }

            int extraCount = existingRecords.Keys.Count(number => !targetRecords.ContainsKey(number));
            return new DatabaseImportPlan(
                displayName,
                databasePath,
                recordsToWrite,
                addedCount,
                updatedCount,
                unchangedCount,
                extraCount);
        }

        static void ClassifyTargetRecord(
            VocabularyRecord targetRecord,
            IReadOnlyDictionary<int, VocabularyRecord> existingRecords,
            ICollection<VocabularyRecord> recordsToWrite,
            ref int addedCount,
            ref int updatedCount,
            ref int unchangedCount)
        {
            if (!existingRecords.TryGetValue(targetRecord.Number, out VocabularyRecord existingRecord))
            {
                recordsToWrite.Add(targetRecord);
                addedCount++;
                return;
            }

            if (!existingRecord.HasSameContent(targetRecord))
            {
                recordsToWrite.Add(targetRecord);
                updatedCount++;
                return;
            }

            unchangedCount++;
        }

        static Dictionary<int, VocabularyRecord> ReadVocabulary(string databasePath)
        {
            using var connection = OpenConnection(databasePath, SqliteOpenMode.ReadOnly);
            ValidateVocabularySchema(connection, databasePath);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = SelectVocabularySql;
            using SqliteDataReader reader = command.ExecuteReader();

            var records = new Dictionary<int, VocabularyRecord>();
            while (reader.Read())
            {
                VocabularyRecord record = ReadVocabularyRecord(reader);
                if (!records.TryAdd(record.Number, record))
                {
                    throw new InvalidDataException(
                        $"資料庫 {databasePath} 含有重複番号 {record.Number}。");
                }
            }

            return records;
        }

        static void ValidateVocabularySchema(
            SqliteConnection connection,
            string databasePath)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Vocabulary)";
            using SqliteDataReader reader = command.ExecuteReader();

            var actualColumns = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                actualColumns.Add(reader.GetString(1));
            }

            string[] missingColumns = VocabularyColumnNames.All
                .Where(columnName => !actualColumns.Contains(columnName))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidDataException(
                    $"資料庫 {databasePath} 的 Vocabulary 缺少欄位："
                    + $"{string.Join("、", missingColumns)}。");
            }
        }

        static VocabularyRecord ReadVocabularyRecord(SqliteDataReader reader)
        {
            return new VocabularyRecord(
                reader.GetInt32(0),
                ReadText(reader, 1),
                ReadText(reader, 2),
                ReadText(reader, 3),
                ReadText(reader, 4),
                ReadText(reader, 5),
                ReadText(reader, 6));
        }

        static string ReadText(SqliteDataReader reader, int ordinal)
        {
            return reader.IsDBNull(ordinal)
                ? string.Empty
                : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        string CreateBackupDirectory()
        {
            string timestamp = DateTime.Now.ToString(
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture);
            string backupDirectory = Path.Combine(
                projectRoot,
                "DatabaseBackups",
                "VocabularyImport",
                timestamp);
            Directory.CreateDirectory(backupDirectory);
            return backupDirectory;
        }

        static IReadOnlyList<DatabaseBackup> CreateBackups(
            VocabularyImportPlan importPlan,
            string backupDirectory)
        {
            var backups = new List<DatabaseBackup>();
            foreach (DatabaseImportPlan databasePlan in importPlan.DatabasePlans)
            {
                if (databasePlan.ChangeCount == 0)
                    continue;

                string backupPath = Path.Combine(
                    backupDirectory,
                    GetBackupFileName(databasePlan.DisplayName));
                CreateDatabaseBackup(databasePlan.DatabasePath, backupPath);
                backups.Add(new DatabaseBackup(databasePlan.DatabasePath, backupPath));
            }

            return backups;
        }

        static string GetBackupFileName(string displayName)
        {
            return displayName == TestDatabaseDisplayName
                ? "TestMode.WordGame.db"
                : "StreamingAssets.WordGame.db";
        }

        static void CreateDatabaseBackup(string databasePath, string backupPath)
        {
            using var source = OpenConnection(databasePath, SqliteOpenMode.ReadOnly);
            using var destination = OpenConnection(backupPath, SqliteOpenMode.ReadWriteCreate);
            source.BackupDatabase(destination);
        }

        static void ApplyDatabasePlans(
            IReadOnlyList<DatabaseImportPlan> databasePlans)
        {
            foreach (DatabaseImportPlan databasePlan in databasePlans)
            {
                if (databasePlan.ChangeCount > 0)
                    ApplyDatabasePlan(databasePlan);
            }
        }

        static void ApplyDatabasePlan(DatabaseImportPlan databasePlan)
        {
            using var connection = OpenConnection(
                databasePlan.DatabasePath,
                SqliteOpenMode.ReadWrite);
            ValidateVocabularySchema(connection, databasePlan.DatabasePath);
            using SqliteTransaction transaction = connection.BeginTransaction();

            try
            {
                WriteVocabularyRecords(connection, transaction, databasePlan.RecordsToWrite);
                VerifyVocabularyRecords(connection, transaction, databasePlan.RecordsToWrite);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        static void WriteVocabularyRecords(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlyList<VocabularyRecord> records)
        {
            using SqliteCommand command = CreateUpsertCommand(connection, transaction);
            foreach (VocabularyRecord record in records)
            {
                SetVocabularyParameters(command, record);
                if (command.ExecuteNonQuery() != 1)
                {
                    throw new InvalidDataException(
                        $"番号 {record.Number} 寫入資料庫時沒有影響任何資料列。");
                }
            }
        }

        static SqliteCommand CreateUpsertCommand(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = UpsertVocabularySql;
            command.Parameters.Add("@number", SqliteType.Integer);
            command.Parameters.Add("@word", SqliteType.Text);
            command.Parameters.Add("@kana", SqliteType.Text);
            command.Parameters.Add("@chinese", SqliteType.Text);
            command.Parameters.Add("@example", SqliteType.Text);
            command.Parameters.Add("@type", SqliteType.Text);
            command.Parameters.Add("@note", SqliteType.Text);
            return command;
        }

        static void SetVocabularyParameters(
            SqliteCommand command,
            VocabularyRecord record)
        {
            command.Parameters["@number"].Value = record.Number;
            command.Parameters["@word"].Value = record.Word;
            command.Parameters["@kana"].Value = record.Kana;
            command.Parameters["@chinese"].Value = record.Chinese;
            command.Parameters["@example"].Value = record.Example;
            command.Parameters["@type"].Value = record.Type;
            command.Parameters["@note"].Value = record.Note;
        }

        static void VerifyVocabularyRecords(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlyList<VocabularyRecord> expectedRecords)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = SelectVocabularyByNumberSql;
            command.Parameters.Add("@number", SqliteType.Integer);

            foreach (VocabularyRecord expectedRecord in expectedRecords)
            {
                command.Parameters["@number"].Value = expectedRecord.Number;
                using SqliteDataReader reader = command.ExecuteReader();
                bool isValid = reader.Read()
                    && ReadVocabularyRecord(reader).HasSameContent(expectedRecord)
                    && !reader.Read();
                if (!isValid)
                {
                    throw new InvalidDataException(
                        $"番号 {expectedRecord.Number} 寫入後驗證失敗。");
                }
            }
        }

        static SqliteConnection OpenConnection(string databasePath, SqliteOpenMode mode)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = mode
            };
            var connection = new SqliteConnection(connectionString.ToString());
            connection.Open();
            return connection;
        }

        static void RestoreBackups(
            IReadOnlyList<DatabaseBackup> backups,
            Exception importException)
        {
            try
            {
                foreach (DatabaseBackup backup in backups)
                {
                    File.Copy(backup.BackupPath, backup.DatabasePath, true);
                }
            }
            catch (Exception restoreException)
            {
                throw new AggregateException(
                    "單字匯入失敗，而且自動還原備份也失敗。請保留 DatabaseBackups 內容並手動檢查資料庫。",
                    importException,
                    restoreException);
            }

            throw new InvalidOperationException(
                "單字匯入失敗，資料庫已從備份還原。",
                importException);
        }

        sealed class DatabaseBackup
        {
            public string DatabasePath { get; }
            public string BackupPath { get; }

            public DatabaseBackup(string databasePath, string backupPath)
            {
                DatabasePath = databasePath;
                BackupPath = backupPath;
            }
        }
    }
}
