using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// The first-run hand is a mint light beside the jar. The two mask textures stay the poses.
    /// Fidelity/GhostHand feathers them. Fidelity/Card is only the fallback if that shader is missing.
    /// </summary>
    public static class GhostHandLook
    {
        public static readonly Vector3 LocalPos = new Vector3(0.072f, 0.066f, -0.024f);
        public static readonly Vector3 LocalEuler = new Vector3(5f, 174f, -4f);
        public static readonly Vector3 LocalScale = new Vector3(0.064f, 0.086f, 1f);
        public static readonly Color Mint = new Color(0.55f, 0.92f, 0.72f, 1f);
        public static readonly Color Rim = new Color(0.72f, 1.05f, 0.90f, 1f);
        public const float Body = 0.30f;
        public const float RimStrength = 1.05f;

        public static Material Create(Texture2D open, Texture2D pinch, float pinchAmount)
        {
            Shader shader = Shader.Find("Fidelity/GhostHand");
            bool ghost = shader != null;
            if (!ghost) shader = Shader.Find("Fidelity/Card");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var mat = new Material(shader) { name = "GhostHand" };
            if (open != null && mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", open);
            if (pinch != null && mat.HasProperty("_PinchTex")) mat.SetTexture("_PinchTex", pinch);
            if (mat.HasProperty("_Pinch")) mat.SetFloat("_Pinch", Mathf.Clamp01(pinchAmount));
            if (ghost)
            {
                mat.SetColor("_Color", Mint);
                mat.SetColor("_RimColor", Rim);
                mat.SetFloat("_Body", Body);
                mat.SetFloat("_Rim", RimStrength);
                return mat;
            }
            mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Src")) mat.SetFloat("_Src", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_Dst")) mat.SetFloat("_Dst", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_Ring")) mat.SetFloat("_Ring", 0f);
            if (mat.HasProperty("_Boil")) mat.SetFloat("_Boil", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            return mat;
        }

        public static void Pose(Transform hand)
        {
            if (hand == null) return;
            hand.localPosition = LocalPos;
            hand.localRotation = Quaternion.Euler(LocalEuler);
            hand.localScale = LocalScale;
        }
    }
}
