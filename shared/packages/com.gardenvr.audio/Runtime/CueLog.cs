using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace GardenVR.Audio
{
    /// <summary>
    /// Development and test log of every cue that actually started.
    /// One JSON object per line: <c>{t, cue, clip, bus, gainDb, pos}</c>.
    /// </summary>
    public sealed class CueLog
    {
        readonly string _path;
        readonly bool _enabled;

        public string Path { get { return _path; } }
        public bool Enabled { get { return _enabled; } }

        CueLog(string path, bool enabled)
        {
            _path = path;
            _enabled = enabled;
        }

        /// <summary>
        /// Opens the log. <paramref name="overridePath"/> wins, then the <c>GARDEN_CUE_LOG</c> environment
        /// variable, then <c>Application.persistentDataPath/logs/cues.jsonl</c>. The file is replaced so one
        /// play session is one log. Release players leave it closed.
        /// </summary>
        public static CueLog Open(string overridePath)
        {
            bool enabled = Debug.isDebugBuild || Application.isEditor;
            if (!enabled) return new CueLog(null, false);

            string path = overridePath;
            if (string.IsNullOrEmpty(path)) path = System.Environment.GetEnvironmentVariable("GARDEN_CUE_LOG");
            if (string.IsNullOrEmpty(path))
                path = System.IO.Path.Combine(Application.persistentDataPath, "logs", "cues.jsonl");

            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, string.Empty, new UTF8Encoding(false));
            return new CueLog(path, true);
        }

        public void Append(float t, string cue, string clip, string bus, float gainDb, Vector3? pos)
        {
            if (!_enabled) return;
            string posJson = "null";
            if (pos.HasValue)
            {
                Vector3 p = pos.Value;
                posJson = "[" + F(p.x) + "," + F(p.y) + "," + F(p.z) + "]";
            }
            string line = "{\"t\":" + F(t)
                + ",\"cue\":\"" + Esc(cue)
                + "\",\"clip\":\"" + Esc(clip)
                + "\",\"bus\":\"" + Esc(bus)
                + "\",\"gainDb\":" + F(gainDb)
                + ",\"pos\":" + posJson + "}";
            File.AppendAllText(_path, line + "\n", new UTF8Encoding(false));
        }

        static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string Esc(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
