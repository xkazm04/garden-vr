using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GardenVR.Capture
{
    public struct DiffResult
    {
        public float MeanAbs;
        public float OutsideMask;
        public float InsideMask;
        public int Width;
        public int Height;
        public int InsideCount;
        public int OutsideCount;
    }

    public static class ImageDiff
    {
        /// <summary>
        /// Mean absolute per-channel difference, 0..1, on raw bytes.
        /// A mask pixel with r &gt;= 128 is inside. A null mask pixel buffer treats every pixel as outside.
        /// </summary>
        public static DiffResult Compare(RgbaImage a, RgbaImage b, RgbaImage mask)
        {
            if (a.Pixels == null || b.Pixels == null) throw new ArgumentException("diff image is missing");
            if (a.Width != b.Width || a.Height != b.Height)
                throw new InvalidOperationException("diff inputs differ in size");
            bool hasMask = mask.Pixels != null;
            if (hasMask && (mask.Width != a.Width || mask.Height != a.Height))
                throw new InvalidOperationException("mask size differs from the images");
            double all = 0, inside = 0, outside = 0;
            int nIn = 0, nOut = 0;
            int n = a.Pixels.Length;
            for (int i = 0; i < n; i++)
            {
                double d = (Math.Abs(a.Pixels[i].r - b.Pixels[i].r)
                    + Math.Abs(a.Pixels[i].g - b.Pixels[i].g)
                    + Math.Abs(a.Pixels[i].b - b.Pixels[i].b)) / (3.0 * 255.0);
                all += d;
                bool inMask = hasMask && mask.Pixels[i].r >= 128;
                if (inMask) { inside += d; nIn++; }
                else { outside += d; nOut++; }
            }
            return new DiffResult
            {
                MeanAbs = (float)(all / n),
                OutsideMask = nOut == 0 ? 0f : (float)(outside / nOut),
                InsideMask = nIn == 0 ? 0f : (float)(inside / nIn),
                Width = a.Width,
                Height = a.Height,
                InsideCount = nIn,
                OutsideCount = nOut
            };
        }
    }

    public static class ImageCrops
    {
        /// <summary>Regions are top-left origin, the same pixel coordinates as the art bibles. <c>x,y,w,h;...</c></summary>
        public static List<RectInt> ParseRegions(string text)
        {
            var list = new List<RectInt>();
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("regions is empty");
            string[] parts = text.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) continue;
                string[] n = part.Split(',');
                if (n.Length != 4) throw new FormatException("region needs x,y,w,h: " + part);
                int x, y, w, h;
                if (!int.TryParse(n[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    || !int.TryParse(n[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                    || !int.TryParse(n[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out w)
                    || !int.TryParse(n[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out h))
                    throw new FormatException("region is not integers: " + part);
                if (w <= 0 || h <= 0) throw new FormatException("region size must be positive: " + part);
                list.Add(new RectInt(x, y, w, h));
            }
            if (list.Count == 0) throw new FormatException("regions is empty");
            return list;
        }

        public static RgbaImage Stack(RgbaImage a, RgbaImage b, IList<RectInt> regionsTopLeft)
        {
            if (a.Pixels == null || b.Pixels == null) throw new ArgumentException("crop image is missing");
            if (a.Width != b.Width || a.Height != b.Height)
                throw new InvalidOperationException("crop inputs differ in size");
            if (regionsTopLeft == null || regionsTopLeft.Count == 0) throw new ArgumentException("no regions");
            int outW = 0, outH = 0;
            for (int i = 0; i < regionsTopLeft.Count; i++)
            {
                RectInt region = regionsTopLeft[i];
                if (region.x < 0 || region.y < 0 || region.xMax > a.Width || region.yMax > a.Height)
                    throw new InvalidOperationException("region outside the image: " + region.x + "," + region.y + "," + region.width + "," + region.height);
                if (region.width * 2 > outW) outW = region.width * 2;
                outH += region.height;
            }
            var img = RgbaImage.Alloc(outW, outH, new Color32(0, 0, 0, 255));
            int y0 = 0;
            for (int i = 0; i < regionsTopLeft.Count; i++)
            {
                RectInt region = regionsTopLeft[i];
                for (int row = 0; row < region.height; row++)
                {
                    for (int col = 0; col < region.width; col++)
                    {
                        img.SetTop(col, y0 + row, a.GetTop(region.x + col, region.y + row));
                        img.SetTop(region.width + col, y0 + row, b.GetTop(region.x + col, region.y + row));
                    }
                }
                y0 += region.height;
            }
            return img;
        }
    }

    public static class SideBySide
    {
        public const int StripHeight = 44;
        public static readonly Color32 Paper = new Color32(236, 232, 220, 255);
        public static readonly Color32 Ink = new Color32(28, 26, 22, 255);
        public static readonly Color32 Pad = new Color32(20, 20, 20, 255);

        public static RgbaImage Compose(RgbaImage reference, RgbaImage render, string referenceName, string renderName, string framing, string state)
        {
            if (reference.Pixels == null || render.Pixels == null) throw new ArgumentException("side-by-side image is missing");
            int picH = Math.Max(reference.Height, render.Height);
            int width = reference.Width + render.Width;
            int height = picH + StripHeight;
            var img = RgbaImage.Alloc(width, height, Pad);
            for (int y = 0; y < StripHeight; y++)
                for (int x = 0; x < width; x++)
                    img.SetTop(x, y, Paper);
            string line1 = "REFERENCE " + (referenceName ?? "") + " | RENDER " + (renderName ?? "");
            string line2 = "Unity URP, batchmode, " + (string.IsNullOrEmpty(framing) ? "none" : framing) + ", " + (string.IsNullOrEmpty(state) ? "none" : state);
            BitmapFont.Draw(img.Pixels, width, height, 8, 6, line1, Ink);
            BitmapFont.Draw(img.Pixels, width, height, 8, 24, line2, Ink);
            Blit(img, 0, StripHeight, reference);
            Blit(img, reference.Width, StripHeight, render);
            return img;
        }

        static void Blit(RgbaImage dest, int dx, int dyTop, RgbaImage src)
        {
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                    dest.SetTop(dx + x, dyTop + y, src.GetTop(x, y));
        }
    }
}
