using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public class GoogleSheetTableManager : MonoBehaviour
    {
        [SerializeField] private GoogleSheetConfig config;
        [SerializeField] private bool loadOnAwake = true;

        private readonly Dictionary<string, Table> _tables = new Dictionary<string, Table>();
        private readonly Dictionary<Type, TypedEntry> _typed = new Dictionary<Type, TypedEntry>();
        private readonly Dictionary<(string spreadSheet, string table), TypedEntry> _typedByTable
            = new Dictionary<(string, string), TypedEntry>();

        public static GoogleSheetTableManager Instance { get; private set; }
        public bool IsLoaded { get; private set; }

        // 정적 콜백 리스트: 자동 생성된 GoogleSheetAccessors가 RegisterOnLoad로 등록한다.
        // LoadAll 끝에 모든 콜백을 호출해 강타입 액세서 필드를 채운다.
        // partial class 확장 대신 이 패턴을 쓰는 이유: UPM 패키지 어셈블리 격리 때문에
        // 사용자의 Generated 코드와 Manager가 서로 다른 어셈블리에 있게 되고,
        // C# partial class는 같은 어셈블리 안에서만 동작하기 때문이다.
        private static readonly List<Action<GoogleSheetTableManager>> _onLoadCallbacks
            = new List<Action<GoogleSheetTableManager>>();

        /// <summary>
        /// LoadAll 완료 후 호출될 콜백을 등록한다. 동일 콜백 중복 등록은 무시된다.
        /// 보통 자동 생성된 GoogleSheetAccessors.Register()가 호출.
        /// </summary>
        public static void RegisterOnLoad(Action<GoogleSheetTableManager> callback)
        {
            if (callback == null) return;
            if (_onLoadCallbacks.Contains(callback)) return;
            _onLoadCallbacks.Add(callback);
        }

        /// <summary>
        /// 등록한 콜백을 해제한다.
        /// </summary>
        public static void UnregisterOnLoad(Action<GoogleSheetTableManager> callback)
        {
            if (callback == null) return;
            _onLoadCallbacks.Remove(callback);
        }

        private class TypedEntry
        {
            public IList List;
            public IDictionary Dict;
            public IDictionary GroupedDict;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[GoogleSheetTableManager] Multiple instances detected; the latest one wins.");
            Instance = this;
            if (loadOnAwake) LoadAll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Table GetTable(string tableName)
        {
            _tables.TryGetValue(tableName, out var table);
            return table;
        }

        public bool TryGetTable(string tableName, out Table table)
        {
            return _tables.TryGetValue(tableName, out table);
        }

        public IReadOnlyList<TRow> GetList<TRow>() where TRow : class
        {
            if (_typed.TryGetValue(typeof(TRow), out var entry) && entry.List is List<TRow> typed)
                return typed;
            return null;
        }

        public IReadOnlyList<TRow> GetList<TRow>(string spreadSheet, string tableName) where TRow : class
        {
            if (_typedByTable.TryGetValue((spreadSheet, tableName), out var entry) && entry.List is List<TRow> typed)
                return typed;
            return null;
        }

        public IReadOnlyDictionary<TKey, TRow> GetDict<TKey, TRow>() where TRow : class
        {
            if (_typed.TryGetValue(typeof(TRow), out var entry) && entry.Dict is Dictionary<TKey, TRow> typed)
                return typed;
            return null;
        }

        public IReadOnlyDictionary<TKey, TRow> GetDict<TKey, TRow>(string spreadSheet, string tableName) where TRow : class
        {
            if (_typedByTable.TryGetValue((spreadSheet, tableName), out var entry) && entry.Dict is Dictionary<TKey, TRow> typed)
                return typed;
            return null;
        }

        public IReadOnlyDictionary<TKey, List<TRow>> GetGroupedDict<TKey, TRow>() where TRow : class
        {
            if (_typed.TryGetValue(typeof(TRow), out var entry) && entry.GroupedDict is Dictionary<TKey, List<TRow>> typed)
                return typed;
            return null;
        }

        public IReadOnlyDictionary<TKey, List<TRow>> GetGroupedDict<TKey, TRow>(string spreadSheet, string tableName) where TRow : class
        {
            if (_typedByTable.TryGetValue((spreadSheet, tableName), out var entry) && entry.GroupedDict is Dictionary<TKey, List<TRow>> typed)
                return typed;
            return null;
        }

        public TRow Find<TKey, TRow>(TKey key) where TRow : class
        {
            var dict = GetDict<TKey, TRow>();
            return (dict != null && dict.TryGetValue(key, out var row)) ? row : null;
        }

        public void Load(Action<bool> handler = null)
        {
            if (IsLoaded)
            {
                handler?.Invoke(true);
                return;
            }

            bool hasError = false;
            var ok = LoadAll(err =>
            {
                hasError = true;
                Debug.LogError(err);
            });
            handler?.Invoke(ok && !hasError);
        }

        public bool LoadAll(Action<string> onError = null)
        {
            IsLoaded = false;
            _tables.Clear();
            _typed.Clear();
            _typedByTable.Clear();

            if (config == null)
            {
                onError?.Invoke("[GoogleSheetTableManager] Config is not assigned.");
                return false;
            }

            foreach (var spreadSheet in config.spreadSheets)
            {
                if (spreadSheet == null || spreadSheet.sheets == null) continue;

                foreach (var entry in spreadSheet.sheets)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.tableName))
                        continue;

                    if (entry.cachedAsset == null)
                    {
                        onError?.Invoke($"[GoogleSheetTableManager] '{entry.tableName}' has no cached asset. Run ChopChopGames/GoogleSheet/LoadTables first.");
                        continue;
                    }

                    if (_tables.ContainsKey(entry.tableName))
                        Debug.LogWarning($"[GoogleSheetTableManager] Duplicate table name '{entry.tableName}' across SpreadSheets — last one wins for type-keyed lookup. Use Get*<...>(spreadSheet, table) overloads to disambiguate.");

                    _tables[entry.tableName] = entry.cachedAsset.ToRuntimeTable();

                    if (string.IsNullOrEmpty(entry.rowTypeName)) continue;

                    var rowType = Type.GetType(entry.rowTypeName);
                    if (rowType == null)
                    {
                        onError?.Invoke($"[GoogleSheetTableManager] Row type not found: '{entry.rowTypeName}' (table '{entry.tableName}'). Did you rename or remove the class?");
                        continue;
                    }

                    try
                    {
                        var list = TypedTableParser.ParseList(entry.cachedAsset, rowType);
                        var typed = new TypedEntry { List = list };

                        switch (entry.dataStructure)
                        {
                            case DataStructure.Dictionary:
                                if (string.IsNullOrEmpty(entry.keyColumn))
                                    onError?.Invoke($"[GoogleSheetTableManager] '{entry.tableName}' is set to Dictionary but keyColumn is empty.");
                                else
                                    typed.Dict = TypedTableParser.BuildDict(list, rowType, entry.keyColumn);
                                break;
                            case DataStructure.DictionaryOfList:
                                if (string.IsNullOrEmpty(entry.keyColumn))
                                    onError?.Invoke($"[GoogleSheetTableManager] '{entry.tableName}' is set to DictionaryOfList but keyColumn is empty.");
                                else
                                    typed.GroupedDict = TypedTableParser.BuildGroupedDict(list, rowType, entry.keyColumn);
                                break;
                        }

                        _typed[rowType] = typed;
                        _typedByTable[(spreadSheet.name, entry.tableName)] = typed;
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"[GoogleSheetTableManager] Typed parse error for '{entry.tableName}' as {rowType.Name}: {ex.Message}");
                    }
                }
            }

            // 등록된 모든 액세서 콜백 호출 (자동 생성된 GoogleSheetAccessors 등)
            for (int i = 0; i < _onLoadCallbacks.Count; i++)
            {
                try
                {
                    _onLoadCallbacks[i]?.Invoke(this);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GoogleSheetTableManager] OnLoad callback failed: {ex.GetBaseException().Message}");
                }
            }

            IsLoaded = true;
            return true;
        }
    }
}
