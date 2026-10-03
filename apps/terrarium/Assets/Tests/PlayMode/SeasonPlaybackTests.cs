using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    /// <summary>
    /// Week 1 keeps the locked mint. Week 3 and week 6 only add warmth, a star fern, and tiny flowers.
    /// </summary>
    public class SeasonPlaybackTests
    {
        [UnityTearDown]
        public IEnumerator Restore()
        {
            JarRitualController controller = Object.FindAnyObjectByType<JarRitualController>();
            if (controller != null && controller.View != null)
            {
                controller.View.lifetimeFronds = 0;
                controller.View.Apply();
            }
            yield return null;
            RitualHarness.ReleaseOverrides();
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Main");
            if (scene.IsValid() && scene.isLoaded)
                yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator Season_WeekOneIsLocked_LaterWeeksOnlyAdd()
        {
            yield return RitualHarness.OpenMain();
            JarView view = RitualHarness.Controller().View;
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.glassMat);
            Assert.IsNotNull(view.flowerMesh, "the authored flower mesh is missing");

            ApplyLifetime(view, Season.FrondsAtEndOfWeek(1));
            Assert.AreEqual(1, Season.Week(view.lifetimeFronds));
            Assert.AreEqual(0f, Season.Warmth(view.lifetimeFronds));
            Color inner1 = view.glassMat.GetColor("_Inner");
            Color moss1 = view.mossMat.GetColor("_Emission");
            Assert.AreEqual(0.18f, inner1.r, 0.001f);
            Assert.AreEqual(0.40f, inner1.g, 0.001f);
            Assert.AreEqual(0.26f, inner1.b, 0.001f);
            Assert.AreEqual(0.13f, moss1.r, 0.001f);
            Assert.AreEqual(0.40f, moss1.g, 0.001f);
            Assert.AreEqual(0.11f, moss1.b, 0.001f);
            Assert.AreEqual(0, CountActive(view, "SeasonSprig"));
            Assert.AreEqual(0, CountActive(view, "SeasonBloom"));

            int at3 = Season.FrondsAtEndOfWeek(3);
            ApplyLifetime(view, at3);
            Color inner3 = view.glassMat.GetColor("_Inner");
            Assert.AreEqual(3, Season.Week(at3));
            Assert.Greater(inner3.r, inner1.r);
            Assert.Less(inner3.b, inner1.b);
            Assert.AreEqual(Season.Sprigs(at3), CountActive(view, "SeasonSprig"));
            Assert.AreEqual(Season.TinyFlowers(at3), CountActive(view, "SeasonBloom"));
            Assert.AreEqual(1, CountActive(view, "SeasonSprig"));
            Assert.AreEqual(1, CountActive(view, "SeasonBloom"));

            int at6 = Season.FrondsAtEndOfWeek(6);
            ApplyLifetime(view, at6);
            Color inner6 = view.glassMat.GetColor("_Inner");
            Color moss6 = view.mossMat.GetColor("_Emission");
            Assert.AreEqual(1f, Season.Warmth(at6));
            Assert.Greater(inner6.r, inner3.r);
            Assert.Greater(moss6.r, moss1.r);
            Assert.AreEqual(2, CountActive(view, "SeasonSprig"));
            Assert.AreEqual(4, CountActive(view, "SeasonBloom"));
            Assert.AreEqual(Season.Sprigs(at6), CountActive(view, "SeasonSprig"));
            Assert.AreEqual(Season.TinyFlowers(at6), CountActive(view, "SeasonBloom"));

            ApplyLifetime(view, Season.FrondsAtEndOfWeek(1));
            Color innerBack = view.glassMat.GetColor("_Inner");
            Assert.AreEqual(inner1.r, innerBack.r, 0.0001f);
            Assert.AreEqual(inner1.g, innerBack.g, 0.0001f);
            Assert.AreEqual(inner1.b, innerBack.b, 0.0001f);
            Assert.AreEqual(0, CountActive(view, "SeasonSprig"));
            Assert.AreEqual(0, CountActive(view, "SeasonBloom"));
        }

        static void ApplyLifetime(JarView view, int fronds)
        {
            var state = new Dictionary<string, string>
            {
                { "breath", "0.5" },
                { "uncoil", "0.3" },
                { "fog", "0.45" },
                { "time", "3" },
                { "lifetime", fronds.ToString(CultureInfo.InvariantCulture) }
            };
            view.ApplyCaptureState(state);
        }

        static int CountActive(JarView view, string prefix)
        {
            int count = 0;
            Transform[] all = view.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal)) count++;
            }
            return count;
        }
    }
}
