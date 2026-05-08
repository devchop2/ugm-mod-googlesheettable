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

        // [v0.1.1] cachedAsset 자동 복구 (수동 실행)
        [MenuItem("ChopChopGames/GoogleSheet/Repair cachedAsset Links", priority = 20)]
        public static void RepairAssetLinks()
        {
            var config = GoogleSheetDownloader.FindConfig();
            if (config == null) { EditorUtility.DisplayDialog("Config 없음", "GoogleSheetConfig 에셋이 없습니다.", "확인"); return; }
            ConfigSyncer.RepairAssetLinks(config);
        }

        [MenuItem("ChopChopGames/GoogleSheet/Repair cachedAsset Links", true)]
        public static bool RepairAssetLinksValidate() => GoogleSheetDownloader.FindConfig() != null;

        // [v0.1.1] 외부 seed JSON 으로 config 재구성
        [MenuItem("ChopChopGames/GoogleSheet/Sync Config From Seed JSON…", priority = 21)]
        public static void SyncConfigFromSeedJson()
        {
            var config = GoogleSheetDownloader.FindConfig();
            if (config == null) { EditorUtility.DisplayDialog("Config 없음", "GoogleSheetConfig 에셋이 없습니다.", "확인"); return; }
            string path = EditorUtility.OpenFilePanel("Seed JSON 선택", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            ConfigSyncer.SyncFromSeedJson(config, path);
        }

        [MenuItem("ChopChopGames/GoogleSheet/Sync Config From Seed JSON…", true)]
        public static bool SyncConfigFromSeedJsonValidate() => GoogleSheetDownloader.FindConfig() != null;

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
