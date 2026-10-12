using System.Collections.Generic;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class BreakInputContractTests
    {
        [Test]
        public void PcPalm_CurrentContractCannotDistinguishReleaseFromContinuedHold()
        {
            var released = Replay(false);
            var held = Replay(true);
            CollectionAssert.AreEqual(released, held,
                "If the provider gains held palm state, replace this blocker reproduction with the B6 integration test.");
            Assert.AreEqual(1, held.FindAll(s => s.StartsWith("PalmOpen")).Count);
            Assert.AreEqual(0, released.FindAll(s => s.StartsWith("Release")).Count);
            Debug.Log("[B6 input blocker] P held for 0.7s then released and P held for 4s produce identical intent streams: one PalmOpen, no Release. IHandIntentSource exposes no palm state.");
        }

        static List<string> Replay(bool keepHolding)
        {
            var mapper = new KbmIntentMapper { ResolveTargetId = _ => "mark.0" };
            var events = new List<string>();
            mapper.Intent += i => events.Add(i.Kind + ":" + i.Held + ":" + i.Strength + ":" + i.TargetId);
            for (int i = 0; i < 240; i++)
                mapper.Tick(new RawKbm { P = keepHolding || i < 42, Dt = 1f / 60f },
                    () => new Ray(Vector3.zero, Vector3.forward));
            Assert.IsFalse(mapper.IsPinching);
            Assert.AreEqual(0, mapper.PinchStrength);
            return events;
        }
    }
}
