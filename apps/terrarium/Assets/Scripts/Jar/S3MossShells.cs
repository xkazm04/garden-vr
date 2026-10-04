using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Spike S3 (T-TER-042). Shell moss is one mesh that holds the mound N times. Copy k carries its
    /// shell fraction in UV1.x, and <c>Fidelity/MossShell</c> offsets it along the normal and discards
    /// texels under that strand height. One mesh is one draw at any shell count.
    /// </summary>
    public static class S3MossShells
    {
        /// <summary>PC preset. About 1k triangles a shell on the decimated mound.</summary>
        public const int PcShells = 16;
        /// <summary>Quest fallback preset. Half the triangles and half the overdraw of the PC stack.</summary>
        public const int QuestShells = 8;
        /// <summary>Top shell fraction. The tallest strands in the height field end just under 1.</summary>
        public const float TopShell = 0.98f;

        /// <summary>The shell fraction of copy <paramref name="index"/> (0 based) of <paramref name="count"/>.</summary>
        public static float ShellT(int index, int count)
        {
            return TopShell * (index + 1) / count;
        }

        public static Mesh BuildStack(Mesh source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count < 1 || count > 64) throw new ArgumentOutOfRangeException(nameof(count), "shell count must be 1 to 64");
            if (!source.isReadable) throw new InvalidOperationException(source.name + " is not readable");

            Vector3[] verts = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uv = source.uv;
            // UV1.x of the source is the per-vertex fur scale (Blender layer FurScale). Missing means 1.
            Vector2[] scale = source.uv2;
            bool hasScale = scale != null && scale.Length == source.vertexCount;
            // Vertex colour carries the cavity term (0 in a hollow, 1 on a crest).
            Color[] cavity = source.colors;
            bool hasCavity = cavity != null && cavity.Length == source.vertexCount;
            int[] tris = source.triangles;
            int n = verts.Length;
            long total = (long)n * count;

            var mesh = new Mesh { name = "MossShellStack" + count };
            if (total > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var v = new Vector3[total];
            var nn = new Vector3[total];
            var u0 = new Vector2[total];
            var u1 = new Vector2[total];
            var cc = new Color[total];
            var t = new int[tris.Length * count];
            for (int k = 0; k < count; k++)
            {
                float shell = ShellT(k, count);
                int vo = k * n;
                Array.Copy(verts, 0, v, vo, n);
                Array.Copy(normals, 0, nn, vo, n);
                Array.Copy(uv, 0, u0, vo, n);
                for (int i = 0; i < n; i++)
                {
                    u1[vo + i] = new Vector2(shell, hasScale ? scale[i].x : 1f);
                    cc[vo + i] = hasCavity ? cavity[i] : Color.white;
                }
                int to = k * tris.Length;
                for (int i = 0; i < tris.Length; i++) t[to + i] = tris[i] + vo;
            }
            mesh.vertices = v;
            mesh.normals = nn;
            mesh.uv = u0;
            mesh.uv2 = u1;
            mesh.colors = cc;
            mesh.triangles = t;
            Bounds b = source.bounds;
            b.Expand(0.008f);
            mesh.bounds = b;
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        public static int Triangles(Mesh stack)
        {
            return stack == null ? 0 : (int)(stack.GetIndexCount(0) / 3);
        }
    }
}
