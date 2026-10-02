using System.IO;
using UnityEngine;

namespace GardenVR.Capture
{
    public static class FrameGrab
    {
        public static Texture2D RenderToTexture(Camera cam, int width, int height, int msaa)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = msaa < 1 ? 1 : msaa;
            rt.useMipMap = false;
            rt.Create();
            RenderTexture previousTarget = cam.targetTexture;
            float previousAspect = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = width / (float)height;
            cam.Render();
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = previous;
            cam.targetTexture = previousTarget;
            cam.aspect = previousAspect;
            Object.DestroyImmediate(rt);
            return tex;
        }

        public static byte[] EncodePixels(Color32[] pixels, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        public static void WritePixels(string path, Color32[] pixels, int width, int height)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, EncodePixels(pixels, width, height));
        }
    }

    public static class PngIO
    {
        /// <summary>Loads file bytes. The texture is linear so GetPixels32 is the stored PNG bytes.</summary>
        public static RgbaImage Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("png not found", path);
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!tex.LoadImage(bytes)) throw new System.InvalidOperationException("not a readable image: " + path);
                return new RgbaImage(tex.width, tex.height, tex.GetPixels32());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
