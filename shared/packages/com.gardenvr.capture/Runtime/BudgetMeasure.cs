using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Capture
{
    public sealed class TextureBudget
    {
        public string Name;
        public int Width;
        public int Height;
    }

    public sealed class BudgetReport
    {
        public int Renderers;
        public int DrawsEst;
        public int ShadowCasters;
        public int TransparentDraws;
        public long Tris;
        public List<TextureBudget> Textures = new List<TextureBudget>();
        public double TexMemAstc6x6MB;
        public double TransparentMeanLayers;
        public double TransparentCoverage;
        public string Framing;
        public string Target;
        public string OverdrawPng;
    }

    public static class BudgetMeasure
    {
        public static bool IsTransparent(Material material)
        {
            if (material == null) return false;
            if (material.renderQueue >= 2500) return true;
            string name = material.shader != null ? material.shader.name : "";
            return name.IndexOf("Glass", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Fidelity/Glass and Fidelity/Toon (unless _Outline is 0) draw twice. Everything else draws once.</summary>
        public static int PassCount(Material material)
        {
            if (material == null || material.shader == null) return 0;
            string name = material.shader.name;
            if (name == "Fidelity/Glass") return 2;
            if (name == "Fidelity/Toon")
            {
                if (material.HasProperty("_Outline") && material.GetFloat("_Outline") <= 0f) return 1;
                return 2;
            }
            return 1;
        }

        public static int MeshTriangles(Renderer renderer)
        {
            if (renderer == null) return 0;
            var particles = renderer as ParticleSystemRenderer;
            if (particles != null)
            {
                ParticleSystem system = particles.GetComponent<ParticleSystem>();
                return system != null ? system.particleCount * 2 : 0;
            }
            Mesh mesh = null;
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) mesh = skinned.sharedMesh;
            else
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh == null) return 0;
            long tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                tris += (long)mesh.GetIndexCount(s) / 3L;
            return (int)tris;
        }

        /// <summary>ASTC 6x6 of the texture's current size, plus a 4/3 factor for the mip chain. Bytes, not MiB.</summary>
        public static double Astc6x6Bytes(int width, int height)
        {
            if (width <= 0 || height <= 0) return 0;
            return Math.Ceiling(width / 6.0) * Math.Ceiling(height / 6.0) * 16.0 * (4.0 / 3.0);
        }

        public static BudgetReport Count(IList<Renderer> renderers)
        {
            var report = new BudgetReport();
            var seen = new HashSet<EntityId>();
            var textures = new List<Texture2D>();
            int draws = 0;
            int transparent = 0;
            int casters = 0;
            long tris = 0;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    report.Renderers++;
                    int meshTris = MeshTriangles(renderer);
                    Material[] materials = renderer.sharedMaterials;
                    if (materials != null)
                    {
                        for (int m = 0; m < materials.Length; m++)
                        {
                            Material material = materials[m];
                            if (material == null) continue;
                            int passes = PassCount(material);
                            draws += passes;
                            tris += (long)meshTris * passes;
                            if (IsTransparent(material)) transparent += passes;
                            CollectTextures(material, seen, textures);
                        }
                    }
                    if (renderer.shadowCastingMode != ShadowCastingMode.Off) casters++;
                }
            }
            report.DrawsEst = draws + casters;
            report.ShadowCasters = casters;
            report.TransparentDraws = transparent;
            report.Tris = tris;
            double bytes = 0;
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D tex = textures[i];
                bytes += Astc6x6Bytes(tex.width, tex.height);
                report.Textures.Add(new TextureBudget { Name = tex.name, Width = tex.width, Height = tex.height });
            }
            report.Textures.Sort(CompareTextures);
            report.TexMemAstc6x6MB = bytes / 1048576.0;
            return report;
        }

        /// <summary>
        /// Swap transparent materials for Fidelity/Overdraw, subtract an opaque-only render, and
        /// score layers inside the target's screen rectangle. Writes a full-frame overdraw PNG.
        /// </summary>
        public static void RenderOverdraw(Camera cam, IList<Renderer> renderers, IList<Renderer> targetRenderers, int width, int height, string pngPath, BudgetReport report)
        {
            if (cam == null) throw new ArgumentNullException("cam");
            if (width < 1 || height < 1) throw new ArgumentException("overdraw size");
            Shader shader = Shader.Find("Fidelity/Overdraw");
            if (shader == null) throw new InvalidOperationException("shader not found: Fidelity/Overdraw");
            var overdraw = new Material(shader);
            var savedMaterials = new Dictionary<Renderer, Material[]>();
            var savedEnabled = new Dictionary<Renderer, bool>();
            Color previousColor = cam.backgroundColor;
            CameraClearFlags previousClear = cam.clearFlags;
            RenderTexture previousTarget = cam.targetTexture;
            float previousAspect = cam.aspect;
            RenderTexture rt = null;
            Texture2D withTransparent = null;
            Texture2D opaqueOnly = null;
            try
            {
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        Renderer renderer = renderers[i];
                        if (renderer == null) continue;
                        savedMaterials[renderer] = renderer.sharedMaterials;
                        savedEnabled[renderer] = renderer.enabled;
                        if (!AnyTransparent(renderer.sharedMaterials)) continue;
                        Material[] replacement = new Material[renderer.sharedMaterials.Length];
                        for (int m = 0; m < replacement.Length; m++) replacement[m] = overdraw;
                        renderer.sharedMaterials = replacement;
                    }
                }
                cam.backgroundColor = Color.black;
                cam.clearFlags = CameraClearFlags.SolidColor;
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                rt.antiAliasing = 1;
                rt.Create();
                cam.targetTexture = rt;
                cam.aspect = width / (float)height;
                cam.Render();
                withTransparent = ReadFloat(rt);
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        Renderer renderer = renderers[i];
                        if (renderer == null || !savedMaterials.ContainsKey(renderer)) continue;
                        renderer.sharedMaterials = savedMaterials[renderer];
                        if (AnyTransparent(renderer.sharedMaterials)) renderer.enabled = false;
                    }
                }
                cam.Render();
                opaqueOnly = ReadFloat(rt);
                RectInt rect = TargetPixels(cam, targetRenderers, width, height);
                Score(withTransparent, opaqueOnly, rect, width, height, pngPath, report);
            }
            finally
            {
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        Renderer renderer = renderers[i];
                        if (renderer == null) continue;
                        Material[] materials;
                        if (savedMaterials.TryGetValue(renderer, out materials)) renderer.sharedMaterials = materials;
                        bool enabled;
                        if (savedEnabled.TryGetValue(renderer, out enabled)) renderer.enabled = enabled;
                    }
                }
                cam.backgroundColor = previousColor;
                cam.clearFlags = previousClear;
                cam.targetTexture = previousTarget;
                cam.aspect = previousAspect;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (withTransparent != null) UnityEngine.Object.DestroyImmediate(withTransparent);
                if (opaqueOnly != null) UnityEngine.Object.DestroyImmediate(opaqueOnly);
                UnityEngine.Object.DestroyImmediate(overdraw);
            }
        }

        static void Score(Texture2D withTransparent, Texture2D opaqueOnly, RectInt rect, int width, int height, string pngPath, BudgetReport report)
        {
            Color[] a = withTransparent.GetPixels();
            Color[] b = opaqueOnly.GetPixels();
            int covered = 0;
            int area = 0;
            double sum = 0;
            var png = new Color32[width * height];
            int x1 = rect.x + rect.width;
            int y1 = rect.y + rect.height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float layers = Mathf.Round(Mathf.Max(0f, a[i].r - b[i].r) * 32f);
                    if (x >= rect.x && x < x1 && y >= rect.y && y < y1)
                    {
                        area++;
                        if (layers > 0f) { covered++; sum += layers; }
                    }
                    float k = layers / 10f;
                    png[i] = new Color32(
                        (byte)(Mathf.Clamp01(k * 2f) * 255f),
                        (byte)(Mathf.Clamp01(k * 1.2f - 0.2f) * 255f),
                        (byte)(Mathf.Clamp01((1f - k * 3f) * (layers > 0f ? 0.6f : 0f)) * 255f),
                        255);
                }
            }
            report.TransparentCoverage = area == 0 ? 0 : (double)covered / area;
            report.TransparentMeanLayers = covered == 0 ? 0 : sum / covered;
            if (!string.IsNullOrEmpty(pngPath))
            {
                FrameGrab.WritePixels(pngPath, png, width, height);
                report.OverdrawPng = pngPath;
            }
        }

        static RectInt TargetPixels(Camera cam, IList<Renderer> targetRenderers, int width, int height)
        {
            if (targetRenderers == null || targetRenderers.Count == 0) return new RectInt(0, 0, width, height);
            bool any = false;
            float minX = width, minY = height, maxX = 0f, maxY = 0f;
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds bounds = renderer.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (c & 1) == 0 ? -1f : 1f,
                        (c & 2) == 0 ? -1f : 1f,
                        (c & 4) == 0 ? -1f : 1f));
                    Vector3 screen = cam.WorldToScreenPoint(corner);
                    if (screen.z < 0f) continue;
                    any = true;
                    if (screen.x < minX) minX = screen.x;
                    if (screen.y < minY) minY = screen.y;
                    if (screen.x > maxX) maxX = screen.x;
                    if (screen.y > maxY) maxY = screen.y;
                }
            }
            if (!any) return new RectInt(0, 0, width, height);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, height - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX), 0, width - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY), 0, height - 1);
            if (x1 < x0) { int swap = x0; x0 = x1; x1 = swap; }
            if (y1 < y0) { int swap = y0; y0 = y1; y1 = swap; }
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        static bool AnyTransparent(Material[] materials)
        {
            if (materials == null) return false;
            for (int i = 0; i < materials.Length; i++)
                if (IsTransparent(materials[i])) return true;
            return false;
        }

        static void CollectTextures(Material material, HashSet<EntityId> seen, List<Texture2D> textures)
        {
            int[] ids = material.GetTexturePropertyNameIDs();
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
            {
                var tex = material.GetTexture(ids[i]) as Texture2D;
                if (tex == null || tex == Texture2D.whiteTexture) continue;
                if (!seen.Add(tex.GetEntityId())) continue;
                textures.Add(tex);
            }
        }

        static int CompareTextures(TextureBudget a, TextureBudget b)
        {
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }

        static Texture2D ReadFloat(RenderTexture rt)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = previous;
            return tex;
        }
    }
}
