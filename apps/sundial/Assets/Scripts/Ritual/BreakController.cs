using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>Routes hand input to core's break; owns only the transient presentation and lifecycle.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(70)]
    public sealed class BreakController : MonoBehaviour
    {
        SundialController _dial;
        BreakView _view;
        IHandIntentSource _source;
        HabitDef _habit;
        bool _freshPalm, _freshPinch;
        string _message;
        public BreakSession Session { get; private set; }
        public bool Active => Session != null && !Session.Complete;
        public bool Paused { get; private set; }
        public BreakView View => _view;

        void Awake()
        {
            _dial = GetComponent<SundialController>();
            _view = gameObject.AddComponent<BreakView>();
        }

        bool Blocked => _dial == null || _dial.Service == null || _dial.Outcome == LoadOutcome.Failed ||
            _dial.Dismissed || _dial.Service.Scrubbing || _dial.Wizard?.Running == true ||
            _dial.CatchUpCard?.BlocksInput == true || _dial.Settings?.Open == true ||
            GetComponent<ReflectionController>()?.Open == true ||
            GetComponent<DuskRitualController>()?.Active == true ||
            GetComponent<WeekDialController>()?.Turn > .02f;

        /// <summary>The entry seam for the nudge controller as well as the Take a break slip.</summary>
        public bool StartBreak()
        {
            if (Active || Blocked) return false;
            _source = _dial.Source;
            // Providers without continuous palm state retain all legacy behavior but cannot offer hand poses.
            if (_dial.Service.Save.Settings.BreakPoses && !(_source is IPalmStateSource)) return false;
            _habit = _dial.Service.EnsureBreakHabit();
            if (_habit == null) { _message = "Make room for a break habit"; return false; }
            _dial.NotifyFocusLost(); // flush a pending tend and pause any background focus block.
            Session = new BreakSession(_dial.Service.Save.Settings.BreakPoses);
            Paused = false; _message = null;
            RequireFreshHands();
            return true;
        }

        public void Pause()
        {
            if (!Active) return;
            Paused = true;
            RequireFreshHands();
        }

        void RequireFreshHands() { _freshPalm = true; _freshPinch = true; }
        void OnDisable() { Pause(); }
        void OnApplicationFocus(bool focused) { if (!focused) Pause(); }
        void OnApplicationPause(bool paused) { if (paused) Pause(); }

        // Routed first by SundialController so a break palm cannot dismiss the dial or tend morning.
        public bool HandleIntent(HandIntent intent)
        {
            bool action = intent.TargetId != null && intent.TargetId.StartsWith("break.", System.StringComparison.Ordinal);
            if (!Active && !action) return false;
            if (intent.Kind == HandIntentKind.Pinch)
            {
                if (!Active && intent.TargetId == "break.start") StartBreak();
                else if (Active && intent.TargetId == "break.pause") Pause();
                else if (Active && intent.TargetId == "break.resume" && !Blocked && _dial.Source?.IsTracked == true)
                { Paused = false; RequireFreshHands(); }
            }
            if (!Active || Paused || Blocked || _dial.Source?.IsTracked != true) return true;
            if (intent.Kind == HandIntentKind.PalmOpen && Session.Part == BreakPart.Stretch && !_freshPalm)
            {
                var target = IntentTargetRegistry.Raycast(intent.Ray);
                int mark = StretchRitualController.ParseMark(target == null ? intent.TargetId : target.Id);
                Session.Update(0, new BreakSample(mark, true, false, false, false));
                if (Session.Part != BreakPart.Stretch) RequireFreshHands();
            }
            return true;
        }

        void Update()
        {
            if (_dial == null) return;
            if (_source != _dial.Source) { Pause(); _source = _dial.Source; }
            bool palm = (_source as IPalmStateSource)?.PalmHeld == true;
            bool pinch = _source?.IsPinching == true;
            if (!palm) _freshPalm = false;
            if (!pinch) _freshPinch = false;
            if (Active)
            {
                if (Blocked || _source?.IsTracked != true) Pause();
                if (!Paused)
                {
                    // Resolve the actual ray. The mapper's sticky dwell target must not count a look away.
                    var target = IntentTargetRegistry.Raycast(_source.LookRay);
                    bool eyes = target != null && target.Id == "break.eye";
                    Session.Update(Mathf.Clamp(Time.deltaTime, 0, .1f),
                        new BreakSample(-1, false, palm && !_freshPalm, pinch && !_freshPinch, eyes));
                    if (Session.ConsumeTend())
                    {
                        var result = _dial.Service.TendRitual(_habit.Id);
                        _message = result != null && result.Ok ? "Break complete" : "Break complete. Habit could not be tended.";
                    }
                }
            }
            bool available = !Blocked && (_dial.Service.Save.Settings.BreakPoses == false || _source is IPalmStateSource);
            _view.Show(Session, Paused, available, Blocked ? null : _message, _source);
        }
    }
}
