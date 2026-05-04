using ChopChopGames.UGM.GoogleSheetTable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    public static class RowTypeResolver
    {
        public static List<Type> CollectRowTypes()
        {
            var found = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                    if (t.GetCustomAttribute<GoogleSheetRowAttribute>(false) == null) continue;
                    found.Add(t);
                }
            }
            return found;
        }

        public static Type Resolve(string tableName, IList<Type> candidates)
        {
            if (string.IsNullOrEmpty(tableName) || candidates == null || candidates.Count == 0)
                return null;

            var hits = candidates.Where(t =>
            {
                var attr = t.GetCustomAttribute<GoogleSheetRowAttribute>(false);
                return attr != null
                    && !string.IsNullOrEmpty(attr.TableName)
                    && string.Equals(attr.TableName, tableName, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (hits.Count == 0) return null;

            if (hits.Count > 1)
            {
                Debug.LogWarning(
                    $"[GoogleSheet] 시트 '{tableName}' 에 [GoogleSheetRow(\"{tableName}\")] 가 붙은 클래스가 {hits.Count}개 — " +
                    string.Join(", ", hits.Select(t => t.FullName)) +
                    ". 첫 번째 항목을 사용합니다.");
            }
            return hits[0];
        }
    }
}
