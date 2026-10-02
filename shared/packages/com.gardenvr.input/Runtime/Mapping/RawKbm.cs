using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>
    /// One frame of keyboard and mouse levels (held, not edges), plus the mouse and the unscaled delta time.
    /// The mapper only reads this struct. The keyboard/mouse provider is the only code that fills it from devices.
    /// </summary>
    public struct RawKbm
    {
        public bool Space;
        public bool LeftMouse;
        public bool MiddleMouse;
        public bool RightMouse;
        public bool F;
        public bool P;
        public bool Enter;
        public bool Tab;
        public bool Shift;
        public bool Esc;
        public bool R;
        public bool F1;
        public bool F2;
        public bool BracketLeft;
        public bool BracketRight;
        public Vector2 MousePosition;
        public Vector2 MouseDelta;
        public float Dt;
    }
}
