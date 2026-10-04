using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GardenVR.Input
{
    /// <summary>
    /// The only Garden VR behaviour that reads keyboard and mouse devices.
    /// It fills a <see cref="RawKbm"/> each frame and forwards it to <see cref="KbmIntentMapper"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class KeyboardMouseIntentSource : MonoBehaviour, IHandIntentSource
    {
        readonly KbmIntentMapper _mapper = new KbmIntentMapper();

        [SerializeField] Camera _camera;
        [SerializeField] HoldMode _holdMode = HoldMode.Hold;

        public event Action<HandIntent> Intent;
        public event Action SystemPause;
        public event Action Recentred;
        /// <summary>L pressed. Apps that offer two looks listen; the rest ignore it.</summary>
        public event Action LookToggled;
        public event Action<DevCommand> DevCommandRaised;

        public bool IsPinching => _mapper.IsPinching;
        public Ray LookRay => _mapper.LookRay;
        public float PinchStrength => _mapper.PinchStrength;
        public bool IsTracked => _mapper.IsTracked;
        public string LookTargetId => _mapper.LookTargetId;
        public Quaternion HeadLocalRotation => _mapper.HeadLocalRotation;

        public HoldMode HoldMode
        {
            get => _holdMode;
            set => _holdMode = value;
        }

        public Camera View
        {
            get => _camera != null ? _camera : Camera.main;
            set => _camera = value;
        }

        public string BindingHint(HandIntentKind kind)
        {
            return _mapper.BindingHint(kind);
        }

        void OnEnable()
        {
            _mapper.Intent += HandleIntent;
            _mapper.SystemPause += HandlePause;
            _mapper.Recentred += HandleRecentred;
            _mapper.LookToggled += HandleLookToggled;
            _mapper.DevCommandRaised += HandleDev;
        }

        void OnDisable()
        {
            _mapper.Intent -= HandleIntent;
            _mapper.SystemPause -= HandlePause;
            _mapper.Recentred -= HandleRecentred;
            _mapper.LookToggled -= HandleLookToggled;
            _mapper.DevCommandRaised -= HandleDev;
        }

        void Update()
        {
            _mapper.HoldMode = _holdMode;
            _mapper.DevCommandsEnabled = Debug.isDebugBuild || Application.isEditor;
            RawKbm raw;
            if (!TryRead(out raw)) return;
            _mapper.Tick(raw, () => BuildRay(raw.MousePosition));
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SystemPause?.Invoke();
        }

        Ray BuildRay(Vector2 screen)
        {
            Camera cam = View;
            if (cam == null) return new Ray(Vector3.zero, Vector3.forward);
            return cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
        }

        static bool TryRead(out RawKbm raw)
        {
            raw = default;
            Keyboard keyboard;
            Mouse mouse;
            try
            {
                keyboard = Keyboard.current;
                mouse = Mouse.current;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            if (keyboard == null || mouse == null) return false;

            raw.Space = keyboard.spaceKey.isPressed;
            raw.LeftMouse = mouse.leftButton.isPressed;
            raw.MiddleMouse = mouse.middleButton.isPressed;
            raw.RightMouse = mouse.rightButton.isPressed;
            raw.F = keyboard.fKey.isPressed;
            raw.P = keyboard.pKey.isPressed;
            raw.Enter = keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed;
            raw.Tab = keyboard.tabKey.isPressed;
            raw.Shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            raw.Esc = keyboard.escapeKey.isPressed;
            raw.R = keyboard.rKey.isPressed;
            raw.F1 = keyboard.f1Key.isPressed;
            raw.F2 = keyboard.f2Key.isPressed;
            raw.F3 = keyboard.f3Key.isPressed;
            raw.BracketLeft = keyboard.leftBracketKey.isPressed;
            raw.BracketRight = keyboard.rightBracketKey.isPressed;
            raw.T = keyboard.tKey.isPressed;
            raw.L = keyboard.lKey.isPressed;
            raw.MousePosition = mouse.position.ReadValue();
            raw.MouseDelta = mouse.delta.ReadValue();
            raw.Dt = Time.unscaledDeltaTime;
            return true;
        }

        void HandleIntent(HandIntent intent) { Intent?.Invoke(intent); }
        void HandlePause() { SystemPause?.Invoke(); }
        void HandleRecentred() { Recentred?.Invoke(); }
        void HandleLookToggled() { LookToggled?.Invoke(); }
        void HandleDev(DevCommand command) { DevCommandRaised?.Invoke(command); }
    }
}
