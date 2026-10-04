using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class BacklitFrondsVariantTests
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
        public void CaptureState_S4_IsOptIn_SplitsFrondsAndFiddle_AndComposes()
        {
            var go = new GameObject("jar-s4");
            var view = go.AddComponent<JarView>();
            try
            {
                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.BacklitFronds);
                Assert.IsFalse(view.FuzzyFiddle);
                Assert.AreEqual("a", view.Variant);

                view.ApplyCaptureState(State("s4"));
                Assert.IsTrue(view.BacklitFronds);
                Assert.IsTrue(view.FuzzyFiddle);
                Assert.AreEqual("s4", view.Variant);
                Assert.AreEqual(S4Fiddle.PcShells, view.FuzzShells);

                view.ApplyCaptureState(State("s4f"));
                Assert.IsTrue(view.BacklitFronds);
                Assert.IsFalse(view.FuzzyFiddle);
                Assert.AreEqual("s4f", view.Variant);

                view.ApplyCaptureState(State("s4h", "fuzz=0"));
                Assert.IsFalse(view.BacklitFronds);
                Assert.IsTrue(view.FuzzyFiddle);
                Assert.AreEqual("s4h", view.Variant);
                Assert.AreEqual(S4Fiddle.QuestShells, view.FuzzShells);

                // The other spikes keep their names, and s4 composes with them.
                view.ApplyCaptureState(State("s1+s3"));
                Assert.AreEqual("s1+s3", view.Variant);
                view.ApplyCaptureState(State("s1+s3+s4"));
                Assert.AreEqual("s1+s3+s4", view.Variant);

                // The next state without a variant is A again, and the shell preset resets.
                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.BacklitFronds);
                Assert.IsFalse(view.FuzzyFiddle);
                Assert.AreEqual(S4Fiddle.PcShells, view.FuzzShells);

                Assert.Throws<System.FormatException>(() => view.ApplyCaptureState(State("s4+glass")));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static Mesh Tube()
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.colors = new[] { Color.black, Color.black, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            return mesh;
        }

        [Test]
        public void FiddleStack_IsOneMesh_TubeFirst_ShellFractionsUpToTop_CoreWeightKept()
        {
            Mesh source = Tube();
            try
            {
                foreach (int shells in new[] { S4Fiddle.QuestShells, 1, S4Fiddle.PcShells })
                {
                    Mesh stack = S4Fiddle.BuildStack(source, shells);
                    try
                    {
                        int copies = shells + 1;
                        Assert.AreEqual(4 * copies, stack.vertexCount);
                        Assert.AreEqual(2 * copies, S4Fiddle.Triangles(stack));
                        Assert.AreEqual(1, stack.subMeshCount, "one mesh is one draw at any shell count");
                        var shell = new List<Vector2>();
                        stack.GetUVs(1, shell);
                        Assert.AreEqual(0f, shell[0].x, "copy 0 is the opaque tube");
                        float top = 0f;
                        for (int i = 0; i < shell.Count; i++) top = Mathf.Max(top, shell[i].x);
                        Assert.AreEqual(shells == 0 ? 0f : S4Fiddle.TopShell, top, 1e-5f);
                        Color[] colours = stack.colors;
                        for (int k = 0; k < copies; k++)
                        {
                            Assert.AreEqual(0f, colours[4 * k].r, 1e-5f, "core weight is copied to every shell");
                            Assert.AreEqual(1f, colours[4 * k + 3].r, 1e-5f);
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(stack);
                    }
                }
                Assert.Throws<System.ArgumentOutOfRangeException>(() => S4Fiddle.BuildStack(source, S4Fiddle.MaxShells + 1));
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}
