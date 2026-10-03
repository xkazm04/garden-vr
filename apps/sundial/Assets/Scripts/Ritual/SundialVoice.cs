using System;
using System.Collections.Generic;
using GardenVR.Audio;
using GardenVR.Input;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Optional dusk narration. A line starts on a hand intent, never on a timer ahead of one.
    /// A line that is still going when the next intent arrives fades out over 150 ms and is not queued.
    /// The guide stays off until the save says otherwise. With the guide off, the same words sit on screen.
    /// </summary>
    public sealed class SundialVoice
    {
        public const float CutFadeSeconds = 0.15f;
        public const string OpenLine = "vo.sun.open.01";
        public const string LastLine = "vo.sun.last.01";
        public const string CloseLine = "vo.sun.close.01";
        public const string FirstLine = "vo.sun.first.01";

        public static readonly string[] InLines = { "vo.sun.in.01", "vo.sun.in.02" };
        public static readonly string[] OutLines = { "vo.sun.out.01", "vo.sun.out.02" };

        public bool Enabled;

        readonly AudioCueService _audio;
        readonly Func<string, bool> _play;
        readonly Action<string> _caption;

        bool _held;
        bool _opened;
        bool _closed;
        int _in;
        int _out;
        float _captionLeft;

        public List<string> Started { get; private set; }
        public List<float> StartedAt { get; private set; }

        public SundialVoice(AudioCueService audio, Func<string, bool> play, Action<string> caption)
        {
            _audio = audio;
            _play = play ?? (id => audio != null && audio.Play(id));
            _caption = caption;
            Started = new List<string>();
            StartedAt = new List<float>();
        }

        public void ResetRitual()
        {
            _held = false;
            _opened = false;
            _closed = false;
            ShowCaption(null, 0f);
        }

        /// <summary>A pinch that ends without a Release can speak on the next hold.</summary>
        public void ObservePinching(bool pinching)
        {
            if (!pinching) _held = false;
        }

        public void Tick(float dt)
        {
            if (_captionLeft <= 0f) return;
            if (dt < 0f) dt = 0f;
            _captionLeft -= dt;
            if (_captionLeft > 0f) return;
            _captionLeft = 0f;
            if (_caption != null) _caption(null);
        }

        /// <summary>
        /// The ritual has begun. A hold that starts it owns the open line, so the same hold does not also speak an in-line.
        /// </summary>
        public void OnRitualStart(bool fromHold)
        {
            if (fromHold) _held = true;
            if (_opened) return;
            _opened = true;
            Speak(OpenLine);
        }

        public void OnIntent(HandIntent intent, int breaths, int target)
        {
            if (intent.Kind == HandIntentKind.PinchHold)
            {
                if (_held) return;
                _held = true;
                Speak(ChooseHold(breaths, target));
                return;
            }
            if (intent.Kind == HandIntentKind.Release)
            {
                _held = false;
                Speak(Next(OutLines, ref _out));
            }
        }

        public void OnComplete()
        {
            if (_closed) return;
            _closed = true;
            Speak(CloseLine);
        }

        string ChooseHold(int breaths, int target)
        {
            if (target > 1 && breaths >= target - 1) return LastLine;
            if (!_opened)
            {
                _opened = true;
                return OpenLine;
            }
            return Next(InLines, ref _in);
        }

        void Speak(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!Enabled)
            {
                ShowCaption(LineText(id), WindowOf(id));
                return;
            }
            if (_audio != null && _audio.IsBusActive("voice"))
                _audio.FadeBus("voice", CutFadeSeconds);
            if (_play == null || !_play(id)) return;
            Started.Add(id);
            StartedAt.Add(_audio != null ? _audio.Now : 0f);
            ShowCaption(null, 0f);
        }

        void ShowCaption(string text, float seconds)
        {
            if (_caption == null) return;
            if (string.IsNullOrEmpty(text) || seconds <= 0f)
            {
                _captionLeft = 0f;
                _caption(null);
                return;
            }
            _captionLeft = seconds;
            _caption(text);
        }

        static string Next(string[] lines, ref int cursor)
        {
            if (lines == null || lines.Length == 0) return null;
            int index = cursor % lines.Length;
            if (index < 0) index = 0;
            cursor++;
            return lines[index];
        }

        /// <summary>On-screen stand-in used when the guide is off. Same words as the baked line.</summary>
        public static string LineText(string id)
        {
            switch (id)
            {
                case OpenLine: return "Three breaths to close the day.";
                case "vo.sun.in.01": return "Breathe in.";
                case "vo.sun.in.02": return "In, slowly.";
                case "vo.sun.out.01": return "And out.";
                case "vo.sun.out.02": return "Let it go.";
                case LastLine: return "One more.";
                case CloseLine: return "That's the day. Goodnight.";
                case FirstLine: return "Here is your day.";
                default: return null;
            }
        }

        public static float WindowOf(string id)
        {
            if (id == null) return 4f;
            if (id == LastLine || id.IndexOf(".in.", StringComparison.Ordinal) >= 0) return 1.8f;
            if (id.IndexOf(".out.", StringComparison.Ordinal) >= 0) return 2.5f;
            return 4f;
        }
    }
}
