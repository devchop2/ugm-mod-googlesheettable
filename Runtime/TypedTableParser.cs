using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace ChopChopGames.UGM.GoogleSheetTable
{
    public static class TypedTableParser
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static IList ParseList(TableAsset asset, Type rowType)
        {
            if (asset == null || rowType == null) return null;

            var listType = typeof(List<>).MakeGenericType(rowType);
            var list = (IList)Activator.CreateInstance(listType);

            var members = GetMemberMap(rowType);
            var runtime = asset.ToRuntimeTable();

            for (int r = 0; r < runtime.RowCount; r++)
            {
                var row = runtime.GetRowAt(r);
                var instance = Activator.CreateInstance(rowType);

                foreach (var col in asset.columns)
                {
                    if (string.IsNullOrEmpty(col)) continue;
                    if (!members.TryGetValue(col, out var member)) continue;

                    var raw = row[col];
                    if (TryConvert(raw, member.ValueType, out var value))
                        member.SetValue(instance, value);
                }
                list.Add(instance);
            }
            return list;
        }

        public static IDictionary BuildDict(IList list, Type rowType, string keyMemberName)
        {
            if (list == null || rowType == null || string.IsNullOrEmpty(keyMemberName)) return null;
            var key = ResolveMember(rowType, keyMemberName);
            if (!key.IsValid) return null;

            var dictType = typeof(Dictionary<,>).MakeGenericType(key.ValueType, rowType);
            var dict = (IDictionary)Activator.CreateInstance(dictType);

            foreach (var item in list)
            {
                var k = key.GetValue(item);
                if (k == null) continue;
                if (dict.Contains(k)) continue;
                dict.Add(k, item);
            }
            return dict;
        }

        public static IDictionary BuildGroupedDict(IList list, Type rowType, string keyMemberName)
        {
            if (list == null || rowType == null || string.IsNullOrEmpty(keyMemberName)) return null;
            var key = ResolveMember(rowType, keyMemberName);
            if (!key.IsValid) return null;

            var listType = typeof(List<>).MakeGenericType(rowType);
            var dictType = typeof(Dictionary<,>).MakeGenericType(key.ValueType, listType);
            var dict = (IDictionary)Activator.CreateInstance(dictType);

            foreach (var item in list)
            {
                var k = key.GetValue(item);
                if (k == null) continue;
                if (!dict.Contains(k))
                    dict[k] = Activator.CreateInstance(listType);
                ((IList)dict[k]).Add(item);
            }
            return dict;
        }

        public static Type GetMemberType(Type rowType, string memberName)
        {
            var m = ResolveMember(rowType, memberName);
            return m.IsValid ? m.ValueType : null;
        }

        private static Dictionary<string, MemberAccessor> GetMemberMap(Type type)
        {
            var result = new Dictionary<string, MemberAccessor>(StringComparer.Ordinal);

            foreach (var p in type.GetProperties(MemberFlags))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (!p.CanWrite) continue;
                result[p.Name] = new MemberAccessor(p);
            }

            foreach (var f in type.GetFields(MemberFlags))
            {
                if (f.IsStatic || f.IsNotSerialized) continue;
                if (f.Name.StartsWith("<", StringComparison.Ordinal)) continue;
                if (!result.ContainsKey(f.Name))
                    result[f.Name] = new MemberAccessor(f);
            }
            return result;
        }

        private static MemberAccessor ResolveMember(Type type, string name)
        {
            var p = type.GetProperty(name, MemberFlags);
            if (p != null && p.CanWrite && p.GetIndexParameters().Length == 0)
                return new MemberAccessor(p);
            var f = type.GetField(name, MemberFlags);
            if (f != null && !f.Name.StartsWith("<", StringComparison.Ordinal))
                return new MemberAccessor(f);
            return default;
        }

        public readonly struct MemberAccessor
        {
            private readonly FieldInfo _field;
            private readonly PropertyInfo _property;

            public MemberAccessor(FieldInfo field) { _field = field; _property = null; }
            public MemberAccessor(PropertyInfo property) { _field = null; _property = property; }

            public bool IsValid => _field != null || _property != null;
            public Type ValueType => _field != null ? _field.FieldType : _property?.PropertyType;

            public object GetValue(object instance)
            {
                if (_field != null) return _field.GetValue(instance);
                if (_property != null) return _property.GetValue(instance);
                return null;
            }

            public void SetValue(object instance, object value)
            {
                if (_field != null) _field.SetValue(instance, value);
                else _property?.SetValue(instance, value);
            }
        }

        private static bool TryConvert(string raw, Type type, out object value)
        {
            value = null;

            if (type == typeof(string))
            {
                value = raw ?? string.Empty;
                return true;
            }

            if (string.IsNullOrEmpty(raw))
            {
                if (type.IsArray)
                {
                    value = Array.CreateInstance(type.GetElementType(), 0);
                    return true;
                }
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    value = Activator.CreateInstance(type);
                    return true;
                }
                value = type.IsValueType ? Activator.CreateInstance(type) : null;
                return true;
            }

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return TryConvert(raw, underlying, out value);

            if (type.IsArray)
            {
                var elemType = type.GetElementType();
                var parts = SplitArray(raw);
                var array = Array.CreateInstance(elemType, parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (TryConvert(parts[i], elemType, out var elem))
                        array.SetValue(elem, i);
                }
                value = array;
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = type.GetGenericArguments()[0];
                var parts = SplitArray(raw);
                var list = (IList)Activator.CreateInstance(type);
                foreach (var p in parts)
                {
                    if (TryConvert(p, elemType, out var elem))
                        list.Add(elem);
                }
                value = list;
                return true;
            }

            if (type.IsEnum)
            {
                try { value = Enum.Parse(type, raw, true); return true; }
                catch { return false; }
            }

            if (type == typeof(bool))
            {
                if (bool.TryParse(raw, out var b)) { value = b; return true; }
                if (raw == "1" || raw.Equals("Y", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
                if (raw == "0" || raw.Equals("N", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
                return false;
            }

            try
            {
                value = Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string[] SplitArray(string raw)
        {
            var parts = raw.Split(new[] { ',', '|' }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return parts;
        }
    }
}
