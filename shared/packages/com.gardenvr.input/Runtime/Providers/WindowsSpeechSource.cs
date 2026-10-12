using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace GardenVR.Input
{
    /// <summary>Lazy Windows dictation. Construction never opens the microphone. End stops capture,
    /// but permits the native recognizer to deliver its final result while completing.</summary>
    public sealed class WindowsSpeechSource : ISpeechToText, ISpeechCaptureState, IDisposable
    {
        bool _failed, _disposed;
        public event Action<string> Final;
        public event Action<string> Partial;
        public bool Listening { get; private set; }
        public bool Available
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return !_failed && !_disposed;
#else
                return false;
#endif
            }
        }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        DictationRecognizer _recognizer;
        bool _acceptResults, _requested;
        float _startTime;
#endif
        public void Begin()
        {
            if (!Available || Listening) return;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_requested) return;
            try
            {
                Close();
                _recognizer = new DictationRecognizer();
                _recognizer.DictationResult += Result;
                _recognizer.DictationHypothesis += Hypothesis;
                _recognizer.DictationComplete += Complete;
                _recognizer.DictationError += Error;
                _acceptResults = true;
                _requested = true;
                _startTime = Time.realtimeSinceStartup;
                _recognizer.Start();
                Debug.Log("[Speech] Windows begin: " + _recognizer.Status);
                Poll();
            }
            catch (Exception ex) { Fail("Begin " + ex.GetType().Name + " " + ex.Message); Close(); }
#endif
        }

        public void End()
        {
            Listening = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            _requested = false;
            try
            {
                if (_recognizer != null && _recognizer.Status == SpeechSystemStatus.Running)
                    _recognizer.Stop();
                else if (_recognizer != null) Close(); // Cancel a pending start before it can open the mic.
            }
            catch (Exception ex) { Fail("End " + ex.GetType().Name + " " + ex.Message); Close(); }
#endif
        }

        /// <summary>Drive from Update. A native start may complete asynchronously; never mistake the initial
        /// Stopped status for a failure. Bound a start that never reports Running to three seconds.</summary>
        public void Poll()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!_requested || _recognizer == null) return;
            Listening = !_failed && _recognizer.Status == SpeechSystemStatus.Running;
            if (_recognizer.Status == SpeechSystemStatus.Failed ||
                (!Listening && Time.realtimeSinceStartup - _startTime >= 3f))
            {
                Fail("Start did not enter Running within 3 seconds; status=" + _recognizer.Status);
                End();
            }
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        void Result(string text, ConfidenceLevel confidence) { if (_acceptResults) Final?.Invoke(text); }
        void Hypothesis(string text) { if (Listening) Partial?.Invoke(text); }
        void Complete(DictationCompletionCause cause)
        {
            Listening = false;
            _requested = false;
            _acceptResults = false;
            Debug.Log("[Speech] Windows complete: " + cause);
            if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.Canceled &&
                cause != DictationCompletionCause.TimeoutExceeded && cause != DictationCompletionCause.PauseLimitExceeded)
                Fail("Completion " + cause);
        }
        void Error(string error, int hresult)
        {
            Fail("HRESULT=0x" + hresult.ToString("X8") + " " + error);
            End();
        }
        void Close()
        {
            _acceptResults = false;
            _requested = false;
            if (_recognizer == null) return;
            _recognizer.DictationResult -= Result;
            _recognizer.DictationHypothesis -= Hypothesis;
            _recognizer.DictationComplete -= Complete;
            _recognizer.DictationError -= Error;
            _recognizer.Dispose();
            _recognizer = null;
        }
#endif
        void Fail(string diagnostic)
        {
            _failed = true;
            Listening = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            _acceptResults = false;
#endif
            // Device diagnostics only. Never include a transcript.
            Debug.Log("[Speech] Windows unavailable: " + diagnostic);
        }
        public void Dispose()
        {
            if (_disposed) return;
            End();
            _disposed = true;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            Close();
#endif
            Final = null;
            Partial = null;
        }
    }
}
