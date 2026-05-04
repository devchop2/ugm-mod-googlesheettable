using System.Collections.Generic;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public enum DataStructure
    {
        List,
        Dictionary,
        DictionaryOfList,
    }

    [CreateAssetMenu(fileName = "GoogleSheetConfig", menuName = "GoogleSheetTable/Config")]
    public class GoogleSheetConfig : ScriptableObject
    {
        [Tooltip("Root folder for generated TableAsset files. Each SpreadSheet creates its own subfolder under this path.")]
        public string outputFolder = "Assets/_UserData/Tables";

        [Tooltip("List of SpreadSheets. Each SpreadSheet has its own spreadsheetId and a list of sheet tabs.")]
        public List<SpreadSheetEntry> spreadSheets = new List<SpreadSheetEntry>();

        [System.Serializable]
        public class SpreadSheetEntry
        {
            [Tooltip("Logical name of this SpreadSheet. Used as the subfolder name under outputFolder.")]
            public string name;

            [Tooltip("Spreadsheet ID — the part of the URL between /d/ and /edit.")]
            public string spreadsheetId;

            [Tooltip("Sheet tabs in this SpreadSheet. Each becomes one TableAsset.")]
            public List<SheetEntry> sheets = new List<SheetEntry>();
        }

        [System.Serializable]
        public class SheetEntry
        {
            [Tooltip("Logical table name used to look this up at runtime.")]
            public string tableName;

            [Tooltip("Sheet gid — the `gid=` query param in the tab's URL.")]
            public string gid;

            [Tooltip("Column header whose values are used as the lookup key for Dictionary / DictionaryOfList. Required when dataStructure != List.")]
            public string keyColumn;

            [Tooltip("Runtime data structure built for this table. List = sequential rows; Dictionary = unique key per row; DictionaryOfList = group rows sharing the same key.")]
            public DataStructure dataStructure = DataStructure.List;

            [Tooltip("Row class for typed parsing. Auto-filled by [GoogleSheetRow(\"tableName\")] when LoadTables runs.")]
            public string rowTypeName;

            [Tooltip("Cached TableAsset created by ChopChopGames/GoogleSheet/LoadTables. Runtime reads this — no network call.")]
            public TableAsset cachedAsset;
        }
    }
}
