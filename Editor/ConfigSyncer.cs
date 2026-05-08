// [v0.1.1] cachedAsset 누락 / config 외부 편집 회복 도구.
//
// 두 가지 복구 시나리오:
//
// (1) RepairAssetLinks
//     outputFolder 안의 .asset 파일들을 스캔해서 tableName 매칭으로 cachedAsset 자동 연결.
//     LoadTables 가 자동으로 호출하기도 하고 사용자가 수동으로도 실행 가능.
//
// (2) SyncFromSeedJson
//     외부 JSON 정의로부터 config 의 spreadSheets 를 통째로 재구성.
//     기존 cachedAsset 은 가능한 한 보존 (tableName 매칭).
//     외부 도구가 만든 config 가 Unity 의 in-memory 상태와 충돌해 잘리는 시나리오에서 복구용.
//
// JSON 형식:
//   [
//     { "name": "skills", "spreadsheetId": "...",
//       "sheets": [
//         ["skills - skills", "1003554570", 1, "id", 2],
//         ...
//       ]
//     },
//     ...
//   ]
//   sheets 의 5개 원소: [tableName, gid, dataStructure(0=List/1=Dict/2=DofL), keyColumn, headerRow(1-based)]

using System;
using System.Collections.Generic;
using System.IO;
using ChopChopGames.UGM.GoogleSheetTable;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    public static class ConfigSyncer
    {
        // ----------------------------------------------------------
        // (1) RepairAssetLinks
        // ----------------------------------------------------------
        /// <summary>
        /// outputFolder 안의 TableAsset 들을 tableName 으로 매칭해 cachedAsset 이 null 인 entry 를 자동 연결.
        /// 외부 편집 / git merge / 파일 이동으로 끊어진 참조를 복구한다.
        /// 반환값: 새로 연결된 entry 수.
        /// </summary>
        public static int RepairAssetLinks(GoogleSheetConfig config, string outputFolderOverride = null, bool silent = false)
        {
            if (config == null) return 0;
            var folder = outputFolderOverride ?? GoogleSheetDownloader.ResolveOutputFolder(config);
            if (string.IsNullOrEmpty(folder)) return 0;

            // tableName -> TableAsset 매칭 맵
            var byName = new Dictionary<string, TableAsset>(StringComparer.Ordinal);
            if (AssetDatabase.IsValidFolder(folder))
            {
                var guids = AssetDatabase.FindAssets($"t:{nameof(TableAsset)}", new[] { folder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
                    if (asset == null) continue;
                    var tn = string.IsNullOrEmpty(asset.tableName)
                        ? Path.GetFileNameWithoutExtension(path)
                        : asset.tableName;
                    if (!string.IsNullOrEmpty(tn) && !byName.ContainsKey(tn))
                        byName[tn] = asset;
                }
            }

            int relinked = 0;
            if (config.spreadSheets != null)
            {
                foreach (var ss in config.spreadSheets)
                {
                    if (ss?.sheets == null) continue;
                    foreach (var sh in ss.sheets)
                    {
                        if (sh == null) continue;
                        if (sh.cachedAsset != null) continue;
                        if (string.IsNullOrEmpty(sh.tableName)) continue;
                        if (byName.TryGetValue(sh.tableName, out var asset))
                        {
                            sh.cachedAsset = asset;
                            relinked++;
                        }
                    }
                }
            }

            if (relinked > 0)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            if (!silent)
            {
                if (relinked > 0)
                    Debug.Log($"[GoogleSheet] RepairAssetLinks 완료 — {relinked}개 cachedAsset 재연결.");
                else
                    Debug.Log("[GoogleSheet] RepairAssetLinks 완료 — 새로 연결할 항목 없음.");
            }
            return relinked;
        }

        // ----------------------------------------------------------
        // (2) SyncFromSeedJson
        // ----------------------------------------------------------
        /// <summary>
        /// 외부 seed JSON 으로부터 config.spreadSheets 를 재구성.
        /// 기존 cachedAsset 은 tableName 매칭으로 최대한 보존.
        /// 반환값: 동기화한 sheet 수 (실패 시 -1).
        /// </summary>
        public static int SyncFromSeedJson(GoogleSheetConfig config, string seedJsonPath)
        {
            if (config == null)
            {
                Debug.LogError("[GoogleSheet] SyncFromSeedJson: config 가 null 입니다.");
                return -1;
            }
            if (string.IsNullOrEmpty(seedJsonPath) || !File.Exists(seedJsonPath))
            {
                Debug.LogError($"[GoogleSheet] SyncFromSeedJson: seed JSON 파일이 없습니다: {seedJsonPath}");
                return -1;
            }

            string json;
            try { json = File.ReadAllText(seedJsonPath); }
            catch (Exception e)
            {
                Debug.LogError($"[GoogleSheet] SyncFromSeedJson: 파일 읽기 실패: {e.Message}");
                return -1;
            }

            List<SeedSpreadSheet> seed;
            try { seed = ParseSeed(json); }
            catch (Exception e)
            {
                Debug.LogError($"[GoogleSheet] SyncFromSeedJson: JSON 파싱 실패: {e.Message}");
                return -1;
            }
            if (seed == null) return -1;

            // 기존 cachedAsset 보존을 위한 lookup 구축
            var existingAssets = new Dictionary<string, TableAsset>(StringComparer.Ordinal);
            if (config.spreadSheets != null)
            {
                foreach (var ss in config.spreadSheets)
                {
                    if (ss?.sheets == null) continue;
                    foreach (var sh in ss.sheets)
                    {
                        if (sh != null && !string.IsNullOrEmpty(sh.tableName) && sh.cachedAsset != null)
                            existingAssets[sh.tableName] = sh.cachedAsset;
                    }
                }
            }

            // 디스크의 .asset 파일도 후보로 등록
            var folder = GoogleSheetDownloader.ResolveOutputFolder(config);
            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
            {
                var guids = AssetDatabase.FindAssets($"t:{nameof(TableAsset)}", new[] { folder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
                    if (asset == null) continue;
                    var tn = string.IsNullOrEmpty(asset.tableName)
                        ? Path.GetFileNameWithoutExtension(path)
                        : asset.tableName;
                    if (!string.IsNullOrEmpty(tn) && !existingAssets.ContainsKey(tn))
                        existingAssets[tn] = asset;
                }
            }

            // 재구성
            config.spreadSheets = new List<GoogleSheetConfig.SpreadSheetEntry>(seed.Count);
            int totalSheets = 0;
            int reLinked = 0;
            foreach (var seedSs in seed)
            {
                var ssEntry = new GoogleSheetConfig.SpreadSheetEntry
                {
                    name = seedSs.name,
                    spreadsheetId = seedSs.spreadsheetId,
                    sheets = new List<GoogleSheetConfig.SheetEntry>(seedSs.sheets.Count),
                };
                foreach (var s in seedSs.sheets)
                {
                    var she = new GoogleSheetConfig.SheetEntry
                    {
                        tableName = s.tableName,
                        gid = s.gid,
                        keyColumn = s.keyColumn,
                        dataStructure = (DataStructure)s.dataStructure,
                        rowTypeName = string.Empty,
                        cachedAsset = null,
                        headerRow = s.headerRow > 0 ? s.headerRow : TsvParser.DefaultHeaderRow,
                    };
                    if (existingAssets.TryGetValue(s.tableName, out var asset))
                    {
                        she.cachedAsset = asset;
                        reLinked++;
                    }
                    ssEntry.sheets.Add(she);
                    totalSheets++;
                }
                config.spreadSheets.Add(ssEntry);
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GoogleSheet] Sync From Seed JSON 완료 — spreadsheets:{config.spreadSheets.Count}, sheets:{totalSheets}, cachedAsset 자동 연결:{reLinked}");
            return totalSheets;
        }

        // ----------------------------------------------------------
        // 작은 JSON 파서 (UnityJsonUtility 가 List<List<...>> 를 다루지 못함)
        // ----------------------------------------------------------
        private class SeedSpreadSheet
        {
            public string name;
            public string spreadsheetId;
            public List<SeedSheet> sheets = new List<SeedSheet>();
        }
        private class SeedSheet
        {
            public string tableName;
            public string gid;
            public int dataStructure;
            public string keyColumn;
            public int headerRow = TsvParser.DefaultHeaderRow;
        }

        private static List<SeedSpreadSheet> ParseSeed(string json)
        {
            int p = 0;
            SkipWhitespace(json, ref p);
            if (p >= json.Length || json[p] != '[')
                throw new FormatException("JSON 최상위가 배열이어야 합니다.");
            p++;
            var result = new List<SeedSpreadSheet>();
            SkipWhitespace(json, ref p);
            if (p < json.Length && json[p] == ']') { p++; return result; }
            while (p < json.Length)
            {
                SkipWhitespace(json, ref p);
                var entry = ParseSpreadSheet(json, ref p);
                if (entry != null) result.Add(entry);
                SkipWhitespace(json, ref p);
                if (p >= json.Length) break;
                if (json[p] == ',') { p++; continue; }
                if (json[p] == ']') { p++; break; }
                p++;
            }
            return result;
        }

        private static SeedSpreadSheet ParseSpreadSheet(string s, ref int p)
        {
            SkipWhitespace(s, ref p);
            if (p >= s.Length || s[p] != '{') return null;
            p++;
            var ss = new SeedSpreadSheet();
            while (p < s.Length)
            {
                SkipWhitespace(s, ref p);
                if (s[p] == '}') { p++; break; }
                string key = ReadString(s, ref p);
                SkipWhitespace(s, ref p);
                if (p < s.Length && s[p] == ':') p++;
                SkipWhitespace(s, ref p);

                if (key == "name") ss.name = ReadString(s, ref p);
                else if (key == "spreadsheetId") ss.spreadsheetId = ReadString(s, ref p);
                else if (key == "sheets")
                {
                    if (p < s.Length && s[p] != '[') SkipValue(s, ref p);
                    else
                    {
                        p++;
                        SkipWhitespace(s, ref p);
                        if (p < s.Length && s[p] != ']')
                        {
                            while (p < s.Length)
                            {
                                SkipWhitespace(s, ref p);
                                var sh = ParseSheetTuple(s, ref p);
                                if (sh != null) ss.sheets.Add(sh);
                                SkipWhitespace(s, ref p);
                                if (p >= s.Length) break;
                                if (s[p] == ',') { p++; continue; }
                                if (s[p] == ']') { p++; break; }
                                p++;
                            }
                        }
                        else if (p < s.Length) p++;
                    }
                }
                else SkipValue(s, ref p);
                SkipWhitespace(s, ref p);
                if (p < s.Length && s[p] == ',') p++;
            }
            return ss;
        }

        private static SeedSheet ParseSheetTuple(string s, ref int p)
        {
            SkipWhitespace(s, ref p);
            if (p >= s.Length || s[p] != '[') return null;
            p++;
            var sh = new SeedSheet();
            int idx = 0;
            while (p < s.Length)
            {
                SkipWhitespace(s, ref p);
                if (s[p] == ']') { p++; break; }
                switch (idx)
                {
                    case 0: sh.tableName = ReadString(s, ref p); break;
                    case 1: sh.gid = ReadString(s, ref p); break;
                    case 2: sh.dataStructure = ReadInt(s, ref p); break;
                    case 3: sh.keyColumn = ReadString(s, ref p); break;
                    case 4: sh.headerRow = ReadInt(s, ref p); break;
                    default: SkipValue(s, ref p); break;
                }
                idx++;
                SkipWhitespace(s, ref p);
                if (p < s.Length && s[p] == ',') p++;
            }
            return sh;
        }

        private static void SkipWhitespace(string s, ref int p)
        {
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
        }

        private static string ReadString(string s, ref int p)
        {
            SkipWhitespace(s, ref p);
            if (p >= s.Length || s[p] != '"') return null;
            p++;
            var sb = new System.Text.StringBuilder();
            while (p < s.Length && s[p] != '"')
            {
                if (s[p] == '\\' && p + 1 < s.Length)
                {
                    char esc = s[p + 1];
                    if (esc == 'n') sb.Append('\n');
                    else if (esc == 't') sb.Append('\t');
                    else if (esc == 'r') sb.Append('\r');
                    else if (esc == '"') sb.Append('"');
                    else if (esc == '\\') sb.Append('\\');
                    else if (esc == '/') sb.Append('/');
                    else if (esc == 'u' && p + 5 < s.Length)
                    {
                        var hex = s.Substring(p + 2, 4);
                        sb.Append((char)Convert.ToInt32(hex, 16));
                        p += 4;
                    }
                    else sb.Append(esc);
                    p += 2;
                }
                else
                {
                    sb.Append(s[p]);
                    p++;
                }
            }
            if (p < s.Length) p++;
            return sb.ToString();
        }

        private static int ReadInt(string s, ref int p)
        {
            SkipWhitespace(s, ref p);
            int start = p;
            if (p < s.Length && s[p] == '-') p++;
            while (p < s.Length && char.IsDigit(s[p])) p++;
            int.TryParse(s.Substring(start, p - start), out var v);
            return v;
        }

        private static void SkipValue(string s, ref int p)
        {
            SkipWhitespace(s, ref p);
            if (p >= s.Length) return;
            char c = s[p];
            if (c == '"') { ReadString(s, ref p); return; }
            if (c == '{' || c == '[')
            {
                int depth = 0;
                bool inStr = false;
                while (p < s.Length)
                {
                    if (inStr)
                    {
                        if (s[p] == '\\' && p + 1 < s.Length) { p += 2; continue; }
                        if (s[p] == '"') inStr = false;
                    }
                    else
                    {
                        if (s[p] == '"') inStr = true;
                        else if (s[p] == '{' || s[p] == '[') depth++;
                        else if (s[p] == '}' || s[p] == ']') { depth--; if (depth == 0) { p++; return; } }
                    }
                    p++;
                }
                return;
            }
            while (p < s.Length && s[p] != ',' && s[p] != '}' && s[p] != ']' && !char.IsWhiteSpace(s[p])) p++;
        }
    }
}
