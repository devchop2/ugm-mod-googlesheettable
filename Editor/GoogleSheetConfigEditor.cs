using ChopChopGames.UGM.GoogleSheetTable;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    [CustomEditor(typeof(GoogleSheetConfig))]
    public class GoogleSheetConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (GoogleSheetConfig)target;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(config.spreadSheets == null || config.spreadSheets.Count == 0))
            {
                if (GUILayout.Button("구글시트에서 로드하기", GUILayout.Height(32)))
                {
                    GoogleSheetDownloader.DownloadAll(config);
                }
            }

            EditorGUILayout.HelpBox(
                "동일한 작업은 상단 메뉴 ChopChopGames/GoogleSheet/LoadTables 에서도 실행할 수 있습니다.\n각 시트를 다운로드해 outputFolder에 .asset(TableAsset) 으로 저장하고 cachedAsset 슬롯에 자동 연결합니다.",
                MessageType.Info);
        }
    }
}
