using System.Collections.Generic;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public class TableRow
    {
        private readonly Dictionary<string, string> _cells;

        public TableRow(Dictionary<string, string> cells)
        {
            _cells = cells;
        }

        public string this[string column]
        {
            get
            {
                _cells.TryGetValue(column, out var value);
                return value ?? string.Empty;
            }
        }

        public bool TryGet(string column, out string value)
        {
            return _cells.TryGetValue(column, out value);
        }

        public int GetInt(string column, int defaultValue = 0)
        {
            return int.TryParse(this[column], out var v) ? v : defaultValue;
        }

        public float GetFloat(string column, float defaultValue = 0f)
        {
            return float.TryParse(this[column], out var v) ? v : defaultValue;
        }

        public bool GetBool(string column, bool defaultValue = false)
        {
            var raw = this[column];
            if (string.IsNullOrEmpty(raw)) return defaultValue;
            if (bool.TryParse(raw, out var v)) return v;
            if (raw == "1" || raw.Equals("Y", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (raw == "0" || raw.Equals("N", System.StringComparison.OrdinalIgnoreCase)) return false;
            return defaultValue;
        }

        public IReadOnlyDictionary<string, string> Cells => _cells;
    }
}
