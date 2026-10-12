using System;
using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>JSON playback, one utterance per Begin. Tick advances only during a hold.
    /// End discards events that have not happened; it never invents a final from a partial.</summary>
    public sealed class ScriptedSpeechSource : ISpeechToText, ISpeechCaptureState
    {
        [Serializable] sealed class Playback { public Utterance[] utterances; }
        [Serializable] sealed class Utterance { public Step[] events; }
        [Serializable] sealed class Step { public float t; public string kind; public string text; }
        Utterance[] _utterances = Array.Empty<Utterance>();
        int _next, _cursor;
        float _time;
        Utterance _current;
        public bool Available => true;
        public bool Listening { get; private set; }
        public event Action<string> Final;
        public event Action<string> Partial;

        public void Load(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var parsed = JsonUtility.FromJson<Playback>(json);
            if (parsed?.utterances == null) throw new FormatException("Speech playback requires utterances.");
            foreach (var utterance in parsed.utterances)
            {
                if (utterance?.events == null) throw new FormatException("Speech utterance requires events.");
                float previous = 0;
                foreach (var step in utterance.events)
                {
                    if (step == null || float.IsNaN(step.t) || float.IsInfinity(step.t) || step.t < previous ||
                        (step.kind != "partial" && step.kind != "final") || string.IsNullOrWhiteSpace(step.text))
                        throw new FormatException("Invalid speech playback event.");
                    previous = step.t;
                }
            }
            End();
            _utterances = parsed.utterances;
            _next = 0;
        }

        public void Begin()
        {
            if (Listening) return;
            Listening = true;
            _current = _next < _utterances.Length ? _utterances[_next++] : null;
            _cursor = 0;
            _time = 0;
            Tick(0);
        }

        public void End() { Listening = false; _current = null; }

        public void Tick(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!Listening) return;
            _time += seconds;
            while (Listening && _current != null && _cursor < _current.events.Length &&
                   _current.events[_cursor].t <= _time + 0.00001f)
            {
                Step step = _current.events[_cursor++];
                if (step.kind == "final") Final?.Invoke(step.text);
                else Partial?.Invoke(step.text);
            }
        }
    }
}
