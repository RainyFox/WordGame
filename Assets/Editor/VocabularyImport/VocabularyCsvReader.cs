using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WordGame.Editor.VocabularyImport
{
    internal static class VocabularyCsvReader
    {
        const int MaximumDisplayedErrors = 20;

        public static IReadOnlyList<VocabularyRecord> Read(string csvPath)
        {
            string csvText = ReadUtf8File(csvPath);
            IReadOnlyList<CsvRow> rows = VocabularyCsvParser.Parse(csvText);
            if (rows.Count == 0)
                throw new InvalidDataException("CSV 沒有標題列。");

            Dictionary<string, int> headerIndices = ReadHeaderIndices(rows[0]);
            ValidateRequiredHeaders(headerIndices);
            IReadOnlyList<VocabularyRecord> records = ReadVocabularyRecords(rows, headerIndices);
            if (records.Count == 0)
                throw new InvalidDataException("CSV 沒有可匯入的單字資料。");

            return records;
        }

        static string ReadUtf8File(string csvPath)
        {
            try
            {
                var utf8 = new UTF8Encoding(false, true);
                return File.ReadAllText(csvPath, utf8);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("CSV 必須使用 UTF-8 編碼。", exception);
            }
        }

        static Dictionary<string, int> ReadHeaderIndices(CsvRow headerRow)
        {
            var headerIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < headerRow.Fields.Count; i++)
            {
                string header = headerRow.Fields[i].Trim().TrimStart('\uFEFF');
                if (string.IsNullOrEmpty(header))
                    throw new InvalidDataException($"CSV 第 {headerRow.LineNumber} 行含有空白欄位名稱。");

                if (!headerIndices.TryAdd(header, i))
                    throw new InvalidDataException($"CSV 欄位「{header}」重複出現。");
            }

            return headerIndices;
        }

        static void ValidateRequiredHeaders(IReadOnlyDictionary<string, int> headerIndices)
        {
            string[] missingHeaders = VocabularyColumnNames.All
                .Where(columnName => !headerIndices.ContainsKey(columnName))
                .ToArray();

            if (missingHeaders.Length > 0)
            {
                throw new InvalidDataException(
                    $"CSV 缺少必要欄位：{string.Join("、", missingHeaders)}。");
            }
        }

        static IReadOnlyList<VocabularyRecord> ReadVocabularyRecords(
            IReadOnlyList<CsvRow> rows,
            IReadOnlyDictionary<string, int> headerIndices)
        {
            var records = new List<VocabularyRecord>();
            var numberLines = new Dictionary<int, int>();
            var errors = new List<string>();

            for (int i = 1; i < rows.Count; i++)
            {
                TryReadVocabularyRecord(
                    rows[i],
                    rows[0].Fields.Count,
                    headerIndices,
                    numberLines,
                    records,
                    errors);
            }

            ThrowIfValidationFailed(errors);
            records.Sort((left, right) => left.Number.CompareTo(right.Number));
            return records;
        }

        static void TryReadVocabularyRecord(
            CsvRow row,
            int expectedFieldCount,
            IReadOnlyDictionary<string, int> headerIndices,
            IDictionary<int, int> numberLines,
            ICollection<VocabularyRecord> records,
            ICollection<string> errors)
        {
            if (row.Fields.Count != expectedFieldCount)
            {
                errors.Add(
                    $"第 {row.LineNumber} 行有 {row.Fields.Count} 欄，應為 {expectedFieldCount} 欄。");
                return;
            }

            if (!TryReadNumber(row, headerIndices, out int number, out string numberError))
            {
                errors.Add(numberError);
                return;
            }

            if (numberLines.TryGetValue(number, out int previousLine))
            {
                errors.Add($"第 {row.LineNumber} 行的番号 {number} 與第 {previousLine} 行重複。");
                return;
            }

            VocabularyRecord record = CreateRecord(row, headerIndices, number);
            ValidateRequiredText(record, row.LineNumber, errors);
            numberLines.Add(number, row.LineNumber);
            records.Add(record);
        }

        static bool TryReadNumber(
            CsvRow row,
            IReadOnlyDictionary<string, int> headerIndices,
            out int number,
            out string error)
        {
            string text = ReadValue(row, headerIndices, VocabularyColumnNames.Number);
            bool isValid = int.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out number) && number > 0;

            error = isValid
                ? null
                : $"第 {row.LineNumber} 行的番号「{text}」必須是大於 0 的整數。";
            return isValid;
        }

        static VocabularyRecord CreateRecord(
            CsvRow row,
            IReadOnlyDictionary<string, int> headerIndices,
            int number)
        {
            return new VocabularyRecord(
                number,
                ReadValue(row, headerIndices, VocabularyColumnNames.Word),
                ReadValue(row, headerIndices, VocabularyColumnNames.Kana),
                ReadValue(row, headerIndices, VocabularyColumnNames.Chinese),
                ReadValue(row, headerIndices, VocabularyColumnNames.Example),
                ReadValue(row, headerIndices, VocabularyColumnNames.Type),
                ReadValue(row, headerIndices, VocabularyColumnNames.Note));
        }

        static string ReadValue(
            CsvRow row,
            IReadOnlyDictionary<string, int> headerIndices,
            string columnName)
        {
            return row.Fields[headerIndices[columnName]].Trim();
        }

        static void ValidateRequiredText(
            VocabularyRecord record,
            int lineNumber,
            ICollection<string> errors)
        {
            AddMissingTextError(record.Word, VocabularyColumnNames.Word, lineNumber, errors);
            AddMissingTextError(record.Kana, VocabularyColumnNames.Kana, lineNumber, errors);
            AddMissingTextError(record.Chinese, VocabularyColumnNames.Chinese, lineNumber, errors);
        }

        static void AddMissingTextError(
            string value,
            string columnName,
            int lineNumber,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add($"第 {lineNumber} 行的「{columnName}」不可空白。");
        }

        static void ThrowIfValidationFailed(IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0)
                return;

            string displayedErrors = string.Join(
                Environment.NewLine,
                errors.Take(MaximumDisplayedErrors));
            string remainingMessage = errors.Count > MaximumDisplayedErrors
                ? $"{Environment.NewLine}……另有 {errors.Count - MaximumDisplayedErrors} 個錯誤。"
                : string.Empty;
            throw new InvalidDataException(
                $"CSV 驗證失敗：{Environment.NewLine}{displayedErrors}{remainingMessage}");
        }
    }
}
