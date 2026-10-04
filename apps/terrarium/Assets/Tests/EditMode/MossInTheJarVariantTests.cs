using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class MossInTheJarVariantTests
    {
        static Dictionary<string, string> State(string variant, string extra = null)
        {
            var state = new Dictionary<string, string>
            {
                { "breath", "0.5" }, { "uncoil", "0.3" }, { "fog", "0.45" }, { "time", "3" }
            };
            if (variant != null) state["variant"] = variant;
            if (extra != null) state[extra.Split('=')[0]] = extra.Split('=')[1];
            return state;
        }

        [Test]
        public void CaptureState_S3_IsOptIn_AndComposesWithS1()
        {
            var go = new GameObject("jar-s3");
            var view = go.AddComponent<JarView>();
            try
            {
                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.MossInTheJar);
                Assert.AreEqual("a", view.Variant);

                view.ApplyCaptureState(State("s3"));
                Assert.IsTrue(view.MossInTheJar);
                Assert.IsFalse(view.StructuredGlass);
                Assert.AreEqual("s3", view.Variant);
                Assert.AreEqual(S3MossShells.PcShells, view.ShellCount);

                view.ApplyCaptureState(State("s1+s3", "shells=8"));
                Assert.IsTrue(view.MossInTheJar);
                Assert.IsTrue(view.StructuredGlass);
                Assert.AreEqual("s1+s3", view.Variant);
                Assert.AreEqual(S3MossShells.QuestShells, view.ShellCount);

                // The next state without a variant is A again, and the shell preset resets.
                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.MossInTheJar);
                Assert.AreEqual(S3MossShells.PcShells, view.ShellCount);

                Assert.Throws<System.FormatException>(() => view.ApplyCaptureState(State("s3+glass")));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ShellStack_HoldsEveryShellInOneMesh_WithFractionsUnderOne()
        {
            var source = new Mesh();
            source.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) };
            source.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            source.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            source.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            try
            {
                foreach (int count in new[] { S3MossShells.QuestShells, S3MossShells.PcShells })
                {
                    Mesh stack = S3MossShells.BuildStack(source, count);
                    try
                    {
                        Assert.AreEqual(4 * count, stack.vertexCount);
                        Assert.AreEqual(2 * count, S3MossShells.Triangles(stack));
                        Assert.AreEqual(1, stack.subMeshCount, "one mesh is one draw at any shell count");
                        var shell = new List<Vector2>();
                        stack.GetUVs(1, shell);
                        float top = 0f;
                        for (int i = 0; i < shell.Count; i++) top = Mathf.Max(top, shell[i].x);
                        Assert.Less(top, 1f);
                        Assert.Greater(top, 0.9f);
                    }
                    finally
                    {
                        Object.DestroyImmediate(stack);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}
