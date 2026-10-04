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
                    if (p.x * p.x + p.z * p.z > 0.075f * 0.075f) rim = Mathf.Min(rim, p.y);
                }
                // 8 to 12 mm in the dossier. The nib clears it (shadow wash sits at 11 mm) and the plants keep their bases in view.
                Assert.GreaterOrEqual(top, 0.0075f);
                Assert.LessOrEqual(top, 0.0105f);
                Assert.Greater(centre, rim + 0.005f, "the crown is higher than the rim, so it is a mound and not a bowl");
                Assert.Less(rim, 0.0015f, "the lip comes down to the paper");
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
                for (int i = 0; i < v.Length; i += 7)
                {
                    Assert.AreEqual((v[i].x + 0.085f) / 0.17f, uv[i].x, 1e-4f);
                    Assert.AreEqual((v[i].z + 0.085f) / 0.17f, uv[i].y, 1e-4f);
                }
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
            StringAssert.Contains("AlphaToMask On", src);
            StringAssert.Contains("UNITY_VERTEX_OUTPUT_STEREO", src);
            StringAssert.Contains("Blend One Zero", src);
            // The dither cell lives in object space, so both eyes and every frame agree.
            StringAssert.Contains("floor(i.op.xz", src);
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
