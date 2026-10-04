using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class KbmIntentMapperTests
    {
        readonly List<GameObject> _owned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            IntentTargetRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
            IntentTargetRegistry.Reset();
        }

        [Test]
        public void ClickVersusHold_SplitsAtPointThreeSeconds()
        {
            Sink click = Drive(new[]
            {
                Frame(0.29f, left: true),
                Frame(0.01f)
            });
            Assert.AreEqual(1, click.Count(HandIntentKind.Pinch), "a press under 0.30s is a click");
            Assert.AreEqual(0, click.Count(HandIntentKind.PinchHold));
            Assert.AreEqual(0, click.Count(HandIntentKind.Release));

            Sink hold = Drive(new[]
            {
                Frame(0.30f, left: true),
                Frame(0.01f)
            });
            Assert.AreEqual(0, hold.Count(HandIntentKind.Pinch), "a press of 0.30s is a hold, not a click");
            Assert.AreEqual(1, hold.Count(HandIntentKind.PinchHold));
            Assert.AreEqual(1, hold.Count(HandIntentKind.Release));
            Assert.AreEqual(0.30f, hold.Last(HandIntentKind.Release).Held, 0.0001f);
        }

        [Test]
        public void Release_CarriesTotalHoldSeconds()
        {
            Sink sink = Drive(new[]
            {
                Frame(0.5f, space: true),
                Frame(0.5f, space: true),
                Frame(0f)
            });
            Assert.AreEqual(0, sink.Count(HandIntentKind.Pinch));
            Assert.GreaterOrEqual(sink.Count(HandIntentKind.PinchHold), 2);
            HandIntent release = sink.Last(HandIntentKind.Release);
            Assert.AreEqual(1f, release.Held, 0.0001f, "Held is the whole press, not the time after 0.30s");
        }

        [Test]
        public void ShortSpaceTap_DoesNotSelect()
        {
            Sink sink = Drive(new[]
            {
                Frame(0.1f, space: true),
                Frame(0.01f)
            });
            Assert.AreEqual(0, sink.Count(HandIntentKind.Pinch));
            Assert.AreEqual(0, sink.Count(HandIntentKind.PinchHold));
            Assert.AreEqual(0, sink.Count(HandIntentKind.Release));
        }

        [Test]
        public void Enter_PinchesTheFocusedTarget()
        {
            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            mapper.FocusOrder = () => new[] { "seed.walk" };

            mapper.Tick(Frame(0.016f, enter: true), Forward);
            Assert.AreEqual(1, sink.Count(HandIntentKind.Pinch));
            Assert.IsNull(sink.Last(HandIntentKind.Pinch).TargetId);

            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, tab: true), Forward);
            Assert.AreEqual("seed.walk", mapper.LookTargetId);
            mapper.Tick(Frame(0.016f, enter: true), Forward);
            Assert.AreEqual("seed.walk", sink.Last(HandIntentKind.Pinch).TargetId);
            Assert.AreEqual(2, sink.Count(HandIntentKind.Pinch));
        }

        [Test]
        public void StrengthRamp_CrossesHysteresisInsidePointOneTwoSeconds()
        {
            var mapper = new KbmIntentMapper();
            var detector = new PinchDetector();
            mapper.Tick(Frame(0.09f, space: true), Forward);
            Assert.GreaterOrEqual(mapper.PinchStrength, 0.8f);
            Assert.IsTrue(mapper.IsTracked);
            Assert.AreEqual(PinchState.Held, detector.Update(0.016f, new PinchSample(mapper.PinchStrength, true)));

            mapper.Tick(Frame(0.12f), Forward);
            Assert.Less(mapper.PinchStrength, 0.5f);
            Assert.AreEqual(PinchState.Open, detector.Update(0.016f, new PinchSample(mapper.PinchStrength, true)));
        }

        [Test]
        public void ToggleMode_SpacePressLatchesUntilNextPress()
        {
            var mapper = new KbmIntentMapper { HoldMode = HoldMode.Toggle };
            var sink = new Sink(mapper);
            mapper.Tick(Frame(0.5f, space: true), Forward);
            Assert.IsTrue(mapper.IsPinching);
            Assert.AreEqual(1, sink.Count(HandIntentKind.PinchHold));

            mapper.Tick(Frame(0.5f), Forward);
            Assert.IsTrue(mapper.IsPinching, "releasing the key does not end a latched hold");
            Assert.AreEqual(0, sink.Count(HandIntentKind.Release));

            mapper.Tick(Frame(0f, space: true), Forward);
            Assert.IsFalse(mapper.IsPinching);
            Assert.AreEqual(1f, sink.Last(HandIntentKind.Release).Held, 0.0001f);
            Assert.AreEqual(0, sink.Count(HandIntentKind.Pinch));
        }

        [Test]
        public void PalmOpen_FiresAtPointSixAndNotAtPointFive()
        {
            var early = new KbmIntentMapper();
            var earlySink = new Sink(early);
            early.Tick(Frame(0.5f, p: true), Forward);
            Assert.AreEqual(0, earlySink.Count(HandIntentKind.PalmOpen));

            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            mapper.Tick(Frame(0.6f, p: true), Forward);
            Assert.AreEqual(1, sink.Count(HandIntentKind.PalmOpen));
            mapper.Tick(Frame(0.6f, p: true), Forward);
            Assert.AreEqual(1, sink.Count(HandIntentKind.PalmOpen), "palm fires once per press");
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.6f, p: true), Forward);
            Assert.AreEqual(2, sink.Count(HandIntentKind.PalmOpen));

            var middle = new KbmIntentMapper();
            var middleSink = new Sink(middle);
            middle.Tick(Frame(0.5f, middle: true), Forward);
            Assert.AreEqual(0, middleSink.Count(HandIntentKind.PalmOpen));
            middle.Tick(Frame(0.6f, middle: true), Forward);
            Assert.AreEqual(1, middleSink.Count(HandIntentKind.PalmOpen));
        }

        [Test]
        public void LookDwell_CommitsAfterPointOneFiveSeconds()
        {
            string hit = "a";
            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            mapper.ResolveTargetId = _ => hit;

            mapper.Tick(Frame(0.14f), Forward);
            Assert.IsNull(mapper.LookTargetId);
            mapper.Tick(Frame(0.02f), Forward);
            Assert.AreEqual("a", mapper.LookTargetId);
            Assert.AreEqual("a", sink.Last(HandIntentKind.Look).TargetId);

            hit = "b";
            mapper.Tick(Frame(0.14f), Forward);
            Assert.AreEqual("a", mapper.LookTargetId, "a new target waits out the dwell");
            mapper.Tick(Frame(0.02f), Forward);
            Assert.AreEqual("b", mapper.LookTargetId);

            hit = null;
            mapper.Tick(Frame(0.20f), Forward);
            Assert.AreEqual("b", mapper.LookTargetId, "looking away does not clear the focused target");
            Assert.AreEqual(5, sink.Count(HandIntentKind.Look), "Look is raised every frame");
        }

        [Test]
        public void Tab_StepsInOrdinalIdOrderAndSkipsDisabled()
        {
            Create("c");
            Create("a");
            IntentTarget hidden = Create("b");
            hidden.enabled = false;

            var mapper = new KbmIntentMapper();
            CollectionAssert.AreEqual(new[] { "a", "c" }, IntentTargetRegistry.OrderedIds());

            mapper.Tick(Frame(0.016f, tab: true), Forward);
            Assert.AreEqual("a", mapper.LookTargetId);
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, tab: true), Forward);
            Assert.AreEqual("c", mapper.LookTargetId);
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, tab: true), Forward);
            Assert.AreEqual("a", mapper.LookTargetId, "Tab wraps");
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, tab: true, shift: true), Forward);
            Assert.AreEqual("c", mapper.LookTargetId, "Shift+Tab steps backward");
        }

        [Test]
        public void DevCommands_AreAbsentUnlessTheFlagIsOn()
        {
            var mapper = new KbmIntentMapper { DevCommandsEnabled = false };
            var sink = new Sink(mapper);
            mapper.Tick(DevFrame(), Forward);
            Assert.AreEqual(0, sink.Dev.Count);

            mapper.Tick(Frame(0.016f), Forward);
            mapper.DevCommandsEnabled = true;
            mapper.Tick(Frame(0.016f, f1: true), Forward);
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, f2: true), Forward);
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, bracketLeft: true), Forward);
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, bracketRight: true), Forward);

            CollectionAssert.AreEqual(new[]
            {
                DevCommand.StateOverlay,
                DevCommand.AutoPace,
                DevCommand.PreviousDay,
                DevCommand.NextDay
            }, sink.Dev);
        }

        [Test]
        public void DevCommand_F3OffersSeedPackets()
        {
            var mapper = new KbmIntentMapper { DevCommandsEnabled = true };
            var sink = new Sink(mapper);
            mapper.Tick(Frame(0.016f, f3: true), Forward);
            mapper.Tick(Frame(0.016f), Forward);
            CollectionAssert.AreEqual(new[] { DevCommand.SeedPackets }, sink.Dev);

            mapper.DevCommandsEnabled = false;
            mapper.Tick(Frame(0.016f, f3: true), Forward);
            mapper.Tick(Frame(0.016f), Forward);
            Assert.AreEqual(1, sink.Dev.Count, "F3 stays quiet when dev commands are off");
        }

        [Test]
        public void LookToggle_RaisesOnPress_InEveryBuild()
        {
            var mapper = new KbmIntentMapper { DevCommandsEnabled = false };
            int toggles = 0;
            mapper.LookToggled += () => toggles++;
            mapper.Tick(new RawKbm { Dt = 0.016f, L = true }, Forward);
            mapper.Tick(new RawKbm { Dt = 0.016f, L = true }, Forward);
            Assert.AreEqual(1, toggles, "a held key is one toggle");
            mapper.Tick(new RawKbm { Dt = 0.016f }, Forward);
            mapper.Tick(new RawKbm { Dt = 0.016f, L = true }, Forward);
            Assert.AreEqual(2, toggles);
        }

        [Test]
        public void HeadDrag_ClampsYawPitchAndRRecentres()
        {
            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            mapper.Tick(new RawKbm { Dt = 0.016f, MouseDelta = new Vector2(100f, 0f) }, Forward);
            AssertNear(Quaternion.identity, mapper.HeadLocalRotation);

            mapper.Tick(new RawKbm { Dt = 0.016f, RightMouse = true, MouseDelta = new Vector2(100f, 0f) }, Forward);
            AssertNear(Quaternion.Euler(0f, 15f, 0f), mapper.HeadLocalRotation);

            mapper.Tick(new RawKbm { Dt = 0.016f, RightMouse = true, MouseDelta = new Vector2(1000f, 0f) }, Forward);
            AssertNear(Quaternion.Euler(0f, 40f, 0f), mapper.HeadLocalRotation);

            mapper.Tick(new RawKbm { Dt = 0.016f, RightMouse = true, MouseDelta = new Vector2(0f, 1000f) }, Forward);
            AssertNear(Quaternion.Euler(-25f, 40f, 0f), mapper.HeadLocalRotation);

            mapper.Tick(new RawKbm { Dt = 0.016f, R = true, MouseDelta = new Vector2(50f, 50f) }, Forward);
            AssertNear(Quaternion.identity, mapper.HeadLocalRotation);
            Assert.AreEqual(1, sink.Recentres);
            mapper.Tick(new RawKbm { Dt = 0.016f, R = true }, Forward);
            Assert.AreEqual(1, sink.Recentres, "R recentres once per press");
        }

        [Test]
        public void Esc_RaisesSystemPauseOncePerPress()
        {
            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            mapper.Tick(Frame(0.016f, esc: true), Forward);
            mapper.Tick(Frame(0.016f, esc: true), Forward);
            Assert.AreEqual(1, sink.Pauses);
            mapper.Tick(Frame(0.016f), Forward);
            mapper.Tick(Frame(0.016f, esc: true), Forward);
            Assert.AreEqual(2, sink.Pauses);
        }

        [Test]
        public void Poke_FiresOnFAndOnPokeOnlyClick()
        {
            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            mapper.ResolveTargetId = _ => "label.walk.today";
            mapper.Tick(Frame(0.016f, f: true), Forward);
            mapper.Tick(Frame(0.016f, f: true), Forward);
            Assert.AreEqual(1, sink.Count(HandIntentKind.Poke));
            Assert.AreEqual("label.walk.today", sink.Last(HandIntentKind.Poke).TargetId);

            IntentTarget pebble = Create("pebble.settings");
            pebble.PokeOnly = true;
            var click = new KbmIntentMapper();
            var clickSink = new Sink(click);
            click.ResolveTargetId = _ => "pebble.settings";
            click.Tick(Frame(0.1f, left: true), Forward);
            click.Tick(Frame(0.01f), Forward);
            Assert.AreEqual(1, clickSink.Count(HandIntentKind.Poke));
            Assert.AreEqual(0, clickSink.Count(HandIntentKind.Pinch));
            Assert.AreEqual("pebble.settings", clickSink.Last(HandIntentKind.Poke).TargetId);
        }

        [Test]
        public void BindingHint_ReturnsThePcStrings()
        {
            var mapper = new KbmIntentMapper();
            Assert.AreEqual("Space or mouse", mapper.BindingHint(HandIntentKind.PinchHold));
            Assert.AreEqual("click", mapper.BindingHint(HandIntentKind.Pinch));
            Assert.AreEqual("F", mapper.BindingHint(HandIntentKind.Poke));
            Assert.AreEqual("hold P", mapper.BindingHint(HandIntentKind.PalmOpen));
        }

        IntentTarget Create(string id)
        {
            var go = new GameObject(id);
            _owned.Add(go);
            var target = go.AddComponent<IntentTarget>();
            target.Id = id;
            return target;
        }

        static Sink Drive(RawKbm[] frames)
        {
            var mapper = new KbmIntentMapper();
            var sink = new Sink(mapper);
            for (int i = 0; i < frames.Length; i++) mapper.Tick(frames[i], Forward);
            return sink;
        }

        static Ray Forward()
        {
            return new Ray(Vector3.zero, Vector3.forward);
        }

        static void AssertNear(Quaternion expected, Quaternion actual)
        {
            Assert.Less(Quaternion.Angle(expected, actual), 0.05f);
        }

        static RawKbm Frame(float dt, bool space = false, bool left = false, bool middle = false,
            bool f = false, bool p = false, bool enter = false, bool tab = false, bool shift = false,
            bool esc = false, bool f1 = false, bool f2 = false, bool f3 = false, bool bracketLeft = false, bool bracketRight = false)
        {
            return new RawKbm
            {
                Dt = dt,
                Space = space,
                LeftMouse = left,
                MiddleMouse = middle,
                F = f,
                P = p,
                Enter = enter,
                Tab = tab,
                Shift = shift,
                Esc = esc,
                F1 = f1,
                F2 = f2,
                F3 = f3,
                BracketLeft = bracketLeft,
                BracketRight = bracketRight
            };
        }

        static RawKbm DevFrame()
        {
            return new RawKbm
            {
                Dt = 0.016f,
                F1 = true,
                F2 = true,
                BracketLeft = true,
                BracketRight = true
            };
        }

        sealed class Sink
        {
            public readonly List<HandIntent> Intents = new List<HandIntent>();
            public readonly List<DevCommand> Dev = new List<DevCommand>();
            public int Pauses;
            public int Recentres;

            public Sink(KbmIntentMapper mapper)
            {
                mapper.Intent += intent => Intents.Add(intent);
                mapper.SystemPause += () => Pauses++;
                mapper.Recentred += () => Recentres++;
                mapper.DevCommandRaised += command => Dev.Add(command);
            }

            public int Count(HandIntentKind kind)
            {
                int n = 0;
                for (int i = 0; i < Intents.Count; i++)
                    if (Intents[i].Kind == kind) n++;
                return n;
            }

            public HandIntent Last(HandIntentKind kind)
            {
                for (int i = Intents.Count - 1; i >= 0; i--)
                    if (Intents[i].Kind == kind) return Intents[i];
                Assert.Fail("no " + kind);
                return default;
            }
        }
    }
}
