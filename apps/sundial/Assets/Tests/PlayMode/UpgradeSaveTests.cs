using System;
using System.Collections;
using System.IO;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    public class UpgradeSaveTests
    {
        UnityAction<Scene, LoadSceneMode> _onLoaded;

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (_onLoaded != null) SceneManager.sceneLoaded -= _onLoaded;
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null;
            SundialController.ClockOverride = null;
            SundialService.DevSeedOnFresh = true;
        }

        [UnityTest]
        public IEnumerator Schema1_LaunchMigratesAndPersistsVisitAndKey()
        {
            string dir = SundialPlay.FreshDir();
            string live = Path.Combine(dir, "save.json");
            const string original = "{\"SchemaVersion\":1,\"Habits\":[],\"Tends\":[],\"Settings\":{\"Voice\":true},\"FirstRunStep\":\"done\"}";
            File.WriteAllText(live, original);
            SundialController controller = null;
            yield return Open(dir, c => controller = c);
            Assert.AreEqual(LoadOutcome.Migrated, controller.Outcome);
            Assert.AreEqual(original, File.ReadAllText(Path.Combine(dir, "save.snapshot.json")));
            var saved = SundialCodec.Read(Json.ParseObject(File.ReadAllText(live)));
            Assert.AreEqual(2, saved.SchemaVersion);
            Assert.AreEqual(controller.Service.Today().Index, saved.LastVisitDay);
            Assert.IsTrue(saved.Settings.Voice);
            Assert.IsFalse(saved.Settings.CoachOptIn);
            Assert.IsTrue(saved.Settings.Nudges);
            Assert.IsTrue(saved.Settings.BreakPoses);
            Assert.AreEqual(CatchUp.DefaultDepth, saved.Settings.CatchUpDepth);
            Assert.AreEqual(2, saved.Settings.NudgeWindows.Count);
            string key = saved.InstallKey;
            Assert.That(key, Does.Match("^[0-9a-f]{32}$"));
            yield return SundialPlay.Unload();
            yield return Open(dir, c => controller = c);
            Assert.AreEqual(LoadOutcome.Loaded, controller.Outcome);
            Assert.AreEqual(key, controller.Service.Save.InstallKey);
        }

        [UnityTest]
        public IEnumerator LaunchCatchUp_SeesPreviousVisit_AndDefersWritingUntilClosed()
        {
            string dir = SundialPlay.FreshDir();
            var seed = new SundialSave { LastVisitDay = 100, FirstRunStep = FirstRunSteps.Done };
            string live = Path.Combine(dir, "save.json");
            File.WriteAllText(live, Json.Write(SundialCodec.Write(seed)));
            Action complete = null;
            int? observedVisit = null;
            _onLoaded = (scene, mode) =>
            {
                if (scene.name != "Main") return;
                var controller = UnityEngine.Object.FindAnyObjectByType<SundialController>();
                if (controller == null)
                    controller = UnityEngine.Object.FindAnyObjectByType<DialView>().gameObject.AddComponent<SundialController>();
                Assert.IsNotNull(controller);
                controller.LaunchCatchUp += done =>
                {
                    observedVisit = controller.Service.Save.LastVisitDay;
                    complete = done;
                };
            };
            SceneManager.sceneLoaded += _onLoaded;
            SundialController active = null;
            yield return Open(dir, c => active = c);
            Assert.IsNotNull(complete, "Start must offer catch-up before updating the visit");
            Assert.AreEqual(100, observedVisit);
            Assert.AreEqual(100, SundialCodec.Read(Json.ParseObject(File.ReadAllText(live))).LastVisitDay);
            complete();
            Assert.AreEqual(active.Service.Today().Index, SundialCodec.Read(Json.ParseObject(File.ReadAllText(live))).LastVisitDay);
            Assert.AreEqual(seed.InstallKey, active.Service.Save.InstallKey);
        }

        static IEnumerator Open(string dir, Action<SundialController> ready)
        {
            yield return SundialPlay.Unload();
            SundialService.DevSeedOnFresh = false;
            SundialController.SaveDirectoryOverride = dir;
            SundialController.ClockOverride = SundialPlay.Clock1420();
            yield return SundialPlay.LoadMain(ready);
            yield return null;
        }
    }
}
