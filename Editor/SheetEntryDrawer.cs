using ChopChopGames.UGM.GoogleSheetTable;
using System;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    [CustomPropertyDrawer(typeof(GoogleSheetConfig.SheetEntry))]
    public class SheetEntryDrawer : PropertyDrawer
    {
        // 6 fields + 1 reload-button row + headerRow line = 8 lines when expanded
        private const int LineCount = 8;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * LineCount + 4;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var tableName = property.FindPropertyRelative("tableName");
            var gid = property.FindPropertyRelative("gid");
            var keyColumn = property.FindPropertyRelative("keyColumn");
            var dataStructure = property.FindPropertyRelative("dataStructure");
            var rowTypeName = property.FindPropertyRelative("rowTypeName");
            var cachedAsset = property.FindPropertyRelative("cachedAsset");
            var headerRow = property.FindPropertyRelative("headerRow");

            var lh = EditorGUIUtility.singleLineHeight;
            var sp = EditorGUIUtility.standardVerticalSpacing;
            var line = new Rect(position.x, position.y, position.width, lh);

            var headerLabel = string.IsNullOrEmpty(tableName.stringValue) ? "(unnamed)" : tableName.stringValue;
            var typeSuffix = ResolveTypeLabel(rowTypeName.stringValue);
            if (!string.IsNullOrEmpty(typeSuffix)) headerLabel += $"  →  {typeSuffix}";

            // [v0.1.1] foldout 라인에 작은 "Reload" 버튼을 우측에 표시 (개별 테이블 다운로드)
            const float kReloadW = 70f;
            var foldoutRect = new Rect(line.x, line.y, line.width - kReloadW - 4, line.height);
            var reloadRect = new Rect(line.xMax - kReloadW, line.y, kReloadW, line.height);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerLabel, true);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(tableName.stringValue) || string.IsNullOrEmpty(gid.stringValue)))
            {
                if (GUI.Button(reloadRect, new GUIContent("⟳ Reload", "이 테이블만 다시 다운로드")))
                {
                    TriggerReload(property);
                }
            }

            if (!property.isExpanded) return;

            using (new EditorGUI.IndentLevelScope())
            {
                line.y += lh + sp;
                EditorGUI.PropertyField(line, tableName);
                line.y += lh + sp;
                EditorGUI.PropertyField(line, gid);
                line.y += lh + sp;
                EditorGUI.PropertyField(line, keyColumn);
                line.y += lh + sp;
                EditorGUI.PropertyField(line, dataStructure);
                line.y += lh + sp;
                // headerRow validate: must be >= 1
                EditorGUI.BeginChangeCheck();
                int newHeader = EditorGUI.IntField(line, new GUIContent("Header Row", "1-based row number where column headers live (default 2 — row 1 is notes)."), Mathf.Max(1, headerRow.intValue));
                if (EditorGUI.EndChangeCheck()) headerRow.intValue = Mathf.Max(1, newHeader);
                line.y += lh + sp;
                EditorGUI.PropertyField(line, cachedAsset);
            }
        }

        private static string ResolveTypeLabel(string aqn)
        {
            if (string.IsNullOrEmpty(aqn)) return null;
            var t = Type.GetType(aqn);
            return t != null ? t.Name : "(missing)";
        }

        // [v0.1.1] 개별 테이블 reload — 부모 spreadsheet 와 entry 를 SerializedProperty 경로에서 역추적
        private static void TriggerReload(SerializedProperty entryProp)
        {
            var so = entryProp.serializedObject;
            so.ApplyModifiedProperties();
            var config = so.targetObject as GoogleSheetConfig;
            if (config == null)
            {
                Debug.LogError("[GoogleSheet] SheetEntry reload: 부모 GoogleSheetConfig 를 찾을 수 없습니다.");
                return;
            }

            // propertyPath 형식 예시: "spreadSheets.Array.data[2].sheets.Array.data[5]"
            var path = entryProp.propertyPath;
            int ssIdx = ExtractArrayIndex(path, "spreadSheets.Array.data[");
            int shIdx = ExtractArrayIndex(path, "sheets.Array.data[");
            if (ssIdx < 0 || shIdx < 0)
            {
                Debug.LogError($"[GoogleSheet] SheetEntry reload: 인덱스 추출 실패 path='{path}'");
                return;
            }
            if (config.spreadSheets == null || ssIdx >= config.spreadSheets.Count) return;
            var ss = config.spreadSheets[ssIdx];
            if (ss?.sheets == null || shIdx >= ss.sheets.Count) return;
            var entry = ss.sheets[shIdx];
            GoogleSheetDownloader.DownloadOne(config, ss, entry);
        }

        private static int ExtractArrayIndex(string path, string marker)
        {
            int start = path.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return -1;
            start += marker.Length;
            int end = path.IndexOf(']', start);
            if (end < 0) return -1;
            int.TryParse(path.Substring(start, end - start), out var v);
            return v;
        }
    }
}
