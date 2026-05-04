using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public class TableAsset : ScriptableObject
    {
        public string tableName;
        public string keyColumn;
        public List<string> columns = new List<string>();
        public List<RowData> rows = new List<RowData>();

        [Serializable]
        public class RowData
        {
            public List<string> cells = new List<string>();
        }

        public Table ToRuntimeTable()
        {
            var runtimeRows = new List<TableRow>(rows.Count);
            foreach (var row in rows)
            {
                var dict = new Dictionary<string, string>(columns.Count);
                for (int c = 0; c < columns.Count; c++)
                {
                    var col = columns[c];
                    if (string.IsNullOrEmpty(col)) continue;
                    dict[col] = (row != null && c < row.cells.Count) ? row.cells[c] : string.Empty;
                }
                runtimeRows.Add(new TableRow(dict));
            }
            return new Table(tableName, columns, runtimeRows, keyColumn);
        }

        public void Populate(Table source, string keyCol)
        {
            tableName = source.Name;
            keyColumn = keyCol;
            columns = new List<string>(source.Columns);
            rows = new List<RowData>(source.RowCount);
            for (int i = 0; i < source.RowCount; i++)
            {
                var row = source.GetRowAt(i);
                var data = new RowData();
                foreach (var col in columns)
                    data.cells.Add(row[col]);
                rows.Add(data);
            }
        }
    }
}
