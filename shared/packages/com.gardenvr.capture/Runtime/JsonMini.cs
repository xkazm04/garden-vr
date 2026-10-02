using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GardenVR.Capture
{
    /// <summary>A small JSON reader for framings.json. Objects, arrays, strings, numbers, true, false, null.</summary>
    public sealed class JsonValue
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type;
        public bool Bool;
        public double Number;
        public string Str;
        public List<JsonValue> Arr;
        public Dictionary<string, JsonValue> Obj;

        public bool IsNull { get { return Type == Kind.Null; } }

        public bool Has(string key)
        {
            return Type == Kind.Object && Obj != null && Obj.ContainsKey(key);
        }

        public JsonValue Get(string key)
        {
            JsonValue value;
            if (Type == Kind.Object && Obj != null && Obj.TryGetValue(key, out value)) return value;
            return null;
        }

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new FormatException("json is null");
            int i = 0;
            JsonValue value = ParseValue(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException("trailing json at " + i);
            return value;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static JsonValue ParseValue(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of json");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return new JsonValue { Type = Kind.String, Str = ParseString(s, ref i) };
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') return ParseNull(s, ref i);
            if (c == '-' || char.IsDigit(c)) return ParseNumber(s, ref i);
            throw new FormatException("unexpected '" + c + "' at " + i);
        }

        static JsonValue ParseObject(string s, ref int i)
        {
            var obj = new Dictionary<string, JsonValue>();
            i++;
            Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return new JsonValue { Type = Kind.Object, Obj = obj }; }
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("object key expected at " + i);
                string key = ParseString(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("':' expected at " + i);
                i++;
                obj[key] = ParseValue(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("unclosed object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new FormatException("',' or '}' expected at " + i);
            }
            return new JsonValue { Type = Kind.Object, Obj = obj };
        }

        static JsonValue ParseArray(string s, ref int i)
        {
            var arr = new List<JsonValue>();
            i++;
            Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return new JsonValue { Type = Kind.Array, Arr = arr }; }
            while (true)
            {
                arr.Add(ParseValue(s, ref i));
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("unclosed array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new FormatException("',' or ']' expected at " + i);
            }
            return new JsonValue { Type = Kind.Array, Arr = arr };
        }

        static string ParseString(string s, ref int i)
        {
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("bad escape");
                char e = s[i++];
                if (e == '"' || e == '\\' || e == '/') sb.Append(e);
                else if (e == 'b') sb.Append('\b');
                else if (e == 'f') sb.Append('\f');
                else if (e == 'n') sb.Append('\n');
                else if (e == 'r') sb.Append('\r');
                else if (e == 't') sb.Append('\t');
                else if (e == 'u')
                {
                    if (i + 4 > s.Length) throw new FormatException("bad unicode escape");
                    int code = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    sb.Append((char)code);
                    i += 4;
                }
                else throw new FormatException("bad escape");
            }
            throw new FormatException("unterminated string");
        }

        static JsonValue ParseBool(string s, ref int i)
        {
            if (Match(s, ref i, "true")) return new JsonValue { Type = Kind.Bool, Bool = true };
            if (Match(s, ref i, "false")) return new JsonValue { Type = Kind.Bool, Bool = false };
            throw new FormatException("bad bool at " + i);
        }

        static JsonValue ParseNull(string s, ref int i)
        {
            if (Match(s, ref i, "null")) return new JsonValue { Type = Kind.Null };
            throw new FormatException("bad null at " + i);
        }

        static bool Match(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length) return false;
            for (int k = 0; k < word.Length; k++)
                if (s[i + k] != word[k]) return false;
            i += word.Length;
            return true;
        }

        static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            if (i >= s.Length || !char.IsDigit(s[i])) throw new FormatException("bad number at " + start);
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                if (i >= s.Length || !char.IsDigit(s[i])) throw new FormatException("bad number at " + start);
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                if (i >= s.Length || !char.IsDigit(s[i])) throw new FormatException("bad number at " + start);
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            string token = s.Substring(start, i - start);
            double number;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                throw new FormatException("bad number " + token);
            return new JsonValue { Type = Kind.Number, Number = number };
        }
    }
}
