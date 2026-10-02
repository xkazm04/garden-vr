using System;
using System.Collections.Generic;

namespace GardenVR.Capture
{
    public static class CaptureState
    {
        /// <summary>Parse <c>k=v,k=v</c>. The value may contain '='. Empty text is an empty map.</summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(text)) return map;
            string[] parts = text.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string pair = parts[i].Trim();
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                if (eq <= 0) throw new FormatException("state pair missing '=': " + pair);
                string key = pair.Substring(0, eq).Trim();
                string value = pair.Substring(eq + 1).Trim();
                if (key.Length == 0) throw new FormatException("state pair missing a key: " + pair);
                map[key] = value;
            }
            return map;
        }
    }
}
