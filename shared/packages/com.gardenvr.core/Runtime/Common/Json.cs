using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GardenVR.Core
{
    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// Minimal JSON. Objects, arrays, strings (with escapes), numbers in the invariant culture, bools and null.
    /// Unknown object members stay on the <see cref="JsonObject"/> and are written back.
    /// <see cref="JsonObject.Passthrough"/> is the dictionary a typed reader keeps for members it does not know.
    /// </summary>
    public static class Json
    {
        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return new Parser(text).Parse();
        }

        public static JsonObject ParseObject(string text)
        {
            var value = Parse(text);
            var obj = value as JsonObject;
            if (obj == null) throw new FormatException("expected a JSON object");
            return obj;
        }

        public static string Write(JsonValue value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var sb = new StringBuilder();
            value.WriteTo(sb);
            return sb.ToString();
        }

        internal static void WriteString(StringBuilder sb, string text)
        {
            sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string text) { _s = text; }

            public JsonValue Parse()
            {
                if (_s.Length > 0 && _s[0] == '\uFEFF') _i = 1;
                var value = ParseValue();
                SkipWs();
                if (_i != _s.Length) throw Error("trailing input");
                return value;
            }

            JsonValue ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) throw Error("truncated");
                char c = _s[_i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return JsonValue.String(ParseString());
                if (c == 't') { Literal("true"); return JsonValue.Bool(true); }
                if (c == 'f') { Literal("false"); return JsonValue.Bool(false); }
                if (c == 'n') { Literal("null"); return JsonValue.Null(); }
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                throw Error("unexpected '" + c + "'");
            }

            JsonObject ParseObject()
            {
                Expect('{');
                var obj = new JsonObject();
                SkipWs();
                if (Consume('}')) return obj;
                while (true)
                {
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != '"') throw Error("expected a key");
                    string key = ParseString();
                    SkipWs();
                    Expect(':');
                    obj.Set(key, ParseValue());
                    SkipWs();
                    if (Consume('}')) return obj;
                    if (Consume(',')) continue;
                    throw Error("expected ',' or '}'");
                }
            }

            JsonArray ParseArray()
            {
                Expect('[');
                var array = new JsonArray();
                SkipWs();
                if (Consume(']')) return array;
                while (true)
                {
                    array.Add(ParseValue());
                    SkipWs();
                    if (Consume(']')) return array;
                    if (Consume(',')) continue;
                    throw Error("expected ',' or ']'");
                }
            }

            string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) throw Error("truncated string");
                        char e = _s[_i++];
                        switch (e)
                        {
                            case '"':
                            case '\\':
                            case '/': sb.Append(e); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_i + 4 > _s.Length) throw Error("truncated unicode escape");
                                int cp = 0;
                                for (int k = 0; k < 4; k++)
                                {
                                    int h = Hex(_s[_i++]);
                                    if (h < 0) throw Error("bad unicode escape");
                                    cp = (cp << 4) | h;
                                }
                                sb.Append((char)cp);
                                break;
                            default: throw Error("bad escape");
                        }
                    }
                    else if (c < 0x20) throw Error("raw control character");
                    else sb.Append(c);
                }
                throw Error("truncated string");
            }

            JsonValue ParseNumber()
            {
                int start = _i;
                if (_s[_i] == '-')
                {
                    _i++;
                    if (_i >= _s.Length) throw Error("truncated number");
                }
                if (_s[_i] == '0') _i++;
                else if (_s[_i] >= '1' && _s[_i] <= '9')
                {
                    while (_i < _s.Length && IsDigit(_s[_i])) _i++;
                }
                else throw Error("expected a digit");
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    if (_i >= _s.Length || !IsDigit(_s[_i])) throw Error("truncated fraction");
                    while (_i < _s.Length && IsDigit(_s[_i])) _i++;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    if (_i >= _s.Length || !IsDigit(_s[_i])) throw Error("truncated exponent");
                    while (_i < _s.Length && IsDigit(_s[_i])) _i++;
                }
                string lexeme = _s.Substring(start, _i - start);
                double check;
                if (!double.TryParse(lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out check)
                    || double.IsNaN(check) || double.IsInfinity(check))
                    throw Error("bad number");
                return JsonValue.NumberLexeme(lexeme);
            }

            void Literal(string word)
            {
                for (int k = 0; k < word.Length; k++)
                {
                    if (_i >= _s.Length || _s[_i] != word[k]) throw Error("expected " + word);
                    _i++;
                }
            }

            void Expect(char c)
            {
                if (_i >= _s.Length || _s[_i] != c) throw Error("expected '" + c + "'");
                _i++;
            }

            bool Consume(char c)
            {
                if (_i < _s.Length && _s[_i] == c) { _i++; return true; }
                return false;
            }

            void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r') return;
                    _i++;
                }
            }

            static bool IsDigit(char c) { return c >= '0' && c <= '9'; }

            static int Hex(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            FormatException Error(string message)
            {
                return new FormatException(message + " at " + _i.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    public abstract class JsonValue
    {
        public JsonKind Kind { get; }

        protected JsonValue(JsonKind kind) { Kind = kind; }

        public bool IsNull { get { return Kind == JsonKind.Null; } }

        public static JsonValue Null() { return JsonNull.Instance; }
        public static JsonValue Bool(bool value) { return new JsonBool(value); }
        public static JsonValue String(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new JsonString(value);
        }
        public static JsonValue Number(int value)
        {
            return new JsonNumber(value.ToString(CultureInfo.InvariantCulture));
        }

        public static JsonValue Number(long value)
        {
            return new JsonNumber(value.ToString(CultureInfo.InvariantCulture));
        }
        public static JsonValue Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("JSON numbers must be finite");
            string lexeme;
            if (value >= -9007199254740992d && value <= 9007199254740992d && value == Math.Round(value))
                lexeme = ((long)value).ToString(CultureInfo.InvariantCulture);
            else
                lexeme = value.ToString("G17", CultureInfo.InvariantCulture);
            return new JsonNumber(lexeme);
        }

        internal static JsonValue NumberLexeme(string lexeme) { return new JsonNumber(lexeme); }

        public abstract void WriteTo(StringBuilder sb);

        public JsonObject AsObject()
        {
            var obj = this as JsonObject;
            if (obj == null) throw new InvalidOperationException("expected an object");
            return obj;
        }

        public JsonArray AsArray()
        {
            var array = this as JsonArray;
            if (array == null) throw new InvalidOperationException("expected an array");
            return array;
        }

        public string AsString()
        {
            var s = this as JsonString;
            if (s == null) throw new InvalidOperationException("expected a string");
            return s.Value;
        }

        public bool AsBool()
        {
            var b = this as JsonBool;
            if (b == null) throw new InvalidOperationException("expected a bool");
            return b.Value;
        }

        public double AsDouble()
        {
            var n = this as JsonNumber;
            if (n == null) throw new InvalidOperationException("expected a number");
            return n.ToDouble();
        }

        public long AsLong()
        {
            var n = this as JsonNumber;
            if (n == null) throw new InvalidOperationException("expected a number");
            return n.ToInt64();
        }

        public int AsInt()
        {
            long n = AsLong();
            if (n < int.MinValue || n > int.MaxValue) throw new FormatException("number is outside int range");
            return (int)n;
        }
    }

    public sealed class JsonObject : JsonValue
    {
        readonly List<string> _order = new List<string>();
        readonly Dictionary<string, JsonValue> _map = new Dictionary<string, JsonValue>();

        public JsonObject() : base(JsonKind.Object) { }

        public int Count { get { return _order.Count; } }

        public IEnumerable<KeyValuePair<string, JsonValue>> Members
        {
            get
            {
                for (int i = 0; i < _order.Count; i++)
                    yield return new KeyValuePair<string, JsonValue>(_order[i], _map[_order[i]]);
            }
        }

        public bool Has(string key) { return key != null && _map.ContainsKey(key); }

        public JsonValue Get(string key)
        {
            JsonValue value;
            if (key == null || !_map.TryGetValue(key, out value))
                throw new KeyNotFoundException("missing member '" + key + "'");
            return value;
        }

        public void Set(string key, JsonValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (_map.ContainsKey(key)) _map[key] = value;
            else
            {
                _map[key] = value;
                _order.Add(key);
            }
        }

        public bool Remove(string key)
        {
            if (key == null || !_map.Remove(key)) return false;
            _order.Remove(key);
            return true;
        }

        /// <summary>Members whose names are not in <paramref name="known"/>. Keep this and pass it to <see cref="Restore"/>.</summary>
        public Dictionary<string, JsonValue> Passthrough(params string[] known)
        {
            var skip = new HashSet<string>();
            if (known != null)
            {
                for (int i = 0; i < known.Length; i++)
                    if (known[i] != null) skip.Add(known[i]);
            }
            var extra = new Dictionary<string, JsonValue>();
            for (int i = 0; i < _order.Count; i++)
            {
                if (!skip.Contains(_order[i])) extra[_order[i]] = _map[_order[i]];
            }
            return extra;
        }

        /// <summary>Writes a passthrough dictionary back. Known keys already on this object are left as they are.</summary>
        public void Restore(IDictionary<string, JsonValue> passthrough)
        {
            if (passthrough == null) return;
            foreach (var kv in passthrough)
            {
                if (kv.Key == null || kv.Value == null) throw new ArgumentException("passthrough entry is incomplete");
                if (!Has(kv.Key)) Set(kv.Key, kv.Value);
            }
        }

        public override void WriteTo(StringBuilder sb)
        {
            sb.Append('{');
            for (int i = 0; i < _order.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Json.WriteString(sb, _order[i]);
                sb.Append(':');
                _map[_order[i]].WriteTo(sb);
            }
            sb.Append('}');
        }
    }

    public sealed class JsonArray : JsonValue
    {
        readonly List<JsonValue> _items = new List<JsonValue>();

        public JsonArray() : base(JsonKind.Array) { }

        public int Count { get { return _items.Count; } }
        public JsonValue this[int index] { get { return _items[index]; } }

        public void Add(JsonValue value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            _items.Add(value);
        }

        public override void WriteTo(StringBuilder sb)
        {
            sb.Append('[');
            for (int i = 0; i < _items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                _items[i].WriteTo(sb);
            }
            sb.Append(']');
        }
    }

    sealed class JsonNull : JsonValue
    {
        public static readonly JsonNull Instance = new JsonNull();
        JsonNull() : base(JsonKind.Null) { }
        public override void WriteTo(StringBuilder sb) { sb.Append("null"); }
    }

    sealed class JsonBool : JsonValue
    {
        public readonly bool Value;
        public JsonBool(bool value) : base(JsonKind.Bool) { Value = value; }
        public override void WriteTo(StringBuilder sb) { sb.Append(Value ? "true" : "false"); }
    }

    sealed class JsonString : JsonValue
    {
        public readonly string Value;
        public JsonString(string value) : base(JsonKind.String) { Value = value; }
        public override void WriteTo(StringBuilder sb) { Json.WriteString(sb, Value); }
    }

    sealed class JsonNumber : JsonValue
    {
        readonly string _lexeme;
        public JsonNumber(string lexeme) : base(JsonKind.Number) { _lexeme = lexeme; }

        public double ToDouble()
        {
            return double.Parse(_lexeme, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public long ToInt64()
        {
            for (int i = 0; i < _lexeme.Length; i++)
            {
                char c = _lexeme[i];
                if (c == '.' || c == 'e' || c == 'E') throw new FormatException("expected an integer");
            }
            return long.Parse(_lexeme, CultureInfo.InvariantCulture);
        }

        public override void WriteTo(StringBuilder sb) { sb.Append(_lexeme); }
    }
}
