using System.Collections.Generic;
using GardenVR.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Additive season details on the locked jar. Week 1 builds nothing.
    /// A star-fern sprig (the second species) appears at week 3, and a second one at week 6.
    /// Tiny blossoms use the authored flower mesh and only increase with the week.
    /// </summary>
    public sealed class SeasonDetails
    {
        public const float SprigHeight = 0.030f;
        const int MaxShownBlooms = 6;

        static readonly Vector3[] SprigSeats =
        {
            new Vector3(0.020f, 0f, -0.012f),
            new Vector3(-0.016f, 0f, 0.008f)
        };

        static readonly Color SprigCool = new Color(0.40f, 0.58f, 0.24f);
        static readonly Color SprigWarm = new Color(0.78f, 0.50f, 0.16f);
        static readonly Color BloomEmission = new Color(0.72f, 0.50f, 0.18f);
        static readonly Color BloomTint = new Color(0.98f, 0.88f, 0.58f, 1f);

        int _sprigs = -1;
        int _blooms = -1;
        int _warmthMilli = int.MinValue;
        Material _bloomMat;
        Material _haloMat;
        readonly List<Transform> _halos = new List<Transform>();

        public int SprigCount { get; private set; }
        public int BloomCount { get; private set; }
        public IReadOnlyList<Transform> Halos => _halos;

        /// <summary>Same golden-angle seats every time, inside the moss and clear of the ritual flower.</summary>
        public static Vector3 BloomLocal(int index)
        {
            float yaw = (index * 137.50776f + 78f) * Mathf.Deg2Rad;
            float radius = 0.015f + (index % 3) * 0.004f;
            return new Vector3(Mathf.Cos(yaw) * radius, JarView.MossBedY + 0.011f, Mathf.Sin(yaw) * radius);
        }

        public void Show(Transform root, int lifetimeFronds, float warmth, Mesh flowerMesh, Material flowerSource, Mesh quad, Material haloSource)
        {
            if (root == null) return;
            int fronds = Mathf.Max(0, lifetimeFronds);
            int sprigs = Season.Sprigs(fronds);
            int blooms = Season.TinyFlowers(fronds);
            int shownBlooms = Mathf.Min(blooms, MaxShownBlooms);
            int milli = Mathf.RoundToInt(Mathf.Clamp01(warmth) * 1000f);
            if (sprigs == _sprigs && shownBlooms == _blooms && milli == _warmthMilli)
            {
                if ((sprigs == 0 && shownBlooms == 0) || root.Find("SeasonDetails") != null) return;
            }

            Clear(root);
            _sprigs = sprigs;
            _blooms = shownBlooms;
            _warmthMilli = milli;
            SprigCount = 0;
            BloomCount = 0;
            _halos.Clear();
            if (sprigs == 0 && shownBlooms == 0) return;

            var host = new GameObject("SeasonDetails");
            host.transform.SetParent(root, false);

            Color sprigEmission = JarView.SeasonColor(SprigCool, SprigWarm, warmth);
            for (int i = 0; i < sprigs && i < SprigSeats.Length; i++)
            {
                Vector3 seat = SprigSeats[i];
                seat.y = JarView.MossBedY;
                GameObject sprig = CompanionGarden.PlaceSprig(
                    host.transform,
                    "SeasonSprig" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    seat,
                    SprigHeight,
                    sprigEmission,
                    "Companions/sprig_gold");
                if (sprig != null) SprigCount++;
            }

            if (shownBlooms > 0 && flowerMesh != null && flowerSource != null)
            {
                Material bloom = BloomMaterial(flowerSource);
                Material halo = HaloMaterial(haloSource);
                for (int i = 0; i < shownBlooms; i++)
                {
                    var go = new GameObject("SeasonBloom" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    go.transform.SetParent(host.transform, false);
                    go.transform.localPosition = BloomLocal(i);
                    go.transform.localRotation = Quaternion.Euler(96f, -24f + i * 18f, 8f);
                    go.transform.localScale = Vector3.one * 0.20f;
                    go.AddComponent<MeshFilter>().sharedMesh = flowerMesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = bloom;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    BloomCount++;
                    if (halo != null && quad != null)
                    {
                        var card = new GameObject("SeasonHalo" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        card.transform.SetParent(go.transform, false);
                        card.transform.localPosition = new Vector3(0f, 0.010f, -0.003f);
                        card.transform.localRotation = Quaternion.identity;
                        card.transform.localScale = new Vector3(0.09f, 0.09f, 1f);
                        card.AddComponent<MeshFilter>().sharedMesh = quad;
                        var haloRenderer = card.AddComponent<MeshRenderer>();
                        haloRenderer.sharedMaterial = halo;
                        haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        haloRenderer.receiveShadows = false;
                        _halos.Add(card.transform);
                    }
                }
            }
        }

        Material BloomMaterial(Material flowerSource)
        {
            if (_bloomMat != null) return _bloomMat;
            _bloomMat = new Material(flowerSource) { name = "SeasonBloomMat" };
            _bloomMat.SetColor("_Tint", BloomTint);
            _bloomMat.SetColor("_Emission", BloomEmission);
            if (_bloomMat.HasProperty("_Rim")) _bloomMat.SetColor("_Rim", BloomEmission * 0.45f);
            return _bloomMat;
        }

        Material HaloMaterial(Material haloSource)
        {
            if (_haloMat != null) return _haloMat;
            if (haloSource == null) return null;
            _haloMat = new Material(haloSource) { name = "SeasonHaloMat" };
            _haloMat.SetColor("_Color", new Color(0.95f, 0.78f, 0.42f, 1f) * 0.35f);
            if (_haloMat.HasProperty("_Falloff")) _haloMat.SetFloat("_Falloff", 1.3f);
            return _haloMat;
        }

        void Clear(Transform root)
        {
            _halos.Clear();
            Transform existing = root.Find("SeasonDetails");
            if (existing != null)
            {
                Renderer[] renderers = existing.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material mat = renderers[i].sharedMaterial;
                    if (mat == null) continue;
                    if (mat == _bloomMat || mat == _haloMat) continue;
                    if (mat.name == "SeasonSprigMat") DestroyObject(mat);
                }
                DestroyObject(existing.gameObject);
            }
            if (_bloomMat != null) DestroyObject(_bloomMat);
            if (_haloMat != null) DestroyObject(_haloMat);
            _bloomMat = null;
            _haloMat = null;
        }

        static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            Object.DestroyImmediate(obj);
        }
    }
}
