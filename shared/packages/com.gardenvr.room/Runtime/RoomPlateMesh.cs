using UnityEngine;

namespace GardenVR.Room
{
    /// <summary>Camera-centred meshes for the PC room stand-in. Built around the local origin.</summary>
    public static class RoomPlateMesh
    {
        public const float Radius = 4f;

        /// <summary>
        /// Spherical patch looking down local +Z. The eye sits at the origin, so a 90 deg view still lands on the card.
        /// </summary>
        public static Mesh Curve(float radius, float yawHalfDegrees, float pitchHalfDegrees, int yawSegments, int pitchSegments)
        {
            int yawCount = yawSegments + 1;
            int pitchCount = pitchSegments + 1;
            var vertices = new Vector3[yawCount * pitchCount];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[yawSegments * pitchSegments * 6];

            for (int y = 0; y < pitchCount; y++)
            {
                float v = y / (float)pitchSegments;
                float pitch = Mathf.Lerp(-pitchHalfDegrees, pitchHalfDegrees, v) * Mathf.Deg2Rad;
                float cp = Mathf.Cos(pitch);
                float sp = Mathf.Sin(pitch);
                for (int x = 0; x < yawCount; x++)
                {
                    float u = x / (float)yawSegments;
                    float yaw = Mathf.Lerp(-yawHalfDegrees, yawHalfDegrees, u) * Mathf.Deg2Rad;
                    int index = y * yawCount + x;
                    vertices[index] = new Vector3(Mathf.Sin(yaw) * cp, sp, Mathf.Cos(yaw) * cp) * radius;
                    uv[index] = new Vector2(u, v);
                }
            }

            int t = 0;
            for (int y = 0; y < pitchSegments; y++)
            {
                for (int x = 0; x < yawSegments; x++)
                {
                    int i = y * yawCount + x;
                    triangles[t++] = i;
                    triangles[t++] = i + yawCount;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + yawCount;
                    triangles[t++] = i + yawCount + 1;
                }
            }

            var mesh = new Mesh { name = "RoomPlateCurve" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Unit quad in the XY plane, white vertex colours, facing +Z. Fidelity/Card multiplies by the vertex colour.</summary>
        public static Mesh TraceQuad()
        {
            var mesh = new Mesh { name = "DeskTraceQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
