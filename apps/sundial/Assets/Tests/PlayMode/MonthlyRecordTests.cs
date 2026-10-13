using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class MonthlyRecordTests
    {
        SundialController _c;
        float _delta;
        string _dir;
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 20, 14, 20, 0, TimeSpan.Zero);
        string Evidence => SundialEvidence.Dir("T-SUN-066");
        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null; SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = false; Time.captureDeltaTime = _delta;
        }
        IEnumerator Boot()
        {
            _dir = SundialPlay.FreshDir();
            SundialService.DevSeedOnFresh = false;
            var clock = new FixedClock(Now, TimeZoneInfo.Utc);
            new SundialService(clock, _dir).SetFirstRunStep(FirstRunSteps.Done);
            SundialController.SaveDirectoryOverride = _dir; SundialController.ClockOverride = clock;
            yield return SundialPlay.LoadMain(c => _c = c);
            SundialPlay.FixedStep();
        }
        HabitDef Plant(string name, LifeZone zone, string arc, int? target = null, HabitSchedule schedule = null)
            => _c.Service.PlantProposal(new CoachProposal { Name = name, Zone = zone, Target = target, Schedule = schedule ?? HabitSchedule.Daily() }, arc);
        IEnumerator Intent(string kind, string id)
        {
            SundialPlay.Play(_c, "{\"t\":0,\"intent\":\"" + kind + "\",\"target\":\"" + id + "\",\"dur\":2}\n");
            yield return SundialPlay.Seconds(0.25f);
        }
        SundialSave Disk() => SundialCodec.Read(Json.ParseObject(File.ReadAllText(Path.Combine(_dir, "save.json"))));
        IEnumerator Capture(string name)
        {
            RecordTilesTests.DisableAsyncShaders();
            var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return null;
            Directory.CreateDirectory(Evidence);
            RecordTilesTests.Shot(cam, Path.Combine(Evidence, name + ".png"));
        }

        [UnityTest, Timeout(180000)] public IEnumerator ZoneTabs_FillFromLedger()
        {
            yield return Boot();
            _c.Service.JumpTo(Now.AddDays(-1));
            var body = Plant("Water", LifeZone.Body, "morning");
            var mind = Plant("Read", LifeZone.Mind, "winddown");
            Plant("Weekend walk", LifeZone.Connection, "midday", schedule: HabitSchedule.OnWeekdays(1 << 5));
            _c.Service.TendRitual(body.Id);
            _c.Service.JumpTo(Now);
            yield return null;
            Assert.AreEqual(0.5f, _c.View.MonthlyFill(LifeZone.Body));
            Assert.AreEqual(0f, _c.View.MonthlyFill(LifeZone.Mind));
            Assert.IsNull(_c.View.MonthlyFill(LifeZone.Work));
            Assert.IsNull(_c.View.MonthlyFill(LifeZone.Connection), "no scheduled days yet");
            foreach (LifeZone zone in Enum.GetValues(typeof(LifeZone)))
            {
                var tab = _c.View.MonthlyTab(zone);
                Assert.IsTrue(tab.gameObject.activeInHierarchy);
                Assert.AreEqual(zone.ToString(), tab.text, "no numbers or percentages on the rim");
                var block = new MaterialPropertyBlock();
                tab.transform.Find("paper").GetComponent<Renderer>().GetPropertyBlock(block);
                var texture = (Texture2D)block.GetTexture("_MainTex");
                var plain = InkLetter.Note(0.048f / 0.029f);
                int changed = texture.GetPixels32().Zip(plain.GetPixels32(), (a, b) => !a.Equals(b)).Count(b => b);
                Assert.AreEqual(zone == LifeZone.Body, changed > 0, "only a filled zone has pencil shading");
            }
            _c.Service.TendRitual(body.Id);
            yield return null;
            Assert.AreEqual(1f, _c.View.MonthlyFill(LifeZone.Body));
            _c.Service.JumpTo(new DateTimeOffset(2026, 11, 1, 14, 20, 0, TimeSpan.Zero));
            yield return null;
            Assert.AreEqual(0f, _c.View.MonthlyFill(LifeZone.Body), "new month starts with no kept days");
        }

        [UnityTest, Timeout(300000)] public IEnumerator MonthTotal_OnLook()
        {
            yield return Boot();
            var s = _c.Service;
            s.JumpTo(Now.AddDays(-20)); // September 30 is deliberately outside the record month.
            string[] names = { "Water", "Plan tomorrow's top 3", "Call mum", "Morning stretch", "Walk after lunch", "Read a little", "Open the curtains", "Write one line", "Phone away" };
            LifeZone[] zones = { LifeZone.Body, LifeZone.Work, LifeZone.Connection, LifeZone.Body, LifeZone.Body, LifeZone.Mind, LifeZone.Mind, LifeZone.Work, LifeZone.Connection };
            for (int i = 0; i < 9; i++) Plant(names[i], zones[i], null, i == 0 ? 8 : (int?)null);
            for (int day = -20; day <= -2; day++)
            {
                s.JumpTo(Now.AddDays(day));
                for (int i = 0; i < 9; i++)
                    if (i == 1 || (day + i + 20) % 4 != 0) s.TendRitual(s.Save.Habits[i].Id);
            }
            s.JumpTo(Now);
            for (int i = 0; i < 3; i++) s.CountQuantity(s.Save.Habits[0].Id);
            yield return SundialPlay.Seconds(1.7f);
            yield return Intent("Look", "plant.midday");
            StringAssert.Contains("Plan tomorrow's top 3", _c.View.ProfileLabel);
            StringAssert.Contains("kept 18 days this month", _c.View.ProfileLabel);
            Assert.AreEqual(9, _c.View.plantOn.Count(on => on));
            Assert.AreEqual(4, _c.GetComponentsInChildren<TextMesh>().Count(t => t.name.StartsWith("zone.tab.")), "onboarding and record share four tabs");
            yield return Capture("DialG1-monthly-record-nine-plants");
            File.WriteAllText(Path.Combine(Evidence, "capture-save.json"), Json.Write(SundialCodec.Write(s.Save)));
            File.WriteAllText(Path.Combine(Evidence, "capture-state.json"), s.StateJson);
            yield return Intent("Look", "plant.morning");
            StringAssert.Contains("3 of 8", _c.View.ProfileLabel);
            yield return Capture("DialG1-quantity-three-of-eight");
            s.JumpTo(new DateTimeOffset(2026, 11, 1, 14, 20, 0, TimeSpan.Zero));
            yield return Intent("Look", "plant.midday");
            StringAssert.Contains("kept 0 days this month", _c.View.ProfileLabel);
        }

        [UnityTest, Timeout(180000)] public IEnumerator Quantity_ReachTarget_TendsOnce()
        {
            yield return Boot();
            var habit = Plant("Water", LifeZone.Body, "morning", 8);
            for (int i = 1; i <= 9; i++)
            {
                yield return Intent("Pinch", i % 2 == 0 ? "tile.morning.6" : "plant.morning");
                Assert.AreEqual(i, _c.Service.Counts.Count(habit.Id, _c.Service.Today().Index));
                Assert.AreEqual(i >= 8, _c.Service.Ledger.IsKept(habit.Id, _c.Service.Today().Index));
                Assert.AreEqual(Mathf.Min(1f, i / 8f), _c.View.tileFill[DialView.TileIndex(0, 0, 6)], 0.0001f);
                Assert.AreEqual(i, Disk().Counts.Count);
            }
            Assert.AreEqual(1, Disk().Tends.Count);
            yield return Intent("Look", "plant.morning");
            StringAssert.Contains("kept 1 days this month", _c.View.ProfileLabel);
            // Undoing an extra unit leaves the day kept.
            yield return Intent("Pinch", "undo.morning");
            Assert.IsTrue(_c.Service.Ledger.IsKept(habit.Id, _c.Service.Today().Index));
            var reload = new SundialService(new FixedClock(Now, TimeZoneInfo.Utc), _dir);
            Assert.AreEqual(8, reload.Counts.Count(habit.Id, reload.Today().Index));
            Assert.AreEqual(1, reload.LiveCount(habit.Id));
        }

        [UnityTest, Timeout(180000)] public IEnumerator Quantity_Undo_ReversesTend()
        {
            yield return Boot();
            var habit = Plant("Read", LifeZone.Mind, "midday", 3);
            yield return Intent("Pinch", "plant.midday");
            yield return Intent("Pinch", "plant.midday");
            yield return Intent("Pinch", "plant.midday");
            Assert.IsTrue(_c.UndoVisible);
            yield return Intent("Pinch", "undo.midday");
            Assert.IsFalse(_c.Service.Ledger.IsKept(habit.Id, _c.Service.Today().Index));
            Assert.AreEqual(2, _c.Service.Counts.Count(habit.Id, _c.Service.Today().Index));
            Assert.AreEqual(2f / 3f, _c.View.tileFill[DialView.TileIndex(1, 0, 6)], 0.0001f);
            Assert.IsNotNull(Disk().Tends.Single().UndoneAtUtcMs);
            yield return Intent("Look", "plant.midday");
            StringAssert.Contains("kept 0 days this month", _c.View.ProfileLabel);
            Assert.AreEqual(0f, _c.View.MonthlyFill(LifeZone.Mind));
            yield return Intent("Pinch", "plant.midday");
            Assert.AreEqual(1, _c.Service.LiveCount(habit.Id));
            _c.Service.JumpTo(_c.Service.Clock.Now.AddSeconds(6.01));
            yield return null;
            Assert.IsFalse(_c.UndoVisible);
            yield return Intent("Pinch", "undo.midday");
            Assert.AreEqual(1, _c.Service.LiveCount(habit.Id));
            // Pausing flushes presentation while the already saved count survives.
            _c.Service.Flush();
            var reload = new SundialService(new FixedClock(Now, TimeZoneInfo.Utc), _dir);
            Assert.AreEqual(3, reload.Counts.Count(habit.Id, reload.Today().Index));
            Assert.AreEqual(1, reload.LiveCount(habit.Id));
        }

        [UnityTest, Timeout(180000)] public IEnumerator NoConsecutiveCountString()
        {
            yield return Boot();
            Plant("Water", LifeZone.Body, "morning", 8);
            yield return Intent("Look", "plant.morning");
            const string forbidden = @"\bstreak\w*\b|\bin a row\b|\bconsecutive\b|\bdays straight\b|\bstraight days\b|\bfail\w*\b|\bsham\w*\b|\bwilt\w*\b";
            var hits = new System.Collections.Generic.List<string>();
            // Gate U5 grep of shipped script string literals. Identifiers/comments are not UI strings.
            foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories))
            foreach (Match literal in Regex.Matches(File.ReadAllText(file), "\"(?:\\\\.|[^\"\\\\])*\""))
                if (Regex.IsMatch(literal.Value, forbidden, RegexOptions.IgnoreCase)) hits.Add(file + ": " + literal.Value);
            foreach (var label in _c.GetComponentsInChildren<TextMesh>(true))
                if (Regex.IsMatch(label.text, forbidden, RegexOptions.IgnoreCase)) hits.Add(label.name + ": " + label.text);
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Evidence, "grep-u5.txt"), "Runtime script string literals and instantiated dial labels\nPattern: " + forbidden + "\nHits: " + hits.Count + "\n" + string.Join("\n", hits));
            Assert.IsEmpty(hits);
            StringAssert.DoesNotContain("\u2014", _c.View.ProfileLabel);
        }
    }
}
