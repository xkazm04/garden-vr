using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class RoomLightVariantTests
    {
        [TearDown]
        public void KeywordOff()
        {
            RoomLightGlobals.Clear();
        }

        [Test]
        public void VariantA_LeavesTheKeywordOff()
        {
            GameObject root = NewDial(out DialView view);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsFalse(RoomLightGlobals.IsOn);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantRoomLight_TurnsTheKeywordAndTheWedgesOnAndOffAgain()
        {
            GameObject root = NewDial(out DialView view);
            try
            {
                view.roomCookie = new Texture2D(4, 4);
                view.roomShadow = new Material(Shader.Find("Fidelity/Card"));
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "roomlight" } });
                Assert.IsTrue(RoomLightGlobals.IsOn);
                Transform wedges = root.transform.Find("RoomLightShadows");
                Assert.IsNotNull(wedges);
                Assert.IsTrue(wedges.gameObject.activeSelf);
                Assert.IsTrue(wedges.Find("GnomonWedge").gameObject.activeSelf);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsFalse(RoomLightGlobals.IsOn);
                Assert.IsFalse(wedges.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantRoomLight_WithoutTheKitThrows()
        {
            GameObject root = NewDial(out DialView view);
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "roomlight" } }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShadersDeclareTheRoomLightTerms()
        {
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            string toon = File.ReadAllText(Path.Combine(fx, "FToon.shader"));
            string card = File.ReadAllText(Path.Combine(fx, "FCard.shader"));
            string catcher = File.ReadAllText(Path.Combine(fx, "FShadowCatcher.shader"));
            StringAssert.Contains("multi_compile_fragment _ _GVR_ROOMLIGHT", toon);
            StringAssert.Contains("RoomLight.hlsl", toon);
            StringAssert.Contains("multi_compile_fragment _ _GVR_ROOMLIGHT", card);
            StringAssert.Contains("_Offset", catcher);
        }

        [Test]
        public void EveryShaderThatReadsTheCookieCompilesTheRoomLightKeyword()
        {
            // The shader list comes from the folder, not from this test, so a new cookie reader is covered on arrival.
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            var readers = new List<string>();
            foreach (string path in Directory.GetFiles(fx, "*.shader"))
            {
                string code = StripComments(File.ReadAllText(path));
                if (!code.Contains("_GvrRoomCookie") && !code.Contains("RoomLight.hlsl")) continue;
                readers.Add(Path.GetFileName(path));
                Assert.IsTrue(DeclaresRoomLightKeyword(code),
                    Path.GetFileName(path) + " reads the room-light cookie but never compiles _GVR_ROOMLIGHT");
            }
            Assert.GreaterOrEqual(readers.Count, 4, string.Join(", ", readers));

            // Seeded control: a pragma that survives only in a comment does not count.
            Assert.IsFalse(DeclaresRoomLightKeyword(StripComments(
                "// #pragma multi_compile _ _GVR_ROOMLIGHT\n/* #pragma multi_compile_fragment _ _GVR_ROOMLIGHT */\nfloat d = _GvrRoomCookie;")));
            Assert.IsTrue(DeclaresRoomLightKeyword("#pragma multi_compile_fragment _ _GVR_ROOMLIGHT"));
        }

        [Test]
        public void TheRoomLightFormulaAndItsGlobalsAreWrittenOnce()
        {
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            var sources = new List<string>();
            var where = new List<string>();
            foreach (string path in Directory.GetFiles(fx))
            {
                if (!path.EndsWith(".shader") && !path.EndsWith(".hlsl")) continue;
                string code = StripComments(File.ReadAllText(path));
                sources.Add(code);
                where.Add(Path.GetFileName(path) + " sites=" + CountSites(new List<string> { code }) + " decl=" + CountDeclarations(new List<string> { code }));
            }
            Assert.AreEqual(1, CountSites(sources), "gain formula sites (_GvrRoomParams.y): " + string.Join(", ", where));
            Assert.AreEqual(1, CountDeclarations(sources), "float4 _GvrRoomParams declarations: " + string.Join(", ", where));

            // Seeded control: two copies must count as two.
            var twice = new List<string> { "float4 _GvrRoomParams; x = _GvrRoomParams.y;", "float4 _GvrRoomParams; y = _GvrRoomParams.y;" };
            Assert.AreEqual(2, CountSites(twice));
            Assert.AreEqual(2, CountDeclarations(twice));
        }

        static int CountSites(List<string> sources)
        {
            int n = 0;
            foreach (string code in sources) n += System.Text.RegularExpressions.Regex.Matches(code, @"_GvrRoomParams\.y").Count;
            return n;
        }

        static int CountDeclarations(List<string> sources)
        {
            int n = 0;
            foreach (string code in sources) n += System.Text.RegularExpressions.Regex.Matches(code, @"float4\s+_GvrRoomParams\b").Count;
            return n;
        }

        static string StripComments(string code)
        {
            code = System.Text.RegularExpressions.Regex.Replace(code, @"/\*.*?\*/", " ", System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(code, @"//[^\n]*", " ");
        }

        static bool DeclaresRoomLightKeyword(string code)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(code, @"#pragma\s+multi_compile(_fragment|_vertex)?\s+_\s+_GVR_ROOMLIGHT\b");
        }

        [Test]
        public void TheCookieIsCappedAndBrighterOnTheWindowSide()
        {
            Assert.LessOrEqual(RoomLightGlobals.Amplitude, 0.15f);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "Art", "Textures", "room_cookie.png"));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)));
                float left = Mean(tex, 0.10f, 0.30f, 0.30f, 0.70f);
                float right = Mean(tex, 0.70f, 0.90f, 0.30f, 0.70f);
                Assert.Greater(left - right, 0.5f, "left " + left + " right " + right);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        // d in -1..1 over a u,v window of the cookie.
        static float Mean(Texture2D tex, float u0, float u1, float v0, float v1)
        {
            Color[] pixels = tex.GetPixels();
            float sum = 0f;
            int count = 0;
            for (int y = Mathf.FloorToInt(v0 * tex.height); y < Mathf.CeilToInt(v1 * tex.height); y++)
            {
                for (int x = Mathf.FloorToInt(u0 * tex.width); x < Mathf.CeilToInt(u1 * tex.width); x++)
                {
                    sum += pixels[y * tex.width + x].r * 2f - 1f;
                    count++;
                }
            }
            return sum / count;
        }

        static GameObject NewDial(out DialView view)
        {
            var root = new GameObject("dial-s2-test");
            view = root.AddComponent<DialView>();
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
