using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class Halo2VariantTests
    {
        static readonly Dictionary<string, string> Pinch = new Dictionary<string, string>
        {
            { "halo", "1" }, { "haloTarget", "midday" }, { "gnomonDeg", "105" }, { "time", "1" }
        };

        static Dictionary<string, string> With(string variant)
        {
            var d = new Dictionary<string, string>(Pinch);
            d["variant"] = variant;
            return d;
        }

        [Test]
        public void VariantA_DrawsTheCardHaloAndBuildsNothing()
        {
            GameObject root = NewDial(out DialView view, out Renderer halo, out _);
            try
            {
                view.ApplyCaptureState(With("a"));
                Assert.IsTrue(halo.enabled);
                Assert.IsNull(view.Halo2Kit);
                Assert.IsNull(halo.transform.Find("PinchHalo2"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantHalo2_ReplacesTheCardHaloWithOneMeshAndBackAgain()
        {
            GameObject root = NewDial(out DialView view, out Renderer halo, out _);
            try
            {
                view.ApplyCaptureState(With("halo2"));
                Halo2 kit = view.Halo2Kit;
                Assert.IsNotNull(kit);
                Transform piece = halo.transform.Find("PinchHalo2");
                Assert.IsNotNull(piece);
                Assert.IsTrue(piece.gameObject.activeSelf);
                Assert.IsFalse(halo.enabled, "the card halo steps aside");
                Assert.AreEqual(1, piece.GetComponentsInChildren<MeshRenderer>(true).Length, "the ring and the ground ellipse are one draw");
                Assert.AreSame(halo.sharedMaterial, kit.Renderer.sharedMaterial, "the same material, so the T-SUN-031 colours and glow stay");
                TestContext.WriteLine("S5 halo2 mesh: verts " + kit.Vertices + ", tris " + kit.Triangles);
                Assert.Less(kit.Triangles, 600);

                view.ApplyCaptureState(With("a"));
                Assert.IsFalse(piece.gameObject.activeSelf);
                Assert.IsTrue(halo.enabled, "look A is back");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheGroundHalfMeetsTheCardHalfAtTheCardBase()
        {
            GameObject root = NewDial(out DialView view, out _, out _);
            try
            {
                view.ApplyCaptureState(With("halo2"));
                Halo2 kit = view.Halo2Kit;
                Mesh mesh = kit.Mesh;
                Vector3[] v = mesh.vertices;
                Vector2[] uv = mesh.uv;
                float tan = Mathf.Tan(kit.PitchDeg * Mathf.Deg2Rad);
                // Every vertex shows up on the card plane, seen from the nominal camera, at the row its UV says. For the card half that
                // is its own height. For the ground half a point lifted y and put z in front of the bulge shows up y - z tan(pitch) up.
                var data = Resources.Load<TextAsset>(Halo2.ResourceJson);
                var file = JsonUtility.FromJson<FileProbe>(data.text);
                float range = file.vTop - file.vBottom;
                int checkedGround = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    float ly = (uv[i].y * range + file.vBottom) / file.cardH;
                    float apparent = v[i].y - (v[i].z - Halo2.BulgeAt(v[i].x)) * tan;
                    Assert.AreEqual(ly, apparent, 1e-4f, "vertex " + i);
                    if (v[i].z - Halo2.BulgeAt(v[i].x) > 1e-4f) checkedGround++;
                }
                Assert.Greater(checkedGround, 20, "the ground half is in front of the base");
                // The card base row exists in both halves and carries the same UV row.
                int baseRows = 0;
                foreach (Vector3 p in v) if (Mathf.Abs(p.y) < 1e-6f) baseRows++;
                Assert.GreaterOrEqual(baseRows, 25);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [System.Serializable]
        sealed class FileProbe
        {
            public float vTop, vBottom, cardH;
        }

        [Test]
        public void EveryBakedMaskLoadsIsOneClosedCurveWithAtMostTwoConcavities()
        {
            var data = Resources.Load<TextAsset>(Halo2.ResourceJson);
            Assert.IsNotNull(data);
            var file = JsonUtility.FromJson<MasksProbe>(data.text);
            Assert.AreEqual(75, file.masks.Length, "nine species by five stages, plus two blooms on each hero stage");
            foreach (Halo2MaskInfo m in file.masks)
            {
                Assert.LessOrEqual(m.concavities, 2, m.key);
                Assert.GreaterOrEqual(m.gapMinG1Px, 4f, m.key);
                // A seed is a speck inside the ground ellipse, so the ring sits further from it than the 4 to 10 px of a plant outline.
                Assert.LessOrEqual(m.gapMinG1Px, m.key.Contains("_seed") ? 14f : 10f, m.key);
                var tex = Resources.Load<Texture2D>(Halo2.ResourceDir + m.key);
                Assert.IsNotNull(tex, m.key);
            }
        }

        [System.Serializable]
        sealed class MasksProbe
        {
            public Halo2MaskInfo[] masks;
        }

        [Test]
        public void AMissingMaskFallsBackToLookAHalo()
        {
            GameObject root = NewDial(out DialView view, out Renderer halo, out _, "plant_nobody_full");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no baked halo"));
                view.ApplyCaptureState(With("halo2"));
                Assert.IsTrue(halo.enabled, "look A's halo draws when there is nothing baked");
                Assert.IsFalse(view.Halo2Kit.Renderer.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheSpillCoversTheGroundEllipseOnTheSoil()
        {
            GameObject root = NewDial(out DialView view, out Renderer halo, out Renderer pool);
            try
            {
                view.ApplyCaptureState(With("halo2"));
                Halo2MaskInfo info;
                Texture2D mask;
                Assert.IsTrue(view.Halo2Kit.TryMask("plant_midday_full", out mask, out info));
                float radius = view.Halo2Kit.RadiusCardUnits(info) * halo.transform.lossyScale.x;
                Assert.AreEqual(radius * 0.92f * 2f, pool.transform.localScale.x, 1e-4f);
                Assert.AreEqual(1f, pool.transform.localScale.y, 1e-6f);
                Assert.IsTrue(pool.enabled);
                Assert.IsTrue(pool.HasPropertyBlock(), "the spill is a property block");
                Assert.AreEqual(2.8f, pool.sharedMaterial.GetFloat("_Falloff"), 1e-5f, "the shared pool material keeps look A's falloff");

                view.ApplyCaptureState(With("a"));
                Assert.IsFalse(pool.HasPropertyBlock(), "look A clears the spill");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheSphereUnionStubIsAUnionOfCirclesAndSmoothsOnRequest()
        {
            var circles = new List<Vector3> { new Vector3(10f, 10f, 4f), new Vector3(16f, 10f, 4f) };
            Assert.Less(HaloSphereUnion.Sdf(new Vector2(13f, 10f), circles, 0f), 0f, "the overlap is inside");
            Assert.AreEqual(2f, HaloSphereUnion.Sdf(new Vector2(4f, 10f), circles, 0f), 1e-4f, "two units outside the left circle");
            float plain = HaloSphereUnion.Sdf(new Vector2(13f, 14.5f), circles, 0f);
            float smooth = HaloSphereUnion.Sdf(new Vector2(13f, 14.5f), circles, 4f);
            Assert.LessOrEqual(smooth, plain, "a smooth minimum only fills the waist");
            bool[] on = HaloSphereUnion.Rasterise(circles, 32, 20, 0f);
            int count = 0;
            foreach (bool b in on) if (b) count++;
            Assert.Greater(count, 90);
            Assert.AreEqual(on.Length, 32 * 20);
        }

        [Test]
        public void TheCardShaderIsUntouched()
        {
            string fx = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(fx, "FCard.shader"));
            StringAssert.Contains("SilhouetteGlow", src);
            StringAssert.DoesNotContain("halo2", src.ToLowerInvariant());
        }

        // A dial with the nine plant cards, a halo card and a pool. Enough for Apply, no model needed.
        static GameObject NewDial(out DialView view, out Renderer halo, out Renderer pool, string plantName = "plant_midday_full")
        {
            var root = new GameObject("dial-s5-test");
            view = root.AddComponent<DialView>();
            var top = new GameObject("DialTop");
            top.transform.SetParent(root.transform, false);
            top.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Fidelity/Toon")) { name = "Dial_Face" };
            var slots = new Transform[9];
            var cardMat = new Material(Shader.Find("Fidelity/Card"));
            for (int i = 0; i < 9; i++)
            {
                var go = new GameObject("plant." + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localScale = new Vector3(0.072f, 0.112f, 0.112f);
                go.transform.localPosition = new Vector3(0.02f * i, 0.016f, 0.02f);
                go.AddComponent<MeshFilter>().sharedMesh = new Mesh();
                go.AddComponent<MeshRenderer>().sharedMaterial = cardMat;
                slots[i] = go.transform;
            }
            view.plantSlots = slots;
            var haloGo = new GameObject("PinchHalo");
            haloGo.transform.SetParent(root.transform, false);
            haloGo.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            halo = haloGo.AddComponent<MeshRenderer>();
            halo.sharedMaterial = new Material(Shader.Find("Fidelity/Card"));
            view.haloRenderer = halo;
            var poolGo = new GameObject("HaloPool");
            poolGo.transform.SetParent(root.transform, false);
            poolGo.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            pool = poolGo.AddComponent<MeshRenderer>();
            pool.sharedMaterial = new Material(Shader.Find("Fidelity/Card"));
            view.poolRenderer = pool;
            var tex = new Texture2D(4, 4) { name = plantName };
            view.middayCards = new[] { tex, tex, tex, tex, tex };
            view.morningCards = view.middayCards;
            view.windDownCards = view.middayCards;
            return root;
        }
    }
}
