using UnityEngine;

namespace GardenVR.Capture
{
    /// <summary>CPU image. Pixels are bottom-left, row-major, matching Texture2D.GetPixels32.</summary>
    public struct RgbaImage
    {
        public int Width;
        public int Height;
        public Color32[] Pixels;

        public RgbaImage(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public static RgbaImage Alloc(int width, int height, Color32 fill)
        {
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
            return new RgbaImage(width, height, pixels);
        }

        public Color32 GetTop(int x, int yTop)
        {
            int y = Height - 1 - yTop;
            return Pixels[y * Width + x];
        }

        public void SetTop(int x, int yTop, Color32 color)
        {
            int y = Height - 1 - yTop;
            Pixels[y * Width + x] = color;
        }
    }
}
