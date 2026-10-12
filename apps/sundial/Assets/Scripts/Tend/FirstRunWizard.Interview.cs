using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    public sealed partial class FirstRunWizard
    {
        public const string ConsentLine = "The coach can use an online model to suggest habits. Your words are not kept.";
        readonly string[] _answers = new string[4];
        readonly List<GameObject> _interviewChrome = new List<GameObject>();
        Task<OnboardingResult> _onboardingCall;
        CancellationTokenSource _interviewCancel;
        string _chromeKey;
        GameObject _dropPacket;
        Vector3 _dropFrom, _dropTo;
        float _dropTime;
        Collider _gnomonTarget;
        bool _gnomonWasEnabled;

        public int QuestionIndex => Onboarding.QuestionIndex(_step);
        public string Question => QuestionIndex >= 0 ? CoachPrompts.OnboardingQuestions[QuestionIndex] : null;
        public bool ZoneTabsVisible => _view != null && _view.MonthlyTab(LifeZone.Body) != null && _view.MonthlyTab(LifeZone.Body).gameObject.activeInHierarchy;

        void InterviewIntent(HandIntent intent)
        {
            var speech = GetComponent<SpeechController>();
            string id = intent.TargetId ?? "";
            if (intent.Kind != HandIntentKind.Pinch) return;
            if (_step == Onboarding.OptIn && (id == "interview.off" || id == "interview.on"))
            {
                _service.Save.Settings.CoachOptIn = id == "interview.on";
                Enter(Onboarding.QuestionStep(0));
                return;
            }
            if (QuestionIndex >= 0 && (id == "interview.skip" || id == "interview.next"))
            {
                if (id == "interview.next" && string.IsNullOrWhiteSpace(speech.Transcript)) return;
                _answers[QuestionIndex] = id == "interview.skip" ? "" : speech.Transcript;
                Enter(Onboarding.AfterQuestion(_step));
                return;
            }
            if (_step != Onboarding.Review) return;
            var draft = _service.Save.Interview;
            if (draft == null) return;
            if (id == "interview.presets")
            {
                _service.Save.Interview = null;
                Enter(FirstRunSteps.Caption);
                return;
            }
            if (id == "interview.done")
            {
                _service.Save.Interview = null;
                Enter(_service.Save.Habits.Count == 0 ? FirstRunSteps.Caption : Onboarding.FirstTend);
                return;
            }
            for (int i = 0; i < draft.Proposals.Count; i++)
            {
                if (!draft.Pending(i)) continue;
                if (id == "interview.skip." + i)
                {
                    draft.Decisions[i] = 2;
                    if (draft.Selected == i) draft.Selected = -1;
                    _service.SetFirstRunStep(_step);
                    ClearInterviewChrome();
                    return;
                }
                if (id == "interview.packet." + i)
                {
                    string arc = Onboarding.CueArc(draft.Proposals[i].Cue);
                    draft.Selected = i;
                    if (arc != null) PlantInterview(i, arc);
                    else { _service.SetFirstRunStep(_step); ClearInterviewChrome(); }
                    return;
                }
            }
            if (draft.Pending(draft.Selected) && id.StartsWith("interview.arc.", StringComparison.Ordinal))
                PlantInterview(draft.Selected, id.Substring("interview.arc.".Length));
        }

        void PlantInterview(int index, string arc)
        {
            var packet = GameObject.Find("interview.packet." + index);
            Vector3 from = packet != null ? packet.transform.position : transform.position + Vector3.up * .2f;
            var habit = _service.AcceptInterview(index, arc);
            if (habit == null) { ClearInterviewChrome(); return; }
            _controller.Play(SundialController.CueSeed, transform);
            if (_dropPacket != null) Destroy(_dropPacket);
            _dropPacket = MakeText("InterviewSeedDrop", "·", 48, .004f, null);
            _dropFrom = from;
            var plant = transform.Find("plant." + SundialArcs.Key(habit.Group));
            _dropTo = plant != null ? plant.position : transform.position;
            _dropTime = 0;
            ClearInterviewChrome();
        }

        void UpdateInterview()
        {
            SetInterviewModal(Onboarding.IsStep(_step));
            var speech = GetComponent<SpeechController>();
            if (_step == Onboarding.Waiting && _onboardingCall == null)
            {
                _interviewCancel = new CancellationTokenSource();
                _onboardingCall = _controller.Coach.OnboardAsync((string[])_answers.Clone(), _interviewCancel.Token);
                Array.Clear(_answers, 0, _answers.Length);
            }
            if (_onboardingCall != null && _onboardingCall.IsCompleted)
            {
                var call = _onboardingCall;
                _onboardingCall = null;
                _interviewCancel.Dispose(); _interviewCancel = null;
                speech.CompleteCoachCall();
                if (_step == Onboarding.Waiting)
                {
                    var proposals = call.Status == TaskStatus.RanToCompletion ? call.Result.Proposals : ScriptedCoach.Onboarding(null);
                    _service.Save.Interview = new OnboardingDraft { Proposals = proposals };
                    Enter(Onboarding.Review);
                }
                else if (call.IsFaulted) { var observed = call.Exception; }
            }
            if (_step == Onboarding.Review && (_service.Save.Interview == null || _service.Save.Interview.Proposals.Count == 0))
            {
                _service.Save.Interview = new OnboardingDraft { Proposals = ScriptedCoach.Onboarding(null) };
                _service.SetFirstRunStep(_step);
            }
            if (QuestionIndex >= 0 && speech.Available && !speech.Armed && (_source == null || !_source.IsPinching)) speech.Arm("interview.speak");
            string key = _step + ":" + (speech.Available ? "voice" : "skip") + ":" + (!string.IsNullOrWhiteSpace(speech.Transcript));
            if (Onboarding.IsStep(_step) && key != _chromeKey) { ClearInterviewChrome(); BuildInterviewChrome(speech); _chromeKey = key; }
            if (_dropPacket != null)
            {
                _dropTime += Time.deltaTime;
                float t = _service.ReducedMotion ? 1 : Mathf.Clamp01(_dropTime / DropSeconds);
                _dropPacket.transform.position = Vector3.Lerp(_dropFrom, _dropTo, t * t);
                if (t >= 1) { Destroy(_dropPacket); _dropPacket = null; }
            }
            EnsureZoneTabs();
        }

        void PauseInterview()
        {
            Array.Clear(_answers, 0, _answers.Length);
            GetComponent<SpeechController>()?.CompleteCoachCall();
            if (_interviewCancel != null) _interviewCancel.Cancel();
        }
        void OnApplicationFocus(bool focus) { if (!focus) PauseInterview(); }
        void OnApplicationPause(bool paused) { if (paused) PauseInterview(); }
        void DisposeInterview()
        {
            PauseInterview();
            SetInterviewModal(false);
            if (_onboardingCall != null) _onboardingCall.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            _interviewCancel?.Dispose();
        }

        void ClearInterviewChrome()
        {
            foreach (var go in _interviewChrome) if (go != null) { go.SetActive(false); Destroy(go); }
            _interviewChrome.Clear(); _chromeKey = null;
        }

        GameObject InterviewNote(string name, string text, Vector3 position, Vector2 size, float font = .0019f, string id = null)
        {
            const float scale = .42f;
            position = new Vector3(position.x * scale, _view.faceY + (position.y - _view.faceY) * scale, position.z * scale);
            size *= scale; font *= scale * 2.2f;
            var go = MakeText(name, text, 48, font, id);
            var bounds = go.GetComponent<MeshRenderer>().localBounds.size;
            float fit = Mathf.Min(1f, size.x * .78f / Mathf.Max(.001f, bounds.x), size.y * .70f / Mathf.Max(.001f, bounds.y));
            go.GetComponent<TextMesh>().characterSize *= fit;
            go.transform.localPosition = position;
            InkLetter.AttachNote(go.transform, size);
            if (id != null) go.GetComponent<BoxCollider>().size = new Vector3(size.x * .8f, size.y * .75f, .006f);
            _interviewChrome.Add(go);
            return go;
        }

        void BuildInterviewChrome(SpeechController speech)
        {
            float y = _view.faceY;
            if (_step == Onboarding.OptIn)
            {
                InterviewNote("InterviewConsent", ConsentLine, new Vector3(0, y + .16f, 0), new Vector2(.66f, .07f), .00145f);
                InterviewNote("interview.off", "Keep coach off", new Vector3(-.10f, y + .10f, 0), new Vector2(.18f, .06f), id: "interview.off");
                InterviewNote("interview.on", "Use online coach", new Vector3(.10f, y + .10f, 0), new Vector2(.18f, .06f), id: "interview.on");
            }
            else if (QuestionIndex >= 0)
            {
                InterviewNote("InterviewQuestion", (QuestionIndex + 1) + " / 4\n" + Wrap(Question, 34), new Vector3(0, y + .18f, 0), new Vector2(.37f, .13f), .0022f);
                if (speech.Available)
                    InterviewNote("interview.speak", "Hold to answer", new Vector3(-.11f, y + .08f, 0), new Vector2(.17f, .06f), id: "interview.speak");
                else InterviewNote("InterviewNoSpeech", "Pinch skip to use starter habits", new Vector3(-.08f, y + .08f, 0), new Vector2(.28f, .06f), .00165f);
                InterviewNote("interview.skip", "skip", new Vector3(.12f, y + .08f, 0), new Vector2(.10f, .06f), id: "interview.skip");
                if (!string.IsNullOrWhiteSpace(speech.Transcript))
                    InterviewNote("interview.next", "Answer ready · next", new Vector3(0, y + .025f, -.025f), new Vector2(.21f, .055f), id: "interview.next");
            }
            else if (_step == Onboarding.Waiting)
                InterviewNote("InterviewWaiting", "Finding a few small habits", new Vector3(0, y + .17f, 0), new Vector2(.32f, .08f));
            else if (_step == Onboarding.Review)
            {
                var draft = _service.Save.Interview;
                bool selecting = draft.Pending(draft.Selected);
                InterviewNote("InterviewInvitation", selecting ? "Choose an arc" : "Pinch a packet to plant it", new Vector3(-.27f, y + .10f, 0), new Vector2(.22f, .085f), .00165f);
                for (int i = 0; i < draft.Proposals.Count; i++)
                {
                    if (!draft.Pending(i)) continue;
                    var p = draft.Proposals[i];
                    float x = (i - 1) * .17f;
                    var packet = InterviewNote("interview.packet." + i, Wrap(p.Name, 23) + "\n" + p.Zone + " · " + ScheduleLabel(p.Schedule) + "\n" + Wrap(p.Cue, 22),
                        new Vector3(x, y + .20f, .015f), new Vector2(.18f, .135f), .00145f, "interview.packet." + i);
                    var art = Resources.Load<InterviewArt>("InterviewArt");
                    if (art != null && art.Packets.Length > i)
                    {
                        var envelope = MakeQuad("envelope." + i, _upright, art.Packets[i], Color.white, "interview.packet." + i);
                        envelope.transform.SetParent(packet.transform, false);
                        envelope.transform.localPosition = new Vector3(0, .095f * .42f, 0);
                        envelope.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        envelope.transform.localScale = new Vector3(.055f * .42f, .075f * .42f, 1);
                        envelope.GetComponent<BoxCollider>().size = new Vector3(1.1f, 1.1f, .005f);
                    }
                    InterviewNote("interview.skip." + i, "not this one", new Vector3(x, y + .115f, .015f), new Vector2(.135f, .044f), .00155f, "interview.skip." + i);
                }
                if (selecting)
                {
                    string[] arcs = { "morning", "midday", "winddown" };
                    string[] labels = { "morning", "midday", "evening" };
                    for (int i = 0; i < 3; i++) InterviewNote("interview.arc." + arcs[i], labels[i], new Vector3((i - 1) * .14f, y + .07f, -.07f), new Vector2(.13f, .05f), .0018f, "interview.arc." + arcs[i]);
                }
                if (_service.Save.Habits.Count > 0 || draft.Complete)
                    InterviewNote("interview.done", _service.Save.Habits.Count > 0 ? "Ready to tend" : "Choose presets", new Vector3(.25f, y + .035f, -.09f), new Vector2(.17f, .065f), .0018f, "interview.done");
                if (_service.Save.Habits.Count == 0 && !draft.Complete)
                    InterviewNote("interview.presets", "Choose presets", new Vector3(-.25f, y + .035f, -.09f), new Vector2(.17f, .065f), .0018f, "interview.presets");
            }
        }

        static string ScheduleLabel(HabitSchedule s)
        {
            if (s.Kind == ScheduleKind.Daily) return "every day";
            if (s.Kind == ScheduleKind.TimesPerWeek) return s.TimesPerWeek + " a week";
            if (s.WeekdayMask == HabitSchedule.WorkWeek) return "weekdays";
            var days = new List<string>();
            string[] labels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            for (int i = 0; i < 7; i++) if ((s.WeekdayMask & (1 << i)) != 0) days.Add(labels[i]);
            return string.Join(" ", days);
        }
        static string Wrap(string text, int width)
        {
            string result = ""; int n = 0;
            foreach (string word in (text ?? "").Split(' '))
            {
                if (n > 0 && n + word.Length + 1 > width) { result += "\n"; n = 0; }
                if (n > 0) { result += " "; n++; }
                result += word; n += word.Length;
            }
            return result;
        }
        void EnsureZoneTabs()
        {
            _view.SetMonthlyRecord(_service.Ledger, _service.Save.Habits, _service.Today());
        }
        void PlaceInterview(Camera cam)
        {
            foreach (var go in _interviewChrome) if (go != null) FaceAway(cam, go.transform, go.transform.position);
            if (_dropPacket != null) FaceAway(cam, _dropPacket.transform, _dropPacket.transform.position);
        }

        void SetInterviewModal(bool on)
        {
            if (on && _gnomonTarget == null)
                foreach (var target in GetComponentsInChildren<IntentTarget>())
                    if (target.Id == "gnomon")
                    {
                        _gnomonTarget = target.GetComponent<Collider>();
                        if (_gnomonTarget != null) { _gnomonWasEnabled = _gnomonTarget.enabled; _gnomonTarget.enabled = false; }
                        break;
                    }
            if (!on && _gnomonTarget != null) { _gnomonTarget.enabled = _gnomonWasEnabled; _gnomonTarget = null; }
        }
    }
}
