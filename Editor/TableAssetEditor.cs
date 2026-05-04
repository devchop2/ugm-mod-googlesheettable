using ChopChopGames.UGM.GoogleSheetTable;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    [CustomEditor(typeof(TableAsset))]
    public class TableAssetEditor : Editor
    {
        private SerializedProperty _tableName;
        private SerializedProperty _keyColumn;
        private SerializedProperty _columns;
        private SerializedProperty _rows;

        private string _filter = string.Empty;
        private Vector2 _scroll;
        private float _cellWidth = 140f;
        private bool _showColumns;

        private GUIStyle _headerLabel;
        private GUIStyle _indexLabel;
        private GUIStyle _cellTextField;
        private GUIStyle _smallButton;

        private const string CellsField = "cells";
        private const float RowHeight = 22f;
        private const float IndexColWidth = 36f;
        private const float ButtonsColWidth = 56f;

        private static Color GridColor   => EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.55f, 0.55f, 0.55f);
        private static Color HeaderBg    => EditorGUIUtility.isProSkin ? new Color(0.27f, 0.32f, 0.40f) : new Color(0.78f, 0.86f, 0.96f);
        private static Color KeyHeaderBg => EditorGUIUtility.isProSkin ? new Color(0.34f, 0.45f, 0.58f) : new Color(0.65f, 0.78f, 0.94f);
        private static Color OddRowBg    => EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.96f, 0.96f, 0.96f);
        private static Color EvenRowBg   => EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.90f, 0.90f, 0.90f);
        private static Color KeyColTint  => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.04f)   : new Color(0f, 0f, 0f, 0.04f);
        private static Color OuterBg     => EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(1f, 1f, 1f);

        private void OnEnable()
        {
            _tableName = serializedObject.FindProperty(nameof(TableAsset.tableName));
            _keyColumn = serializedObject.FindProperty(nameof(TableAsset.keyColumn));
            _columns = serializedObject.FindProperty(nameof(TableAsset.columns));
            _rows = serializedObject.FindProperty(nameof(TableAsset.rows));
        }

        private void EnsureStyles()
        {
            if (_headerLabel == null)
            {
                _headerLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 0, 0),
                    clipping = TextClipping.Clip,
                };
            }
            if (_indexLabel == null)
            {
                _indexLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                };
            }
            if (_cellTextField == null)
            {
                _cellTextField = new GUIStyle(EditorStyles.textField)
                {
                    margin = new RectOffset(0, 0, 0, 0),
                    fixedHeight = RowHeight - 4,
                };
            }
            if (_smallButton == null)
            {
                _smallButton = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 10,
                    padding = new RectOffset(2, 2, 1, 1),
                };
            }
        }

        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();

            DrawMeta();
            EditorGUILayout.Space();
            DrawToolbar();
            EditorGUILayout.Space();
            DrawTable();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "이 데이터는 ChopChopGames/GoogleSheet/LoadTables 실행 시 시트로 다시 덮어써집니다.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMeta()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(_tableName);

            var colNames = new string[_columns.arraySize];
            for (int i = 0; i < _columns.arraySize; i++)
                colNames[i] = _columns.GetArrayElementAtIndex(i).stringValue;

            if (colNames.Length > 0)
            {
                int current = Array.IndexOf(colNames, _keyColumn.stringValue);
                if (current < 0) current = 0;
                int picked = EditorGUILayout.Popup("Key Column", current, colNames);
                if (picked >= 0 && picked < colNames.Length)
                    _keyColumn.stringValue = colNames[picked];
            }
            else
            {
                EditorGUILayout.PropertyField(_keyColumn);
            }

            _showColumns = EditorGUILayout.Foldout(_showColumns, $"Columns ({_columns.arraySize})", true);
            if (_showColumns)
            {
                using (new EditorGUI.DisabledScope(true))
                using (new EditorGUI.IndentLevelScope())
                {
                    for (int i = 0; i < colNames.Length; i++)
                        EditorGUILayout.LabelField($"[{i}]", colNames[i]);
                }
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            _filter = EditorGUILayout.TextField("Filter", _filter);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) _filter = string.Empty;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Cell Width", GUILayout.Width(70));
            _cellWidth = GUILayout.HorizontalSlider(_cellWidth, 60f, 320f, GUILayout.Width(160));
            GUILayout.Space(8);
            GUILayout.Label($"Rows: {_rows.arraySize}", GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Row", GUILayout.Width(80))) AddRow();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTable()
        {
            int colCount = _columns.arraySize;
            if (colCount == 0)
            {
                EditorGUILayout.HelpBox("컬럼이 비어있습니다. ChopChopGames/GoogleSheet/LoadTables 를 먼저 실행하세요.", MessageType.Warning);
                return;
            }

            int keyIdx = -1;
            for (int i = 0; i < colCount; i++)
            {
                if (_columns.GetArrayElementAtIndex(i).stringValue == _keyColumn.stringValue)
                {
                    keyIdx = i;
                    break;
                }
            }

            var visible = new List<int>(_rows.arraySize);
            for (int r = 0; r < _rows.arraySize; r++)
            {
                if (string.IsNullOrEmpty(_filter) || RowMatches(r, keyIdx))
                    visible.Add(r);
            }

            float contentWidth  = IndexColWidth + ButtonsColWidth + _cellWidth * colCount;
            float bodyContentHeight = RowHeight * visible.Count + 2;

            Rect viewport = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.MinHeight(260),
                GUILayout.MaxHeight(640),
                GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(viewport, GridColor);
            var inner = new Rect(viewport.x + 1, viewport.y + 1, viewport.width - 2, viewport.height - 2);
            EditorGUI.DrawRect(inner, OuterBg);

            var headerArea = new Rect(inner.x, inner.y, inner.width, RowHeight);
            var bodyArea   = new Rect(inner.x, inner.y + RowHeight, inner.width, inner.height - RowHeight);

            DrawStickyHeader(headerArea, contentWidth, colCount, keyIdx);

            int deleteRow = -1, duplicateRow = -1;
            var bodyContent = new Rect(0, 0, contentWidth, bodyContentHeight);

            _scroll = GUI.BeginScrollView(bodyArea, _scroll, bodyContent);
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    int r = visible[i];
                    float y = i * RowHeight;
                    bool oddRow = (i & 1) == 1;
                    DrawDataRow(r, y, oddRow, colCount, keyIdx, ref deleteRow, ref duplicateRow);
                }
            }
            GUI.EndScrollView();

            if (deleteRow >= 0)
            {
                _rows.DeleteArrayElementAtIndex(deleteRow);
            }
            else if (duplicateRow >= 0)
            {
                DuplicateRow(duplicateRow);
            }
        }

        private void DrawStickyHeader(Rect headerArea, float contentWidth, int colCount, int keyIdx)
        {
            GUI.BeginClip(headerArea);
            try
            {
                float xOffset = -_scroll.x;
                var rect = new Rect(xOffset, 0, IndexColWidth, RowHeight);
                DrawHeaderCell(rect, "#", false);

                rect.x += IndexColWidth;
                rect.width = ButtonsColWidth;
                DrawHeaderCell(rect, string.Empty, false);

                rect.x += ButtonsColWidth;
                rect.width = _cellWidth;
                for (int c = 0; c < colCount; c++)
                {
                    bool isKey = (c == keyIdx);
                    var name = _columns.GetArrayElementAtIndex(c).stringValue;
                    DrawHeaderCell(rect, isKey ? "★ " + name : name, isKey);
                    rect.x += _cellWidth;
                }
            }
            finally
            {
                GUI.EndClip();
            }
        }

        private void DrawHeaderCell(Rect rect, string label, bool isKey)
        {
            EditorGUI.DrawRect(rect, isKey ? KeyHeaderBg : HeaderBg);
            DrawCellBorder(rect);
            GUI.Label(rect, label, _headerLabel);
        }

        private void DrawDataRow(int rowIndex, float y, bool oddRow, int colCount, int keyIdx, ref int deleteRow, ref int duplicateRow)
        {
            Color rowBg = oddRow ? OddRowBg : EvenRowBg;

            var rect = new Rect(0, y, IndexColWidth, RowHeight);
            EditorGUI.DrawRect(rect, rowBg);
            DrawCellBorder(rect);
            GUI.Label(rect, rowIndex.ToString(), _indexLabel);

            rect.x += IndexColWidth;
            rect.width = ButtonsColWidth;
            EditorGUI.DrawRect(rect, rowBg);
            DrawCellBorder(rect);
            var btn = new Rect(rect.x + 3, rect.y + 3, 22, RowHeight - 6);
            if (GUI.Button(btn, new GUIContent("X", "행 삭제"), _smallButton)) deleteRow = rowIndex;
            btn.x += 24;
            if (GUI.Button(btn, new GUIContent("D", "행 복제"), _smallButton)) duplicateRow = rowIndex;

            rect.x += ButtonsColWidth;
            rect.width = _cellWidth;

            var cellsProp = _rows.GetArrayElementAtIndex(rowIndex).FindPropertyRelative(CellsField);
            EnsureCellCount(cellsProp, colCount);

            for (int c = 0; c < colCount; c++)
            {
                EditorGUI.DrawRect(rect, rowBg);
                if (c == keyIdx)
                    EditorGUI.DrawRect(rect, KeyColTint);
                DrawCellBorder(rect);

                var cell = cellsProp.GetArrayElementAtIndex(c);
                var fieldRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, RowHeight - 4);
                cell.stringValue = EditorGUI.TextField(fieldRect, cell.stringValue, _cellTextField);

                rect.x += _cellWidth;
            }
        }

        private static void DrawCellBorder(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), GridColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), GridColor);
        }

        private bool RowMatches(int rowIndex, int keyIdx)
        {
            var cellsProp = _rows.GetArrayElementAtIndex(rowIndex).FindPropertyRelative(CellsField);
            if (keyIdx >= 0 && keyIdx < cellsProp.arraySize)
            {
                var v = cellsProp.GetArrayElementAtIndex(keyIdx).stringValue ?? string.Empty;
                return v.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            for (int c = 0; c < cellsProp.arraySize; c++)
            {
                var v = cellsProp.GetArrayElementAtIndex(c).stringValue;
                if (!string.IsNullOrEmpty(v) && v.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static void EnsureCellCount(SerializedProperty cellsProp, int desired)
        {
            while (cellsProp.arraySize < desired)
            {
                cellsProp.InsertArrayElementAtIndex(cellsProp.arraySize);
                cellsProp.GetArrayElementAtIndex(cellsProp.arraySize - 1).stringValue = string.Empty;
            }
            while (cellsProp.arraySize > desired)
                cellsProp.DeleteArrayElementAtIndex(cellsProp.arraySize - 1);
        }

        private void AddRow()
        {
            int insertAt = _rows.arraySize;
            _rows.InsertArrayElementAtIndex(insertAt);
            var newCells = _rows.GetArrayElementAtIndex(insertAt).FindPropertyRelative(CellsField);
            newCells.ClearArray();
            for (int c = 0; c < _columns.arraySize; c++)
            {
                newCells.InsertArrayElementAtIndex(c);
                newCells.GetArrayElementAtIndex(c).stringValue = string.Empty;
            }
        }

        private void DuplicateRow(int srcIndex)
        {
            if (srcIndex < 0 || srcIndex >= _rows.arraySize) return;

            var srcCells = _rows.GetArrayElementAtIndex(srcIndex).FindPropertyRelative(CellsField);
            var values = new List<string>(srcCells.arraySize);
            for (int i = 0; i < srcCells.arraySize; i++)
                values.Add(srcCells.GetArrayElementAtIndex(i).stringValue);

            int insertAt = srcIndex + 1;
            _rows.InsertArrayElementAtIndex(insertAt);
            var newCells = _rows.GetArrayElementAtIndex(insertAt).FindPropertyRelative(CellsField);
            newCells.ClearArray();
            for (int i = 0; i < values.Count; i++)
            {
                newCells.InsertArrayElementAtIndex(i);
                newCells.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }
    }
}
