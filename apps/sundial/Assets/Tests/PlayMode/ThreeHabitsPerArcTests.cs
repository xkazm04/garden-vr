using System.Collections;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class ThreeHabitsPerArcTests
    {
        [UnityTearDown]
        public IEnumerator Restore()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ThreeHabitsPerArc_TendAll()
        {
            SundialController controller = null;
            yield return SundialPlay.Open(c => controller = c);
            yield return null;

            SundialPlay.Play(controller, "{\"t\":0.20,\"intent\":\"Pinch\",\"target\":\"more.morning\"}\n");
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(0.55f);

            SundialService service = controller.Service;
            HabitDef stretch = service.HabitAt("morning", 1);
            Assert.IsNotNull(stretch, "Another should admit the next morning habit");
            Assert.AreEqual("stretch", stretch.Id);
            Assert.AreEqual("reed", stretch.Species);
            Assert.AreEqual(1, controller.CueCount(SundialController.CueSeed));

            Assert.IsNotNull(service.TryAdmit(SeedCatalog.Find("morning", "bed")));
            Assert.IsNotNull(service.TryAdmit(SeedCatalog.Find("midday", "walk")));
            Assert.IsNotNull(service.TryAdmit(SeedCatalog.Find("midday", "lunch")));
            Assert.IsNotNull(service.TryAdmit(SeedCatalog.Find("winddown", "phone")));
            Assert.IsNotNull(service.TryAdmit(SeedCatalog.Find("winddown", "read")));
            Assert.IsNull(service.TryAdmit(SeedCatalog.Find("morning", "water")));
            Assert.AreEqual(3, service.LiveInArc("morning"));
            Assert.AreEqual(3, service.LiveInArc("midday"));
            Assert.AreEqual(3, service.LiveInArc("winddown"));
            Assert.AreEqual("top3", service.HabitForArc("midday").Id);
            Assert.AreEqual("walk", service.HabitAt("midday", 1).Id);
            Assert.AreEqual("lunch", service.HabitAt("midday", 2).Id);
            Assert.AreEqual("breaths", service.HabitForArc("winddown").Id);

            SundialPlay.Play(controller,
                "{\"t\":0.15,\"intent\":\"Pinch\",\"target\":\"plant.midday\"}\n" +
                "{\"t\":0.50,\"intent\":\"Pinch\",\"target\":\"undo.midday\"}\n");
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(0.85f);
            Assert.AreEqual(0, controller.LiveCount("top3"));
            Assert.AreEqual(0, controller.LiveCount("walk"));

            yield return PinchFlush(controller, "plant.midday.r1");
            Assert.AreEqual(1, controller.LiveCount("walk"));
            Assert.AreEqual(0, controller.LiveCount("top3"));
            yield return null;
            Assert.AreEqual(SundialArcs.TileDigit(TileState.Kept), controller.View.tiles[DialView.TileIndex(1, 1, 6)]);
            Assert.AreEqual(SundialArcs.TileDigit(TileState.Today), controller.View.tiles[DialView.TileIndex(1, 0, 6)]);

            yield return PinchFlush(controller, "plant.morning");
            yield return PinchFlush(controller, "plant.morning.r1");
            yield return PinchFlush(controller, "plant.morning.r2");
            yield return PinchFlush(controller, "plant.midday");
            yield return PinchFlush(controller, "plant.midday.r2");
            yield return PinchFlush(controller, "plant.winddown.r1");
            yield return PinchFlush(controller, "plant.winddown.r2");
            TendResult ritual = service.TendRitual("breaths");
            Assert.IsTrue(ritual != null && ritual.Ok, ritual == null ? "no ritual" : ritual.Reason);
            yield return null;

            Assert.AreEqual(1, controller.LiveCount("water"));
            Assert.AreEqual(1, controller.LiveCount("stretch"));
            Assert.AreEqual(1, controller.LiveCount("bed"));
            Assert.AreEqual(1, controller.LiveCount("top3"));
            Assert.AreEqual(1, controller.LiveCount("walk"));
            Assert.AreEqual(1, controller.LiveCount("lunch"));
            Assert.AreEqual(1, controller.LiveCount("phone"));
            Assert.AreEqual(1, controller.LiveCount("read"));
            Assert.AreEqual(1, controller.LiveCount("breaths"));
            Assert.AreEqual(9, CountPlants(controller.View), "nine plant cards");
            Mesh mesh = controller.View.BuiltTileMesh;
            Assert.IsNotNull(mesh);
            Assert.AreEqual(630, mesh.triangles.Length / 3);

            service.JumpTo(new System.DateTimeOffset(2026, 10, 4, 8, 0, 0, System.TimeSpan.Zero));
            yield return null;
            TendResult again = service.BackfillYesterday(service.HabitForArc("morning"));
            Assert.IsFalse(again.Ok);
            Assert.AreEqual("already-kept", again.Reason);
        }

        static IEnumerator PinchFlush(SundialController controller, string id)
        {
            SundialPlay.Play(controller, "{\"t\":0.12,\"intent\":\"Pinch\",\"target\":\"" + id + "\"}\n");
            SundialPlay.FixedStep();
            yield return SundialPlay.Seconds(0.35f);
            Assert.IsTrue(controller.Service.IsPending, id + " did not arm");
            controller.Service.Flush();
            yield return null;
        }

        static int CountPlants(DialView view)
        {
            int plants = 0;
            if (view == null) return 0;
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled && renderer.gameObject.name.StartsWith("plant."))
                    plants++;
            }
            return plants;
        }
    }
}
