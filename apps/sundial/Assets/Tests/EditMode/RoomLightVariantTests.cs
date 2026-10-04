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
            StringAssert.Contains("_GvrRoomCookie", toon);
            StringAssert.Contains("multi_compile_fragment _ _GVR_ROOMLIGHT", card);
            StringAssert.Contains("_Offset", catcher);
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
