using System.Collections.Generic;
using System.Text;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public static class TsvParser
    {
        // Default layout: row 1 is reserved for notes/descriptions, headers on row 2 (1-based), data starts at row 3.
        // Pass headerRow=1 for sheets where the very first row is the header (no notes row).
        // headerRow is 1-BASED to match the user-visible Google Sheets row numbers.
        public const int DefaultHeaderRow = 2;

        public static Table Parse(string name, string tsv, string keyColumn = null, int headerRow = DefaultHeaderRow)
        {
            return ParseInternal(name, tsv, '\t', keyColumn, headerRow);
        }

        public static Table ParseCsv(string name, string csv, string keyColumn = null, int headerRow = DefaultHeaderRow)
        {
            return ParseInternal(name, csv, ',', keyColumn, headerRow);
        }

        private static Table ParseInternal(string name, string text, char delimiter, string keyColumn, int headerRow)
        {
            // headerRow is 1-based. Internal index needs to be 0-based.
            if (headerRow < 1) headerRow = 1;
            int headerIndex = headerRow - 1;
            int dataStartIndex = headerIndex + 1;

            var rawRows = SplitRows(text, delimiter);
            if (rawRows.Count <= headerIndex)
                return new Table(name, new List<string>(), new List<TableRow>(), keyColumn);

            var headers = rawRows[headerIndex];
            var rows = new List<TableRow>(System.Math.Max(0, rawRows.Count - dataStartIndex));

            for (int i = dataStartIndex; i < rawRows.Count; i++)
            {
                var cells = rawRows[i];
                if (IsEmpty(cells)) continue;

                var dict = new Dictionary<string, string>(headers.Count);
                for (int c = 0; c < headers.Count; c++)
                {
                    var col = headers[c];
                    if (string.IsNullOrEmpty(col)) continue;
                    dict[col] = c < cells.Count ? cells[c] : string.Empty;
                }
                rows.Add(new TableRow(dict));
            }

            return new Table(name, headers, rows, keyColumn);
        }

        private static bool IsEmpty(List<string> cells)
        {
            foreach (var c in cells)
                if (!string.IsNullOrEmpty(c)) return false;
            return true;
        }

        private static List<List<string>> SplitRows(string text, char delimiter)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == delimiter)
                {
                    currentRow.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (ch == '\r')
                {
                    // skip — handled by \n
                }
                else if (ch == '\n')
                {
                    currentRow.Add(sb.ToString());
                    sb.Length = 0;
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    sb.Append(ch);
                }
            }

            if (sb.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(sb.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }
    }
}
