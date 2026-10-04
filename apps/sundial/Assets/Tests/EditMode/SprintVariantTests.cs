using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    /// <summary>
    /// T-SUN-050. variant=sprint stacks the winners of the five spikes and the layout pass. These tests check the stack and the
    /// seams between the parts (the face, the room light on the drawn leaves and wedges, the cookie under the layout scale), and that
    /// look A comes back exactly.
    /// </summary>
    public class SprintVariantTests
    {
        const float FaceRadius = 0.1472f;

        [Test]
        public void SprintIsKnown_AndExpandsToTheStack()
        {
            GameObject root = NewDial(out DialView view, out _, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint" } });
                Assert.IsTrue(view.LayoutOn, "layout");
                Assert.IsNotNull(view.Mound, "soil mound");
                Assert.IsTrue(RoomLightGlobals.IsOn, "room light");
                Assert.IsNotNull(view.LeafAssemblyFor(1), "midday assembly");
                Assert.IsNotNull(view.LeafAssemblyFor(2), "evening assembly");
                Assert.Throws<System.FormatException>(() =>
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint+nonsense" } }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HaloV2DrawsForTheCardPlant_AndTheDrawnLeavesKeepTheLayoutHalo()
        {
            GameObject root = NewDial(out DialView view, out _, out _, out _);
            try
            {
                Renderer halo = view.haloRenderer;
                view.ApplyCaptureState(new Dictionary<string, string>
                {
                    { "halo", "1" }, { "haloTarget", "morning" }, { "gnomonDeg", "105" }, { "time", "1" }, { "variant", "sprint" }
                });
                Assert.IsNotNull(view.Halo2Kit, "the morning card gets halo v2");
                Assert.IsNotNull(root.transform.Find("PinchHalo").Find("PinchHalo2"));
                Assert.IsTrue(root.transform.Find("PinchHalo").Find("PinchHalo2").gameObject.activeSelf);
                Assert.IsFalse(halo.enabled, "and the card halo steps aside");
                view.ApplyCaptureState(new Dictionary<string, string> { { "haloTarget", "midday" } });
                Assert.IsFalse(root.transform.Find("PinchHalo").Find("PinchHalo2").gameObject.activeSelf, "a drawn-leaf plant has no baked mask");
                Assert.IsTrue(halo.enabled, "so the halo stroke (the layout's wider close) draws");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheMorningPlantStaysACard_BecauseItsAssemblyLostToIt()
        {
            GameObject root = NewDial(out DialView view, out Renderer[] cards, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint" } });
                Assert.IsTrue(cards[0].enabled, "the morning card draws");
                Assert.IsNull(view.LeafAssemblyFor(0), "and no morning assembly is built");
                Assert.IsFalse(cards[3].enabled, "the midday card steps aside");
                Assert.IsFalse(cards[6].enabled, "the evening card steps aside");
                Assert.IsTrue(root.transform.Find("LeafPlant.midday").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("LeafPlant.evening").gameObject.activeSelf);
                // Naming leafplant on top gives all three (the alternative the owner can compare).
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint+leafplant" } });
                Assert.IsFalse(cards[0].enabled);
                Assert.IsNotNull(view.LeafAssemblyFor(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheFaceIsTheWatercolourPaperOnTheLayoutDisc()
        {
            GameObject root = NewDial(out DialView view, out _, out MeshRenderer top, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint" } });
                Material face = top.sharedMaterial;
                Assert.AreEqual("Dial_Face_Layout_S1", face.name);
                Assert.IsTrue(face.IsKeywordEnabled("_GVR_WATERCOLOUR"));
                Assert.AreEqual("dial_face_layout_s1", face.GetTexture("_MainTex").name);
                Assert.AreEqual("dial_control_layout", face.GetTexture("_ControlTex").name);
                Assert.IsNotNull(face.GetTexture("_PaperH"));
                Assert.AreEqual(DialLayout.InkRing * FaceRadius, face.GetFloat("_InkRingR"), 1e-4f, "the ink ring sits on the layout's inner band edge");
                // S1 alone and the layout alone each keep their own face.
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout" } });
                Assert.AreEqual("Dial_Face_Layout", top.sharedMaterial.name);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreEqual("Dial_Face", top.sharedMaterial.name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheControlMapCarriesTheWashIdsOnTheWiderDisc()
        {
            // The remap is a radial stretch, so a pixel at the new outer edge of the wash (0.80 of the radius) still has a wash id,
            // and one in the band (0.90) has none. The ids keep the shader thresholds (0.16, 0.50, 0.83).
            var control = Resources.Load<Texture2D>("Layout/dial_control_layout");
            Assert.IsNotNull(control);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Layout", "dial_control_layout.png"));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)));
            int w = tex.width;
            float c = (w - 1) * 0.5f;
            int inside = 0;
            int outside = 0;
            for (int a = 0; a < 360; a += 5)
            {
                float rad = a * Mathf.Deg2Rad;
                Color wash = tex.GetPixel(Mathf.RoundToInt(c + Mathf.Cos(rad) * 0.80f * c), Mathf.RoundToInt(c + Mathf.Sin(rad) * 0.80f * c));
                Color band = tex.GetPixel(Mathf.RoundToInt(c + Mathf.Cos(rad) * 0.90f * c), Mathf.RoundToInt(c + Mathf.Sin(rad) * 0.90f * c));
                if (wash.a > 0.16f) inside++;
                if (band.a <= 0.16f) outside++;
            }
            // Three arcs cover 220 degrees of the 360 (the rest is paper between them), so about 60% of the samples are in a wash.
            Assert.Greater(inside, 72 * 0.45f, "washes reach 0.80");
            Assert.AreEqual(72, outside, "no wash in the band");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void TheRoomLightWedgeStandsUnderADrawnLeafPlant()
        {
            GameObject root = NewDial(out DialView view, out Renderer[] cards, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint" } });
                Transform wedges = root.transform.Find("RoomLightShadows");
                Assert.IsNotNull(wedges);
                Assert.IsFalse(cards[3].enabled, "the midday card is hidden");
                Assert.IsTrue(wedges.Find("PlantWedge.3").gameObject.activeSelf, "yet its wash wedge is drawn");
                Assert.IsTrue(wedges.Find("PlantWedge.6").gameObject.activeSelf);
                Assert.IsTrue(wedges.Find("PlantWedge.0").gameObject.activeSelf, "the morning card keeps its wedge");
                Assert.IsFalse(wedges.Find("PlantWedge.1").gameObject.activeSelf, "a plant that is not drawn throws none");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheCookieIsReadAtTheLookASizeWhateverTheLayoutScale()
        {
            GameObject root = NewDial(out DialView view, out _, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "roomlight" } });
                Matrix4x4 plain = Shader.GetGlobalMatrix("_GvrRoomW2C");
                Vector3 tenCm = root.transform.position + new Vector3(0.1f, 0f, 0f);
                Assert.AreEqual(0.1f, plain.MultiplyPoint3x4(tenCm).x, 1e-5f);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout+roomlight" } });
                Matrix4x4 scaled = Shader.GetGlobalMatrix("_GvrRoomW2C");
                // A point 10 cm from the dial centre on the table is 10 cm into the cookie, the same as look A, not 10 / 0.8843.
                Vector3 got = scaled.MultiplyPoint3x4(root.transform.position + new Vector3(0.1f, 0f, 0f));
                Assert.AreEqual(0.1f + DialLayout.Offset.x, got.x, 1e-4f, "the table distance, shifted by the layout offset only");
                Assert.Greater(Mathf.Abs(0.1f / DialLayout.Scale - got.x), 0.01f, "not stretched by 1 / " + DialLayout.Scale);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsFalse(RoomLightGlobals.IsOn);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LookAComesBackExactly()
        {
            GameObject root = NewDial(out DialView view, out Renderer[] cards, out MeshRenderer top, out MeshRenderer disc);
            try
            {
                Mesh contact = view.contactRenderer.GetComponent<MeshFilter>().sharedMesh;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "sprint" } });
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreEqual(Vector3.one, root.transform.localScale);
                Assert.AreEqual(Vector3.zero, root.transform.localPosition);
                Assert.AreEqual("Dial_Face", top.sharedMaterial.name);
                Assert.IsFalse(RoomLightGlobals.IsOn);
                for (int arc = 0; arc < 3; arc++) Assert.IsTrue(cards[arc * 3].enabled, "card " + arc);
                Assert.IsFalse(root.transform.Find("LeafPlant.midday").gameObject.activeSelf, "no assembly is drawn");
                Assert.IsFalse(root.transform.Find("LeafPlant.evening").gameObject.activeSelf);
                Assert.AreSame(contact, view.contactRenderer.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsTrue(disc.enabled, "the old soil disc is back");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheLeafShaderHasTheRoomLightPathAndStillKeepsTheStereoRules()
        {
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            string leaf = File.ReadAllText(Path.Combine(fx, "FLeaf.shader"));
            StringAssert.Contains("multi_compile _ _GVR_ROOMLIGHT", leaf);
            // The cookie is read once per vertex at the plant's base point, so a fragment of the plant takes no extra sample.
            StringAssert.Contains("SAMPLE_TEXTURE2D_LOD(_GvrRoomCookie", leaf);
            StringAssert.Contains("0.985", leaf);
            // The same cap and bias as Toon and Card, from the same globals.
            StringAssert.Contains("_GvrRoomParams.y", leaf);
            StringAssert.DoesNotContain("_ScreenParams", leaf);
            StringAssert.DoesNotContain("ComputeScreenPos", leaf);
            StringAssert.DoesNotContain("unity_StereoEyeIndex", leaf);
            var material = Resources.Load<Material>("LeafPlant/Leaf_Midday");
            Assert.IsNotNull(material);
            Assert.IsTrue(material.shader.isSupported);
        }

        [Test]
        public void TheLeafAtlasesTakeTheCookieAtTheBasePointLikeTheCards()
        {
            // The plant is one value (the cookie does not smear up it), exactly as the card's alpha-to-coverage path does.
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            string card = File.ReadAllText(Path.Combine(fx, "FCard.shader"));
            string leaf = File.ReadAllText(Path.Combine(fx, "FLeaf.shader"));
            StringAssert.Contains("TransformObjectToWorld(float3(0, 0, 0))", card);
            StringAssert.Contains("TransformObjectToWorld(float3(0, 0, 0))", leaf);
        }

        // A dial with nine cards, a contact mesh, a soil disc and a face. Enough for the whole sprint, no model needed.
        static GameObject NewDial(out DialView view, out Renderer[] cards, out MeshRenderer top, out MeshRenderer disc)
        {
            var root = new GameObject("dial-sprint-test");
            view = root.AddComponent<DialView>();
            view.faceRadius = FaceRadius;
            var topGo = new GameObject("DialTop");
            topGo.transform.SetParent(root.transform, false);
            topGo.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            top = topGo.AddComponent<MeshRenderer>();
            top.sharedMaterial = new Material(Shader.Find("Fidelity/Toon")) { name = "Dial_Face" };
            var soil = new GameObject("Soil");
            soil.transform.SetParent(root.transform, false);
            soil.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            disc = soil.AddComponent<MeshRenderer>();
            disc.sharedMaterial = new Material(Shader.Find("Fidelity/Toon")) { name = "Dial_Soil" };
            var slots = new Transform[9];
            cards = new Renderer[9];
            var mat = new Material(Shader.Find("Fidelity/Card"));
            for (int i = 0; i < 9; i++)
            {
                var go = new GameObject("plant." + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localScale = new Vector3(0.072f, 0.112f, 0.112f);
                go.AddComponent<MeshFilter>().sharedMesh = new Mesh();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                slots[i] = go.transform;
                cards[i] = r;
            }
            view.plantSlots = slots;
            var contactGo = new GameObject("PlantContacts");
            contactGo.transform.SetParent(root.transform, false);
            var quad = new Mesh();
            quad.vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward };
            quad.triangles = new[] { 0, 2, 1 };
            contactGo.AddComponent<MeshFilter>().sharedMesh = quad;
            view.contactRenderer = contactGo.AddComponent<MeshRenderer>();
            view.contactRenderer.sharedMaterial = new Material(Shader.Find("Fidelity/Card"));
            var haloGo = new GameObject("PinchHalo");
            haloGo.transform.SetParent(root.transform, false);
            haloGo.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            var halo = haloGo.AddComponent<MeshRenderer>();
            halo.sharedMaterial = new Material(Shader.Find("Fidelity/Card"));
            view.haloRenderer = halo;
            var poolGo = new GameObject("HaloPool");
            poolGo.transform.SetParent(root.transform, false);
            poolGo.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            view.poolRenderer = poolGo.AddComponent<MeshRenderer>();
            view.poolRenderer.sharedMaterial = new Material(Shader.Find("Fidelity/Card"));
            var morningCard = new Texture2D(4, 4) { name = "plant_sunrise_full" };
            var middayCard = new Texture2D(4, 4) { name = "plant_midday_full" };
            var duskCard = new Texture2D(4, 4) { name = "plant_dusk_full" };
            view.morningCards = new[] { morningCard, morningCard, morningCard, morningCard, morningCard };
            view.middayCards = new[] { middayCard, middayCard, middayCard, middayCard, middayCard };
            view.windDownCards = new[] { duskCard, duskCard, duskCard, duskCard, duskCard };
            view.roomCookie = new Texture2D(4, 4);
            view.roomShadow = new Material(Shader.Find("Fidelity/Card"));
            return root;
        }
    }
}
