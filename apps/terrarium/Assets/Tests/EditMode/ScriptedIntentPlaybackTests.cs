using System.Collections.Generic;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class ScriptedIntentPlaybackTests
    {
        GameObject _go;
        ScriptedIntentSource _src;
        readonly List<GameObject> _owned = new List<GameObject>();

        const string Sample =
            "{\"t\":10.0,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":4.2}\n" +
            "{\"t\":14.2,\"intent\":\"Release\"}\n" +
            "\n" +
            "{\"t\":20,\"intent\":\"Look\",\"target\":\"seed.walk\",\"dur\":0.4}\n" +
            "{\"t\":21,\"intent\":\"Pinch\",\"target\":\"seed.walk\"}\n" +
            "{\"t\":30,\"intent\":\"PalmOpen\"}\n" +
            "{\"t\":31,\"intent\":\"Lost\",\"dur\":2.0}\n" +
            "{\"t\":34,\"intent\":\"Poke\",\"target\":\"pebble.settings\"}\n";

        [SetUp]
        public void SetUp()
        {
            IntentTargetRegistry.Reset();
            _go = new GameObject("playback");
            _src = _go.AddComponent<ScriptedIntentSource>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
            IntentTargetRegistry.Reset();
        }

        [Test]
        public void Playback_ParsesEveryLineKind()
        {
            var seed = new GameObject("seed.walk");
            _owned.Add(seed);
            seed.transform.position = new Vector3(0f, 0f, 5f);
            var target = seed.AddComponent<IntentTarget>();
            target.Id = "seed.walk";

            _src.Load(Sample);
            var kinds = new HashSet<HandIntentKind>();
            string pinchTarget = null;
            string lookTarget = null;
            Vector3 pinchDir = Vector3.zero;
            bool sawUntracked = false;
            _src.Intent += intent =>
            {
                kinds.Add(intent.Kind);
                if (intent.Kind == HandIntentKind.Pinch)
                {
                    pinchTarget = intent.TargetId;
                    pinchDir = intent.Ray.direction;
                }
                if (intent.Kind == HandIntentKind.Look) lookTarget = intent.TargetId;
            };

            int steps = 0;
            while (!_src.Finished && steps < 4000)
            {
                _src.Tick(0.05f);
                if (!_src.IsTracked) sawUntracked = true;
                steps++;
            }

            Assert.IsTrue(_src.Finished, "playback did not finish, steps=" + steps);
            Assert.IsTrue(sawUntracked, "Lost did not clear tracking");
            Assert.IsTrue(kinds.Contains(HandIntentKind.PinchHold));
            Assert.IsTrue(kinds.Contains(HandIntentKind.Release));
            Assert.IsTrue(kinds.Contains(HandIntentKind.Look));
            Assert.IsTrue(kinds.Contains(HandIntentKind.Pinch));
            Assert.IsTrue(kinds.Contains(HandIntentKind.PalmOpen));
            Assert.IsTrue(kinds.Contains(HandIntentKind.Poke));
            Assert.AreEqual("seed.walk", pinchTarget);
            Assert.AreEqual("seed.walk", lookTarget);
            Assert.Greater(Vector3.Dot(pinchDir.normalized, Vector3.forward), 0.99f);
        }

        [Test]
        public void Playback_BadLineReportsItsLineNumber()
        {
            _src.Load("{\"t\":1,\"intent\":\"Poke\",\"target\":\"pebble\"}");
            ScriptedIntentParseException ex = Assert.Throws<ScriptedIntentParseException>(() =>
                _src.Load("{\"t\":0,\"intent\":\"Pinch\"}\n\n{not-json}\n"));
            Assert.AreEqual(3, ex.LineNumber);
            StringAssert.Contains("line 3", ex.Message);

            int pokes = 0;
            _src.Intent += intent =>
            {
                if (intent.Kind == HandIntentKind.Poke) pokes++;
            };
            _src.Tick(1f);
            Assert.AreEqual(1, pokes, "a rejected script must leave the previous one in place");
        }

        [Test]
        public void Playback_HoldsStrengthForTheDuration()
        {
            _src.Load("{\"t\":0,\"intent\":\"PinchHold\",\"target\":\"jar\",\"dur\":1}");
            _src.Tick(0.09f);
            Assert.GreaterOrEqual(_src.PinchStrength, 0.8f);
            Assert.IsTrue(_src.IsPinching);
            _src.Tick(0.5f);
            Assert.GreaterOrEqual(_src.PinchStrength, 0.99f);
            Assert.IsTrue(_src.IsPinching);
            Assert.IsFalse(_src.Finished);
        }

        [Test]
        public void Playback_LostReportsUntracked()
        {
            _src.Load("{\"t\":0,\"intent\":\"Lost\",\"dur\":2}");
            Assert.IsTrue(_src.IsTracked);
            _src.Tick(0.5f);
            Assert.IsFalse(_src.IsTracked);
            Assert.IsFalse(_src.Finished);
            _src.Tick(1.6f);
            Assert.IsTrue(_src.IsTracked);
            Assert.IsTrue(_src.Finished);
        }

        [Test]
        public void Playback_FinishedBecomesTrueAfterTheLastEvent()
        {
            _src.Load("{\"t\":1.0,\"intent\":\"PalmOpen\"}\n{\"t\":2.0,\"intent\":\"Poke\"}");
            Assert.IsFalse(_src.Finished);
            _src.Tick(1f);
            Assert.IsFalse(_src.Finished);
            _src.Tick(1f);
            Assert.IsTrue(_src.Finished);
            _src.Tick(5f);
            Assert.IsTrue(_src.Finished);
        }

        [Test]
        public void Playback_BindingHintsMatchTheProvider()
        {
            Assert.AreEqual("Space or mouse", _src.BindingHint(HandIntentKind.PinchHold));
            Assert.AreEqual("click", _src.BindingHint(HandIntentKind.Pinch));
            Assert.AreEqual("F", _src.BindingHint(HandIntentKind.Poke));
            Assert.AreEqual("hold P", _src.BindingHint(HandIntentKind.PalmOpen));
        }
    }
}
