using System;
using System.IO;
using GardenVR.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class CaptureEditModeTests
    {
        [Test]
        public void ImageCheck_AllBlack()
        {
            Color32[] pixels = Fill(4, 4, new Color32(0, 0, 0, 255));
            ImageStats stats = ImageCheck.Analyze(pixels, 4, 4, null);
            Assert.AreEqual(1f, stats.BlackFrac, 1e-5f);
            Assert.AreEqual(0f, stats.WhiteFrac, 1e-5f);
            Assert.AreEqual(0f, stats.MagentaFrac, 1e-5f);
            Assert.AreEqual(0f, stats.MeanLuma, 1e-5f);
            string json = ImageCheck.ToJson(stats);
            StringAssert.Contains("\"blackFrac\"", json);
            StringAssert.Contains("\"meanLuma\"", json);
            StringAssert.Contains("\"magentaFrac\"", json);
            StringAssert.Contains("\"sha256\"", json);
        }

        [Test]
        public void ImageCheck_AllMagenta()
        {
            Color32[] pixels = Fill(4, 4, new Color32(255, 0, 255, 255));
            ImageStats stats = ImageCheck.Analyze(pixels, 4, 4, null);
            Assert.AreEqual(1f, stats.MagentaFrac, 1e-5f);
            Assert.AreEqual(0f, stats.BlackFrac, 1e-5f);
            Assert.Greater(stats.MeanLuma, 0.1f);
        }

        [Test]
        public void ImageCheck_Gradient()
        {
            var pixels = new Color32[256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)i, (byte)i, (byte)i, 255);
            ImageStats stats = ImageCheck.Analyze(pixels, 256, 1, null);
            Assert.AreEqual(0.5f, stats.MeanLuma, 0.001f);
            Assert.AreEqual(0f, stats.MagentaFrac, 1e-5f);
            Assert.Greater(stats.BlackFrac, 0f);
            Assert.Less(stats.BlackFrac, 0.05f);
            Assert.Greater(stats.WhiteFrac, 0f);
            Assert.Less(stats.WhiteFrac, 0.05f);
        }

        [Test]
        public void SideBySide_SizeAndPixels()
        {
            RgbaImage left = Solid(24, 6, new Color32(200, 10, 10, 255));
            RgbaImage right = Solid(16, 6, new Color32(10, 10, 200, 255));
            RgbaImage sbs = SideBySide.Compose(left, right, "ref", "ren", "DialG1", "none");
            Assert.AreEqual(40, sbs.Width);
            Assert.AreEqual(6 + SideBySide.StripHeight, sbs.Height);
            Assert.AreEqual(SideBySide.Paper.r, sbs.GetTop(0, 0).r);
            Assert.AreEqual(200, sbs.GetTop(0, SideBySide.StripHeight).r);
            Assert.AreEqual(200, sbs.GetTop(24, SideBySide.StripHeight).b);
        }

        [Test]
        public void SideBySide_LabelStripChangesWithCaption()
        {
            RgbaImage left = Solid(220, 4, new Color32(1, 1, 1, 255));
            RgbaImage right = Solid(220, 4, new Color32(2, 2, 2, 255));
            RgbaImage a = SideBySide.Compose(left, right, "aaa", "bbb", "DialG1", "none");
            RgbaImage b = SideBySide.Compose(left, right, "zzz", "bbb", "DialG1", "none");
            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Height, b.Height);
            Assert.IsTrue(StripDiffers(a, b), "label strip did not change when the reference name changed");
        }

        [Test]
        public void Diff_WithoutMask()
        {
            RgbaImage a = Solid(2, 2, new Color32(255, 0, 0, 255));
            RgbaImage b = Solid(2, 2, new Color32(0, 0, 0, 255));
            DiffResult diff = ImageDiff.Compare(a, b, default(RgbaImage));
            Assert.AreEqual(1f / 3f, diff.MeanAbs, 1e-5f);
            Assert.AreEqual(diff.MeanAbs, diff.OutsideMask, 1e-5f);
            Assert.AreEqual(0f, diff.InsideMask, 1e-5f);
            Assert.AreEqual(0, diff.InsideCount);
            Assert.AreEqual(4, diff.OutsideCount);
            StringAssert.Contains("\"outsideMask\"", CaptureJson.Diff(diff));
        }

        [Test]
        public void Diff_WithMask()
        {
            var a = new RgbaImage(2, 1, new[] { new Color32(255, 0, 0, 255), new Color32(255, 0, 0, 255) });
            var b = new RgbaImage(2, 1, new[] { new Color32(255, 0, 0, 255), new Color32(0, 0, 0, 255) });
            var mask = new RgbaImage(2, 1, new[] { new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 255) });
            DiffResult diff = ImageDiff.Compare(a, b, mask);
            Assert.AreEqual(0f, diff.InsideMask, 1e-5f);
            Assert.AreEqual(1f / 3f, diff.OutsideMask, 1e-5f);
            Assert.AreEqual(1, diff.InsideCount);
            Assert.AreEqual(1, diff.OutsideCount);
            Assert.AreEqual(1f / 6f, diff.MeanAbs, 1e-5f);
        }

        [Test]
        public void Framings_BuiltInsMatchFidelityTools()
        {
            FramingCatalog catalog = Framings.Parse("");
            Framing dial = catalog.Get("DialG1");
            Assert.AreEqual(30f, dial.Fov, 0.0001f);
            Assert.AreEqual(0f, dial.Eye.x, 0.0001f);
            Assert.AreEqual(0.322f, dial.Eye.y, 0.0001f);
            Assert.AreEqual(-0.411f, dial.Eye.z, 0.0001f);
            Assert.AreEqual(0.012f, dial.LookAt.y, 0.0001f);
            Assert.AreEqual(3.6f, dial.LensShift.x, 0.0001f);
            Assert.AreEqual(0.5f, dial.LensShift.y, 0.0001f);
            Assert.AreEqual("plate-dial.png", dial.Plate);
            Assert.AreEqual(1824, dial.Width);
            Assert.AreEqual(1024, dial.Height);

            Framing jar = catalog.Get("JarG1");
            Assert.AreEqual(28.2f, jar.Fov, 0.0001f);
            Assert.AreEqual(0.175f, jar.Eye.y, 0.0001f);
            Assert.AreEqual(-0.44f, jar.Eye.z, 0.0001f);
            Assert.AreEqual(0.072f, jar.LookAt.y, 0.0001f);
            Assert.AreEqual(0f, jar.LensShift.x, 0.0001f);
            Assert.AreEqual(-0.4f, jar.LensShift.y, 0.0001f);
            Assert.AreEqual("plate-jar.png", jar.Plate);

            Framing seated = catalog.Get("SeatedPOV");
            Assert.AreEqual(90f, seated.Fov, 0.0001f);
            Assert.AreEqual(0f, seated.Eye.x, 0.0001f);
            Assert.AreEqual(1.15f, seated.Eye.y, 0.0001f);
            Assert.IsTrue(string.IsNullOrEmpty(seated.Plate));
            Assert.Less(seated.LookAt.y, seated.Eye.y);
        }

        [Test]
        public void Framings_JsonOverrideAndAddition()
        {
            const string json = "{\"overrides\":{\"DialG1\":{\"fov\":33.5,\"lensShift\":[1.5,-2]}},\"additions\":[{\"name\":\"DeskClose\",\"eye\":[0,1.2,-0.4],\"lookAt\":[0,0.7,0.2],\"fov\":55}]}";
            FramingCatalog catalog = Framings.Parse(json);
            Framing dial = catalog.Get("DialG1");
            Assert.AreEqual(33.5f, dial.Fov, 0.0001f);
            Assert.AreEqual(1.5f, dial.LensShift.x, 0.0001f);
            Assert.AreEqual(-2f, dial.LensShift.y, 0.0001f);
            Assert.AreEqual("plate-dial.png", dial.Plate);
            Assert.AreEqual(0.322f, dial.Eye.y, 0.0001f);
            Framing added = catalog.Get("DeskClose");
            Assert.AreEqual(55f, added.Fov, 0.0001f);
            Assert.AreEqual(1.2f, added.Eye.y, 0.0001f);
            Assert.IsTrue(string.IsNullOrEmpty(added.Plate));
            Assert.Throws<FormatException>(() => Framings.Parse("{\"additions\":[{\"name\":\"MissingFields\"}]}"));

            string path = Path.Combine(Application.dataPath, "Capture", "framings.json");
            Assert.IsTrue(File.Exists(path), path);
            FramingCatalog app = Framings.Parse(File.ReadAllText(path));
            Assert.AreEqual(30f, app.Get("DialG1").Fov, 0.0001f);
            Assert.AreEqual(90f, app.Get("SeatedPOV").Fov, 0.0001f);
        }

        [Test]
        public void Crops_SamePixelRows()
        {
            RgbaImage a = RgbaImage.Alloc(4, 2, new Color32(1, 0, 0, 255));
            RgbaImage b = RgbaImage.Alloc(4, 2, new Color32(2, 0, 0, 255));
            a.SetTop(0, 0, new Color32(9, 0, 0, 255));
            b.SetTop(0, 0, new Color32(8, 0, 0, 255));
            var regions = ImageCrops.ParseRegions("0,0,2,1;1,1,2,1");
            RgbaImage stacked = ImageCrops.Stack(a, b, regions);
            Assert.AreEqual(4, stacked.Width);
            Assert.AreEqual(2, stacked.Height);
            Assert.AreEqual(9, stacked.GetTop(0, 0).r);
            Assert.AreEqual(8, stacked.GetTop(2, 0).r);
            Assert.AreEqual(1, stacked.GetTop(0, 1).r);
            Assert.AreEqual(2, stacked.GetTop(2, 1).r);
        }

        [Test]
        public void Contract_StateSyntaxAndFrameNames()
        {
            var state = CaptureState.Parse("breath=0.5, halo=on");
            Assert.AreEqual("0.5", state["breath"]);
            Assert.AreEqual("on", state["halo"]);
            Assert.AreEqual(0, CaptureState.Parse("").Count);
            Assert.AreEqual("f0000.png", SequenceRecorder.FrameFileName(0));
            Assert.AreEqual("f0012.png", SequenceRecorder.FrameFileName(12));
        }

        [Test]
        public void Measure_TwoOpaqueOneTransparent_KnownCounts()
        {
            var root = new GameObject("measure-root");
            Material opaque = null;
            Material transparent = null;
            try
            {
                GameObject cubeA = GameObject.CreatePrimitive(PrimitiveType.Cube);
                GameObject cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cubeA.transform.SetParent(root.transform, false);
                cubeB.transform.SetParent(root.transform, false);
                quad.transform.SetParent(root.transform, false);
                Material source = cubeA.GetComponent<Renderer>().sharedMaterial;
                Assert.IsNotNull(source, "primitive material missing");
                Assert.IsNotNull(source.shader, "primitive shader missing");
                opaque = new Material(source);
                transparent = new Material(source);
                transparent.renderQueue = 3000;
                Assert.AreEqual(1, BudgetMeasure.PassCount(opaque), source.shader.name);
                Assert.IsFalse(BudgetMeasure.IsTransparent(opaque));
                Assert.IsTrue(BudgetMeasure.IsTransparent(transparent));

                Renderer ra = cubeA.GetComponent<Renderer>();
                Renderer rb = cubeB.GetComponent<Renderer>();
                Renderer rq = quad.GetComponent<Renderer>();
                ra.sharedMaterial = opaque;
                rb.sharedMaterial = opaque;
                rq.sharedMaterial = transparent;
                ra.shadowCastingMode = ShadowCastingMode.Off;
                rb.shadowCastingMode = ShadowCastingMode.Off;
                rq.shadowCastingMode = ShadowCastingMode.On;

                BudgetReport report = BudgetMeasure.Count(root.GetComponentsInChildren<Renderer>());
                Assert.AreEqual(3, report.Renderers);
                Assert.AreEqual(1, report.ShadowCasters);
                Assert.AreEqual(1, report.TransparentDraws);
                Assert.AreEqual(4, report.DrawsEst, "drawsEst shader=" + source.shader.name);
                Assert.AreEqual(12, BudgetMeasure.MeshTriangles(ra), "cube tris");
                Assert.AreEqual(2, BudgetMeasure.MeshTriangles(rq), "quad tris");
                Assert.AreEqual(26, report.Tris);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (opaque != null) UnityEngine.Object.DestroyImmediate(opaque);
                if (transparent != null) UnityEngine.Object.DestroyImmediate(transparent);
            }
        }

        static Color32[] Fill(int width, int height, Color32 color)
        {
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            return pixels;
        }

        static RgbaImage Solid(int width, int height, Color32 color)
        {
            return new RgbaImage(width, height, Fill(width, height, color));
        }

        static bool StripDiffers(RgbaImage a, RgbaImage b)
        {
            int strip = SideBySide.StripHeight * a.Width;
            int start = (a.Height - SideBySide.StripHeight) * a.Width;
            for (int i = 0; i < strip; i++)
            {
                Color32 pa = a.Pixels[start + i];
                Color32 pb = b.Pixels[start + i];
                if (pa.r != pb.r || pa.g != pb.g || pa.b != pb.b) return true;
            }
            return false;
        }
    }
}
