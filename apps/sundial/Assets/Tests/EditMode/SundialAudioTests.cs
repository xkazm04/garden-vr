using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GardenVR.Audio;
using GardenVR.Core;
using GardenVR.Input;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Sundial.Tests.EditMode
{
    public class SundialAudioTests
    {
        readonly List<GameObject> _owned = new List<GameObject>();
        int _savedMute;

        static readonly string[] Required =
        {
            "amb.kitchen", "dial.appear", "tend.tock", "tile.ink", "tend.undo", "tile.hatch",
            "packet.open", "seed.drop", "bloom.flutter", "dusk.chime", "tab.tap",
            "bed.morning", "bed.midday", "bed.dusk",
            "vo.sun.open.01", "vo.sun.in.01", "vo.sun.in.02", "vo.sun.out.01", "vo.sun.out.02",
            "vo.sun.last.01", "vo.sun.close.01", "vo.sun.first.01"
        };

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
        public void Manifest_ListsEverySundialCue_AndClipsExist()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Audio", "cues.json"));
            CueManifest manifest = CueManifest.Parse(json);
            for (int i = 0; i < Required.Length; i++)
            {
                Assert.IsTrue(manifest.Contains(Required[i]), "missing cue " + Required[i]);
                CueDefinition cue;
                Assert.IsTrue(manifest.TryGet(Required[i], out cue));
                Assert.IsFalse(cue.placeholder, Required[i] + " is still a placeholder");
                Assert.IsFalse(cue.MissingClip, Required[i] + " has no clip");
            }

            string root = Path.Combine(Application.dataPath, "Audio");
            for (int i = 0; i < manifest.cues.Length; i++)
            {
                CueDefinition cue = manifest.cues[i];
                if (cue.placeholder) continue;
                for (int c = 0; c < cue.clips.Length; c++)
                {
                    string file = Path.Combine(root, cue.clips[c].Replace('/', Path.DirectorySeparatorChar));
                    Assert.IsTrue(File.Exists(file), cue.id + " missing " + cue.clips[c]);
                }
            }
        }

        [Test]
        public void Scripts_OnlyCallCueIdsThatExist()
        {
            CueManifest manifest = CueManifest.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Audio", "cues.json")));
            var regex = new Regex(
                "\"((?:amb\\.kitchen|dial\\.appear|tend\\.tock|tend\\.undo|tile\\.ink|tile\\.hatch|packet\\.open|seed\\.drop|bloom\\.flutter|dusk\\.chime|tab\\.tap|bed\\.morning|bed\\.midday|bed\\.dusk|vo\\.sun\\.[a-z0-9.]+))\"",
                RegexOptions.Compiled);
            var found = new HashSet<string>();
            string[] files = Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                MatchCollection matches = regex.Matches(File.ReadAllText(files[i]));
                for (int m = 0; m < matches.Count; m++)
                {
                    string id = matches[m].Groups[1].Value;
                    found.Add(id);
                    Assert.IsTrue(manifest.Contains(id), "cue id is not in the manifest: " + id + " in " + files[i]);
                }
            }

            for (int i = 0; i < Required.Length; i++)
                Assert.IsTrue(found.Contains(Required[i]), "Sundial scripts do not call " + Required[i]);
        }

        [Test]
        public void Beds_CrossfadeOnArcChange()
        {
            Assert.AreEqual(4f, ArcBeds.CrossfadeSeconds, 0.001f);
            AudioCueService svc = NewService(30f, BedManifest());
            svc.Beds = true;
            var beds = new ArcBeds();

            Assert.IsNull(beds.Sync(svc, false, ArcId.Morning));
            Assert.IsFalse(svc.IsPlaying(ArcBeds.Morning));
            Assert.AreEqual(0, svc.ActiveVoices, "a bed started while Beds was off");

            Assert.AreEqual(ArcBeds.Morning, beds.Sync(svc, true, ArcId.Morning));
            Assert.IsTrue(svc.Play(ArcBeds.Morning));
            Assert.IsTrue(svc.IsPlaying(ArcBeds.Morning));
            Assert.AreEqual(0, svc.FadingCount);
            Assert.IsNull(beds.Sync(svc, true, ArcId.Morning), "the same arc restarted the bed");

            Assert.AreEqual(ArcBeds.Midday, beds.Sync(svc, true, ArcId.Midday));
            Assert.IsFalse(svc.IsPlaying(ArcBeds.Morning), "the morning bed was still a playing voice");
            Assert.Greater(svc.FadingCount, 0, "the morning bed was cut instead of faded");
            Assert.IsTrue(svc.Play(ArcBeds.Midday));
            Assert.IsTrue(svc.IsPlaying(ArcBeds.Midday));
            Assert.IsFalse(svc.IsPlaying(ArcBeds.Morning));

            svc.Advance(3.9f);
            Assert.Greater(svc.FadingCount, 0, "the morning bed ended before 4 s");
            svc.Advance(0.2f);
            Assert.AreEqual(0, svc.FadingCount, "the crossfade ran past 4 s");
            Assert.IsTrue(svc.IsPlaying(ArcBeds.Midday));

            Assert.AreEqual(ArcBeds.Dusk, beds.Sync(svc, true, ArcId.WindDown));
            Assert.IsTrue(svc.Play(ArcBeds.Dusk));
            Assert.IsFalse(svc.IsPlaying(ArcBeds.Midday));
            Assert.IsTrue(svc.IsPlaying(ArcBeds.Dusk));

            Assert.IsNull(beds.Sync(svc, false, ArcId.WindDown));
            Assert.IsFalse(svc.IsPlaying(ArcBeds.Dusk), "turning beds off left the dusk bed playing");
            Assert.Greater(svc.FadingCount, 0);
        }

        [Test]
        public void Voice_NeverLeadsTheHold()
        {
            AudioCueService svc = NewService(2f, VoiceManifest());
            var guide = new SundialVoice(svc, null, null) { Enabled = true };

            svc.Advance(1.2f);
            guide.Tick(1.2f);
            Assert.AreEqual(0, guide.Started.Count, "a line started before any PinchHold");

            float openedAt = svc.Now;
            guide.OnIntent(Hold(), 0, 3);
            Assert.AreEqual(1, guide.Started.Count);
            Assert.GreaterOrEqual(guide.StartedAt[0], openedAt);
            Assert.AreEqual(SundialVoice.OpenLine, guide.Started[0]);
            guide.OnIntent(Hold(), 0, 3);
            Assert.AreEqual(1, guide.Started.Count, "a second line started during the same hold");

            guide.OnIntent(LetGo(), 0, 3);
            svc.Advance(2f);

            float inhaleAt = svc.Now;
            guide.OnIntent(Hold(), 1, 3);
            string inLine = null;
            float inAt = -1f;
            for (int i = 0; i < guide.Started.Count; i++)
            {
                if (guide.Started[i].IndexOf(".in.") < 0) continue;
                Assert.GreaterOrEqual(guide.StartedAt[i], inhaleAt, guide.Started[i] + " led the hold");
                inLine = guide.Started[i];
                inAt = guide.StartedAt[i];
            }
            Assert.AreEqual("vo.sun.in.01", inLine);
            Assert.GreaterOrEqual(inAt, inhaleAt);

            guide.OnIntent(LetGo(), 1, 3);
            guide.OnIntent(Hold(), 2, 3);
            Assert.AreEqual(SundialVoice.LastLine, guide.Started[guide.Started.Count - 1]);
            Assert.GreaterOrEqual(guide.StartedAt[guide.StartedAt.Count - 1], inhaleAt);
        }

        [Test]
        public void Voice_CutsWhenFaster()
        {
            AudioCueService svc = NewService(2f, VoiceManifest());
            var guide = new SundialVoice(svc, null, null) { Enabled = true };
            guide.OnIntent(Hold(), 0, 3);
            Assert.IsTrue(svc.IsPlaying(SundialVoice.OpenLine));
            svc.Advance(0.2f);
            guide.OnIntent(LetGo(), 0, 3);

            Assert.IsFalse(svc.IsPlaying(SundialVoice.OpenLine), "the line kept going after the release");
            Assert.IsTrue(svc.IsPlaying("vo.sun.out.01"), "the next line waited instead of starting");
            Assert.Greater(svc.FadingCount, 0, "the cut did not fade");
            Assert.AreEqual(0.2f, guide.StartedAt[1] - guide.StartedAt[0], 0.02f);

            svc.Advance(SundialVoice.CutFadeSeconds);
            Assert.AreEqual(0, svc.FadingCount);
            svc.Advance(3f);
            Assert.AreEqual(2, guide.Started.Count, "a line was queued behind the one that was cut");
        }

        [Test]
        public void VoiceCatalog_MatchesTheFrozenLines()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Audio", "Voice", "lines.json"));
            string[] banned = { "stress", "anxiety", "therapy", "heal" };
            for (int i = 0; i < Required.Length; i++)
            {
                if (Required[i].IndexOf("vo.sun.") != 0) continue;
                string text = SundialVoice.LineText(Required[i]);
                Assert.IsFalse(string.IsNullOrEmpty(text), Required[i]);
                StringAssert.Contains(text, json, Required[i]);
                Assert.IsFalse(text.IndexOf('\u2014') >= 0, Required[i]);
                for (int b = 0; b < banned.Length; b++)
                    Assert.IsFalse(text.ToLowerInvariant().Contains(banned[b]), Required[i] + " " + banned[b]);
                if (Required[i].IndexOf(".in.") >= 0 || Required[i] == SundialVoice.LastLine)
                    Assert.AreEqual(1.8f, SundialVoice.WindowOf(Required[i]), 0.001f);
                else if (Required[i].IndexOf(".out.") >= 0)
                    Assert.AreEqual(2.5f, SundialVoice.WindowOf(Required[i]), 0.001f);
                else
                    Assert.AreEqual(4f, SundialVoice.WindowOf(Required[i]), 0.001f);
            }
        }

        AudioCueService NewService(float seconds, string manifest)
        {
            var go = new GameObject("sundial-audio-test");
            _owned.Add(go);
            var svc = go.AddComponent<AudioCueService>();
            svc.ManualClock = true;
            svc.UseSyntheticClips(seconds);
            svc.LoadManifest(manifest);
            svc.VoiceGuide = true;
            svc.Beds = false;
            return svc;
        }

        static string BedManifest()
        {
            return "{\"app\":\"test\",\"version\":1,\"cues\":["
                + Cue(ArcBeds.Morning, "music", 4, true, "Music/bed.morning.ogg") + ","
                + Cue(ArcBeds.Midday, "music", 4, true, "Music/bed.midday.ogg") + ","
                + Cue(ArcBeds.Dusk, "music", 4, true, "Music/bed.dusk.ogg")
                + "]}";
        }

        static string VoiceManifest()
        {
            string[] voice =
            {
                SundialVoice.OpenLine, "vo.sun.in.01", "vo.sun.in.02",
                "vo.sun.out.01", "vo.sun.out.02", SundialVoice.LastLine,
                SundialVoice.CloseLine, SundialVoice.FirstLine
            };
            var parts = new System.Text.StringBuilder();
            parts.Append("{\"app\":\"test\",\"version\":1,\"cues\":[");
            for (int i = 0; i < voice.Length; i++)
            {
                if (i > 0) parts.Append(',');
                parts.Append(Cue(voice[i], "voice", 0, false, "Voice/" + voice[i] + ".mp3"));
            }
            parts.Append("]}");
            return parts.ToString();
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
            return new HandIntent(HandIntentKind.PinchHold, new Ray(Vector3.zero, Vector3.forward), 1f, 0f, true, "plant.winddown");
        }

        static HandIntent LetGo()
        {
            return new HandIntent(HandIntentKind.Release, new Ray(Vector3.zero, Vector3.forward), 0.2f, 0.2f, true, "plant.winddown");
        }
    }
}
