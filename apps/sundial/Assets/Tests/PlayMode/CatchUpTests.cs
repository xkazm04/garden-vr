using System;
using System.Collections;
using System.IO;
using System.Linq;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class CatchUpTests
    {
        sealed class Hands : IHandIntentSource
        {
            public event Action<HandIntent> Intent;
            public event Action SystemPause;
            public bool IsPinching => false;
            public bool IsTracked => true;
            public float PinchStrength => 0;
            public Ray LookRay => new Ray(Vector3.zero, Vector3.forward);
            public string BindingHint(HandIntentKind kind) => HandBindings.PcHint(kind);
            public void Emit(string id, HandIntentKind kind) => Intent?.Invoke(new HandIntent(kind, LookRay, targetId: id));
            public void Pause() => SystemPause?.Invoke();
        }
        SundialController _c;
        CatchUpCard Card => _c.CatchUpCard;
        Hands _hands;
        FixedClock _clock;
        string _dir;
        float _delta;
        int _today;
        string Evidence => SundialEvidence.Dir("T-SUN-065");
        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null; SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = false; Time.captureDeltaTime = _delta;
        }
        IEnumerator Boot(bool first = false, int depth = 2, int habits = 3)
        {
            yield return SundialPlay.Unload();
            _clock = SundialPlay.Clock1420(); _today = GardenDay.From(_clock.Now, GardenDay.DefaultBoundary).Index;
            _dir = SundialPlay.FreshDir();
            var save = new SundialSave { LastVisitDay = first ? (int?)null : _today - 3, FirstRunStep = FirstRunSteps.Done };
            save.Settings.CatchUpDepth = depth;
            string[] names = { "Drink water", "Read a page", "Call home" };
            for (int i = 0; i < habits; i++)
                save.Habits.Add(new HabitDef
                {
                    Id = "habit" + i, Name = names[i % 3], PresetKey = "water", Group = SundialArcs.FromIndex(i % 3),
                    Row = i / 3, Slot = i % 3, Kind = HabitKind.LifeCheckIn, CreatedDay = _today - 9
                });
            File.WriteAllText(Path.Combine(_dir, "save.json"), Json.Write(SundialCodec.Write(save)));
            SundialService.DevSeedOnFresh = false;
            SundialController.SaveDirectoryOverride = _dir; SundialController.ClockOverride = _clock;
            yield return SundialPlay.LoadMain(c => _c = c);
            _hands = new Hands(); _c.SetSource(_hands); SundialPlay.FixedStep();
            yield return null; yield return null;
        }
        IEnumerator Emit(string id, HandIntentKind kind = HandIntentKind.Pinch)
        {
            _hands.Emit(id, kind); yield return null; yield return null;
        }
        SundialSave Disk() => SundialCodec.Read(Json.ParseObject(File.ReadAllText(Path.Combine(_dir, "save.json"))));
        IEnumerator Capture(string name)
        {
            RecordTilesTests.DisableAsyncShaders(); var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return null; yield return null;
            Directory.CreateDirectory(Evidence);
            RecordTilesTests.Shot(cam, Path.Combine(Evidence, name + ".png"));
            Physics.SyncTransforms();
            foreach (var target in _c.GetComponentsInChildren<IntentTarget>())
            {
                if (!target.Id.StartsWith("catchup.") || !target.gameObject.activeInHierarchy) continue;
                var col = target.GetComponent<Collider>(); var point = cam.WorldToViewportPoint(col.bounds.center);
                Assert.That(point.x, Is.InRange(.01f, .99f), target.Id);
                Assert.That(point.y, Is.InRange(.01f, .99f), target.Id);
                Assert.IsTrue(Physics.Raycast(cam.transform.position, col.bounds.center - cam.transform.position, out var hit));
                Assert.AreEqual(target.Id, hit.collider.GetComponent<IntentTarget>()?.Id, target.Id + " obstructed");
            }
        }
        [UnityTest] public IEnumerator CatchUp_TwoDaysAway_OffersTwoDays()
        {
            yield return Boot(); Assert.IsTrue(Card.Open);
            Assert.AreEqual(6, Card.Items.Count);
            CollectionAssert.AreEqual(new[] { _today - 2, _today - 1 }, Card.Items.Select(x => x.Day.Index).Distinct());
            Assert.AreEqual(_today - 3, Disk().LastVisitDay);
            yield return Capture("DialG1-catch-up");
            yield return Emit(CatchUpCard.DoneId);
            Assert.IsFalse(Card.Open); Assert.IsEmpty(_c.Ledger.Events); Assert.AreEqual(_today, Disk().LastVisitDay);
            yield return SundialPlay.Unload();
            yield return SundialPlay.LoadMain(c => _c = c); yield return null;
            Assert.IsFalse(Card.Open, "same-day relaunch must not repeat the card");
        }
        [UnityTest] public IEnumerator CatchUp_Logged_IsHatchedLate()
        {
            yield return Boot(); var item = Card.Items[0]; string id = CatchUpCard.RowId(item);
            yield return Emit(id); yield return Emit(id);
            Assert.AreEqual(1, _c.Ledger.Events.Count);
            var ev = _c.Ledger.Events[0]; Assert.IsTrue(ev.Late); Assert.AreEqual(TendSource.Backfill, ev.Source);
            Assert.AreEqual(item.Day.Index, ev.Day); Assert.AreEqual(TileState.Late, _c.Service.PlantFor(item.Habit).Window[4]);
            Assert.AreEqual(2, _c.View.tiles[DialView.TileIndex(0, 0, 4)], "shader state must be late hatch");
            Assert.IsTrue(Disk().Tends.Single().Late); Assert.AreEqual(_today - 3, Disk().LastVisitDay);
            yield return Emit(CatchUpCard.DoneId); yield return Capture("DialG1-catch-up-late");
            var reloaded = new SundialService(_clock, _dir);
            Assert.AreEqual(TileState.Late, reloaded.PlantFor(reloaded.Save.Habits[0]).Window[4]);
        }
        [UnityTest] public IEnumerator CatchUp_FirstVisit_ShowsNothing()
        {
            yield return Boot(first: true); Assert.IsFalse(Card.Open); Assert.IsEmpty(Card.Items);
            Assert.IsEmpty(_c.Ledger.Events); Assert.AreEqual(_today, Disk().LastVisitDay);
        }
        [UnityTest] public IEnumerator CatchUp_DepthOne_QuestionAlsoLogsLate()
        {
            yield return Boot(depth: 1); Assert.AreEqual(3, Card.Items.Count);
            Assert.IsTrue(Card.Items.All(x => x.Day.Index == _today - 1));
            yield return Emit(null, HandIntentKind.PalmOpen);
            Assert.IsFalse(Card.Open); Assert.IsFalse(_c.Dismissed); Assert.IsEmpty(_c.Ledger.Events);
            Assert.AreEqual(_today, Disk().LastVisitDay);
            yield return Emit("tile.morning.yesterday.ask"); Assert.IsTrue(_c.PromptVisible);
            yield return Emit(SundialController.PromptYesId);
            Assert.AreEqual(TendSource.Backfill, _c.Ledger.Events.Single().Source);
            Assert.IsTrue(_c.Ledger.Events.Single().Late);
            Assert.AreEqual(TileState.Late, _c.Service.PlantFor(_c.Service.Save.Habits[0]).Window[5]);
        }
        [UnityTest] public IEnumerator CatchUp_Pages_Pause_AndBackgroundIntents()
        {
            yield return Boot(depth: 3, habits: 9); // Last visit excludes its own day: two days, eighteen rows.
            Assert.AreEqual(18, Card.Items.Count);
            yield return Emit("plant.morning"); yield return Emit(SettingsTabs.TabId, HandIntentKind.Poke);
            yield return Emit("gnomon", HandIntentKind.PinchHold);
            Assert.IsEmpty(_c.Ledger.Events); Assert.IsFalse(_c.Settings.Open); Assert.IsFalse(_c.Service.Scrubbing);
            _hands.Pause(); yield return null; Assert.IsTrue(Card.Open); Assert.AreEqual(_today - 3, Disk().LastVisitDay);
            yield return Emit("catchup.next"); Assert.AreEqual(1, Card.Page);
            yield return Capture("DialG1-catch-up-nine-habits");
            yield return Emit("catchup.previous"); Assert.AreEqual(0, Card.Page);
            yield return Emit("catchup.next"); yield return Emit("catchup.next"); Assert.AreEqual(2, Card.Page);
            var item = Card.Items[12]; yield return Emit(CatchUpCard.RowId(item));
            Assert.AreEqual(item.Habit.Id, _c.Ledger.Events.Single().HabitId);
            yield return Emit(null, HandIntentKind.PalmOpen); Assert.IsFalse(_c.Dismissed);
        }
        [UnityTest] public IEnumerator CatchUp_WeeklyTarget_RefreshesRemainingRows()
        {
            yield return Boot(habits: 1);
            _c.Service.Save.Habits[0].Schedule = HabitSchedule.PerWeek(1);
            var stale = Card.Items[1]; yield return Emit(CatchUpCard.RowId(Card.Items[0]));
            Assert.IsEmpty(Card.Items);
            Assert.IsFalse(_c.Service.LogCatchUp(stale).Ok); Assert.AreEqual(1, _c.Ledger.Events.Count);
            Assert.IsTrue(Card.Open); yield return Emit(CatchUpCard.DoneId); Assert.IsFalse(Card.Open);
        }
        [UnityTest] public IEnumerator CatchUp_DayRollover_RefreshesStaleRows()
        {
            yield return Boot(); var stale = Card.Items[0];
            _clock.Now = _clock.Now.AddDays(1); yield return null; yield return null;
            Assert.IsFalse(Card.Items.Any(x => x.Day.Index == stale.Day.Index));
            yield return Emit(CatchUpCard.RowId(stale)); Assert.IsEmpty(_c.Ledger.Events);
            yield return Emit(CatchUpCard.DoneId); Assert.AreEqual(_today + 1, Disk().LastVisitDay);
        }
    }
}
