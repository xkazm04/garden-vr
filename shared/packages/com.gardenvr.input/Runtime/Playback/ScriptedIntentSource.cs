using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>A JSONL line the tiny playback parser could not read. <see cref="LineNumber"/> is 1-based.</summary>
    public sealed class ScriptedIntentParseException : Exception
    {
        public int LineNumber { get; }

        public ScriptedIntentParseException(int lineNumber, string reason)
            : base("line " + lineNumber + ": " + reason)
        {
            LineNumber = lineNumber;
        }
    }

    /// <summary>
    /// Plays hand intents from a JSONL script, one object per line, addressed by target id.
    /// Call <see cref="Tick"/> from tests, or leave the behaviour enabled and it advances with <c>Time.deltaTime</c>.
    /// Strength ramps with the same 0.12 second smoothstep as <see cref="KbmIntentMapper"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ScriptedIntentSource : MonoBehaviour, IHandIntentSource
    {
        const double Epsilon = 0.0001;

        readonly List<Step> _steps = new List<Step>();
        PinchStrengthRamp _ramp = new PinchStrengthRamp();
        double _time;
        int _cursor;
        bool _loaded;
        bool _holding;
        bool _looking;
        double _holdStartedAt;
        double _holdUntil;
        double _lookUntil;
        double _lostUntil;
        string _holdTarget;
        string _lookTarget;

        public event Action<HandIntent> Intent;
        public event Action SystemPause;

        public bool IsPinching { get; private set; }
        public Ray LookRay { get; private set; } = new Ray(Vector3.zero, Vector3.forward);
        public float PinchStrength { get; private set; }
        public bool IsTracked { get; private set; } = true;
        public bool Finished { get; private set; }

        public string BindingHint(HandIntentKind kind)
        {
            return HandBindings.PcHint(kind);
        }

        /// <summary>Replace the script. A bad line throws and leaves the previous script in place.</summary>
        public void Load(string jsonl)
        {
            if (jsonl == null) throw new ArgumentNullException(nameof(jsonl));
            List<Step> parsed = ScriptedIntentParser.Parse(jsonl);
            _steps.Clear();
            _steps.AddRange(parsed);
            _time = 0;
            _cursor = 0;
            _holding = false;
            _looking = false;
            _holdStartedAt = 0;
            _holdUntil = 0;
            _lookUntil = 0;
            _lostUntil = 0;
            _holdTarget = null;
            _lookTarget = null;
            _ramp = new PinchStrengthRamp();
            PinchStrength = 0f;
            IsPinching = false;
            IsTracked = true;
            LookRay = new Ray(Vector3.zero, Vector3.forward);
            _loaded = true;
            Finished = _steps.Count == 0;
        }

        public void Tick(float dt)
        {
            if (!_loaded) return;
            if (dt < 0f) dt = 0f;
            _time += dt;

            while (_cursor < _steps.Count && _time + Epsilon >= _steps[_cursor].T)
            {
                Apply(_steps[_cursor]);
                _cursor++;
            }

            _ramp.Tick(dt);
            PinchStrength = _ramp.Value;

            if (_holding && _time <= _holdUntil + Epsilon)
            {
                Ray ray = RayFor(_holdTarget);
                LookRay = ray;
                float held = (float)Math.Max(0.0, _time - _holdStartedAt);
                Emit(HandIntentKind.PinchHold, ray, PinchStrength, held, _holdTarget);
            }
            else
            {
                _holding = false;
            }

            if (_looking && _time <= _lookUntil + Epsilon)
            {
                Ray ray = RayFor(_lookTarget);
                LookRay = ray;
                Emit(HandIntentKind.Look, ray, 1f, 0f, _lookTarget);
            }
            else
            {
                _looking = false;
            }

            if (!IsTracked && _time > _lostUntil + Epsilon)
                IsTracked = true;

            IsPinching = _holding && IsTracked;
            Finished = ComputeFinished();
        }

        void Update()
        {
            if (!_loaded || Finished) return;
            Tick(Time.deltaTime);
        }

        void Apply(Step step)
        {
            switch (step.Intent)
            {
                case "PinchHold":
                    _holding = true;
                    _holdStartedAt = step.T;
                    _holdUntil = step.HasDur ? step.T + step.Dur : double.PositiveInfinity;
                    _holdTarget = step.Target;
                    _ramp.Rise();
                    break;
                case "Release":
                    float held = (float)Math.Max(0.0, _time - _holdStartedAt);
                    string releaseTarget = string.IsNullOrEmpty(step.Target) ? _holdTarget : step.Target;
                    _holding = false;
                    Ray releaseRay = RayFor(releaseTarget);
                    LookRay = releaseRay;
                    Emit(HandIntentKind.Release, releaseRay, _ramp.Value, held, releaseTarget);
                    _ramp.Fall();
                    break;
                case "Look":
                    if (step.HasDur)
                    {
                        _looking = true;
                        _lookTarget = step.Target;
                        _lookUntil = step.T + step.Dur;
                    }
                    else
                    {
                        Ray lookRay = RayFor(step.Target);
                        LookRay = lookRay;
                        Emit(HandIntentKind.Look, lookRay, 1f, 0f, step.Target);
                    }
                    break;
                case "Pinch":
                    EmitAt(HandIntentKind.Pinch, step);
                    break;
                case "Poke":
                    EmitAt(HandIntentKind.Poke, step);
                    break;
                case "PalmOpen":
                    EmitAt(HandIntentKind.PalmOpen, step);
                    break;
                case "Lost":
                    IsTracked = false;
                    _lostUntil = step.HasDur ? step.T + step.Dur : _time;
                    break;
            }
        }

        void EmitAt(HandIntentKind kind, Step step)
        {
            Ray ray = RayFor(step.Target);
            LookRay = ray;
            Emit(kind, ray, 1f, 0f, step.Target);
        }

        bool ComputeFinished()
        {
            if (!_loaded) return false;
            if (_steps.Count == 0) return true;
            if (_cursor < _steps.Count) return false;
            for (int i = 0; i < _steps.Count; i++)
            {
                Step step = _steps[i];
                if (step.HasDur && _time + Epsilon < step.T + step.Dur) return false;
            }
            return true;
        }

        static Ray RayFor(string id)
        {
            IntentTarget target;
            if (!string.IsNullOrEmpty(id) && IntentTargetRegistry.TryGet(id, out target) && target != null)
            {
                Vector3 dir = target.transform.position;
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
                return new Ray(Vector3.zero, dir.normalized);
            }
            return new Ray(Vector3.zero, Vector3.forward);
        }

        void Emit(HandIntentKind kind, Ray ray, float strength, float held, string targetId)
        {
            Intent?.Invoke(new HandIntent(kind, ray, strength, held, true, targetId));
        }

        sealed class Step
        {
            public double T;
            public string Intent;
            public string Target;
            public double Dur;
            public bool HasDur;
            public int Line;
        }

        static class ScriptedIntentParser
        {
            public static List<Step> Parse(string text)
            {
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                text = text.Replace("\r\n", "\n").Replace('\r', '\n');
                string[] lines = text.Split('\n');
                var steps = new List<Step>();
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    steps.Add(ParseLine(line, i + 1));
                }
                steps.Sort((a, b) =>
                {
                    int cmp = a.T.CompareTo(b.T);
                    return cmp != 0 ? cmp : a.Line.CompareTo(b.Line);
                });
                return steps;
            }

            static Step ParseLine(string text, int line)
            {
                Dictionary<string, string> map = ParseObject(text, line);
                double t = RequireNumber(map, "t", line, true, out _);
                if (t < 0) throw new ScriptedIntentParseException(line, "negative \"t\"");
                string intent;
                if (!map.TryGetValue("intent", out intent) || string.IsNullOrEmpty(intent))
                    throw new ScriptedIntentParseException(line, "missing \"intent\"");
                if (!IsKnown(intent))
                    throw new ScriptedIntentParseException(line, "unknown intent \"" + intent + "\"");
                bool hasDur;
                double dur = RequireNumber(map, "dur", line, false, out hasDur);
                if (hasDur && dur < 0) throw new ScriptedIntentParseException(line, "negative \"dur\"");
                string target = null;
                map.TryGetValue("target", out target);
                return new Step
                {
                    T = t,
                    Intent = intent,
                    Target = string.IsNullOrEmpty(target) ? null : target,
                    Dur = dur,
                    HasDur = hasDur,
                    Line = line
                };
            }

            static bool IsKnown(string intent)
            {
                switch (intent)
                {
                    case "Look":
                    case "Pinch":
                    case "PinchHold":
                    case "Release":
                    case "Poke":
                    case "PalmOpen":
                    case "Lost":
                        return true;
                    default:
                        return false;
                }
            }

            static double RequireNumber(Dictionary<string, string> map, string key, int line, bool required, out bool present)
            {
                string text;
                present = map.TryGetValue(key, out text);
                if (!present)
                {
                    if (required) throw new ScriptedIntentParseException(line, "missing \"" + key + "\"");
                    return 0;
                }
                double value;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || double.IsNaN(value) || double.IsInfinity(value))
                    throw new ScriptedIntentParseException(line, "bad number for \"" + key + "\"");
                return value;
            }

            static Dictionary<string, string> ParseObject(string text, int line)
            {
                if (text.Length < 2 || text[0] != '{' || text[text.Length - 1] != '}')
                    throw new ScriptedIntentParseException(line, "expected a JSON object");
                string body = text.Substring(1, text.Length - 2);
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                int i = 0;
                bool needComma = false;
                while (true)
                {
                    i = Skip(body, i);
                    if (i >= body.Length) break;
                    if (body[i] == ',')
                    {
                        if (!needComma) throw new ScriptedIntentParseException(line, "unexpected comma");
                        needComma = false;
                        i++;
                        continue;
                    }
                    if (needComma) throw new ScriptedIntentParseException(line, "expected a comma");
                    string key = ReadString(body, ref i, line);
                    i = Skip(body, i);
                    if (i >= body.Length || body[i] != ':')
                        throw new ScriptedIntentParseException(line, "expected ':'");
                    i++;
                    i = Skip(body, i);
                    map[key] = ReadValue(body, ref i, line);
                    needComma = true;
                }
                return map;
            }

            static int Skip(string s, int i)
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
                return i;
            }

            static string ReadString(string s, ref int i, int line)
            {
                if (i >= s.Length || s[i] != '"')
                    throw new ScriptedIntentParseException(line, "expected a string");
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (i >= s.Length) throw new ScriptedIntentParseException(line, "bad escape");
                    char e = s[i++];
                    if (e == '"' || e == '\\' || e == '/') sb.Append(e);
                    else if (e == 'n') sb.Append('\n');
                    else if (e == 't') sb.Append('\t');
                    else throw new ScriptedIntentParseException(line, "bad escape");
                }
                throw new ScriptedIntentParseException(line, "unterminated string");
            }

            static string ReadValue(string s, ref int i, int line)
            {
                if (i >= s.Length) throw new ScriptedIntentParseException(line, "expected a value");
                if (s[i] == '"') return ReadString(s, ref i, line);
                int start = i;
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ',' || c == ' ' || c == '\t') break;
                    i++;
                }
                if (i == start) throw new ScriptedIntentParseException(line, "expected a value");
                return s.Substring(start, i - start);
            }
        }
    }
}
