using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class LeafPlantVariantTests
    {
        [Test]
        public void VariantA_DrawsTheCardAndBuildsNoAssembly()
        {
            GameObject root = NewDial(out DialView view, out Renderer[] cards, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsTrue(cards[3].enabled, "the midday card draws in look A");
                Assert.IsNull(view.LeafAssembly);
                Assert.IsNull(root.transform.Find("LeafPlant.midday"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantLeafPlant_SwapsTheMiddayCardForOneMeshAndBackAgain()
        {
            GameObject root = NewDial(out DialView view, out Renderer[] cards, out MeshRenderer contact);
            try
            {
                Mesh before = contact.GetComponent<MeshFilter>().sharedMesh;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" } });
                Transform plant = root.transform.Find("LeafPlant.midday");
                Assert.IsNotNull(plant);
                Assert.IsTrue(plant.gameObject.activeSelf);
                Assert.IsFalse(cards[3].enabled, "the card steps aside");
                Assert.IsTrue(cards[0].enabled, "the other plants keep their cards");
                Assert.IsTrue(cards[6].enabled);
                Assert.AreNotSame(before, contact.GetComponent<MeshFilter>().sharedMesh, "the old midday contact steps aside");
                MeshFilter mesh = plant.GetComponent<MeshFilter>();
                Assert.Greater(mesh.sharedMesh.triangles.Length / 3, 100);
                // Two renderers under the plant: the one mesh and the contact quad. That is the whole draw budget of the swap.
                Assert.AreEqual(2, plant.GetComponentsInChildren<MeshRenderer>(true).Length);

                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsFalse(plant.gameObject.activeSelf);
                Assert.IsTrue(cards[3].enabled);
                Assert.AreSame(before, contact.GetComponent<MeshFilter>().sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheAssemblyIsBetween20And60CardsAndIsDeterministic()
        {
            GameObject root = NewDial(out DialView view, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" }, { "bloom.midday", "2" } });
                LeafPlant plant = view.LeafAssembly;
                Assert.IsNotNull(plant);
                Assert.GreaterOrEqual(plant.Cards, 20);
                Assert.LessOrEqual(plant.Cards, 60);
                int cards = plant.Cards;
                int tris = plant.Triangles;
                int verts = plant.Vertices;
                // Bud and no-bloom rebuilds are different plants. Going back to open rebuilds the same one.
                view.ApplyCaptureState(new Dictionary<string, string> { { "bloom.midday", "1" } });
                Assert.AreNotEqual(tris, plant.Triangles);
                view.ApplyCaptureState(new Dictionary<string, string> { { "bloom.midday", "2" } });
                Assert.AreEqual(cards, plant.Cards);
                Assert.AreEqual(tris, plant.Triangles);
                Assert.AreEqual(verts, plant.Vertices);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NormalsComeFromOneEllipsoid_UpOnTopDownUnderneathOutwardAtTheSides()
        {
            Assert.Greater(LeafPlant.EllipsoidNormal(new Vector3(0f, 0.09f, 0f)).y, 0.99f);
            Assert.Less(LeafPlant.EllipsoidNormal(new Vector3(0f, -0.01f, 0f)).y, -0.99f);
            Assert.Greater(LeafPlant.EllipsoidNormal(new Vector3(0.04f, 0.04f, 0f)).x, 0.9f);
            Assert.Less(LeafPlant.EllipsoidNormal(new Vector3(0f, 0.04f, -0.04f)).z, -0.9f);
        }

        [Test]
        public void TheHaloSilhouetteHasTheAssemblyInIt()
        {
            GameObject root = NewDial(out DialView view, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" }, { "bloom.midday", "2" } });
                Texture2D sil = view.LeafAssembly.BakeSilhouette(new Vector3(0f, -0.6f, 0.8f), new Vector2(0.072f, 0.112f), 256, 512, 1f);
                try
                {
                    Color32[] px = sil.GetPixels32();
                    int on = 0;
                    int topHalf = 0;
                    for (int i = 0; i < px.Length; i++)
                    {
                        if (px[i].a < 128) continue;
                        on++;
                        if (i / 256 > 256) topHalf++;
                    }
                    Assert.Greater(on, 3000, "silhouette pixels " + on);
                    Assert.Greater(topHalf, 500, "the heads reach the upper half of the card");
                }
                finally
                {
                    Object.DestroyImmediate(sil);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LeafShaderKeepsTheStereoRules()
        {
            string fx = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "shared", "packages", "com.gardenvr.fx", "Runtime", "Shaders"));
            string leaf = File.ReadAllText(Path.Combine(fx, "FLeaf.shader"));
            StringAssert.Contains("AlphaToMask On", leaf);
            StringAssert.Contains("UNITY_VERTEX_OUTPUT_STEREO", leaf);
            StringAssert.Contains("floor(_T *", leaf);
            // Nothing in it may read the screen position or an eye index: both eyes must get the same pixels.
            StringAssert.DoesNotContain("_ScreenParams", leaf);
            StringAssert.DoesNotContain("ComputeScreenPos", leaf);
            StringAssert.DoesNotContain("unity_StereoEyeIndex", leaf);
            StringAssert.DoesNotContain("SV_POSITION.xy", leaf);
        }

        [Test]
        public void ResourcesCarryTheAtlasMaterialAndParts()
        {
            var material = Resources.Load<Material>(LeafPlant.ResourceMaterial);
            var parts = Resources.Load<TextAsset>(LeafPlant.ResourceParts);
            Assert.IsNotNull(material);
            Assert.IsNotNull(parts);
            Assert.AreEqual("Fidelity/Leaf", material.shader.name);
            var atlas = material.mainTexture as Texture2D;
            Assert.IsNotNull(atlas);
            Assert.IsTrue(atlas.isReadable, "the halo silhouette reads the atlas alpha");
        }

        // A dial with nine card renderers and a contact mesh. Enough for ApplyLeafPlant, no model needed.
        static GameObject NewDial(out DialView view, out Renderer[] cards, out MeshRenderer contact)
        {
            var root = new GameObject("dial-s3-test");
            view = root.AddComponent<DialView>();
            var top = new GameObject("DialTop");
            top.transform.SetParent(root.transform, false);
            var face = top.AddComponent<MeshRenderer>();
            face.sharedMaterial = new Material(Shader.Find("Fidelity/Toon")) { name = "Dial_Face" };
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
            contact = contactGo.AddComponent<MeshRenderer>();
            contact.sharedMaterial = new Material(Shader.Find("Fidelity/Card"));
            view.contactRenderer = contact;
            return root;
        }
    }
}
