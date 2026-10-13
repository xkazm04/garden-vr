using System;
using System.Collections;
using System.IO;
using GardenVR.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GardenVR.Sundial.Tests.PlayMode
{
    // Presentation-only evidence. This does not claim to exercise a PC break controller.
    public class BreakViewTests
    {
        float _delta;
        [SetUp] public void Setup() { _delta = Time.captureDeltaTime; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SundialPlay.Unload();
            SundialController.SaveDirectoryOverride = null; SundialController.ClockOverride = null;
            Time.captureDeltaTime = _delta;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BreakView_PresentsPosesAndEyeRestAtDialG1()
        {
            SundialController c = null;
            yield return SundialPlay.Open(value => c = value);
            SundialPlay.FixedStep();
            var session = new BreakSession(true);
            var view = c.gameObject.AddComponent<BreakView>();
            string dir = SundialEvidence.Dir("T-SUN-063");
            Directory.CreateDirectory(dir);
            RecordTilesTests.DisableAsyncShaders();
            var cam = RecordTilesTests.PoseDial(); RecordTilesTests.AttachPlate(cam);
            yield return SundialPlay.Seconds(.2f);
            for (int i = 0; i < 3; i++) session.Update(0, new BreakSample(i, true, false, false, false));
            view.Show(session, false, true, null, c.Source);
            yield return null;
            RecordTilesTests.Shot(cam, Path.Combine(dir, "DialG1-break-palm-preview.png"));
            session.Update(3, new BreakSample(-1, false, true, false, false));
            view.Show(session, false, true, null, c.Source);
            yield return null;
            RecordTilesTests.Shot(cam, Path.Combine(dir, "DialG1-break-pinch-preview.png"));
            session.Update(3, new BreakSample(-1, false, false, true, false));
            session.Update(3, new BreakSample(-1, false, true, false, false));
            session.Update(7, new BreakSample(-1, false, false, false, true));
            Assert.AreEqual(13, session.Eyes.SecondsLeft);
            view.Show(session, false, true, null, c.Source);
            yield return null;
            RecordTilesTests.Shot(cam, Path.Combine(dir, "DialG1-break-eye-preview.png"));
            Assert.That(Vector3.Distance(cam.transform.position, view.EyeTarget.position), Is.EqualTo(3).Within(.001));
            Physics.SyncTransforms();
            var ray = new Ray(cam.transform.position, view.EyeTarget.position - cam.transform.position);
            Assert.IsTrue(Physics.Raycast(ray, out var hit));
            Assert.AreEqual("break.eye", hit.collider.name, "the room plate must not occlude the gaze target");
            File.WriteAllText(Path.Combine(dir, "view-preview.txt"), "Presentation-only preview using core samples, not PC input.\nEye distance=3m; SecondsLeft=13; ray hits break.eye\n");
        }
    }
}
