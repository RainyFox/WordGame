using System;
using System.Collections.Generic;

namespace WordGame.Editor.VocabularyImport
{
    internal static class VocabularyColumnNames
    {
        public const string Number = "番号";
        public const string Word = "単語";
        public const string Kana = "かな";
        public const string Chinese = "中国語";
        public const string Example = "例";
        public const string Type = "タイプ";
        public const string Note = "備考";

        public static readonly IReadOnlyList<string> All = Array.AsReadOnly(new[]
        {
            Number,
            Word,
            Kana,
            Chinese,
            Example,
            Type,
            Note
        });
    }

    internal sealed class VocabularyRecord
    {
        public int Number { get; }
        public string Word { get; }
        public string Kana { get; }
        public string Chinese { get; }
        public string Example { get; }
        public string Type { get; }
        public string Note { get; }

        public VocabularyRecord(
            int number,
            string word,
            string kana,
            string chinese,
            string example,
            string type,
            string note)
        {
            Number = number;
            Word = word;
            Kana = kana;
            Chinese = chinese;
            Example = example;
            Type = type;
            Note = note;
        }

        public bool HasSameContent(VocabularyRecord other)
        {
            return other != null
                && Number == other.Number
                && string.Equals(Word, other.Word, StringComparison.Ordinal)
                && string.Equals(Kana, other.Kana, StringComparison.Ordinal)
                && string.Equals(Chinese, other.Chinese, StringComparison.Ordinal)
                && string.Equals(Example, other.Example, StringComparison.Ordinal)
                && string.Equals(Type, other.Type, StringComparison.Ordinal)
                && string.Equals(Note, other.Note, StringComparison.Ordinal);
        }
    }

    internal sealed class CsvRow
    {
        public int LineNumber { get; }
        public IReadOnlyList<string> Fields { get; }

        public CsvRow(int lineNumber, IReadOnlyList<string> fields)
        {
            LineNumber = lineNumber;
            Fields = fields;
        }
    }

    internal sealed class DatabaseImportPlan
    {
        public string DisplayName { get; }
        public string DatabasePath { get; }
        public IReadOnlyList<VocabularyRecord> RecordsToWrite { get; }
        public int AddedCount { get; }
        public int UpdatedCount { get; }
        public int UnchangedCount { get; }
        public int ExtraCount { get; }
        public int ChangeCount => AddedCount + UpdatedCount;

        public DatabaseImportPlan(
            string displayName,
            string databasePath,
            IReadOnlyList<VocabularyRecord> recordsToWrite,
            int addedCount,
            int updatedCount,
            int unchangedCount,
            int extraCount)
        {
            DisplayName = displayName;
            DatabasePath = databasePath;
            RecordsToWrite = recordsToWrite;
            AddedCount = addedCount;
            UpdatedCount = updatedCount;
            UnchangedCount = unchangedCount;
            ExtraCount = extraCount;
        }
    }

    internal sealed class VocabularyImportPlan
    {
        public int CsvRecordCount { get; }
        public IReadOnlyList<DatabaseImportPlan> DatabasePlans { get; }
        public bool HasChanges { get; }

        public VocabularyImportPlan(
            int csvRecordCount,
            IReadOnlyList<DatabaseImportPlan> databasePlans)
        {
            CsvRecordCount = csvRecordCount;
            DatabasePlans = databasePlans;

            foreach (DatabaseImportPlan databasePlan in databasePlans)
            {
                if (databasePlan.ChangeCount > 0)
                {
                    HasChanges = true;
                    break;
                }
            }
        }
    }

    internal sealed class VocabularyImportResult
    {
        public string BackupDirectory { get; }

        public VocabularyImportResult(string backupDirectory)
        {
            BackupDirectory = backupDirectory;
        }
    }
}
