using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GardenVR.Capture
{
    public struct ImageStats
    {
        public float MeanLuma;
        public float BlackFrac;
        public float WhiteFrac;
        public float MagentaFrac;
        public int Width;
        public int Height;
        public string Sha256;
    }

    public static class ImageCheck
    {
        public const float BlackLuma = 0.02f;
        public const float WhiteLuma = 0.98f;

        public static float Luma(Color32 c)
        {
            return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        }

        public static bool IsMagenta(Color32 c)
        {
            return c.r >= 200 && c.b >= 200 && c.g <= 48 && (c.r - c.g) >= 100 && (c.b - c.g) >= 100;
        }

        public static ImageStats Analyze(Color32[] pixels, int width, int height, byte[] pngBytes)
        {
            if (pixels == null || pixels.Length != width * height || width < 1 || height < 1)
                throw new System.ArgumentException("pixel buffer does not match width and height");
            double lumaSum = 0;
            int black = 0, white = 0, magenta = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                float luma = Luma(pixels[i]);
                lumaSum += luma;
                if (luma < BlackLuma) black++;
                if (luma >= WhiteLuma) white++;
                if (IsMagenta(pixels[i])) magenta++;
            }
            float n = pixels.Length;
            return new ImageStats
            {
                MeanLuma = (float)(lumaSum / n),
                BlackFrac = black / n,
                WhiteFrac = white / n,
                MagentaFrac = magenta / n,
                Width = width,
                Height = height,
                Sha256 = pngBytes == null ? "" : Hash(pngBytes)
            };
        }

        public static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string ToJson(ImageStats stats)
        {
            return CaptureJson.Check(stats);
        }
    }
}
