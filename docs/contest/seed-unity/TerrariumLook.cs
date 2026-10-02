using UnityEngine;

namespace Terrarium
{
    /// <summary>One art direction: materials, light and post. The scene and the rules are identical across looks.</summary>
    [CreateAssetMenu(menuName = "Terrarium/Look")]
    public sealed class TerrariumLook : ScriptableObject
    {
        public string displayName;
        [TextArea] public string intent;

        [Header("Materials")]
        public Material glass, soil, pebble, moss, frond, frondActive, dew, table, cork, flower;
        public bool corkLid = true;

        [Header("Light")]
        public Color background = new Color(0.86f, 0.84f, 0.80f);
        public Color keyColor = new Color(1f, 0.95f, 0.86f);
        public float keyIntensity = 1.6f;
        public Vector3 keyEuler = new Vector3(38f, -32f, 0f);
        public Color ambientSky = new Color(0.62f, 0.66f, 0.70f), ambientEquator = new Color(0.55f, 0.52f, 0.48f), ambientGround = new Color(0.25f, 0.22f, 0.20f);
        public Color answerGlow = new Color(1f, 0.85f, 0.55f);
        public float answerGlowIntensity = 0.6f;

        [Header("Post")]
        public float bloomIntensity = 0.25f, bloomThreshold = 1.0f, postExposure = 0f, vignette = 0.18f, saturation = 0f, contrast = 0f;
        public bool acesTonemap = true;
    }
}
