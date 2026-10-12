using System.Collections.Generic;
using System.Reflection;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class BreakInputContractTests
    {
        static void Lifecycle(KeyboardMouseIntentSource provider, string method, params object[] args) => typeof(KeyboardMouseIntentSource).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(provider, args);
        static readonly Ray Ray = new Ray(Vector3.zero, Vector3.forward);
        static void Tick(KbmIntentMapper mapper, bool held, float dt = .1f, bool middle = false)
        {
            mapper.Tick(new RawKbm { P = held && !middle, MiddleMouse = held && middle, Dt = dt }, () => Ray);
        }

        [Test]
        public void PcPalm_ContractDistinguishesReleaseFromContinuedHold()
        {
            var released = new KbmIntentMapper(); var held = new KbmIntentMapper();
            var a = new List<HandIntentKind>(); var b = new List<HandIntentKind>();
            released.Intent += i => a.Add(i.Kind); held.Intent += i => b.Add(i.Kind);
            int releases = 0; released.PalmReleased += () => releases++;
            for (int i = 0; i < 240; i++)
            {
                Tick(released, i < 42, 1f / 60); Tick(held, true, 1f / 60);
            }
            CollectionAssert.AreEqual(a, b, "legacy intent stream stays compatible");
            Assert.AreEqual(1, b.FindAll(k => k == HandIntentKind.PalmOpen).Count);
            Assert.IsFalse(released.PalmHeld); Assert.IsTrue(held.PalmHeld);
            Assert.AreEqual(1, releases);
        }

        [TestCase(false), TestCase(true)]
        public void PcPalm_PressHoldRelease_OneCommit(bool middle)
        {
            var mapper = new KbmIntentMapper(); int commits = 0, releases = 0;
            mapper.Intent += i => { if (i.Kind == HandIntentKind.PalmOpen) commits++; };
            mapper.PalmReleased += () => { Assert.IsFalse(mapper.PalmHeld); releases++; };
            Tick(mapper, true, .1f, middle);
            Assert.IsTrue(mapper.PalmHeld); Assert.AreEqual(0, commits);
            for (int i = 0; i < 4; i++) Tick(mapper, true, .1f, middle);
            Assert.AreEqual(0, commits, "no early commit before 0.6 s");
            Tick(mapper, true, .1f, middle); Assert.AreEqual(1, commits);
            for (int i = 0; i < 40; i++) Tick(mapper, true, .1f, middle);
            Assert.AreEqual(1, commits);
            Tick(mapper, false); Tick(mapper, false);
            Assert.IsFalse(mapper.PalmHeld); Assert.AreEqual(1, releases);
            Tick(mapper, true, .6f, middle); Assert.AreEqual(2, commits);
        }

        [Test]
        public void PcPalm_TrackingLoss_RequiresFreshGesture()
        {
            var mapper = new KbmIntentMapper(); int releases = 0;
            mapper.PalmReleased += () => releases++;
            Tick(mapper, true); mapper.CancelPalmHold(); mapper.CancelPalmHold();
            Assert.IsFalse(mapper.PalmHeld); Assert.AreEqual(1, releases);
            Tick(mapper, true, 4); Assert.IsFalse(mapper.PalmHeld);
            Tick(mapper, false); Tick(mapper, true); Assert.IsTrue(mapper.PalmHeld);
        }

        [Test]
        public void PcPalm_ProviderFocusLossAndDisable_ReleaseBeforePause()
        {
            var go = new GameObject("palm provider test");
            try
            {
                var provider = go.AddComponent<KeyboardMouseIntentSource>();
                Lifecycle(provider, "OnEnable"); // EditMode does not run MonoBehaviour lifecycle automatically.
                var mapper = (KbmIntentMapper)typeof(KeyboardMouseIntentSource)
                    .GetField("_mapper", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(provider);
                IPalmStateSource palm = provider;
                int releases = 0, pauses = 0;
                palm.PalmReleased += () => releases++;
                provider.SystemPause += () => { Assert.IsFalse(palm.PalmHeld); pauses++; };
                Tick(mapper, true, .7f); Assert.IsTrue(palm.PalmHeld);
                Lifecycle(provider, "OnApplicationFocus", false);
                Assert.IsFalse(palm.PalmHeld); Assert.AreEqual(1, releases); Assert.AreEqual(1, pauses);
                Lifecycle(provider, "OnApplicationFocus", true);
                Tick(mapper, true); Assert.IsFalse(palm.PalmHeld);
                Tick(mapper, false); Tick(mapper, true);
                Lifecycle(provider, "OnDisable");
                Assert.IsFalse(palm.PalmHeld); Assert.AreEqual(2, releases);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
