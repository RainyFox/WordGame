using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WordGame.Editor.VocabularyImport
{
    internal static class VocabularyCsvParser
    {
        public static IReadOnlyList<CsvRow> Parse(string text)
        {
            return new Parser(text ?? string.Empty).ParseRows();
        }

        sealed class Parser
        {
            readonly string text;
            int index;
            int lineNumber = 1;

            public Parser(string text)
            {
                this.text = text;
            }

            public IReadOnlyList<CsvRow> ParseRows()
            {
                var rows = new List<CsvRow>();
                while (!IsAtEnd)
                {
                    CsvRow row = ReadRow();
                    if (!IsBlankRow(row))
                        rows.Add(row);
                }

                return rows;
            }

            CsvRow ReadRow()
            {
                int rowLineNumber = lineNumber;
                var fields = new List<string>();

                while (true)
                {
                    fields.Add(ReadField());
                    if (IsAtEnd)
                        break;

                    if (CurrentCharacter == ',')
                    {
                        index++;
                        continue;
                    }

                    if (IsCurrentCharacterNewline())
                    {
                        ConsumeNewline();
                        break;
                    }

                    throw CreateFormatException("欄位結束後含有無效字元");
                }

                return new CsvRow(rowLineNumber, fields);
            }

            string ReadField()
            {
                return !IsAtEnd && CurrentCharacter == '"'
                    ? ReadQuotedField()
                    : ReadUnquotedField();
            }

            string ReadQuotedField()
            {
                int fieldStartLine = lineNumber;
                var value = new StringBuilder();
                index++;

                while (!IsAtEnd)
                {
                    if (CurrentCharacter == '"')
                    {
                        index++;
                        if (!IsAtEnd && CurrentCharacter == '"')
                        {
                            value.Append('"');
                            index++;
                            continue;
                        }

                        return value.ToString();
                    }

                    if (IsCurrentCharacterNewline())
                    {
                        value.Append('\n');
                        ConsumeNewline();
                        continue;
                    }

                    value.Append(CurrentCharacter);
                    index++;
                }

                throw new InvalidDataException($"CSV 第 {fieldStartLine} 行的引號沒有結束。");
            }

            string ReadUnquotedField()
            {
                int startIndex = index;
                while (!IsAtEnd && CurrentCharacter != ',' && !IsCurrentCharacterNewline())
                {
                    if (CurrentCharacter == '"')
                        throw CreateFormatException("未加引號的欄位中出現引號");

                    index++;
                }

                return text.Substring(startIndex, index - startIndex);
            }

            void ConsumeNewline()
            {
                if (CurrentCharacter == '\r')
                {
                    index++;
                    if (!IsAtEnd && CurrentCharacter == '\n')
                        index++;
                }
                else
                {
                    index++;
                }

                lineNumber++;
            }

            bool IsCurrentCharacterNewline()
            {
                return !IsAtEnd && (CurrentCharacter == '\r' || CurrentCharacter == '\n');
            }

            InvalidDataException CreateFormatException(string message)
            {
                return new InvalidDataException($"CSV 第 {lineNumber} 行格式錯誤：{message}。");
            }

            static bool IsBlankRow(CsvRow row)
            {
                foreach (string field in row.Fields)
                {
                    if (!string.IsNullOrWhiteSpace(field))
                        return false;
                }

                return true;
            }

            char CurrentCharacter => text[index];
            bool IsAtEnd => index >= text.Length;
        }
    }
}
