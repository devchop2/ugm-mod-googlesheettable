using ChopChopGames.UGM.GoogleSheetTable;
using System;
using System.Collections.Generic;
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

        // ----------------------------------------------------------
        // Top-level batch download
        // ----------------------------------------------------------
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

            var folder = ResolveOutputFolder(config);
            if (folder == null) return; // 에러 다이얼로그는 ResolveOutputFolder 내에서 처리
            EnsureFolder(folder);

            // [v0.1.1] LoadTables 직전에 항상 cachedAsset 링크 자동 복구를 실행한다.
            // 외부 편집 / 파일 이동 / 브랜치 머지로 끊어진 참조가 있을 경우 다운로드 실패로 잘못 진단되는 걸 방지.
            int relinked = ConfigSyncer.RepairAssetLinks(config, folder, silent: true);
            if (relinked > 0)
                Debug.Log($"[GoogleSheet] LoadTables 시작 전 cachedAsset 자동 복구: {relinked}개 재연결");

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

                            if (DownloadOneInternal(client, spreadSheet, entry, spreadSheetFolder, rowTypeCandidates, ref autoLinked))
                                success++;
                            else
                                failed++;
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

        // ----------------------------------------------------------
        // [v0.1.1] Single-table download — used by per-row reload buttons
        // ----------------------------------------------------------
        public static bool DownloadOne(GoogleSheetConfig config,
                                       GoogleSheetConfig.SpreadSheetEntry spreadSheet,
                                       GoogleSheetConfig.SheetEntry entry)
        {
            if (config == null || spreadSheet == null || entry == null)
            {
                Debug.LogError("[GoogleSheet] DownloadOne: 인자가 null 입니다.");
                return false;
            }

            var folder = ResolveOutputFolder(config);
            if (folder == null) return false;
            EnsureFolder(folder);

            var safeSpreadSheetName = MakeSafeFileName(spreadSheet.name);
            var spreadSheetFolder = $"{folder}/{safeSpreadSheetName}";
            EnsureFolder(spreadSheetFolder);

            var rowTypeCandidates = RowTypeResolver.CollectRowTypes();
            int autoLinked = 0;
            bool ok;
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                    ok = DownloadOneInternal(client, spreadSheet, entry, spreadSheetFolder, rowTypeCandidates, ref autoLinked);

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (ok)
                Debug.Log($"[GoogleSheet] '{spreadSheet.name} / {entry.tableName}' 단일 다운로드 완료" + (autoLinked > 0 ? " (rowType 자동 연결)" : ""));
            return ok;
        }

        // ----------------------------------------------------------
        // Internal: 단일 시트 다운로드 + .asset 갱신 + cachedAsset 연결
        // 성공 시 true, 실패 시 false. 호출자는 success/failed 카운터 처리.
        // ----------------------------------------------------------
        private static bool DownloadOneInternal(HttpClient client,
                                                GoogleSheetConfig.SpreadSheetEntry spreadSheet,
                                                GoogleSheetConfig.SheetEntry entry,
                                                string spreadSheetFolder,
                                                List<Type> rowTypeCandidates,
                                                ref int autoLinked)
        {
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
                Debug.LogError(
                    $"[GoogleSheet] 구글로부터 다운로드 실패 — '{spreadSheet.name} / {entry.tableName}'\n" +
                    $"  사유: {ex.GetBaseException().Message}\n" +
                    $"  URL: {url}\n" +
                    $"  ▶ 해결: 시트 공유 권한을 '링크가 있는 모든 사용자: 뷰어' 로 변경하세요.");
                return false;
            }

            // [v0.1.1] silent 실패 감지 — 비공개 시트는 200 OK 와 함께 로그인 HTML 을 반환한다.
            // TSV 가 아닌 HTML 응답을 받으면 garbage TableAsset 이 생기므로 사전 차단.
            if (LooksLikeHtmlResponse(text))
            {
                Debug.LogError(
                    $"[GoogleSheet] 구글로부터 다운로드 실패 — '{spreadSheet.name} / {entry.tableName}'\n" +
                    $"  사유: TSV 가 아닌 HTML 응답을 받았습니다 (대개 로그인 리다이렉트 = 비공개 시트).\n" +
                    $"  URL: {url}\n" +
                    $"  ▶ 해결: 시트 공유 권한을 '링크가 있는 모든 사용자: 뷰어' 로 변경하세요.");
                return false;
            }

            Table parsed;
            try
            {
                parsed = TsvParser.Parse(entry.tableName, text, entry.keyColumn, entry.headerRow);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[GoogleSheet] 구글로부터 다운로드 실패 — '{spreadSheet.name} / {entry.tableName}' (파싱 오류)\n" +
                    $"  사유: {ex.Message}\n" +
                    $"  URL: {url}");
                return false;
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
            return true;
        }

        // ----------------------------------------------------------
        // outputFolder 검증 헬퍼
        // ----------------------------------------------------------
        public static string ResolveOutputFolder(GoogleSheetConfig config)
        {
            var folder = string.IsNullOrEmpty(config.outputFolder)
                ? "Assets/GoogleSheetTable/Tables"
                : config.outputFolder.Replace('\\', '/').TrimEnd('/');

            if (!folder.StartsWith("Assets/") && folder != "Assets")
            {
                EditorUtility.DisplayDialog("로드 실패", $"outputFolder는 Assets/ 하위여야 합니다: {folder}", "확인");
                return null;
            }
            return folder;
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

        public static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var ch in invalid) name = name.Replace(ch, '_');
            return name;
        }

        // [v0.1.1] 다운로드 응답이 TSV 가 아닌 HTML(로그인 리다이렉트) 인지 휴리스틱 감지.
        // 비공개 시트는 HTTP 200 + HTML 을 반환하므로 try/catch 만으로는 잡히지 않는다.
        private static bool LooksLikeHtmlResponse(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int i = 0;
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == '﻿')) i++;
            if (i >= text.Length) return false;
            int remaining = text.Length - i;
            if (remaining >= 9 && text.Substring(i, 9).Equals("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)) return true;
            if (remaining >= 5 && text.Substring(i, 5).Equals("<html", StringComparison.OrdinalIgnoreCase)) return true;
            if (remaining >= 6 && text.Substring(i, 6).Equals("<HTML>", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
