using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    /// <summary>T-SUN-052. variant=layout: proportions only. Look A, the habit record and the shared rules stay as they were.</summary>
    public class LayoutVariantTests
    {
        const float FaceRadius = 0.1472f;

        [Test]
        public void VariantA_LeavesTheDialExactlyAsItWas()
        {
            GameObject root = NewDial(out DialView view, out MeshRenderer disc, out MeshRenderer top);
            try
            {
                root.transform.localScale = Vector3.one;
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.IsFalse(view.LayoutOn);
                Assert.AreEqual(Vector3.one, root.transform.localScale);
                Assert.AreEqual("Dial_Face", top.sharedMaterial.name);
                Assert.AreEqual(Vector3.one, disc.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Layout_ScalesTheDialToTheMeasuredSizeAndGivesItBack()
        {
            GameObject root = NewDial(out DialView view, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout" } });
                Assert.IsTrue(view.LayoutOn);
                Assert.AreEqual(DialLayout.Scale, root.transform.localScale.x, 1e-5f);
                Assert.AreEqual(DialLayout.Scale, root.transform.localScale.y, 1e-5f);
                Assert.AreEqual(DialLayout.Scale, root.transform.localScale.z, 1e-5f);
                // 1148.9 px at 1.0 against the reference's 1008.3 px at the same framing and the same hand.
                Assert.AreEqual(1008.3f / 1148.9f, DialLayout.Scale, 0.01f);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreEqual(Vector3.one, root.transform.localScale);
                Assert.AreEqual(Vector3.zero, root.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VariantsCombineWithAPlus_AndAnUnknownOneStillThrows()
        {
            GameObject root = NewDial(out DialView view, out MeshRenderer disc, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout+soilmound+leafplant" } });
                Assert.IsTrue(view.LayoutOn);
                Assert.IsNotNull(view.Mound, "the mound is drawn under the layout");
                Assert.IsFalse(disc.enabled, "and the old disc steps aside");
                Assert.Throws<System.FormatException>(() =>
                    view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout+nonsense" } }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheBedIsWiderAndNearerThanTheOldMound()
        {
            Bounds old = MoundBounds(false, out float oldCentreZ);
            Bounds bed = MoundBounds(true, out float bedCentreZ);
            TestContext.WriteLine("mound x " + old.min.x.ToString("0.000") + ".." + old.max.x.ToString("0.000") + " z " + old.min.z.ToString("0.000") + ".." + old.max.z.ToString("0.000")
                                  + " | bed x " + bed.min.x.ToString("0.000") + ".." + bed.max.x.ToString("0.000") + " z " + bed.min.z.ToString("0.000") + ".." + bed.max.z.ToString("0.000"));
            Assert.Greater(bed.size.x, old.size.x * 1.3f, "the bed fills the face");
            Assert.Less(bedCentreZ, oldCentreZ - 0.04f, "centred nearer the viewer");
            // It stays inside the wash disc (the inner ring at 0.82 of the face radius), allowing the ragged lip and the pebbles.
            Assert.Less(Mathf.Max(Mathf.Abs(bed.min.x), Mathf.Abs(bed.max.x)), DialLayout.InnerRing * FaceRadius * 1.08f);
            Assert.Greater(bed.min.z, -DialLayout.InnerRing * FaceRadius * 1.08f);
            // The gnomon base (the origin) is on the bed.
            Assert.Greater(bed.max.z, 0f);
        }

        [Test]
        public void WithoutTheMound_TheFlatDiscBecomesTheBedWhateverPlaneItsMeshLiesIn()
        {
            GameObject root = NewDial(out DialView view, out MeshRenderer disc, out _);
            try
            {
                // The imported disc lies in the local XY plane and the node turns it flat, as the FBX does.
                var mesh = new Mesh();
                mesh.vertices = new[] { new Vector3(-0.07f, -0.07f, 0f), new Vector3(0.07f, -0.07f, 0f), new Vector3(0.07f, 0.07f, 0f), new Vector3(-0.07f, 0.07f, 0f) };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                disc.GetComponent<MeshFilter>().sharedMesh = mesh;
                disc.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout" } });
                Bounds b = disc.bounds;
                float s = DialLayout.Scale;
                Assert.AreEqual(2f * DialLayout.BedHalfX * FaceRadius * s, b.size.x, 1e-3f);
                Assert.AreEqual(2f * DialLayout.BedHalfZ * FaceRadius * s, b.size.z, 1e-3f);
                Assert.AreEqual(DialLayout.BedCentreZ * FaceRadius * s + DialLayout.Offset.z, b.center.z, 1e-3f);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreEqual(Vector3.one, disc.transform.localScale);
                Assert.AreEqual(Vector3.zero, disc.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlantsStandOnTheBed_AndMorningAndDuskStayInTheirArcs()
        {
            for (int arc = 0; arc < 3; arc++)
                Assert.IsTrue(DialLayout.InsideBed(DialLayout.PlantSpot[arc]), "arc " + arc + " stands on the bed");
            float morning = Angle(DialLayout.PlantSpot[0]);
            if (morning < 0f) morning += 360f;
            float dusk = Angle(DialLayout.PlantSpot[2]);
            Assert.That(morning, Is.InRange(DialView.MorningArc1, DialView.MorningArc0));
            Assert.That(dusk, Is.InRange(DialView.WindDownArc1, DialView.WindDownArc0));
            // Midday stands clear of the start of its arc (22 degrees) to stay on the bed. It must not wander into the morning arc.
            float midday = Angle(DialLayout.PlantSpot[1]);
            Assert.Greater(midday, DialView.WindDownArc1);
            Assert.Less(midday, DialView.MiddayArc0);
            // Two plants never stand closer than a third of a face radius (the halo of one must not run over the other).
            for (int a = 0; a < 3; a++)
                for (int b = a + 1; b < 3; b++)
                    Assert.Greater((DialLayout.PlantSpot[a] - DialLayout.PlantSpot[b]).magnitude, 0.33f);
        }

        [Test]
        public void TheTileRowsStayOrderedAndRowZeroIsInTheBand()
        {
            float[] r = DialLayout.TileRowRadius;
            Assert.AreEqual(3, r.Length, "three rows per arc, as the habit record has");
            Assert.Greater(r[0], r[1]);
            Assert.Greater(r[1], r[2]);
            Assert.Greater(r[0], DialLayout.InnerRing, "row 0 sits in the band");
            Assert.Less(r[0], 0.95f, "and clear of the ticks");
            Assert.Greater(r[2], 0.5f);
        }

        [Test]
        public void TheFaceMaterialSwapsAndComesBack()
        {
            GameObject root = NewDial(out DialView view, out _, out MeshRenderer top);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "layout" } });
                Assert.AreEqual("Dial_Face_Layout", top.sharedMaterial.name);
                Assert.AreEqual("dial_face_layout", top.sharedMaterial.GetTexture("_MainTex").name);
                Assert.AreEqual(DialLayout.InkRing * FaceRadius, top.sharedMaterial.GetFloat("_InkRingR"), 1e-4f);
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", "a" } });
                Assert.AreEqual("Dial_Face", top.sharedMaterial.name);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheHaloStrokeIsTheLockedOneAndOnlyTheCloseIsWider()
        {
            Assert.Greater(DialLayout.HaloClosePx, (int)DialView.HaloClosePx);
            Assert.AreEqual(3.0f, DialView.HaloCorePx, "T-SUN-031 core width");
            Assert.AreEqual(8.0f, DialView.HaloGlowPx, "T-SUN-031 glow width");
            Vector4 c = DialLayout.HaloCanvas;
            Assert.LessOrEqual(c.x, -0.5f);
            Assert.GreaterOrEqual(c.z, 0.5f);
            Assert.LessOrEqual(c.y, 0f);
            Assert.GreaterOrEqual(c.w, 1f);
        }

        static float Angle(Vector2 p)
        {
            return Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
        }

        static Bounds MoundBounds(bool layout, out float centreZ)
        {
            GameObject root = NewDial(out DialView view, out _, out _);
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string> { { "variant", layout ? "layout+soilmound" : "soilmound" } });
                Mesh mesh = view.Mound.Renderer.GetComponent<MeshFilter>().sharedMesh;
                var b = new Bounds(mesh.vertices[0], Vector3.zero);
                float sum = 0f;
                foreach (Vector3 v in mesh.vertices)
                {
                    b.Encapsulate(v);
                    sum += v.z;
                }
                centreZ = sum / mesh.vertexCount;
                return b;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject NewDial(out DialView view, out MeshRenderer disc, out MeshRenderer top)
        {
            var root = new GameObject("dial-layout-test");
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
            view.plantSlots = new Transform[9];
            return root;
        }
    }
}
