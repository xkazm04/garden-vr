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

        static readonly string[] Names = { "LeafPlant.morning", "LeafPlant.midday", "LeafPlant.evening" };
        static readonly string[] BloomKeys = { "bloom.morning", "bloom.midday", "bloom.winddown" };

        [Test]
        public void VariantLeafPlant_SwapsEachHeroCardForOneMeshAndBackAgain()
        {
            GameObject root = NewDial(out DialView view, out Renderer[] cards, out MeshRenderer contact);
            try
            {
                Mesh before = contact.GetComponent<MeshFilter>().sharedMesh;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" } });
                var plants = new Transform[3];
                for (int arc = 0; arc < 3; arc++)
                {
                    plants[arc] = root.transform.Find(Names[arc]);
                    Assert.IsNotNull(plants[arc], Names[arc]);
                    Assert.IsTrue(plants[arc].gameObject.activeSelf);
                    Assert.IsFalse(cards[arc * 3].enabled, "the card steps aside for " + Names[arc]);
                    MeshFilter mesh = plants[arc].GetComponent<MeshFilter>();
                    Assert.Greater(mesh.sharedMesh.triangles.Length / 3, 100);
                    // Two renderers under each plant: the one mesh and the contact quad. That is the whole draw budget of the swap.
                    Assert.AreEqual(2, plants[arc].GetComponentsInChildren<MeshRenderer>(true).Length);
                }
                Assert.AreNotSame(before, contact.GetComponent<MeshFilter>().sharedMesh, "the old contact discs step aside");

                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                for (int arc = 0; arc < 3; arc++)
                {
                    Assert.IsFalse(plants[arc].gameObject.activeSelf);
                    Assert.IsTrue(cards[arc * 3].enabled);
                }
                Assert.AreSame(before, contact.GetComponent<MeshFilter>().sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryAssemblyIsUnderTheSixtyCardCapAndDeterministic()
        {
            for (int arc = 0; arc < 3; arc++)
            {
                GameObject root = NewDial(out DialView view, out _, out _);
                try
                {
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" }, { BloomKeys[arc], "2" } });
                    LeafPlant plant = view.LeafAssemblyFor(arc);
                    Assert.IsNotNull(plant, Names[arc]);
                    TestContext.WriteLine(Names[arc] + " open: cards " + plant.Cards + ", verts " + plant.Vertices + ", tris " + plant.Triangles);
                    Assert.GreaterOrEqual(plant.Cards, 20, Names[arc]);
                    Assert.LessOrEqual(plant.Cards, LeafSpecies.CardCap, Names[arc] + " cards " + plant.Cards);
                    int cards = plant.Cards;
                    int tris = plant.Triangles;
                    int verts = plant.Vertices;
                    // Bud and no-bloom rebuilds are different plants. Going back to open rebuilds the same one.
                    view.ApplyCaptureState(new Dictionary<string, string> { { BloomKeys[arc], "1" } });
                    TestContext.WriteLine(Names[arc] + " bud: cards " + plant.Cards + ", tris " + plant.Triangles);
                    Assert.AreNotEqual(tris, plant.Triangles, Names[arc]);
                    Assert.LessOrEqual(plant.Cards, LeafSpecies.CardCap);
                    view.ApplyCaptureState(new Dictionary<string, string> { { BloomKeys[arc], "0" } });
                    TestContext.WriteLine(Names[arc] + " none: cards " + plant.Cards + ", tris " + plant.Triangles);
                    Assert.LessOrEqual(plant.Cards, LeafSpecies.CardCap);
                    view.ApplyCaptureState(new Dictionary<string, string> { { BloomKeys[arc], "2" } });
                    Assert.AreEqual(cards, plant.Cards, Names[arc]);
                    Assert.AreEqual(tris, plant.Triangles, Names[arc]);
                    Assert.AreEqual(verts, plant.Vertices, Names[arc]);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void MiddayIsBushierThanTheFirstSpikeAndStillInsideTheCap()
        {
            // T-SUN-045 built 52 cards: 32 leaves in one size class. The second size class adds leaves up to the 60 card cap.
            GameObject root = NewDial(out DialView view, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" }, { "bloom.midday", "2" } });
                LeafPlant plant = view.LeafAssembly;
                Assert.Greater(plant.Cards, 52);
                Assert.LessOrEqual(plant.Cards, LeafSpecies.CardCap);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EachAssemblyStaysNearItsOldCardBoxAndHasDepth()
        {
            for (int arc = 0; arc < 3; arc++)
            {
                GameObject root = NewDial(out DialView view, out _, out _);
                try
                {
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" }, { BloomKeys[arc], "2" } });
                    LeafPlant plant = view.LeafAssemblyFor(arc);
                    Bounds b = plant.Root.GetComponent<MeshFilter>().sharedMesh.bounds;
                    TestContext.WriteLine(Names[arc] + ": size (m) " + b.size.x.ToString("0.0000") + " x " + b.size.y.ToString("0.0000") + " x " + b.size.z.ToString("0.0000"));
                    // The old cards are about 0.06 to 0.07 wide and 0.09 to 0.11 high with the plant inside their lower three quarters.
                    Assert.Less(b.size.x, 0.12f, Names[arc]);
                    Assert.Greater(b.size.y, 0.05f, Names[arc]);
                    Assert.Less(b.size.y, 0.11f, Names[arc]);
                    Assert.Greater(b.size.z, 0.025f, Names[arc] + " has real depth; a billboard card has none");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
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
        public void TheHaloSilhouetteHasEveryAssemblyInIt()
        {
            var cardSize = new[] { new Vector2(0.064f, 0.096f), new Vector2(0.072f, 0.112f), new Vector2(0.0593f, 0.0935f) };
            for (int arc = 0; arc < 3; arc++)
            {
                GameObject root = NewDial(out DialView view, out _, out _);
                try
                {
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "leafplant" }, { BloomKeys[arc], "2" } });
                    Texture2D sil = view.LeafAssemblyFor(arc).BakeSilhouette(new Vector3(0f, -0.6f, 0.8f), cardSize[arc], 256, 512, 1f);
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
                        Assert.Greater(on, 2500, Names[arc] + " silhouette pixels " + on);
                        Assert.Greater(topHalf, 150, Names[arc] + " reaches the upper half of the card");
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
        public void ResourcesCarryTheAtlasMaterialAndPartsOfEverySpecies()
        {
            foreach (LeafSpecies species in LeafSpecies.ByArc)
            {
                var material = Resources.Load<Material>(species.MaterialResource);
                var parts = Resources.Load<TextAsset>(species.PartsResource);
                Assert.IsNotNull(material, species.Id);
                Assert.IsNotNull(parts, species.Id);
                Assert.AreEqual("Fidelity/Leaf", material.shader.name);
                var atlas = material.mainTexture as Texture2D;
                Assert.IsNotNull(atlas, species.Id);
                Assert.IsTrue(atlas.isReadable, "the halo silhouette reads the atlas alpha");
                // The slicer found the bare run of every stem piece, and the veins went to the pencil layer.
                StringAssert.Contains("\"bare\"", parts.text, species.Id);
                StringAssert.Contains("\"pencil_px\"", parts.text, species.Id);
            }
            Assert.AreEqual("LeafPlant/Leaf_Midday", LeafPlant.ResourceMaterial);
            Assert.AreEqual(LeafPlant.ResourceMaterial, LeafSpecies.Midday.MaterialResource);
            Assert.AreEqual(LeafPlant.ResourceParts, LeafSpecies.Midday.PartsResource);
        }

        [Test]
        public void SpeciesLayoutsCoverTheirStemsAndHeads()
        {
            foreach (LeafSpecies species in LeafSpecies.ByArc)
            {
                Assert.AreEqual(species.Arc, System.Array.IndexOf(LeafSpecies.ByArc, species), species.Id);
                foreach (LeafHeadDef h in species.Heads)
                    Assert.Less(h.Stem, species.Stems.Length, species.Id);
                foreach (LeafExtraBud e in species.ExtraBuds)
                    Assert.Less(e.Stem, species.Stems.Length, species.Id);
            }
            Assert.AreEqual(LeafLayout.Basal, LeafSpecies.Evening.Layout);
            Assert.IsFalse(LeafSpecies.Evening.UsesFaceFlowers, "lavender has spikes, not face-on flowers");
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
