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
            // [추가] Reload 옆에 "Delete" 버튼 — config entry + cachedAsset(.asset) 삭제 (시작 로딩은 config 기반이라 자동 제외)
            const float kReloadW = 70f;
            const float kDeleteW = 62f;
            const float kGap = 4f;
            var foldoutRect = new Rect(line.x, line.y, line.width - kReloadW - kDeleteW - kGap * 2, line.height);
            var reloadRect = new Rect(line.xMax - kReloadW - kDeleteW - kGap, line.y, kReloadW, line.height);
            var deleteRect = new Rect(line.xMax - kDeleteW, line.y, kDeleteW, line.height);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerLabel, true);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(tableName.stringValue) || string.IsNullOrEmpty(gid.stringValue)))
            {
                if (GUI.Button(reloadRect, new GUIContent("⟳ Reload", "이 테이블만 다시 다운로드")))
                {
                    TriggerReload(property);
                }
            }

            // Delete 버튼 — 빨간 톤으로 강조
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.95f, 0.5f, 0.5f);
            if (GUI.Button(deleteRect, new GUIContent("✕ Delete", "이 sheet 항목을 config 에서 제거하고, 로드된 .asset 파일도 삭제합니다.")))
            {
                TriggerDelete(property);
            }
            GUI.backgroundColor = prevColor;

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

        // [추가] 개별 sheet 삭제 — config entry 제거 + cachedAsset(.asset) 파일 삭제.
        //        시작 시 로딩(GoogleSheetTableManager.LoadAll)과 런타임 조회(GoogleSheetTableLoader)는
        //        모두 config.spreadSheets 를 순회하므로, config entry 제거만으로 로딩 대상에서도 자동 제외된다.
        private static void TriggerDelete(SerializedProperty entryProp)
        {
            var so = entryProp.serializedObject;
            so.ApplyModifiedProperties();
            var config = so.targetObject as GoogleSheetConfig;
            if (config == null)
            {
                Debug.LogError("[GoogleSheet] SheetEntry delete: 부모 GoogleSheetConfig 를 찾을 수 없습니다.");
                return;
            }

            var path = entryProp.propertyPath;
            int ssIdx = ExtractArrayIndex(path, "spreadSheets.Array.data[");
            int shIdx = ExtractArrayIndex(path, "sheets.Array.data[");
            if (ssIdx < 0 || shIdx < 0)
            {
                Debug.LogError($"[GoogleSheet] SheetEntry delete: 인덱스 추출 실패 path='{path}'");
                return;
            }
            if (config.spreadSheets == null || ssIdx >= config.spreadSheets.Count) return;
            var ss = config.spreadSheets[ssIdx];
            if (ss?.sheets == null || shIdx >= ss.sheets.Count) return;

            var entry = ss.sheets[shIdx];
            var tableLabel = string.IsNullOrEmpty(entry?.tableName) ? "(unnamed)" : entry.tableName;
            var assetPath = (entry != null && entry.cachedAsset != null)
                ? AssetDatabase.GetAssetPath(entry.cachedAsset) : string.Empty;

            var msg = $"'{tableLabel}' sheet 를 삭제할까요?\n\n" +
                      "• config 목록에서 제거됩니다.\n" +
                      "• 게임 시작 시 로딩 대상에서도 제외됩니다.\n" +
                      (string.IsNullOrEmpty(assetPath)
                          ? "• 연결된 .asset 파일은 없습니다."
                          : $"• 로드된 .asset 파일도 삭제됩니다:\n  {assetPath}");

            if (!EditorUtility.DisplayDialog("Sheet 삭제 확인", msg, "삭제", "취소")) return;

            // OnGUI 도중 리스트를 즉시 수정하면 레이아웃이 깨지므로 다음 에디터 틱으로 지연 실행.
            EditorApplication.delayCall += () => DeleteEntry(config, ssIdx, shIdx);
            GUIUtility.ExitGUI();
        }

        private static void DeleteEntry(GoogleSheetConfig config, int ssIdx, int shIdx)
        {
            if (config == null || config.spreadSheets == null) return;
            if (ssIdx < 0 || ssIdx >= config.spreadSheets.Count) return;
            var ss = config.spreadSheets[ssIdx];
            if (ss?.sheets == null || shIdx < 0 || shIdx >= ss.sheets.Count) return;

            var entry = ss.sheets[shIdx];

            // 1) 로드된 .asset 파일 삭제
            if (entry != null && entry.cachedAsset != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(entry.cachedAsset);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (AssetDatabase.DeleteAsset(assetPath))
                        Debug.Log($"[GoogleSheet] cachedAsset 삭제: {assetPath}");
                    else
                        Debug.LogWarning($"[GoogleSheet] cachedAsset 삭제 실패: {assetPath}");
                }
            }

            // 2) config entry 제거 (→ 시작 로딩/런타임 조회 대상에서도 제외)
            Undo.RecordObject(config, "Delete Sheet Entry");
            var removedName = entry?.tableName;
            ss.sheets.RemoveAt(shIdx);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GoogleSheet] SheetEntry 삭제 완료 — '{removedName}' (spreadSheets[{ssIdx}].sheets[{shIdx}]).");
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
