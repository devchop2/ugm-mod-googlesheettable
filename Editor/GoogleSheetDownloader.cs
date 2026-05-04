using ChopChopGames.UGM.GoogleSheetTable;
using System;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable.EditorTools
{
    public static class GoogleSheetDownloader
    {
        public const string DefaultConfigPath = "Assets/_UserData/GoogleSheetConfig.asset";

        public static GoogleSheetConfig FindConfig()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(GoogleSheetConfig)}");
            if (guids.Length == 0) return null;
            if (guids.Length > 1)
                Debug.LogWarning($"[GoogleSheet] {guids.Length}개의 Config가 발견됨. 첫 번째 항목을 사용합니다.");
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GoogleSheetConfig>(path);
        }

        public static GoogleSheetConfig FindOrCreateConfig()
        {
            var config = FindConfig();
            if (config != null) return config;

            EnsureFolder(Path.GetDirectoryName(DefaultConfigPath).Replace('\\', '/'));
            config = ScriptableObject.CreateInstance<GoogleSheetConfig>();
            AssetDatabase.CreateAsset(config, DefaultConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GoogleSheet] Config 생성: {DefaultConfigPath}");
            return config;
        }

        public static void DownloadAll(GoogleSheetConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[GoogleSheet] Config가 null입니다.");
                return;
            }
            if (config.spreadSheets == null || config.spreadSheets.Count == 0)
            {
                EditorUtility.DisplayDialog("로드 실패", "정의된 SpreadSheet가 없습니다.", "확인");
                return;
            }

            var folder = string.IsNullOrEmpty(config.outputFolder)
                ? "Assets/GoogleSheetTable/Tables"
                : config.outputFolder.Replace('\\', '/').TrimEnd('/');

            if (!folder.StartsWith("Assets/") && folder != "Assets")
            {
                EditorUtility.DisplayDialog("로드 실패", $"outputFolder는 Assets/ 하위여야 합니다: {folder}", "확인");
                return;
            }

            EnsureFolder(folder);

            int totalSheets = 0;
            foreach (var ss in config.spreadSheets)
                if (ss?.sheets != null) totalSheets += ss.sheets.Count;

            var rowTypeCandidates = RowTypeResolver.CollectRowTypes();
            int success = 0, failed = 0, autoLinked = 0;
            int processed = 0;
            bool cancelled = false;

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                {
                    foreach (var spreadSheet in config.spreadSheets)
                    {
                        if (cancelled) break;
                        if (spreadSheet == null) continue;

                        if (string.IsNullOrEmpty(spreadSheet.name))
                        {
                            Debug.LogWarning("[GoogleSheet] 이름 없는 SpreadSheet 항목을 건너뜁니다.");
                            failed += spreadSheet.sheets?.Count ?? 0;
                            processed += spreadSheet.sheets?.Count ?? 0;
                            continue;
                        }
                        if (string.IsNullOrEmpty(spreadSheet.spreadsheetId))
                        {
                            Debug.LogWarning($"[GoogleSheet] '{spreadSheet.name}' 의 spreadsheetId가 비어있습니다. 건너뜁니다.");
                            failed += spreadSheet.sheets?.Count ?? 0;
                            processed += spreadSheet.sheets?.Count ?? 0;
                            continue;
                        }

                        var safeSpreadSheetName = MakeSafeFileName(spreadSheet.name);
                        var spreadSheetFolder = $"{folder}/{safeSpreadSheetName}";
                        EnsureFolder(spreadSheetFolder);

                        if (spreadSheet.sheets == null) continue;

                        foreach (var entry in spreadSheet.sheets)
                        {
                            if (cancelled) break;
                            processed++;

                            if (entry == null || string.IsNullOrEmpty(entry.tableName) || string.IsNullOrEmpty(entry.gid))
                            {
                                failed++;
                                continue;
                            }

                            if (EditorUtility.DisplayCancelableProgressBar(
                                    "구글시트 로드",
                                    $"{spreadSheet.name} / {entry.tableName} ({processed}/{totalSheets})",
                                    (float)(processed - 1) / Mathf.Max(1, totalSheets)))
                            {
                                Debug.LogWarning("[GoogleSheet] 사용자가 다운로드를 취소했습니다.");
                                cancelled = true;
                                break;
                            }

                            var url = GoogleSheetLoader.BuildUrl(spreadSheet.spreadsheetId, entry.gid);
                            string text;
                            try
                            {
                                var task = client.GetStringAsync(url);
                                task.Wait();
                                text = task.Result;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[GoogleSheet] '{spreadSheet.name}/{entry.tableName}' 다운로드 실패: {ex.GetBaseException().Message}\nURL: {url}\n시트 공유 권한(링크 보유 사용자: 뷰어)을 확인하세요.");
                                failed++;
                                continue;
                            }

                            Table parsed;
                            try
                            {
                                parsed = TsvParser.Parse(entry.tableName, text, entry.keyColumn);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[GoogleSheet] '{spreadSheet.name}/{entry.tableName}' 파싱 실패: {ex.Message}");
                                failed++;
                                continue;
                            }

                            var safeTableName = MakeSafeFileName(entry.tableName);
                            var path = $"{spreadSheetFolder}/{safeTableName}.asset";

                            var asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
                            if (asset == null)
                            {
                                asset = ScriptableObject.CreateInstance<TableAsset>();
                                asset.Populate(parsed, entry.keyColumn);
                                AssetDatabase.CreateAsset(asset, path);
                            }
                            else
                            {
                                asset.Populate(parsed, entry.keyColumn);
                                EditorUtility.SetDirty(asset);
                            }

                            entry.cachedAsset = asset;

                            if (string.IsNullOrEmpty(entry.rowTypeName))
                            {
                                var resolved = RowTypeResolver.Resolve(entry.tableName, rowTypeCandidates);
                                if (resolved != null)
                                {
                                    entry.rowTypeName = resolved.AssemblyQualifiedName;
                                    autoLinked++;
                                    Debug.Log($"[GoogleSheet] '{spreadSheet.name}/{entry.tableName}' → {resolved.FullName} 자동 연결됨.");
                                }
                            }

                            success++;
                        }
                    }
                }

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[GoogleSheet] 로드 완료 — 성공: {success}, 실패: {failed}, 자동 연결: {autoLinked}");

                AccessorGenerator.Generate(config);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var ch in invalid) name = name.Replace(ch, '_');
            return name;
        }
    }
}
