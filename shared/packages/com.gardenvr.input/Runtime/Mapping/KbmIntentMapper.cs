using System;
using System.Collections.Generic;
using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>
    /// Pure keyboard/mouse mapping. Feed it a <see cref="RawKbm"/> and a look-ray function each frame.
    /// It does not read devices. Rules match docs/plans/terrarium.md section 4.
    /// </summary>
    public sealed class KbmIntentMapper
    {
        public const float HoldCommitSeconds = 0.30f;
        public const float PalmCommitSeconds = 0.6f;
        public const float DwellSeconds = 0.15f;
        public const float DegreesPerPixel = 0.15f;
        public const float YawLimitDegrees = 40f;
        public const float PitchLimitDegrees = 25f;

        readonly PinchStrengthRamp _ramp = new PinchStrengthRamp();
        RawKbm _prev;
        bool _want;
        bool _committed;
        bool _usedMouse;
        bool _latched;
        float _holdTime;
        bool _palmDown;
        bool _palmFired;
        float _palmTime;
        string _dwellCandidate;
        float _dwellTime;
        float _yaw;
        float _pitch;
        string _frameHit;

        public HoldMode HoldMode { get; set; } = HoldMode.Hold;

        /// <summary>Injected by the provider from Debug.isDebugBuild || Application.isEditor. Tests set this directly.</summary>
        public bool DevCommandsEnabled { get; set; }

        /// <summary>Optional. When null, the ray is resolved through <see cref="IntentTargetRegistry"/>.</summary>
        public Func<Ray, string> ResolveTargetId;

        /// <summary>Optional poke-only test. When null, the registry target's flag is used.</summary>
        public Func<string, bool> IsPokeOnlyTarget;

        /// <summary>Optional Tab order. When null, the registry's ordinal id list is used.</summary>
        public Func<IReadOnlyList<string>> FocusOrder;

        public float PinchStrength { get; private set; }
        public bool IsPinching { get; private set; }
        public bool IsTracked => true;
        public Ray LookRay { get; private set; }
        public string LookTargetId { get; private set; }
        public Quaternion HeadLocalRotation { get; private set; } = Quaternion.identity;

        public event Action<HandIntent> Intent;
        public event Action SystemPause;
        public event Action Recentred;
        public event Action<DevCommand> DevCommandRaised;

        public string BindingHint(HandIntentKind kind)
        {
            return HandBindings.PcHint(kind);
        }

        public void Tick(in RawKbm raw, Func<Ray> lookRay)
        {
            float dt = raw.Dt > 0f ? raw.Dt : 0f;
            Ray ray = lookRay != null ? lookRay() : new Ray(Vector3.zero, Vector3.forward);
            LookRay = ray;
            _frameHit = ResolveHit(ray);

            UpdateHead(raw);
            if (Pressed(raw.Esc, _prev.Esc))
                SystemPause?.Invoke();
            UpdateDev(raw);
            UpdatePalm(raw, dt, ray);
            if (Pressed(raw.F, _prev.F))
                Emit(HandIntentKind.Poke, ray, 1f, 0f, _frameHit);

            UpdateHold(raw, dt, ray);
            UpdateDwell(dt);
            UpdateTab(raw);
            if (Pressed(raw.Enter, _prev.Enter))
                EmitSelect(ray, LookTargetId);

            Emit(HandIntentKind.Look, ray, 1f, 0f, LookTargetId);
            _prev = raw;
        }

        void UpdateHead(in RawKbm raw)
        {
            if (raw.RightMouse)
            {
                _yaw += raw.MouseDelta.x * DegreesPerPixel;
                _pitch -= raw.MouseDelta.y * DegreesPerPixel;
                _yaw = Mathf.Clamp(_yaw, -YawLimitDegrees, YawLimitDegrees);
                _pitch = Mathf.Clamp(_pitch, -PitchLimitDegrees, PitchLimitDegrees);
            }
            if (Pressed(raw.R, _prev.R))
            {
                _yaw = 0f;
                _pitch = 0f;
                Recentred?.Invoke();
            }
            HeadLocalRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        void UpdateDev(in RawKbm raw)
        {
            if (!DevCommandsEnabled) return;
            if (Pressed(raw.F1, _prev.F1)) DevCommandRaised?.Invoke(DevCommand.StateOverlay);
            if (Pressed(raw.F2, _prev.F2)) DevCommandRaised?.Invoke(DevCommand.AutoPace);
            if (Pressed(raw.BracketLeft, _prev.BracketLeft)) DevCommandRaised?.Invoke(DevCommand.PreviousDay);
            if (Pressed(raw.BracketRight, _prev.BracketRight)) DevCommandRaised?.Invoke(DevCommand.NextDay);
        }

        void UpdatePalm(in RawKbm raw, float dt, Ray ray)
        {
            bool down = raw.P || raw.MiddleMouse;
            if (!down)
            {
                _palmDown = false;
                _palmTime = 0f;
                _palmFired = false;
                return;
            }
            if (!_palmDown)
            {
                _palmDown = true;
                _palmTime = 0f;
                _palmFired = false;
            }
            _palmTime += dt;
            if (_palmFired || _palmTime < PalmCommitSeconds) return;
            _palmFired = true;
            Emit(HandIntentKind.PalmOpen, ray, 1f, _palmTime, null);
        }

        void UpdateHold(in RawKbm raw, float dt, Ray ray)
        {
            if (HoldMode == HoldMode.Toggle && Pressed(raw.Space, _prev.Space))
                _latched = !_latched;

            bool mouse = raw.LeftMouse;
            bool spaceHolds = HoldMode == HoldMode.Hold && raw.Space;
            bool want = mouse || spaceHolds || _latched;

            bool ended = false;
            bool endedCommitted = false;
            bool endedClick = false;
            float endedHeld = 0f;

            if (want && !_want)
            {
                _want = true;
                _holdTime = 0f;
                _committed = _latched;
                _usedMouse = mouse;
                _ramp.Rise();
            }
            else if (!want && _want)
            {
                ended = true;
                endedCommitted = _committed;
                endedClick = _usedMouse && !_committed;
                endedHeld = _holdTime;
                _want = false;
                _committed = false;
                _usedMouse = false;
                _ramp.Fall();
            }

            if (_want)
            {
                _holdTime += dt;
                if (mouse) _usedMouse = true;
                if (_latched || _holdTime >= HoldCommitSeconds)
                    _committed = true;
            }

            _ramp.Tick(dt);
            PinchStrength = _ramp.Value;

            if (ended && endedCommitted)
                Emit(HandIntentKind.Release, ray, PinchStrength, endedHeld, Aim());
            else if (ended && endedClick)
                EmitSelect(ray, _frameHit);

            if (_want && _committed)
                Emit(HandIntentKind.PinchHold, ray, PinchStrength, _holdTime, Aim());

            IsPinching = _want && _committed;
        }

        void UpdateDwell(float dt)
        {
            string hit = _frameHit;
            if (hit != _dwellCandidate)
            {
                _dwellCandidate = hit;
                _dwellTime = 0f;
            }
            if (string.IsNullOrEmpty(_dwellCandidate)) return;
            _dwellTime += dt;
            if (_dwellTime >= DwellSeconds)
                LookTargetId = _dwellCandidate;
        }

        void UpdateTab(in RawKbm raw)
        {
            if (!Pressed(raw.Tab, _prev.Tab)) return;
            List<string> ids = CurrentIds();
            if (ids.Count == 0) return;
            int index = -1;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == LookTargetId)
                {
                    index = i;
                    break;
                }
            }
            int next;
            if (index < 0) next = raw.Shift ? ids.Count - 1 : 0;
            else if (raw.Shift) next = (index - 1 + ids.Count) % ids.Count;
            else next = (index + 1) % ids.Count;
            LookTargetId = ids[next];
            _dwellCandidate = LookTargetId;
            _dwellTime = 0f;
        }

        List<string> CurrentIds()
        {
            IReadOnlyList<string> src = FocusOrder != null ? FocusOrder() : IntentTargetRegistry.OrderedIds();
            var list = new List<string>();
            if (src == null) return list;
            var seen = new HashSet<string>();
            for (int i = 0; i < src.Count; i++)
            {
                string id = src[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                list.Add(id);
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        string ResolveHit(Ray ray)
        {
            string id = null;
            if (ResolveTargetId != null) id = ResolveTargetId(ray);
            else
            {
                IntentTarget target = IntentTargetRegistry.Raycast(ray);
                if (target != null) id = target.Id;
            }
            return string.IsNullOrEmpty(id) ? null : id;
        }

        string Aim()
        {
            return string.IsNullOrEmpty(_frameHit) ? LookTargetId : _frameHit;
        }

        void EmitSelect(Ray ray, string targetId)
        {
            HandIntentKind kind = PokeOnly(targetId) ? HandIntentKind.Poke : HandIntentKind.Pinch;
            Emit(kind, ray, 1f, 0f, targetId);
        }

        bool PokeOnly(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (IsPokeOnlyTarget != null) return IsPokeOnlyTarget(id);
            IntentTarget target;
            return IntentTargetRegistry.TryGet(id, out target) && target != null && target.PokeOnly;
        }

        void Emit(HandIntentKind kind, Ray ray, float strength, float held, string targetId)
        {
            Intent?.Invoke(new HandIntent(kind, ray, strength, held, true, targetId));
        }

        static bool Pressed(bool now, bool before)
        {
            return now && !before;
        }
    }
}
