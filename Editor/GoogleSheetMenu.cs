using ChopChopGames.UGM.GoogleSheetTable;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    public static class GoogleSheetMenu
    {
        [MenuItem("ChopChopGames/GoogleSheet/Config", priority = 1)]
        public static void OpenOrCreateConfig()
        {
            var config = GoogleSheetDownloader.FindOrCreateConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("ChopChopGames/GoogleSheet/LoadTables", priority = 2)]
        public static void LoadTables()
        {
            var config = GoogleSheetDownloader.FindConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog(
                    "Config 없음",
                    "GoogleSheetConfig 에셋이 프로젝트에 없습니다.\nChopChopGames/GoogleSheet/Config 메뉴로 먼저 생성하세요.",
                    "확인");
                return;
            }
            GoogleSheetDownloader.DownloadAll(config);
        }

        [MenuItem("ChopChopGames/GoogleSheet/LoadTables", true)]
        public static bool LoadTablesValidate()
        {
            return GoogleSheetDownloader.FindConfig() != null;
        }

        [MenuItem("ChopChopGames/GoogleSheet/Generate Accessors", priority = 3)]
        public static void GenerateAccessors()
        {
            var config = GoogleSheetDownloader.FindConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Config 없음",
                    "GoogleSheetConfig 에셋이 프로젝트에 없습니다.\nChopChopGames/GoogleSheet/Config 메뉴로 먼저 생성하세요.",
                    "확인");
                return;
            }
            AccessorGenerator.Generate(config);
        }

        [MenuItem("ChopChopGames/GoogleSheet/Generate Accessors", true)]
        public static bool GenerateAccessorsValidate()
        {
            return GoogleSheetDownloader.FindConfig() != null;
        }
    }
}
