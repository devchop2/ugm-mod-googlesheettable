using ChopChopGames.UGM.GoogleSheetTable;
using System;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    [CustomPropertyDrawer(typeof(GoogleSheetConfig.SheetEntry))]
    public class SheetEntryDrawer : PropertyDrawer
    {
        private const int LineCount = 6;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * LineCount + 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var tableName = property.FindPropertyRelative("tableName");
            var gid = property.FindPropertyRelative("gid");
            var keyColumn = property.FindPropertyRelative("keyColumn");
            var dataStructure = property.FindPropertyRelative("dataStructure");
            var rowTypeName = property.FindPropertyRelative("rowTypeName");
            var cachedAsset = property.FindPropertyRelative("cachedAsset");

            var lh = EditorGUIUtility.singleLineHeight;
            var sp = EditorGUIUtility.standardVerticalSpacing;
            var line = new Rect(position.x, position.y, position.width, lh);

            var headerLabel = string.IsNullOrEmpty(tableName.stringValue) ? "(unnamed)" : tableName.stringValue;
            var typeSuffix = ResolveTypeLabel(rowTypeName.stringValue);
            if (!string.IsNullOrEmpty(typeSuffix)) headerLabel += $"  →  {typeSuffix}";

            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, headerLabel, true);
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
                EditorGUI.PropertyField(line, cachedAsset);
            }
        }

        private static string ResolveTypeLabel(string aqn)
        {
            if (string.IsNullOrEmpty(aqn)) return null;
            var t = Type.GetType(aqn);
            return t != null ? t.Name : "(missing)";
        }
    }
}
