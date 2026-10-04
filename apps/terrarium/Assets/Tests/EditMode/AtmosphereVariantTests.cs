using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    /// <summary>Spike S5 (T-TER-045): variant s5 is opt-in, composes with the glass variants, and resets on an empty state.</summary>
    public class AtmosphereVariantTests
    {
        static Dictionary<string, string> State(string variant, params string[] extra)
        {
            var map = new Dictionary<string, string> { { "breath", "0.5" }, { "uncoil", "0.3" }, { "fog", "0.45" }, { "time", "3" } };
            if (variant != null) map["variant"] = variant;
            for (int i = 0; i + 1 < extra.Length; i += 2) map[extra[i]] = extra[i + 1];
            return map;
        }

        [Test]
        public void CaptureState_S5_IsOptIn_ComposesAndResets()
        {
            var go = new GameObject("jar-s5");
            var view = go.AddComponent<JarView>();
            try
            {
                Assert.IsFalse(view.Atmosphere);
                view.ApplyCaptureState(State("s5"));
                Assert.IsTrue(view.Atmosphere);
                Assert.AreEqual("s5", view.Variant);
                Assert.IsTrue(view.StructuredGlass, "s5 rides on the structured pane (the haze lives in its front pass)");
                view.ApplyCaptureState(State("s2b2+s5"));
                Assert.AreEqual("s2b2+s5", view.Variant);
                Assert.AreEqual(3, view.ThickGlassMode);
                view.ApplyCaptureState(State("s2b1+s5"));
                Assert.AreEqual("s2b1+s5", view.Variant);

                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.Atmosphere);
                Assert.IsFalse(view.StructuredGlass);
                Assert.AreEqual("a", view.Variant);

                // The ablation knobs parse. An unknown token still throws.
                view.ApplyCaptureState(State("s5", "s5haze", "0", "s5steam", "0", "s5spore", "0", "s5desk", "0", "s5cork", "0", "s5sigma", "12"));
                Assert.IsTrue(view.Atmosphere);
                Assert.Throws<System.FormatException>(() => view.ApplyCaptureState(State("s7")));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaptureState_S6_ImpliesMossAndFiddle_ComposesAndResets()
        {
            var go = new GameObject("jar-s6");
            var view = go.AddComponent<JarView>();
            try
            {
                Assert.IsFalse(view.SilhouettePass);
                view.ApplyCaptureState(State("s2b2+s5+s6"));
                Assert.IsTrue(view.SilhouettePass);
                Assert.AreEqual("s2b2+s5+s6", view.Variant);
                Assert.IsTrue(view.MossInTheJar, "s6 brings the dense moss mound");
                Assert.IsTrue(view.FuzzyFiddle, "s6 brings the thick s4h fiddlehead");
                Assert.IsFalse(view.BacklitFronds, "s6 does not change the fronds");
                Assert.AreEqual(3, view.ThickGlassMode);
                view.ApplyCaptureState(State("s2b1+s5+s6"));
                Assert.AreEqual("s2b1+s5+s6", view.Variant);
                Assert.AreEqual(2, view.ThickGlassMode);
                // Alone, s6 still sits on the thick shell.
                view.ApplyCaptureState(State("s6"));
                Assert.AreEqual(1, view.ThickGlassMode);
                Assert.AreEqual("s2b0+s6", view.Variant);

                view.ApplyCaptureState(State("s2b2+s5"));
                Assert.IsFalse(view.SilhouettePass);
                Assert.IsFalse(view.MossInTheJar);
                Assert.IsFalse(view.FuzzyFiddle);
                view.ApplyCaptureState(State(null));
                Assert.IsFalse(view.SilhouettePass);
                Assert.AreEqual("a", view.Variant);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
