using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class StructuredGlassVariantTests
    {
        [Test]
        public void CaptureState_S1_IsOptIn_AndDefaultStaysA()
        {
            var go = new GameObject("jar-variant");
            var view = go.AddComponent<JarView>();
            try
            {
                view.ApplyCaptureState(new Dictionary<string, string>
                {
                    { "breath", "0.5" },
                    { "uncoil", "0.3" },
                    { "fog", "0.45" },
                    { "time", "3" },
                    { "variant", "s1" }
                });
                Assert.IsTrue(view.StructuredGlass);
                Assert.AreEqual("s1", view.Variant);

                view.ApplyCaptureState(new Dictionary<string, string>
                {
                    { "breath", "0.5" },
                    { "uncoil", "0.3" },
                    { "fog", "0.45" },
                    { "time", "3" }
                });
                Assert.IsFalse(view.StructuredGlass);
                Assert.AreEqual("a", view.Variant);

                Assert.Throws<System.FormatException>(() => view.ApplyCaptureState(new Dictionary<string, string>
                {
                    { "variant", "glass" }
                }));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
