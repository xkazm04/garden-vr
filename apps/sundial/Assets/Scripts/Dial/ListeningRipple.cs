using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>A small irregular ink ring on the front of the dial, present only during capture.</summary>
    public sealed class ListeningRipple : MonoBehaviour
    {
        LineRenderer _line;
        Material _ink;
        public bool ReducedMotion { get; set; }
        public static ListeningRipple Create(Transform parent)
        {
            var go = new GameObject("speech.listening");
            go.transform.SetParent(parent, false);
            var dial = parent.GetComponent<DialView>();
            float radius = dial != null ? dial.faceRadius : 0.1472f;
            float faceY = dial != null ? dial.faceY : 0.012f;
            go.transform.localPosition = new Vector3(-0.018f, faceY + 0.002f, -radius * 0.76f);
            var ripple = go.AddComponent<ListeningRipple>();
            // Reuse the app's included ink shader; a Shader.Find-only URP shader is stripped in players.
            ripple._ink = new Material(InkLetter.Card());
            ripple._ink.SetColor("_Color", InkLetter.Ink);
            ripple._ink.SetTexture("_MainTex", Texture2D.whiteTexture);
            ripple._line = go.AddComponent<LineRenderer>();
            ripple._line.sharedMaterial = ripple._ink;
            ripple._line.useWorldSpace = false;
            ripple._line.loop = true;
            ripple._line.widthMultiplier = 0.0007f;
            ripple._line.positionCount = 64;
            ripple._line.shadowCastingMode = ShadowCastingMode.Off;
            ripple._line.receiveShadows = false;
            var label = new GameObject("label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0.026f, 0.001f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            InkLetter.Apply(label.AddComponent<TextMesh>(), "listening", 48, 0.0011f);
            ripple.Draw();
            go.SetActive(false);
            return ripple;
        }
        void Update() { Draw(); }
        void Draw()
        {
            if (_line == null) return;
            float pulse = ReducedMotion ? 1f : 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 3f);
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                float radius = 0.006f * pulse * (1f + 0.04f * Mathf.Sin(angle * 3f + 0.4f));
                _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }
        void OnDestroy() { if (_ink != null) Destroy(_ink); }
    }
}
