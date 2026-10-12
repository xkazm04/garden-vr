using System;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>Hold-to-speak adapter for interview/reflection consumers. Dormant until Arm is called.
    /// No save/model reference. Call CompleteCoachCall (also on cancellation) to discard transient text.</summary>
    public sealed class SpeechController : MonoBehaviour
    {
        ISpeechToText _speech;
        IHandIntentSource _source;
        bool _holding, _blockedUntilRelease, _accept, _ownsSpeech;
        string _target;
        ListeningRipple _ripple;
        public bool Armed { get; private set; }
        public bool UsingPresetFallback { get; private set; }
        public bool Available => !UsingPresetFallback && _speech != null && _speech.Available;
        public bool Listening => _holding && Available &&
            (!(_speech is ISpeechCaptureState state) || state.Listening);
        public string Transcript { get; private set; } = "";
        public string PartialText { get; private set; } = "";
        public event Action<string> Final;
        public event Action<string> Partial;
        public bool RippleVisible => _ripple != null && _ripple.gameObject.activeSelf;

        void Awake()
        {
            _ripple = ListeningRipple.Create(transform);
            SetSpeech(new WindowsSpeechSource(), true);
        }

        void Start()
        {
            var controller = GetComponent<SundialController>();
            if (_source == null && controller != null) SetSource(controller.Source);
        }

        public void SetSpeech(ISpeechToText speech, bool takeOwnership = false)
        {
            CompleteCoachCall();
            if (_speech != null)
            {
                _speech.Final -= OnFinal;
                _speech.Partial -= OnPartial;
                if (_ownsSpeech && _speech is IDisposable disposable) disposable.Dispose();
            }
            _speech = speech;
            UsingPresetFallback = false;
            _ownsSpeech = takeOwnership;
            if (_speech != null) { _speech.Final += OnFinal; _speech.Partial += OnPartial; }
        }

        public void SetSource(IHandIntentSource source)
        {
            CompleteCoachCall();
            Unsubscribe();
            _source = source;
            if (isActiveAndEnabled) Subscribe();
        }

        /// <summary>Target id of the speaking affordance; null accepts any hold in an exclusive speech context.</summary>
        public void Arm(string targetId = null)
        {
            CompleteCoachCall();
            _target = targetId;
            Armed = true;
            _blockedUntilRelease = _source != null && _source.IsPinching;
        }

        public void CompleteCoachCall()
        {
            _accept = false;
            Stop();
            Armed = false;
            Transcript = "";
            PartialText = "";
        }

        void OnIntent(HandIntent intent)
        {
            if (GetComponent<SundialController>()?.CatchUpCard?.BlocksInput == true) return;
            if (intent.Kind == HandIntentKind.Release)
            {
                Stop();
                _blockedUntilRelease = false;
            }
            else if (intent.Kind == HandIntentKind.PalmOpen) Pause();
            else if (intent.Kind == HandIntentKind.PinchHold && Armed && !_blockedUntilRelease && !_holding &&
                     Available && (_source == null || _source.IsTracked) &&
                     (_target == null || _target == intent.TargetId))
            {
                _holding = true;
                _accept = true;
                PartialText = "";
                _speech.Begin();
                RefreshRipple();
            }
        }

        void Stop()
        {
            _holding = false;
            _speech?.End();
            PartialText = "";
            RefreshRipple();
        }

        void Pause()
        {
            CompleteCoachCall();
            _blockedUntilRelease = true;
        }

        void OnFinal(string text)
        {
            if (!_accept || string.IsNullOrWhiteSpace(text)) return;
            Transcript = Transcript.Length == 0 ? text : Transcript + " " + text;
            PartialText = "";
            Final?.Invoke(text);
        }
        void OnPartial(string text)
        {
            if (!Listening || !_accept) return;
            PartialText = text ?? "";
            Partial?.Invoke(PartialText);
        }
        void Update()
        {
            if (_source != null && !_source.IsTracked) Pause();
            if (_speech is WindowsSpeechSource windows) windows.Poll();
            if (_speech != null && !_speech.Available)
            {
                // Never replay invented answers as if the player spoke them. The empty script is inert;
                // preset selection remains the complete first-run path when a device cannot capture.
                SetSpeech(new ScriptedSpeechSource());
                UsingPresetFallback = true;
            }
            if (_speech is ScriptedSpeechSource scripted) scripted.Tick(Time.unscaledDeltaTime);
            RefreshRipple();
        }
        void RefreshRipple()
        {
            if (_ripple == null) return;
            var controller = GetComponent<SundialController>();
            _ripple.ReducedMotion = controller != null && controller.Service != null && controller.Service.Save.Settings.ReducedMotion;
            _ripple.gameObject.SetActive(Listening);
        }
        void Subscribe() { if (_source != null) { _source.Intent += OnIntent; _source.SystemPause += Pause; } }
        void Unsubscribe() { if (_source != null) { _source.Intent -= OnIntent; _source.SystemPause -= Pause; } }
        void OnEnable() { Subscribe(); }
        void OnDisable() { Pause(); Unsubscribe(); }
        void OnApplicationFocus(bool focus) { if (!focus) Pause(); }
        void OnApplicationPause(bool paused) { if (paused) Pause(); }
        void OnDestroy() { SetSpeech(null); }
    }
}
