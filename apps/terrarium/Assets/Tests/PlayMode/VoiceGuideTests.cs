using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GardenVR.Audio;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GardenVR.Terrarium.Tests
{
    public class VoiceGuidePolicyTests
    {
        readonly List<GameObject> _owned = new List<GameObject>();
        int _savedMute;

        [SetUp]
        public void SetUp()
        {
            AudioCueService.SuppressAutoLoad = true;
            AudioCueService.LogPathOverride = null;
            _savedMute = PlayerPrefs.GetInt(AudioCueService.MuteKey, 0);
            AudioCueService.TestingResetMute();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
            AudioCueService.SuppressAutoLoad = false;
            AudioCueService.LogPathOverride = null;
            AudioCueService.TestingRestoreMute(_savedMute);
        }

        [Test]
        public void VoiceGuide_NeverLeadsTheHold()
        {
            AudioCueService svc = NewService(1.5f);
            var guide = new VoiceGuide(svc, null, null) { Enabled = true };

            svc.Advance(1.2f);
            guide.OnBreathCounted(1);
            guide.OnBreathCounted(2);
            Assert.AreEqual(0, guide.Started.Count, "a line started before any PinchHold");

            float openedAt = svc.Now;
            guide.OnIntent(Hold(), 0, 6, false, false);
            Assert.AreEqual(1, guide.Started.Count);
            Assert.GreaterOrEqual(guide.StartedAt[0], openedAt);
            Assert.AreEqual("vo.ter.open.01", guide.Started[0]);
            guide.OnIntent(Hold(), 0, 6, false, false);
            Assert.AreEqual(1, guide.Started.Count, "a second in-line started during the same hold");

            guide.OnIntent(LetGo(), 0, 6, false, false);
            svc.Advance(2f);

            float inhaleAt = svc.Now;
            guide.OnIntent(Hold(), 1, 6, false, false);
            string inLine = null;
            float inAt = -1f;
            for (int i = 0; i < guide.Started.Count; i++)
            {
                if (!guide.Started[i].StartsWith("vo.ter.in.")) continue;
                Assert.GreaterOrEqual(guide.StartedAt[i], inhaleAt, guide.Started[i] + " led the hold");
                inLine = guide.Started[i];
                inAt = guide.StartedAt[i];
            }
            Assert.AreEqual("vo.ter.in.01", inLine);
            Assert.GreaterOrEqual(inAt, inhaleAt);

            var back = new VoiceGuide(svc, null, null) { Enabled = true };
            Assert.AreEqual(0, back.Started.Count);
            back.OnIntent(Hold(), 0, 6, true, false);
            Assert.AreEqual(VoiceGuide.ReturnLine, back.Started[0]);
            Assert.IsFalse(back.Started[0].StartsWith("vo.ter.in."));

            var paused = new VoiceGuide(svc, null, null) { Enabled = true };
            paused.OnIntent(Hold(), 1, 6, false, true);
            Assert.AreEqual(VoiceGuide.ResumeLine, paused.Started[0]);
            Assert.IsFalse(paused.Started[0].StartsWith("vo.ter.in."));
        }

        [Test]
        public void VoiceGuide_CutsWhenUserIsFaster()
        {
            AudioCueService svc = NewService(2f);
            var guide = new VoiceGuide(svc, null, null) { Enabled = true };
            guide.OnIntent(Hold(), 0, 6, false, false);
            Assert.IsTrue(svc.IsPlaying("vo.ter.open.01"));
            svc.Advance(0.2f);
            guide.OnIntent(LetGo(), 0, 6, false, false);

            Assert.IsFalse(svc.IsPlaying("vo.ter.open.01"), "the line kept going after the release");
            Assert.IsTrue(svc.IsPlaying("vo.ter.out.01"), "the next line waited instead of starting");
            Assert.Greater(svc.FadingCount, 0, "the cut did not fade");
            Assert.AreEqual(0.2f, guide.StartedAt[1] - guide.StartedAt[0], 0.02f);

            svc.Advance(VoiceGuide.CutFadeSeconds);
            Assert.AreEqual(0, svc.FadingCount);
            svc.Advance(3f);
            Assert.AreEqual(2, guide.Started.Count, "a line was queued behind the one that was cut");
        }

        [Test]
        public void Bed_DucksUnderVoice()
        {
            AudioCueService svc = NewService(2f);
            Assert.IsTrue(svc.Play("bed.night"));
            Assert.AreEqual(0f, svc.DuckDb("music"), 0.001f);
            Assert.IsTrue(svc.IsPlaying("bed.night"));

            var guide = new VoiceGuide(svc, null, null) { Enabled = true };
            guide.OnIntent(Hold(), 0, 6, false, false);
            Assert.IsTrue(svc.IsPlaying("vo.ter.open.01"));
            svc.Advance(AudioCueService.DuckAttack);
            Assert.AreEqual(AudioCueService.MusicDuckDb, svc.DuckDb("music"), 0.02f);
            Assert.AreEqual(0f, svc.DuckDb("voice"), 0.001f);
            Assert.IsTrue(svc.IsPlaying("bed.night"));

            svc.Stop("vo.ter.open.01");
            Assert.AreEqual(AudioCueService.MusicDuckDb, svc.DuckDb("music"), 0.02f);
            svc.Advance(AudioCueService.DuckRelease);
            Assert.AreEqual(0f, svc.DuckDb("music"), 0.02f);
            Assert.IsTrue(svc.IsPlaying("bed.night"));
        }

        [Test]
        public void VoiceCatalog_MatchesTheFrozenLines()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Audio", "Voice", "lines.json"));
            string[] ids =
            {
                "vo.ter.open.01", "vo.ter.open.02", "vo.ter.in.01", "vo.ter.in.02", "vo.ter.in.03",
                "vo.ter.out.01", "vo.ter.out.02", "vo.ter.out.03", "vo.ter.mid.01", "vo.ter.last.01",
                "vo.ter.close.01", "vo.ter.close.02", "vo.ter.return.01", "vo.ter.resume.01"
            };
            for (int i = 0; i < ids.Length; i++)
            {
                string text = VoiceGuide.LineText(ids[i]);
                Assert.IsNotNull(text, ids[i]);
                StringAssert.Contains(text, json, ids[i]);
                Assert.IsFalse(text.IndexOf('\u2014') >= 0, ids[i]);
            }
        }

        AudioCueService NewService(float seconds)
        {
            var go = new GameObject("voice-guide-test");
            _owned.Add(go);
            var svc = go.AddComponent<AudioCueService>();
            svc.ManualClock = true;
            svc.UseSyntheticClips(seconds);
            svc.LoadManifest(Manifest());
            svc.VoiceGuide = true;
            svc.Beds = true;
            return svc;
        }

        static string Manifest()
        {
            string[] voice =
            {
                "vo.ter.open.01", "vo.ter.open.02", "vo.ter.in.01", "vo.ter.in.02", "vo.ter.in.03",
                "vo.ter.out.01", "vo.ter.out.02", "vo.ter.out.03", "vo.ter.mid.01", "vo.ter.last.01",
                "vo.ter.close.01", "vo.ter.close.02", "vo.ter.return.01", "vo.ter.resume.01"
            };
            var sb = new StringBuilder();
            sb.Append("{\"app\":\"test\",\"version\":1,\"cues\":[");
            sb.Append(Cue("bed.night", "music", 4, true, "Music/bed.night.ogg"));
            for (int i = 0; i < voice.Length; i++)
            {
                sb.Append(',');
                sb.Append(Cue(voice[i], "voice", 0, false, "Voice/" + voice[i] + ".mp3"));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string Cue(string id, string bus, int band, bool loop, string clip)
        {
            return "{\"id\":\"" + id + "\",\"clips\":[\"" + clip + "\"],\"bus\":\"" + bus
                + "\",\"space\":\"2d\",\"priorityBand\":" + band
                + ",\"maxConcurrent\":1,\"cooldownS\":0,\"pitchJitter\":0,\"gainDb\":0,\"loop\":"
                + (loop ? "true" : "false") + ",\"placeholder\":false}";
        }

        static HandIntent Hold()
        {
            return new HandIntent(HandIntentKind.PinchHold, new Ray(Vector3.zero, Vector3.forward), 1f, 0f, true, "jar");
        }

        static HandIntent LetGo()
        {
            return new HandIntent(HandIntentKind.Release, new Ray(Vector3.zero, Vector3.forward), 0.2f, 0.2f, true, "jar");
        }
    }

    public class VoiceShellTests
    {
        float _savedDelta;

        [SetUp]
        public void SaveStep()
        {
            _savedDelta = Time.captureDeltaTime;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.captureDeltaTime = _savedDelta;
            Scene scene = SceneManager.GetSceneByName("Main");
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
            RitualHarness.ReleaseOverrides();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator VoiceShell_AfterFirstAnswer_TogglesAndRemembers()
        {
            yield return RitualHarness.OpenMain();
            JarRitualController controller = RitualHarness.Controller();
            Assert.IsFalse(controller.Service.Settings.VoiceGuide);
            Assert.IsFalse(controller.Service.Settings.NightBed);
            Assert.IsFalse(controller.VoiceShellVisible);
            RitualHarness.Play(controller, RitualScripts.SixShort());
            RitualHarness.FixedStep();

            float app = 0f;
            while (app < 40f && !controller.VoiceShellVisible)
            {
                yield return null;
                app += Time.deltaTime;
            }

            Assert.IsTrue(controller.HasAnswer, "the first ritual did not answer");
            Assert.IsTrue(controller.VoiceShellVisible, "the voice shell did not appear after the first answer");
            Assert.AreEqual("A voice can follow your breath.", controller.VoiceShellLabel);

            RitualHarness.Play(controller,
                "{\"t\":0.05,\"intent\":\"Poke\",\"target\":\"shell.voice\"}\n{\"t\":0.05,\"intent\":\"Look\",\"dur\":0.4}\n");
            float poke = 0f;
            while (poke < 2f && !controller.Service.Settings.VoiceGuide)
            {
                yield return null;
                poke += Time.deltaTime;
            }

            Assert.IsTrue(controller.Service.Settings.VoiceGuide, "poking the shell did not turn the guide on");
            Assert.AreEqual("Voice guide on.", controller.VoiceShellLabel);

            yield return RitualHarness.OpenMain();
            controller = RitualHarness.Controller();
            Assert.IsTrue(controller.Service.Settings.VoiceGuide, "the guide was not remembered");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(controller.VoiceShellVisible);
            Assert.AreEqual("Voice guide on.", controller.VoiceShellLabel);
        }
    }
}
