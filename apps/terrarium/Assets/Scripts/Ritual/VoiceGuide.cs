using System;
using System.Collections.Generic;
using GardenVR.Audio;
using GardenVR.Input;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Optional breath narration. Lines start on a hand intent, never on a timer ahead of one.
    /// A line that is still going when the next intent arrives fades out over 150 ms and is not queued.
    /// The guide and the night bed stay off until the save says otherwise.
    /// </summary>
    public sealed class VoiceGuide
    {
        public const float CutFadeSeconds = 0.15f;
        public const float OfferAfterSeconds = 4f;
        public const string ShellId = "shell.voice";
        public const string BedId = "bed.night";
        public const string MidLine = "vo.ter.mid.01";
        public const string LastLine = "vo.ter.last.01";
        public const string ReturnLine = "vo.ter.return.01";
        public const string ResumeLine = "vo.ter.resume.01";

        public static readonly string[] OpenLines = { "vo.ter.open.01", "vo.ter.open.02" };
        public static readonly string[] InLines = { "vo.ter.in.01", "vo.ter.in.02", "vo.ter.in.03" };
        public static readonly string[] OutLines = { "vo.ter.out.01", "vo.ter.out.02", "vo.ter.out.03" };
        public static readonly string[] CloseLines = { "vo.ter.close.01", "vo.ter.close.02" };

        public bool Enabled;

        readonly AudioCueService _audio;
        readonly Func<string, bool> _play;
        readonly Action<string> _caption;

        bool _held;
        bool _opened;
        bool _welcomed;
        bool _mid;
        int _open;
        int _in;
        int _out;
        int _close;
        float _captionLeft;

        public List<string> Started { get; private set; }
        public List<float> StartedAt { get; private set; }

        public VoiceGuide(AudioCueService audio, Func<string, bool> play, Action<string> caption)
        {
            _audio = audio;
            _play = play ?? (id => audio != null && audio.Play(id));
            _caption = caption;
            Started = new List<string>();
            StartedAt = new List<float>();
        }

        /// <summary>A new evening. Rotation cursors stay so the next ritual hears the next variant.</summary>
        public void ResetRitual()
        {
            _held = false;
            _opened = false;
            _welcomed = false;
            _mid = false;
            ShowCaption(null, 0f);
        }

        /// <summary>Clears the hold when the pinch ends without a Release intent, so the next hold can speak.</summary>
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

        public void OnIntent(HandIntent intent, int breaths, int target, bool returning, bool resuming)
        {
            if (intent.Kind == HandIntentKind.PinchHold)
            {
                if (_held) return;
                _held = true;
                Speak(ChooseHold(breaths, target, returning, resuming));
                return;
            }
            if (intent.Kind == HandIntentKind.Release)
            {
                _held = false;
                Speak(Next(OutLines, ref _out));
            }
        }

        /// <summary>The line after the third counted breath. It replaces an out-line that is still playing.</summary>
        public void OnBreathCounted(int breaths)
        {
            if (breaths != 3 || _mid) return;
            _mid = true;
            Speak(MidLine);
        }

        public void OnAnswer()
        {
            Speak(Next(CloseLines, ref _close));
        }

        string ChooseHold(int breaths, int target, bool returning, bool resuming)
        {
            if (returning && !_welcomed)
            {
                _welcomed = true;
                _opened = true;
                return ReturnLine;
            }
            if (resuming) return ResumeLine;
            if (target > 1 && breaths >= target - 1) return LastLine;
            if (!_opened)
            {
                _opened = true;
                return Next(OpenLines, ref _open);
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
                case "vo.ter.open.01": return "Let's take six slow breaths together.";
                case "vo.ter.open.02": return "Settle in. The jar will follow you.";
                case "vo.ter.in.01": return "Breathe in.";
                case "vo.ter.in.02": return "In, slowly.";
                case "vo.ter.in.03": return "Fill up gently.";
                case "vo.ter.out.01": return "And let go.";
                case "vo.ter.out.02": return "Let it out, softly.";
                case "vo.ter.out.03": return "All the way out.";
                case "vo.ter.mid.01": return "Halfway. The fern is listening.";
                case "vo.ter.last.01": return "One last breath.";
                case "vo.ter.close.01": return "Six breaths. This frond stays.";
                case "vo.ter.close.02": return "Rest here as long as you like.";
                case "vo.ter.return.01": return "Welcome back. Everything you grew is still here.";
                case "vo.ter.resume.01": return "Whenever you are ready.";
                default: return null;
            }
        }

        public static float WindowOf(string id)
        {
            if (id == null) return 4f;
            if (id.IndexOf(".in.", StringComparison.Ordinal) >= 0 || id == LastLine) return 1.8f;
            if (id.IndexOf(".out.", StringComparison.Ordinal) >= 0) return 2.5f;
            return 4f;
        }
    }
}
