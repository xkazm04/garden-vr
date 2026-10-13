using UnityEngine;

namespace GardenVR.Room
{
    /// <summary>Meshes for the PC room stand-in. Built around the local origin.</summary>
    public static class RoomPlateMesh
    {
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
