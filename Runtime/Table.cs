using System.Collections.Generic;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public class Table
    {
        public string Name { get; }
        public IReadOnlyList<string> Columns { get; }
        public IReadOnlyList<TableRow> Rows => _rows;

        private readonly List<TableRow> _rows;
        private readonly Dictionary<string, TableRow> _rowsByKey;
        private readonly string _keyColumn;

        public Table(string name, IReadOnlyList<string> columns, List<TableRow> rows, string keyColumn = null)
        {
            Name = name;
            Columns = columns;
            _rows = rows;
            _keyColumn = keyColumn;

            _rowsByKey = new Dictionary<string, TableRow>();
            if (!string.IsNullOrEmpty(keyColumn))
            {
                foreach (var row in rows)
                {
                    var key = row[keyColumn];
                    if (!string.IsNullOrEmpty(key) && !_rowsByKey.ContainsKey(key))
                        _rowsByKey.Add(key, row);
                }
            }
        }

        public TableRow GetRow(string key)
        {
            _rowsByKey.TryGetValue(key, out var row);
            return row;
        }

        public bool TryGetRow(string key, out TableRow row)
        {
            return _rowsByKey.TryGetValue(key, out row);
        }

        public TableRow GetRowAt(int index)
        {
            return (index < 0 || index >= _rows.Count) ? null : _rows[index];
        }

        public int RowCount => _rows.Count;
    }
}
