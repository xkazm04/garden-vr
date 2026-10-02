using System.Collections.Generic;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class IntentTargetTests
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
        public void Raycast_MissesHiddenAndDisabledTargets()
        {
            var go = new GameObject("jar");
            _owned.Add(go);
            go.AddComponent<BoxCollider>();
            var target = go.AddComponent<IntentTarget>();
            target.Id = "jar";

            Ray hit = new Ray(new Vector3(0f, 0f, -2f), Vector3.forward);
            Ray miss = new Ray(new Vector3(0f, 5f, -2f), Vector3.forward);
            Assert.AreEqual("jar", IntentTargetRegistry.Raycast(hit).Id);
            Assert.IsNull(IntentTargetRegistry.Raycast(miss));

            target.enabled = false;
            Assert.IsNull(IntentTargetRegistry.Raycast(hit));
            CollectionAssert.DoesNotContain(IntentTargetRegistry.OrderedIds(), "jar");

            target.enabled = true;
            Assert.AreEqual("jar", IntentTargetRegistry.Raycast(hit).Id);

            go.SetActive(false);
            Assert.IsNull(IntentTargetRegistry.Raycast(hit));
            CollectionAssert.DoesNotContain(IntentTargetRegistry.OrderedIds(), "jar");
        }
    }
}
