using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class SoilMoundVariantTests
    {
        [Test]
        public void VariantA_KeepsTheOldDiscAndBuildsNoMound()
        {
            GameObject root = NewDial(out DialView view, out MeshRenderer disc);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsTrue(disc.enabled);
                Assert.IsNull(view.Mound);
                Assert.IsNull(root.transform.Find("SoilMound"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantSoilMound_SwapsTheDiscForOneMeshAndBackAgain()
        {
            GameObject root = NewDial(out DialView view, out MeshRenderer disc);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                Transform mound = root.transform.Find("SoilMound");
                Assert.IsNotNull(mound);
                Assert.IsTrue(mound.gameObject.activeSelf);
                Assert.IsFalse(disc.enabled, "the old disc steps aside");
                Assert.AreEqual(1, mound.GetComponentsInChildren<MeshRenderer>(true).Length, "the mound and its pebbles are one renderer");
                SoilMound m = view.Mound;
                TestContext.WriteLine("S4 mound: verts " + m.Vertices + ", tris " + m.Triangles + ", pebbles " + m.Pebbles + ", crown (mm) " + (m.Crown * 1000f).ToString("0.00"));
                Assert.Greater(m.Triangles, 1000);
                Assert.Less(m.Triangles, 8000, "the PC budget; the Quest fallback trims it");
                Assert.AreEqual("Fidelity/SoilMound", m.Renderer.sharedMaterial.shader.name);

                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsFalse(mound.gameObject.activeSelf);
                Assert.IsTrue(disc.enabled, "look A is back");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheMoundIsRaisedNotSunkenAndStaysUnderTheNib()
        {
            GameObject root = NewDial(out DialView view, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                Mesh mesh = view.Mound.Renderer.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] v = mesh.vertices;
                float rim = float.MaxValue, centre = 0f, top = 0f;
                foreach (Vector3 p in v)
                {
                    top = Mathf.Max(top, p.y);
                    if (p.x * p.x + p.z * p.z < 0.01f * 0.01f) centre = Mathf.Max(centre, p.y);
                    if (p.x * p.x + p.z * p.z > 0.105f * 0.105f) rim = Mathf.Min(rim, p.y);
                }
                // T-SUN-051: a lower crown than S4's 7.5 to 10.5 mm. Still clear of the paper, still under the shadow wash at 11 mm.
                Assert.GreaterOrEqual(top, 0.0040f);
                Assert.LessOrEqual(top, 0.0065f);
                Assert.Greater(centre, rim + 0.003f, "the crown is higher than the rim, so it is a bed and not a bowl");
                Assert.Less(rim, 0.0015f, "the lip comes down to the paper");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheBedIsAWideCrescentAlongTheNearRimNotACompactHeap()
        {
            GameObject root = NewDial(out DialView view, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                Mesh mesh = view.Mound.Renderer.GetComponent<MeshFilter>().sharedMesh;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (Vector3 p in mesh.vertices)
                {
                    x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                    z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
                }
                TestContext.WriteLine("S4b bed box (mm): x " + (x0 * 1000f).ToString("0") + " to " + (x1 * 1000f).ToString("0") + ", z " + (z0 * 1000f).ToString("0") + " to " + (z1 * 1000f).ToString("0"));
                Assert.Greater(x1 - x0, 0.20f, "wider than the S4 heap (0.17 m)");
                Assert.Less(z0, -0.100f, "reaches the near rim");
                Assert.Less(z1, 0.06f, "the far half of the dial stays wash");
                Assert.Greater(z1, 0.03f, "the base of the midday plant (z 0.0265) is on the bed");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheLayoutMapsTheBedBoxOntoItsEllipseBox()
        {
            float r = 0.1472f;
            DialLayout.BedBoxMap(r, 0.113f, -0.113f, 0.040f, out float sx, out float sz, out float oz);
            Assert.AreEqual(DialLayout.BedHalfX * r, 0.113f * sx, 1e-5f);
            Assert.AreEqual((DialLayout.BedCentreZ - DialLayout.BedHalfZ) * r, -0.113f * sz + oz, 1e-5f);
            Assert.AreEqual((DialLayout.BedCentreZ + DialLayout.BedHalfZ) * r, 0.040f * sz + oz, 1e-5f);
        }

        [Test]
        public void TheWashBleedIsOffWithoutAFaceTextureAndSwitchesOnWithOne()
        {
            GameObject root = NewDial(out DialView view, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                Assert.AreEqual(0f, view.Mound.Renderer.sharedMaterial.GetFloat("_WashOn"));
                view.Mound.SetWash(Texture2D.whiteTexture, new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f));
                Assert.AreEqual(1f, view.Mound.Renderer.sharedMaterial.GetFloat("_WashOn"));
                Assert.AreSame(Texture2D.whiteTexture, view.Mound.Renderer.sharedMaterial.GetTexture("_WashTex"));
                view.Mound.SetWash(null, Vector3.zero, Vector3.zero);
                Assert.AreEqual(0f, view.Mound.Renderer.sharedMaterial.GetFloat("_WashOn"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheBuildIsDeterministic()
        {
            GameObject root = NewDial(out DialView view, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                int tris = view.Mound.Triangles;
                int verts = view.Mound.Vertices;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                Assert.AreEqual(tris, view.Mound.Triangles);
                Assert.AreEqual(verts, view.Mound.Vertices);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UvIsTheDialPlaneSoThePaintingIsAPlanarProjection()
        {
            GameObject root = NewDial(out DialView view, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "soilmound" } });
                Mesh mesh = view.Mound.Renderer.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] v = mesh.vertices;
                Vector2[] uv = mesh.uv;
                var box = JsonUtility.FromJson<Box>(Resources.Load<TextAsset>(SoilMound.ResourceData).text);
                int checkedPoints = 0;
                for (int i = 0; i < v.Length; i += 7)
                {
                    Assert.AreEqual((v[i].x + box.span) / (2f * box.span), uv[i].x, 1e-4f);
                    Assert.AreEqual((v[i].z - box.zMin) / (box.zMax - box.zMin), uv[i].y, 1e-4f);
                    checkedPoints++;
                }
                Assert.Greater(checkedPoints, 100);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShaderKeepsTheStereoRulesAndDrawsNoTransparentLayer()
        {
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            string src = File.ReadAllText(Path.Combine(fx, "FSoilMound.shader"));
            // T-SUN-051: the edge is a smooth wash bleed. No alpha to coverage, no dither, no blend: opaque.
            StringAssert.DoesNotContain("AlphaToMask On", src);
            StringAssert.DoesNotContain("dither", src);
            StringAssert.Contains("UNITY_VERTEX_OUTPUT_STEREO", src);
            StringAssert.Contains("Blend One Zero", src);
            // The wash is the face's own texture, read at the object-space dial position, so both eyes and every frame agree.
            StringAssert.Contains("_WashTex", src);
            StringAssert.Contains("i.op.x, i.op.z", src);
            StringAssert.DoesNotContain("_ScreenParams", src);
            StringAssert.DoesNotContain("ComputeScreenPos", src);
            StringAssert.DoesNotContain("unity_StereoEyeIndex", src);
        }

        [Test]
        public void ResourcesCarryThePaintingMaterialAndHeightfield()
        {
            var material = Resources.Load<Material>(SoilMound.ResourceMaterial);
            var data = Resources.Load<TextAsset>(SoilMound.ResourceData);
            Assert.IsNotNull(material);
            Assert.IsNotNull(data);
            Assert.AreEqual("Fidelity/SoilMound", material.shader.name);
            Assert.IsNotNull(material.mainTexture);
            Assert.IsNotNull(material.GetTexture("_PaperH"), "the earth shares the face's paper height map");
        }

        [System.Serializable]
        class Box { public float span, zMin, zMax; }

        static GameObject NewDial(out DialView view, out MeshRenderer disc)
        {
            var root = new GameObject("dial-s4-test");
            view = root.AddComponent<DialView>();
            var top = new GameObject("DialTop");
            top.transform.SetParent(root.transform, false);
            top.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Fidelity/Toon")) { name = "Dial_Face" };
            var soil = new GameObject("Soil");
            soil.transform.SetParent(root.transform, false);
            soil.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            disc = soil.AddComponent<MeshRenderer>();
            disc.sharedMaterial = new Material(Shader.Find("Fidelity/Toon")) { name = "Dial_Soil" };
            view.plantSlots = new Transform[9];
            return root;
        }
    }
}
