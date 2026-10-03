using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GardenVR.Core;
using GardenVR.Input;
using GardenVR.Room;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// First launch, taught by the jar. Timed beats are the entrance only.
    /// Hold, release, breath, answer, seeds and the voice offer advance on the real event.
    /// A relaunch continues from <see cref="TerrariumSave.FirstRunStep"/>. The jar stays put once it has arrived.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstRunDirector : MonoBehaviour
    {
        public const float DeskTime = 2.5f;
        public const float LandTime = 4f;
        public const float DropSeconds = 0.6f;
        public const float DropHeight = 0.08f;
        public const float PuffTime = 6f;
        public const float WordsTime = 8f;
        public const float QuietSeconds = 4f;
        public const float IdleSeconds = 8f;
        public const float GhostCycleSeconds = 1.5f;
        public const float WordFadeSeconds = 0.45f;

        readonly Dictionary<string, float> _times = new Dictionary<string, float>();

        JarRitualController _controller;
        Vector3 _rest;
        TextMeshPro _words;
        Renderer _ghostRenderer;
        Material _ghostMat;
        Texture2D _openHand;
        Texture2D _pinchHand;

        bool _reduced;
        bool _replayIntro;
        bool _cuedLand;
        bool _cuedPuff;
        bool _wordsShown;
        bool _releaseSeen;
        bool _ghostGone;
        bool _wordsFaded;
        bool _arrivalMoved;
        int _answerLine;
        int _events;
        int _ghostFrame;
        float _idle;
        float _ghostClock;
        float _alpha;
        float _alphaTarget;
        float _lift;

        public bool Running { get; private set; }
        public string Step { get; private set; }
        public bool WordsVisible { get; private set; }
        public string WordsText { get; private set; }
        public float WordsAlpha { get { return _alpha; } }
        public bool GhostVisible { get; private set; }
        public float JarLift { get { return _lift; } }
        public bool ArrivalMoved { get { return _arrivalMoved; } }

        public void Begin(JarRitualController controller)
        {
            _controller = controller;
            _rest = transform.localPosition;
            GardenService service = controller != null ? controller.Service : null;
            _reduced = service != null && service.Settings != null && service.Settings.ReducedMotion;
            string saved = service != null && service.Document != null ? service.Document.FirstRunStep : null;
            LoadOutcome outcome = service != null ? service.Outcome : LoadOutcome.Failed;
            Running = FirstRunSteps.ShouldRun(outcome, saved);
            if (!Running)
            {
                enabled = false;
                return;
            }

            bool resume = FirstRunSteps.IndexOf(saved) >= 0;
            _replayIntro = !FirstRunSteps.AtLeast(saved, FirstRunSteps.Arrive);
            if (_reduced) SettleIntro();
            if (resume)
            {
                Step = saved;
                _times[saved] = 0f;
                _cuedLand = true;
                _cuedPuff = true;
                _releaseSeen = FirstRunSteps.AtLeast(saved, FirstRunSteps.Release);
                _ghostGone = FirstRunSteps.AtLeast(saved, FirstRunSteps.Breathe);
                _wordsFaded = _ghostGone;
                _alphaTarget = 0f;
                if (!_wordsFaded && FirstRunSteps.AtLeast(saved, FirstRunSteps.Hold))
                {
                    _wordsShown = true;
                    Show(_releaseSeen ? EtchContrast.ReleaseLine : HoldLine(), 1f);
                    _alpha = 1f;
                }
                Debug.Log("[FirstRun] resume step=" + saved + " t=0.000");
                if (IsMidRitual(saved)) controller.OfferRitualBack();
            }
            else
            {
                Advance(FirstRunSteps.Room);
            }

            BuildWords();
            BuildGhost();
            ApplyLift(controller != null ? controller.AppTime : 0f);
        }

        public void OnIntent(HandIntent intent)
        {
            if (!Running) return;
            _idle = 0f;
            if (_wordsFaded && _alphaTarget > 0f)
                _alphaTarget = 0f;
            if (intent.Kind != HandIntentKind.Release || _releaseSeen) return;
            _releaseSeen = true;
            if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Release))
                Advance(FirstRunSteps.Release);
            if (_wordsShown && !_wordsFaded)
                Show(EtchContrast.ReleaseLine, 1f);
        }

        public void Tick(float dt)
        {
            if (!Running || _controller == null) return;
            if (dt < 0f) dt = 0f;
            float t = _controller.AppTime;
            if (_replayIntro)
                TickIntro(t);
            else
                ApplyLift(LandTime);

            DrainBreaths();
            TickIdle(dt);
            TickAnswerWords();
            TickWords(dt);
            TickGhost(dt);
            TickAfterAnswer();
        }

        public string TimelineLog()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < FirstRunSteps.Order.Length; i++)
            {
                string id = FirstRunSteps.Order[i];
                float when;
                if (!_times.TryGetValue(id, out when)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(id).Append('=').Append(when.ToString("0.000", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        void TickIntro(float t)
        {
            ApplyLift(t);
            if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Desk) && t >= DeskTime)
                Advance(FirstRunSteps.Desk);
            if (!_cuedLand && t >= LandTime)
            {
                _cuedLand = true;
                _controller.PlayCue("jar.land", transform);
                if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Arrive))
                    Advance(FirstRunSteps.Arrive);
            }
            if (!_cuedPuff && t >= PuffTime && _cuedLand)
            {
                _cuedPuff = true;
                if (!_reduced && _controller.View != null) _controller.View.PuffCork(0.8f);
                Transform cork = FindNamed(transform, "Cork");
                _controller.PlayCue("jar.lid", cork != null ? cork : transform);
            }
            if (!_wordsShown && !_wordsFaded && t >= WordsTime && _cuedLand)
            {
                _wordsShown = true;
                if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Hold))
                    Advance(FirstRunSteps.Hold);
                Show(_releaseSeen ? EtchContrast.ReleaseLine : HoldLine(), 1f);
            }
        }

        void ApplyLift(float t)
        {
            bool travel = _replayIntro && !_reduced;
            float lift = 0f;
            if (travel)
            {
                float start = LandTime - DropSeconds;
                if (t <= start) lift = DropHeight;
                else if (t < LandTime)
                {
                    float u = Mathf.Clamp01((t - start) / DropSeconds);
                    float eased = 1f - (1f - u) * (1f - u);
                    lift = DropHeight * (1f - eased);
                }
            }
            _lift = lift;
            if (lift > 0.01f) _arrivalMoved = true;
            transform.localPosition = _rest + new Vector3(0f, lift, 0f);
        }

        void DrainBreaths()
        {
            BreathSession session = _controller.Session;
            if (session == null) return;
            while (_events < session.Events.Count)
            {
                BreathEvent ev = session.Events[_events++];
                if (ev.Kind != BreathEventKind.BreathCounted) continue;
                if (ev.Breaths >= 1) _ghostGone = true;
                if (ev.Breaths < 2 || _wordsFaded) continue;
                _wordsFaded = true;
                _alphaTarget = 0f;
                if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Breathe))
                    Advance(FirstRunSteps.Breathe);
            }
        }

        void TickIdle(float dt)
        {
            if (!_wordsFaded || _controller.HasAnswer || _controller.Session == null) return;
            BreathPhase phase = _controller.Session.Phase;
            bool waiting = phase == BreathPhase.Waiting || phase == BreathPhase.Exhaling;
            if (!waiting || _controller.HoldingBreath)
            {
                _idle = 0f;
                return;
            }
            _idle += dt;
            if (_idle < IdleSeconds || _alphaTarget > 0f) return;
            Show(HoldLine() + "\n" + EtchContrast.ReleaseLine, 1f);
        }

        void TickAnswerWords()
        {
            if (_controller == null || !_controller.HasAnswer) return;
            bool quiet = _controller.AnswerTime < QuietSeconds;
            if (quiet)
            {
                if (_answerLine == 1) return;
                _answerLine = 1;
                Show(EtchContrast.AnswerLine, 1f);
                return;
            }
            if (_answerLine != 1) return;
            _answerLine = 2;
            _alphaTarget = 0f;
        }

        void TickWords(float dt)
        {
            if (_reduced) _alpha = _alphaTarget;
            else
            {
                float step = WordFadeSeconds <= 0.0001f ? 1f : dt / WordFadeSeconds;
                _alpha = Mathf.MoveTowards(_alpha, _alphaTarget, step);
            }
            WordsVisible = _alpha > 0.04f;
            if (_words == null) return;
            Color color = EtchContrast.SceneInk;
            color.a = _alpha * BreathLight();
            _words.color = color;
            if (_words.gameObject.activeSelf != WordsVisible)
                _words.gameObject.SetActive(WordsVisible);
        }

        /// <summary>The etch brightens while the breath is held and eases off as it leaves.</summary>
        float BreathLight()
        {
            if (_reduced || _controller == null || _controller.Session == null) return 1f;
            BreathSession session = _controller.Session;
            if (session.Phase == BreathPhase.Inhaling)
                return 0.55f + 0.45f * Mathf.Clamp01(session.Uncoil);
            if (session.Phase == BreathPhase.Exhaling)
                return 0.42f + 0.40f * (1f - Mathf.Clamp01(session.Fog));
            return 0.72f;
        }

        void TickGhost(float dt)
        {
            bool show = !_ghostGone && _wordsShown && FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Breathe);
            GhostVisible = show && _ghostRenderer != null;
            if (_ghostRenderer != null && _ghostRenderer.enabled != GhostVisible)
                _ghostRenderer.enabled = GhostVisible;
            if (!GhostVisible || _ghostMat == null || _reduced) return;
            _ghostClock += dt;
            if (_ghostClock < GhostCycleSeconds) return;
            _ghostClock = 0f;
            _ghostFrame = 1 - _ghostFrame;
            Texture2D frame = _ghostFrame == 0 ? _openHand : _pinchHand;
            if (frame != null) _ghostMat.SetTexture("_MainTex", frame);
        }

        void TickAfterAnswer()
        {
            if (!_controller.HasAnswer) return;
            if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Answer))
            {
                Advance(FirstRunSteps.Answer);
                Debug.Log("answerAt=" + _controller.AppTime.ToString("0.000", CultureInfo.InvariantCulture));
            }
            if (_controller.AnswerTime < QuietSeconds) return;
            if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Seeds))
            {
                _controller.OfferSeedPackets();
                Advance(FirstRunSteps.Seeds);
            }
            if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Voice))
                Advance(FirstRunSteps.Voice);
            if (FirstRunSteps.IndexOf(Step) < FirstRunSteps.IndexOf(FirstRunSteps.Done))
                Advance(FirstRunSteps.Done);
        }

        void Advance(string step)
        {
            if (step == Step) return;
            if (FirstRunSteps.IndexOf(step) < FirstRunSteps.IndexOf(Step)) return;
            Step = step;
            float t = _controller != null ? _controller.AppTime : 0f;
            _times[step] = t;
            Debug.Log("[FirstRun] step=" + step + " t=" + t.ToString("0.000", CultureInfo.InvariantCulture));
            if (_controller != null && _controller.Service != null)
                _controller.Service.SetFirstRunStep(step);
        }

        void Show(string text, float target)
        {
            WordsText = text;
            _alphaTarget = target;
            if (_words == null) return;
            bool was = _words.gameObject.activeSelf;
            _words.gameObject.SetActive(true);
            EtchedLettering.Fit(_words, text, EtchedLettering.WordsWidth, EtchedLettering.WordsCap);
            if (!was) _words.gameObject.SetActive(false);
        }

        string HoldLine()
        {
            string binding = _controller != null ? _controller.HoldBinding : "Space or mouse";
            if (string.IsNullOrEmpty(binding)) binding = "Space or mouse";
            return EtchContrast.HoldLine + "\n" + binding;
        }

        void SettleIntro()
        {
            PcRoomPlate plate = FindAnyObjectByType<PcRoomPlate>();
            if (plate != null)
            {
                plate.FadeSeconds = 0f;
                plate.Show(0f);
            }
            PcDeskAnchor desk = FindAnyObjectByType<PcDeskAnchor>();
            if (desk == null) return;
            desk.ApplyPose();
            desk.enabled = false;
        }

        void BuildWords()
        {
            _words = EtchedLettering.Place(transform, "EtchedWords", WordsText ?? "",
                EtchedLettering.GlassPos, Quaternion.identity, EtchedLettering.WordsWidth, EtchedLettering.WordsCap);
            _words.gameObject.SetActive(false);
        }

        void BuildGhost()
        {
            _openHand = Resources.Load<Texture2D>("Ghost/hand-open");
            _pinchHand = Resources.Load<Texture2D>("Ghost/hand-pinch");
            if (_openHand == null)
            {
                Debug.LogError("[FirstRun] ghost hand texture missing");
                return;
            }
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GhostHand";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.078f, 0.072f, -0.02f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(0.052f, 0.070f, 1f);
            _ghostMat = new Material(shader) { name = "GhostHand" };
            _ghostMat.SetTexture("_MainTex", _openHand);
            _ghostMat.SetColor("_Color", Color.white);
            _ghostMat.SetFloat("_Src", (float)BlendMode.SrcAlpha);
            _ghostMat.SetFloat("_Dst", (float)BlendMode.OneMinusSrcAlpha);
            _ghostMat.SetFloat("_Ring", 0f);
            _ghostMat.SetFloat("_Boil", 0f);
            _ghostMat.SetFloat("_ZWrite", 0f);
            _ghostRenderer = go.GetComponent<Renderer>();
            _ghostRenderer.sharedMaterial = _ghostMat;
            _ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ghostRenderer.receiveShadows = false;
            _ghostRenderer.enabled = false;
        }

        static bool IsMidRitual(string step)
        {
            return step == FirstRunSteps.Hold || step == FirstRunSteps.Release || step == FirstRunSteps.Breathe;
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }
            return null;
        }

        void OnDestroy()
        {
            if (_ghostMat != null) Destroy(_ghostMat);
        }
    }
}
