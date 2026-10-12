using System;
using System.Collections;
using System.IO;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class HabitProfileTests
    {
        float _delta;
        SundialController _controller;
        static readonly DateTimeOffset Saturday = new DateTimeOffset(2026, 10, 10, 14, 20, 0, TimeSpan.Zero);

        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Restore()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = false;
            Time.captureDeltaTime = _delta;
        }

        IEnumerator Open()
        {
            SundialService.DevSeedOnFresh = false;
            SundialController.SaveDirectoryOverride = SundialPlay.FreshDir();
            var clock = new FixedClock(Saturday, TimeZoneInfo.Utc);
            SundialController.ClockOverride = clock;
            var save = new SundialService(clock, SundialController.SaveDirectoryOverride);
            save.SetFirstRunStep(FirstRunSteps.Done);
            yield return SundialPlay.LoadMain(c => _controller = c);
            SundialPlay.FixedStep();
        }

        static CoachProposal Proposal(string name, LifeZone zone, HabitSchedule schedule)
        {
            return new CoachProposal { Name = name, Zone = zone, Schedule = schedule };
        }

        IEnumerator Look(string target)
        {
            SundialPlay.Play(_controller, "{\"t\":0,\"intent\":\"Look\",\"target\":\"" + target + "\",\"dur\":5}\n");
            yield return SundialPlay.Seconds(0.5f);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Profile_NameAndZoneShowOnLook()
        {
            yield return Open();
            var habit = _controller.Service.PlantProposal(Proposal("Call mum", LifeZone.Connection, HabitSchedule.PerWeek(3)), "midday");
            Assert.IsNotNull(habit);
            yield return Look("plant.midday");
            StringAssert.Contains("Call mum\nConnection", _controller.View.ProfileLabel);
            Assert.IsTrue(_controller.View.ZoneDot(3).enabled);
            var block = new MaterialPropertyBlock();
            _controller.View.ZoneDot(3).GetPropertyBlock(block);
            Assert.Less(Vector4.Distance(DialView.ZoneColor(LifeZone.Connection), block.GetColor("_Color")), 0.0001f);
            Assert.AreEqual("Call mum", WeekPageInk.PlantLine(habit));
            // Names are data, not rich text markup.
            _controller.Service.PlantProposal(Proposal("Read <b>slowly</b>", LifeZone.Mind, HabitSchedule.Daily()), "midday");
            yield return Look("plant.midday.r1");
            StringAssert.Contains("Read <b>slowly</b>", _controller.View.ProfileLabel);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Weekdays_OffDayTileIsBlank()
        {
            yield return Open();
            var habit = _controller.Service.PlantProposal(Proposal("Walk after lunch", LifeZone.Body, HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek)), "midday");
            yield return Look("tile.midday.6");
            Assert.AreEqual(TileState.NotPlanned, _controller.Service.PlantFor(habit).Window[6]);
            Assert.AreEqual(0, _controller.View.tiles[DialView.TileIndex(1, 0, 6)], "blank paper shader state");
            StringAssert.Contains("not planned", _controller.View.ProfileLabel);
            Assert.IsFalse(_controller.Service.PlantFor(habit).DueNow);
            _controller.Service.JumpTo(Saturday.AddDays(2));
            yield return null;
            Assert.AreEqual(TileState.Today, _controller.Service.PlantFor(habit).Window[6]);
            Assert.AreEqual(TileState.NotPlanned, _controller.Service.PlantFor(habit).Window[5]);
            Assert.IsFalse(_controller.AskVisible("midday"));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PerWeek_NeverMarksADayMissed()
        {
            yield return Open();
            _controller.Service.JumpTo(Saturday.AddDays(-5));
            var habit = _controller.Service.PlantProposal(Proposal("Call mum", LifeZone.Connection, HabitSchedule.PerWeek(3)), "winddown");
            Assert.IsTrue(_controller.Service.TendRitual(habit.Id).Ok);
            _controller.Service.JumpTo(Saturday.AddDays(-3));
            Assert.IsTrue(_controller.Service.TendRitual(habit.Id).Ok);
            _controller.Service.JumpTo(Saturday);
            yield return Look("tile.winddown.5");
            StringAssert.Contains("2 of 3 this week", _controller.View.ProfileLabel);
            CollectionAssert.DoesNotContain(_controller.Service.PlantFor(habit).Window, TileState.Missed);
            var record = WeekDial.Read(_controller.Service.Save.Habits, _controller.Service.Ledger, _controller.Service.Today());
            CollectionAssert.DoesNotContain(record.For(habit.Id).Days, TileState.Missed);
            Assert.AreEqual(0, _controller.View.tiles[DialView.TileIndex(2, 0, 5)]);
            Assert.IsFalse(_controller.AskVisible("winddown"));
            _controller.Service.JumpTo(Saturday.AddDays(2));
            yield return Look("plant.winddown");
            StringAssert.Contains("0 of 3 this week", _controller.View.ProfileLabel);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator NinePlants_ThreeArcsFull_AdmitRefusesTenth()
        {
            yield return Open();
            var service = _controller.Service;
            service.JumpTo(Saturday.AddDays(-18));
            string[] names = { "Water", "Plan tomorrow's top 3", "Call mum", "Morning stretch", "Walk after lunch", "Read a little", "Open the curtains", "Write one line", "Phone away" };
            LifeZone[] zones = { LifeZone.Body, LifeZone.Work, LifeZone.Connection, LifeZone.Body, LifeZone.Body, LifeZone.Mind, LifeZone.Mind, LifeZone.Work, LifeZone.Connection };
            for (int i = 0; i < 9; i++)
            {
                var habit = service.PlantProposal(Proposal(names[i], zones[i], i == 2 ? HabitSchedule.PerWeek(3) : i == 4 ? HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek) : HabitSchedule.Daily()), null);
                Assert.IsNotNull(habit);
                Assert.AreEqual(SundialArcs.FromIndex(i % 3), habit.Group);
                Assert.AreEqual(i / 3, habit.Row);
            }
            for (int arc = 0; arc < 3; arc++) Assert.AreEqual(3, service.LiveInArc(SundialArcs.FromIndex(arc)));
            string before = File.ReadAllText(Path.Combine(SundialController.SaveDirectoryOverride, "save.json"));
            Assert.IsNull(service.PlantProposal(Proposal("A tenth", LifeZone.Mind, HabitSchedule.Daily()), null));
            Assert.IsNull(service.TryAdmit(SeedCatalog.Find("morning", "water")));
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(SundialController.SaveDirectoryOverride, "save.json")));
            for (int day = -18; day <= 0; day++)
            {
                service.JumpTo(Saturday.AddDays(day));
                for (int i = 0; i < 9; i++)
                    if ((day + i + 18) % 4 != 0) Assert.IsTrue(service.TendRitual(service.Save.Habits[i].Id).Ok);
            }
            yield return SundialPlay.Seconds(1.7f);
            for (int slot = 0; slot < 9; slot++)
            {
                Assert.IsTrue(_controller.View.plantOn[slot]);
                Assert.IsTrue(_controller.View.ZoneDot(slot).enabled);
            }
            RecordTilesTests.DisableAsyncShaders();
            Camera camera = RecordTilesTests.PoseDial();
            RecordTilesTests.AttachPlate(camera);
            yield return Look("plant.midday");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../orchestration/runs/sundial/T-SUN-059"));
            Directory.CreateDirectory(dir);
            RecordTilesTests.Shot(camera, Path.Combine(dir, "DialG1-nine-plants.png"));
            File.WriteAllText(Path.Combine(dir, "capture-state.json"), service.StateJson);
            File.WriteAllText(Path.Combine(dir, "capture-save.json"), Json.Write(SundialCodec.Write(service.Save)));
            yield return Look("plant.winddown");
            RecordTilesTests.Shot(camera, Path.Combine(dir, "DialG1-weekly-look.png"));
        }
    }
}
