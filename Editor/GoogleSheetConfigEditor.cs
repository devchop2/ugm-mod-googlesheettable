using ChopChopGames.UGM.GoogleSheetTable;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    [CustomEditor(typeof(GoogleSheetConfig))]
    public class GoogleSheetConfigEditor : Editor
    {
        // [v0.1.1] 검색 / 정렬 / 외부 sync 도구를 포함하는 커스텀 인스펙터.
        // SerializedProperty 로 직접 그려야 검색 필터링이 가능하므로 DrawDefaultInspector 를 떠난다.

        private SerializedProperty _outputFolder;
        private SerializedProperty _spreadSheets;

        private string _filter = string.Empty;
        private Vector2 _scroll;

        private void OnEnable()
        {
            _outputFolder = serializedObject.FindProperty(nameof(GoogleSheetConfig.outputFolder));
            _spreadSheets = serializedObject.FindProperty(nameof(GoogleSheetConfig.spreadSheets));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var config = (GoogleSheetConfig)target;

            // outputFolder
            EditorGUILayout.PropertyField(_outputFolder);

            EditorGUILayout.Space(4);
            DrawToolbar(config);
            EditorGUILayout.Space(4);

            DrawFilteredSpreadSheets();

            EditorGUILayout.Space();
            DrawActionButtons(config);

            serializedObject.ApplyModifiedProperties();
        }

        // ----------------------------------------------------------
        // 검색 / 정렬 툴바
        // ----------------------------------------------------------
        private void DrawToolbar(GoogleSheetConfig config)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("검색", GUILayout.Width(34));
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarTextField);
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    _filter = string.Empty;

                GUILayout.Space(8);
                if (GUILayout.Button(new GUIContent("Sort SpreadSheets A→Z", "최상위 SpreadSheet 목록을 이름 기준으로 정렬"), EditorStyles.toolbarButton, GUILayout.Width(170)))
                    SortSpreadSheets(config);
                if (GUILayout.Button(new GUIContent("Sort Sheets A→Z", "각 SpreadSheet 안의 sheet 목록을 tableName 기준으로 정렬"), EditorStyles.toolbarButton, GUILayout.Width(140)))
                    SortSheetsWithin(config);
            }
        }

        private static void SortSpreadSheets(GoogleSheetConfig config)
        {
            if (config.spreadSheets == null) return;
            Undo.RecordObject(config, "Sort SpreadSheets");
            config.spreadSheets.Sort((a, b) => string.Compare(
                a?.name ?? string.Empty, b?.name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            EditorUtility.SetDirty(config);
            Debug.Log("[GoogleSheet] SpreadSheets 를 이름 순으로 정렬했습니다.");
        }

        private static void SortSheetsWithin(GoogleSheetConfig config)
        {
            if (config.spreadSheets == null) return;
            Undo.RecordObject(config, "Sort Sheets");
            int total = 0;
            foreach (var ss in config.spreadSheets)
            {
                if (ss?.sheets == null) continue;
                ss.sheets.Sort((a, b) => string.Compare(
                    a?.tableName ?? string.Empty, b?.tableName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                total += ss.sheets.Count;
            }
            EditorUtility.SetDirty(config);
            Debug.Log($"[GoogleSheet] 각 SpreadSheet 안의 sheets 를 정렬했습니다 (총 {total}개).");
        }

        // ----------------------------------------------------------
        // 검색 필터를 적용한 spreadSheets 표시
        // ----------------------------------------------------------
        private void DrawFilteredSpreadSheets()
        {
            EditorGUILayout.LabelField($"SpreadSheets ({_spreadSheets.arraySize})", EditorStyles.boldLabel);

            // toolbar 와 본문 사이 약간의 간격
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(800)))
            {
                _scroll = scroll.scrollPosition;

                bool hasFilter = !string.IsNullOrEmpty(_filter);
                int visibleCount = 0;

                for (int i = 0; i < _spreadSheets.arraySize; i++)
                {
                    var ssProp = _spreadSheets.GetArrayElementAtIndex(i);
                    var nameProp = ssProp.FindPropertyRelative("name");
                    var sheetsProp = ssProp.FindPropertyRelative("sheets");

                    bool ssMatches = hasFilter && Match(nameProp.stringValue, _filter);
                    var matchingSheetIdx = new List<int>();
                    if (sheetsProp != null && sheetsProp.arraySize > 0)
                    {
                        for (int j = 0; j < sheetsProp.arraySize; j++)
                        {
                            var tnProp = sheetsProp.GetArrayElementAtIndex(j).FindPropertyRelative("tableName");
                            if (!hasFilter || Match(tnProp.stringValue, _filter))
                                matchingSheetIdx.Add(j);
                        }
                    }

                    bool show = !hasFilter || ssMatches || matchingSheetIdx.Count > 0;
                    if (!show) continue;
                    visibleCount++;

                    DrawSpreadSheetEntry(ssProp, nameProp, sheetsProp, hasFilter, ssMatches, matchingSheetIdx);
                }

                if (hasFilter && visibleCount == 0)
                    EditorGUILayout.HelpBox($"'{_filter}' 검색 결과 없음.", MessageType.Info);
            }

            // Add new SpreadSheet button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add SpreadSheet", GUILayout.Width(160)))
                {
                    _spreadSheets.InsertArrayElementAtIndex(_spreadSheets.arraySize);
                    var added = _spreadSheets.GetArrayElementAtIndex(_spreadSheets.arraySize - 1);
                    added.FindPropertyRelative("name").stringValue = string.Empty;
                    added.FindPropertyRelative("spreadsheetId").stringValue = string.Empty;
                    var sheetsArr = added.FindPropertyRelative("sheets");
                    if (sheetsArr != null) sheetsArr.ClearArray();
                }
            }
        }

        private void DrawSpreadSheetEntry(SerializedProperty ssProp,
                                          SerializedProperty nameProp,
                                          SerializedProperty sheetsProp,
                                          bool hasFilter, bool ssMatches, List<int> matchingSheetIdx)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var ssLabel = string.IsNullOrEmpty(nameProp.stringValue) ? "(unnamed)" : nameProp.stringValue;
                    int shownCount = (hasFilter && !ssMatches) ? matchingSheetIdx.Count : (sheetsProp?.arraySize ?? 0);
                    ssProp.isExpanded = EditorGUILayout.Foldout(ssProp.isExpanded, $"{ssLabel}  ({shownCount} sheet)", true);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("X", "이 SpreadSheet 항목 삭제"), GUILayout.Width(24)))
                    {
                        if (EditorUtility.DisplayDialog("삭제 확인", $"SpreadSheet '{ssLabel}' 를 삭제할까요?\n(연결된 .asset 파일은 삭제하지 않습니다)", "삭제", "취소"))
                        {
                            int idx = _spreadSheets.IndexOfPropertyOrSelf(ssProp);
                            if (idx >= 0) _spreadSheets.DeleteArrayElementAtIndex(idx);
                        }
                        return;
                    }
                }

                if (!ssProp.isExpanded) return;

                EditorGUILayout.PropertyField(nameProp);
                EditorGUILayout.PropertyField(ssProp.FindPropertyRelative("spreadsheetId"));

                if (sheetsProp == null || sheetsProp.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("이 SpreadSheet 에는 sheet 가 없습니다.", MessageType.None);
                }
                else
                {
                    // 필터 일치하는 sheet 만 표시 (ssMatches 면 전부, 아니면 matchingSheetIdx 만)
                    bool showAll = !hasFilter || ssMatches;
                    if (showAll)
                    {
                        for (int j = 0; j < sheetsProp.arraySize; j++)
                            EditorGUILayout.PropertyField(sheetsProp.GetArrayElementAtIndex(j));
                    }
                    else
                    {
                        foreach (var j in matchingSheetIdx)
                            EditorGUILayout.PropertyField(sheetsProp.GetArrayElementAtIndex(j));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ Add Sheet", GUILayout.Width(110)))
                    {
                        sheetsProp.InsertArrayElementAtIndex(sheetsProp.arraySize);
                        var added = sheetsProp.GetArrayElementAtIndex(sheetsProp.arraySize - 1);
                        added.FindPropertyRelative("tableName").stringValue = string.Empty;
                        added.FindPropertyRelative("gid").stringValue = string.Empty;
                        added.FindPropertyRelative("keyColumn").stringValue = string.Empty;
                        var hr = added.FindPropertyRelative("headerRow");
                        if (hr != null) hr.intValue = TsvParser.DefaultHeaderRow;
                    }
                }
            }
        }

        private static bool Match(string text, string needle)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ----------------------------------------------------------
        // 하단 액션 버튼들
        // ----------------------------------------------------------
        private void DrawActionButtons(GoogleSheetConfig config)
        {
            using (new EditorGUI.DisabledScope(config.spreadSheets == null || config.spreadSheets.Count == 0))
            {
                if (GUILayout.Button("구글시트에서 로드하기 (LoadTables)", GUILayout.Height(28)))
                    GoogleSheetDownloader.DownloadAll(config);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Repair cachedAsset Links", "outputFolder 의 .asset 들을 tableName 매칭으로 cachedAsset 에 자동 연결")))
                    ConfigSyncer.RepairAssetLinks(config);
                if (GUILayout.Button(new GUIContent("Sync From Seed JSON…", "외부 JSON 으로 spreadSheets 를 재구성 (cachedAsset 은 가능한 한 보존)")))
                    PromptSyncFromSeed(config);
            }
            EditorGUILayout.HelpBox(
                "메뉴: ChopChopGames > GoogleSheet 에서 동일 작업을 실행할 수 있습니다.\nLoadTables 직전에는 Repair cachedAsset Links 가 자동으로 한 번 실행됩니다.",
                MessageType.Info);
        }

        private static void PromptSyncFromSeed(GoogleSheetConfig config)
        {
            string path = EditorUtility.OpenFilePanel("Seed JSON 선택", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            ConfigSyncer.SyncFromSeedJson(config, path);
        }
    }

    // ----------------------------------------------------------
    // SerializedProperty 헬퍼: 자기 자신의 array index 검색
    // ----------------------------------------------------------
    internal static class SerializedPropertyExtensions
    {
        public static int IndexOfPropertyOrSelf(this SerializedProperty arrayProp, SerializedProperty element)
        {
            // path: "...spreadSheets.Array.data[N]"
            var path = element.propertyPath;
            int open = path.LastIndexOf('[');
            int close = path.LastIndexOf(']');
            if (open < 0 || close < 0 || close <= open) return -1;
            if (int.TryParse(path.Substring(open + 1, close - open - 1), out var v)) return v;
            return -1;
        }
    }
}
