using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    /// <summary>T-TER-047: variant sprint stacks the spike winners, names itself, keeps the lost spikes off, and resets.</summary>
    public class SprintCompositeVariantTests
    {
        static Dictionary<string, string> State(string variant, params string[] extra)
        {
            var map = new Dictionary<string, string> { { "breath", "0.5" }, { "uncoil", "0.3" }, { "fog", "0.45" }, { "time", "3" } };
            if (variant != null) map["variant"] = variant;
            for (int i = 0; i + 1 < extra.Length; i += 2) map[extra[i]] = extra[i + 1];
            return map;
        }

        [Test]
        public void CaptureState_Sprint_StacksTheWinners_AndResets()
        {
            var go = new GameObject("jar-sprint");
            var view = go.AddComponent<JarView>();
            try
            {
                Assert.IsFalse(view.SprintComposite);
                view.ApplyCaptureState(State("sprint"));
                Assert.IsTrue(view.SprintComposite);
                Assert.AreEqual("sprint", view.Variant);
                Assert.AreEqual(3, view.ThickGlassMode, "S2 B2 is the PC best path");
                Assert.IsTrue(view.StructuredGlass, "S1 pane");
                Assert.IsTrue(view.Atmosphere, "S5, with the restored inner glow");
                Assert.IsTrue(view.SilhouettePass, "S6");
                Assert.IsTrue(view.MossInTheJar, "S3");
                Assert.IsTrue(view.FuzzyFiddle, "S4 fiddlehead");
                Assert.IsFalse(view.BacklitFronds, "the s4f frond regrade lost and stays out");

                // Even when s4 or s4f is named, the composite keeps the locked fronds.
                view.ApplyCaptureState(State("sprint+s4"));
                Assert.IsFalse(view.BacklitFronds);
                Assert.IsTrue(view.FuzzyFiddle);

                // The Quest path names its glass tier and takes the cheaper shells.
                view.ApplyCaptureState(State("sprint+s2b1", "shells", "8", "fuzz", "0"));
                Assert.AreEqual("sprint+s2b1", view.Variant);
                Assert.AreEqual(2, view.ThickGlassMode);
                Assert.AreEqual(8, view.ShellCount);
                Assert.AreEqual(0, view.FuzzShells);

                view.ApplyCaptureState(State("s2b2+s5+s6"));
                Assert.IsFalse(view.SprintComposite, "the T-TER-049 string is not the composite");
                Assert.AreEqual("s2b2+s5+s6", view.Variant);

                view.ApplyCaptureState(State("sprint", "sprintclear", "0"));
                Assert.IsTrue(view.SprintComposite);
                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.SprintComposite);
                Assert.IsFalse(view.SilhouettePass);
                Assert.IsFalse(view.Atmosphere);
                Assert.AreEqual("a", view.Variant);
                Assert.Throws<System.FormatException>(() => view.ApplyCaptureState(State("sprint+s7")));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>T-TER-034: the player's L key flips the look and leaves the rest of the jar's state alone.</summary>
        [Test]
        public void SetLook_FlipsTheLook_AndKeepsTheState()
        {
            var go = new GameObject("jar-setlook");
            var view = go.AddComponent<JarView>();
            try
            {
                view.ApplyCaptureState(State(null));
                view.breath = 0.5f;
                view.fog = 0.45f;
                view.SetLook("sprint");
                Assert.IsTrue(view.SprintLook);
                Assert.IsTrue(view.SprintComposite);
                Assert.AreEqual("sprint", view.Variant);
                Assert.AreEqual(0.5f, view.breath, 1e-6f);
                Assert.AreEqual(0.45f, view.fog, 1e-6f);
                view.SetLook("a");
                Assert.IsFalse(view.SprintLook);
                Assert.AreEqual("a", view.Variant);
                Assert.IsFalse(view.SilhouettePass);
                Assert.IsFalse(view.Atmosphere);
                Assert.AreEqual(0.5f, view.breath, 1e-6f);
                Assert.Throws<System.FormatException>(() => view.SetLook("sprint+s7"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
