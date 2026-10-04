using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class ThickGlassVariantTests
    {
        static Dictionary<string, string> State(string variant, params string[] extra)
        {
            var map = new Dictionary<string, string> { { "breath", "0.5" }, { "uncoil", "0.3" }, { "fog", "0.45" }, { "time", "3" } };
            if (variant != null) map["variant"] = variant;
            for (int i = 0; i + 1 < extra.Length; i += 2) map[extra[i]] = extra[i + 1];
            return map;
        }

        [Test]
        public void CaptureState_S2_IsOptIn_ComposesAndResets()
        {
            var go = new GameObject("jar-s2");
            var view = go.AddComponent<JarView>();
            try
            {
                Assert.AreEqual(0, view.ThickGlassMode);
                view.ApplyCaptureState(State("s2b0"));
                Assert.AreEqual(1, view.ThickGlassMode);
                Assert.AreEqual("s2b0", view.Variant);
                Assert.IsTrue(view.StructuredGlass, "every S2 look uses the structured pane");
                view.ApplyCaptureState(State("s2b1"));
                Assert.AreEqual(2, view.ThickGlassMode);
                view.ApplyCaptureState(State("s2b2+s3"));
                Assert.AreEqual(3, view.ThickGlassMode);
                Assert.AreEqual("s3+s2b2", view.Variant, "parts print in a fixed order: s1, s3, s2, s4");
                view.ApplyCaptureState(State("s1+s2b1"));
                Assert.AreEqual("s1+s2b1", view.Variant);

                view.ApplyCaptureState(State(null));
                Assert.AreEqual(0, view.ThickGlassMode);
                Assert.IsFalse(view.StructuredGlass);
                Assert.AreEqual("a", view.Variant);

                view.ApplyCaptureState(State("s2b2", "s2off", "14", "s2disp", "0.1", "s2mix", "0.5", "s2proxy", "30"));
                Assert.AreEqual(3, view.ThickGlassMode);
                Assert.Throws<System.FormatException>(() => view.ApplyCaptureState(State("s2b3")));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
