using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Terrarium.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Terrarium
{
    /// <summary>
    /// Glue between a pinch source, the pure core (BreathSession + Garden) and the view.
    /// Pinch sources, in Auto order: real/simulated XR hands (XR Hands subsystem: thumb-tip to index-tip distance),
    /// keyboard Space (desktop), or a scripted SimulatedHand when launched with -scriptedHand.
    /// </summary>
    public sealed class TerrariumDriver : MonoBehaviour
    {
        public enum Source { Auto, XRHands, Keyboard, Scripted }
        public TerrariumView view;
        public Source source = Source.Auto;

        public static TerrariumDriver Instance { get; private set; }
        public BreathSession Session { get; private set; }
        public Garden Garden { get; private set; }
        public string LastSource { get; private set; } = "none";
        public float LastStrength { get; private set; }

        SimulatedHand _scripted; float _scriptedT;
        bool _answered;
        int _today;
        string SavePath => Path.Combine(Application.persistentDataPath, "garden.txt");
        static readonly List<XRHandSubsystem> _subs = new List<XRHandSubsystem>();

        void Awake()
        {
            Instance = this;
            var args = Environment.GetCommandLineArgs();
            bool fresh = Array.IndexOf(args, "-freshGarden") >= 0;
            if (Array.IndexOf(args, "-scriptedHand") >= 0) source = Source.Scripted;
            int offIdx = Array.IndexOf(args, "-dayOffset");
            int offset = offIdx >= 0 && offIdx + 1 < args.Length ? int.Parse(args[offIdx + 1], CultureInfo.InvariantCulture) : 0;
            _today = Garden.DayNumber(DateTime.Now) + offset;
            Garden = !fresh && File.Exists(SavePath) ? Garden.Deserialize(File.ReadAllText(SavePath)) : new Garden();
            Session = new BreathSession();
            _scripted = new SimulatedHand(seed: 3).Rest(2.5f).Breaths(6, 4f, 4f);
        }

        void Start()
        {
            view.permanentFronds = Garden.Fronds; view.flowers = Garden.Flowers; view.vitality = Garden.Vitality(_today);
            view.Build();
            Debug.Log($"[Terrarium] start: {StateJson()}");
        }

        void Update()
        {
            var s = ReadSample();
            LastStrength = s.Strength;
            var before = Session.Breaths;
            Session.Update(Time.deltaTime, s);
            if (Session.Breaths != before) Debug.Log($"[Terrarium] breath {Session.Breaths}/{Session.TargetBreaths} via {LastSource}");

            if (Session.Phase == BreathPhase.Complete && !_answered)
            {
                _answered = true;
                var answer = Garden.CompleteRitual(_today);
                try { File.WriteAllText(SavePath, Garden.Serialize()); } catch (Exception e) { Debug.LogWarning("[Terrarium] save failed: " + e.Message); }
                view.vitality = 1f;
                view.PlayAnswer();
                Debug.Log($"[Terrarium] ritual complete -> newFrond={answer.NewFrond} recovered={answer.Recovered} flower={answer.Flower} dew={answer.DewBeads}; {StateJson()}");
            }

            view.uncoil = Session.Uncoil; view.fog = Session.Fog;
            if (_answered && !view.AnswerPlaying && view.permanentFronds != Garden.Fronds)
            {
                // the answer has played: the grown frond joins the garden for good, a new fiddlehead waits for tomorrow
                view.permanentFronds = Garden.Fronds; view.flowers = Garden.Flowers;
            }
            if (_answered && !view.AnswerPlaying) view.uncoil = 0f;
            view.Apply(Time.deltaTime);
        }

        PinchSample ReadSample()
        {
            if (source == Source.Scripted) { LastSource = "scripted"; _scriptedT += Time.deltaTime; return _scripted.Sample(_scriptedT); }
            if (source == Source.XRHands || source == Source.Auto)
            {
                if (TryXRHands(out var xs)) { LastSource = "xr-hands"; return xs; }
                if (source == Source.XRHands) { LastSource = "xr-hands(lost)"; return PinchSample.Lost; }
            }
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null) { LastSource = "keyboard"; return new PinchSample(kb.spaceKey.isPressed ? 0.95f : 0.1f, true); }
#endif
            LastSource = "none";
            return new PinchSample(0.1f, true);
        }

        static bool TryXRHands(out PinchSample sample)
        {
            sample = PinchSample.Lost;
            SubsystemManager.GetSubsystems(_subs);
            XRHandSubsystem hs = null;
            foreach (var s in _subs) if (s.running) { hs = s; break; }
            if (hs == null) return false;
            float best = -1f;
            foreach (var hand in new[] { hs.rightHand, hs.leftHand })
            {
                if (!hand.isTracked) continue;
                if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var t) || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var i)) continue;
                float d = Vector3.Distance(t.position, i.position);
                best = Mathf.Max(best, 1f - Mathf.Clamp01((d - 0.015f) / (0.05f - 0.015f)));
            }
            if (best < 0f) return true; // subsystem running but no hand: genuinely lost (pauses the ritual)
            sample = new PinchSample(best, true);
            return true;
        }

        public string StateJson() => string.Format(CultureInfo.InvariantCulture,
            "{{\"phase\":\"{0}\",\"breaths\":{1},\"target\":{2},\"uncoil\":{3:0.000},\"fog\":{4:0.000},\"pinch\":\"{5}\",\"strength\":{6:0.00},\"source\":\"{7}\",\"fronds\":{8},\"vitality\":{9:0.00},\"rituals\":{10},\"t\":{11:0.00}}}",
            Session?.Phase, Session?.Breaths ?? 0, Session?.TargetBreaths ?? 0, Session?.Uncoil ?? 0, Session?.Fog ?? 0, Session?.Pinch,
            LastStrength, LastSource, Garden?.Fronds ?? 0, Garden?.Vitality(_today) ?? 1, Garden?.RitualsCompleted ?? 0, Session?.Time ?? 0);
    }
}
