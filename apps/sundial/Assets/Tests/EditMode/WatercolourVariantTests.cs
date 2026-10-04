using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class WatercolourVariantTests
    {
        [Test]
        public void VariantA_KeepsTheFaceMaterial()
        {
            GameObject root = NewDial();
            try
            {
                var view = root.GetComponent<DialView>();
                var renderer = root.GetComponentInChildren<MeshRenderer>();
                var face = renderer.sharedMaterial;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreSame(face, renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantWatercolour_SwapsToTheS1Material()
        {
            GameObject root = NewDial();
            try
            {
                var view = root.GetComponent<DialView>();
                var renderer = root.GetComponentInChildren<MeshRenderer>();
                var face = renderer.sharedMaterial;
                var s1 = new Material(face) { name = "Dial_Face_S1" };
                s1.EnableKeyword("_GVR_WATERCOLOUR");
                view.faceWatercolour = s1;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "watercolour" } });
                Assert.AreSame(s1, renderer.sharedMaterial);
                Assert.IsTrue(s1.IsKeywordEnabled("_GVR_WATERCOLOUR"));
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreSame(face, renderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantWatercolour_WithoutMaterialThrows()
        {
            GameObject root = NewDial();
            try
            {
                var view = root.GetComponent<DialView>();
                Assert.Throws<System.InvalidOperationException>(() =>
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "watercolour" } }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnknownVariantThrows()
        {
            GameObject root = NewDial();
            try
            {
                var view = root.GetComponent<DialView>();
                Assert.Throws<System.FormatException>(() =>
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "glass" } }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShaderDeclaresTheWatercolourTerms()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..",
                "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders", "FToon.shader"));
            string text = File.ReadAllText(path);
            StringAssert.Contains("_GVR_WATERCOLOUR", text);
            StringAssert.Contains("_ControlTex", text);
            StringAssert.Contains("_PaperH", text);
            StringAssert.Contains("C - (C - C * C) * (D - 1.0)", text);
            StringAssert.Contains("PaperHeight", text);
        }

        static GameObject NewDial()
        {
            var root = new GameObject("dial-s1-test");
            root.AddComponent<DialView>();
            var top = new GameObject("DialTop");
            top.transform.SetParent(root.transform, false);
            var renderer = top.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Fidelity/Toon");
            Assert.IsNotNull(shader, "Fidelity/Toon");
            renderer.sharedMaterial = new Material(shader) { name = "Dial_Face" };
            return root;
        }
    }
}
