using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>A key in a <see cref="SettingsRegistry"/>. Constructed by <see cref="SettingsRegistry.Define{T}"/>.</summary>
    public sealed class SettingKey<T>
    {
        public string Name { get; }
        public T Default { get; }

        public SettingKey(string name, T defaultValue)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("setting name");
            Name = name;
            Default = defaultValue;
        }
    }

    /// <summary>
    /// Closed list of settings. Callers only get and set keys this registry defined.
    /// A value equal to its default is not stored and is not written.
    /// </summary>
    public sealed class SettingsRegistry
    {
        readonly List<string> _order = new List<string>();
        readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();
        readonly Dictionary<string, object> _defaults = new Dictionary<string, object>();
        readonly Dictionary<string, object> _stored = new Dictionary<string, object>();

        public SettingKey<T> Define<T>(string name, T defaultValue)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("setting name");
            if (_types.ContainsKey(name)) throw new ArgumentException("duplicate setting '" + name + "'");
            var type = typeof(T);
            if (!Supported(type)) throw new ArgumentException("unsupported setting type " + type.Name);
            if (type == typeof(double))
            {
                var number = (double)(object)defaultValue;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new ArgumentException("a setting default must be a finite number");
            }
            _types[name] = type;
            _defaults[name] = defaultValue;
            _order.Add(name);
            return new SettingKey<T>(name, defaultValue);
        }

        public T Get<T>(SettingKey<T> key)
        {
            Require(key);
            object stored;
            if (_stored.TryGetValue(key.Name, out stored)) return (T)stored;
            return key.Default;
        }

        public void Set<T>(SettingKey<T> key, T value)
        {
            Require(key);
            if (value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                throw new ArgumentException("a setting value must be a finite number");
            if (object.Equals(value, key.Default)) _stored.Remove(key.Name);
            else _stored[key.Name] = value;
        }

        public bool IsStored(string name) { return _stored.ContainsKey(name); }

        /// <summary>Only values that differ from their defaults. Definition order.</summary>
        public JsonObject ToJson()
        {
            var obj = new JsonObject();
            for (int i = 0; i < _order.Count; i++)
            {
                object stored;
                if (_stored.TryGetValue(_order[i], out stored))
                    obj.Set(_order[i], Box(stored));
            }
            return obj;
        }

        /// <summary>Unknown keys are rejected. Values equal to the default are not stored. A failed read leaves the previous values.</summary>
        public void ReadJson(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var next = new Dictionary<string, object>();
            foreach (var kv in obj.Members)
            {
                Type type;
                if (!_types.TryGetValue(kv.Key, out type))
                    throw new ArgumentException("unknown setting '" + kv.Key + "'");
                object parsed;
                try { parsed = Unbox(kv.Value, type); }
                catch (Exception ex) { throw new FormatException("setting '" + kv.Key + "' has the wrong type", ex); }
                if (object.Equals(parsed, _defaults[kv.Key])) continue;
                next[kv.Key] = parsed;
            }
            _stored.Clear();
            foreach (var kv in next) _stored[kv.Key] = kv.Value;
        }

        void Require<T>(SettingKey<T> key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            Type type;
            if (!_types.TryGetValue(key.Name, out type) || type != typeof(T) || !object.Equals(_defaults[key.Name], key.Default))
                throw new ArgumentException("unknown setting '" + key.Name + "'");
        }

        static bool Supported(Type type)
        {
            return type == typeof(bool) || type == typeof(int) || type == typeof(long)
                || type == typeof(double) || type == typeof(string);
        }

        static JsonValue Box(object value)
        {
            if (value is bool b) return JsonValue.Bool(b);
            if (value is int i) return JsonValue.Number(i);
            if (value is long l) return JsonValue.Number(l);
            if (value is double d) return JsonValue.Number(d);
            if (value is string s) return JsonValue.String(s);
            throw new InvalidOperationException("unsupported stored setting");
        }

        static object Unbox(JsonValue value, Type type)
        {
            if (type == typeof(bool)) return value.AsBool();
            if (type == typeof(int)) return value.AsInt();
            if (type == typeof(long)) return value.AsLong();
            if (type == typeof(double)) return value.AsDouble();
            if (type == typeof(string)) return value.AsString();
            throw new InvalidOperationException("unsupported setting type");
        }
    }
}
