using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Four short dashes on the breath ring, one at each quarter. They stay dim so the
    /// sweep still reads as one ring. Shown only while the box pace is the one in use.
    /// </summary>
    public static class BoxPaceMarks
    {
        public const string RootName = "BoxSides";

        static readonly Color Stone = new Color(0.16f, 0.28f, 0.20f, 1f);
        static readonly Color Glow = new Color(0.22f, 0.46f, 0.32f, 1f);

        public static void Present(Transform ring)
        {
            if (ring == null) return;
            Clear(ring);
            Transform space = ring.parent != null ? ring.parent : ring;
            var root = new GameObject(RootName);
            root.transform.SetParent(space, false);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            Vector3 center = space == ring ? Vector3.zero : ring.localPosition;
            float radius = 0.070f;
            if (ring.localScale.x > 0.05f && ring.localScale.x < 0.4f)
                radius = ring.localScale.x * 0.46f;
            float y = center.y + 0.0035f;
            // Flat on the desk. The ring card is rotated, so these stay in the jar's plane.
            Dash(root.transform, new Vector3(center.x, y, center.z - radius), new Vector3(0.006f, 0.0025f, 0.016f));
            Dash(root.transform, new Vector3(center.x, y, center.z + radius), new Vector3(0.006f, 0.0025f, 0.016f));
            Dash(root.transform, new Vector3(center.x - radius, y, center.z), new Vector3(0.016f, 0.0025f, 0.006f));
            Dash(root.transform, new Vector3(center.x + radius, y, center.z), new Vector3(0.016f, 0.0025f, 0.006f));
        }

        public static void Clear(Transform ring)
        {
            if (ring == null) return;
            Remove(ring);
            if (ring.parent != null) Remove(ring.parent);
        }

        static void Remove(Transform host)
        {
            Transform old = host.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        public static Transform FindRing(Transform jar)
        {
            if (jar == null) return null;
            Transform[] all = jar.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "BreathRing") return all[i];
            }
            return null;
        }

        static void Dash(Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Side";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Shader shader = Shader.Find("Fidelity/Glow");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "BoxSide" };
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", Stone);
            if (mat.HasProperty("_Emission")) mat.SetColor("_Emission", Glow);
            if (mat.HasProperty("_Rim")) mat.SetColor("_Rim", Color.black);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Stone);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
