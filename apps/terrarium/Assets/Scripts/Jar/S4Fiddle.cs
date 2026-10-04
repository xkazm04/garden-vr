using System;
using UnityEngine;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Spike S4 (T-TER-043). The fuzzy fiddlehead is one mesh that holds the tube once plus N fuzz shells. Copy 0 is
    /// the opaque tube. Copy k (1..N) carries its shell fraction k / N in UV1.x, and <c>Fidelity/FiddleFuzz</c>
    /// pushes it out along the normal and keeps the texels under the hair height. One mesh is one draw at any N.
    /// </summary>
    public static class S4Fiddle
    {
        /// <summary>PC preset. Two shells, as the dossier asks.</summary>
        public const int PcShells = 2;
        /// <summary>Quest fallback. The bare tube with the core gradient, no shell fuzz. One third of the PC triangles.</summary>
        public const int QuestShells = 0;
        public const int MaxShells = 4;
        /// <summary>Top shell fraction. The hair height map has few texels above 0.8, so a shell at 1 would be empty.</summary>
        public const float TopShell = 0.8f;

        /// <summary>The shell fraction of copy <paramref name="copy"/> (0 is the tube) of a stack with <paramref name="shells"/> shells.</summary>
        public static float ShellT(int copy, int shells)
        {
            return shells <= 0 ? 0f : TopShell * copy / shells;
        }

        public static Mesh BuildStack(Mesh source, int shells)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (shells < 0 || shells > MaxShells) throw new ArgumentOutOfRangeException(nameof(shells), "fuzz shells must be 0 to " + MaxShells);
            if (!source.isReadable) throw new InvalidOperationException(source.name + " is not readable");

            Vector3[] verts = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uv = source.uv;
            Color[] colours = source.colors;
            bool hasColours = colours != null && colours.Length == source.vertexCount;
            int[] tris = source.triangles;
            int n = verts.Length;
            int copies = shells + 1;

            var mesh = new Mesh { name = source.name + "Fuzz" + shells };
            var v = new Vector3[n * copies];
            var nn = new Vector3[n * copies];
            var u0 = new Vector2[n * copies];
            var u1 = new Vector2[n * copies];
            var cc = new Color[n * copies];
            var t = new int[tris.Length * copies];
            for (int k = 0; k < copies; k++)
            {
                float shell = ShellT(k, shells);
                int vo = k * n;
                Array.Copy(verts, 0, v, vo, n);
                Array.Copy(normals, 0, nn, vo, n);
                Array.Copy(uv, 0, u0, vo, n);
                for (int i = 0; i < n; i++)
                {
                    u1[vo + i] = new Vector2(shell, 1f);
                    cc[vo + i] = hasColours ? colours[i] : Color.black;
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
