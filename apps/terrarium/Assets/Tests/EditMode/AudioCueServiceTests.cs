using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using GardenVR.Audio;
using NUnit.Framework;
using UnityEngine;

namespace GardenVR.Terrarium.Tests
{
    public class AudioCueServiceTests
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
        public void Manifest_ListsEveryTerrariumCue_AndClipsExist()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Audio", "cues.json"));
            CueManifest manifest = CueManifest.Parse(json);
            string[] ids =
            {
                "amb.room", "jar.land", "jar.lid", "breath.exhale.end", "fog.hiss", "answer.chime",
                "dew.drop", "moss.ripple", "seed.appear", "habit.pluck", "habit.undo", "pause.hold",
                "lookback.shimmer", "pebble.tap", "bed.night",
                "vo.ter.open.01", "vo.ter.open.02", "vo.ter.in.01", "vo.ter.in.02", "vo.ter.in.03",
                "vo.ter.out.01", "vo.ter.out.02", "vo.ter.out.03", "vo.ter.mid.01", "vo.ter.last.01",
                "vo.ter.close.01", "vo.ter.close.02", "vo.ter.return.01", "vo.ter.resume.01"
            };
            for (int i = 0; i < ids.Length; i++)
                Assert.IsTrue(manifest.Contains(ids[i]), "missing cue " + ids[i]);

            string root = Path.Combine(Application.dataPath, "Audio");
            for (int i = 0; i < manifest.cues.Length; i++)
            {
                CueDefinition cue = manifest.cues[i];
                if (cue.placeholder) continue;
                Assert.IsFalse(cue.MissingClip, cue.id + " has no clip");
                for (int c = 0; c < cue.clips.Length; c++)
                {
                    string file = Path.Combine(root, cue.clips[c].Replace('/', Path.DirectorySeparatorChar));
                    Assert.IsTrue(File.Exists(file), cue.id + " missing " + cue.clips[c]);
                }
            }
        }

        [Test]
        public void Scripts_OnlyPlayCueIdsThatExist()
        {
            CueManifest manifest = CueManifest.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Audio", "cues.json")));
            var found = new HashSet<string>();
            var regex = new Regex("Play\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);
            string[] files = Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                MatchCollection matches = regex.Matches(text);
                for (int m = 0; m < matches.Count; m++)
                {
                    string id = matches[m].Groups[1].Value;
                    found.Add(id);
                    Assert.IsTrue(manifest.Contains(id), "Play id is not in the manifest: " + id + " in " + files[i]);
                    StringAssert.DoesNotContain("miss", id);
                }
            }

            string[] wired =
            {
                "amb.room", "jar.land", "jar.lid", "breath.exhale.end", "fog.hiss", "answer.chime",
                "dew.drop", "moss.ripple", "seed.appear", "habit.pluck", "habit.undo", "pause.hold", "pebble.tap"
            };
            for (int i = 0; i < wired.Length; i++)
                Assert.IsTrue(found.Contains(wired[i]), "ritual does not play " + wired[i]);
        }

        [Test]
        public void Cooldown_DropsUntilTheWindowPasses()
        {
            AudioCueService svc = New(Join(One("tick", "interaction", 2, 1f, false, "Sfx/a.mp3", "Sfx/b.mp3")));
            Assert.IsTrue(svc.Play("tick"));
            Assert.AreEqual("Sfx/a.mp3", svc.LastClip);
            Assert.IsFalse(svc.Play("tick"));
            Assert.AreEqual(1, svc.ActiveVoices);
            svc.Advance(0.99f);
            Assert.IsFalse(svc.Play("tick"));
            svc.Advance(0.01f);
            Assert.IsTrue(svc.Play("tick"));
            Assert.AreEqual("Sfx/b.mp3", svc.LastClip);
        }

        [Test]
        public void Duck_AttacksWhileVoicePlays_AndReleasesAfter()
        {
            AudioCueService svc = New(Join(
                One("bed", "music", 4, 0f, true, "Music/bed.mp3"),
                One("air", "ambient", 3, 0f, true, "Sfx/air.mp3"),
                One("line", "voice", 0, 0f, false, "Voice/line.mp3")));
            Assert.IsTrue(svc.Play("bed"));
            Assert.IsTrue(svc.Play("air"));
            Assert.AreEqual(0f, svc.DuckDb("music"), 0.001f);
            Assert.IsTrue(svc.Play("line"));
            svc.Advance(AudioCueService.DuckAttack);
            Assert.AreEqual(AudioCueService.MusicDuckDb, svc.DuckDb("music"), 0.02f);
            Assert.AreEqual(AudioCueService.AmbientDuckDb, svc.DuckDb("ambient"), 0.02f);
            Assert.AreEqual(0f, svc.DuckDb("interaction"), 0.001f);
            svc.Stop("line");
            Assert.AreEqual(AudioCueService.MusicDuckDb, svc.DuckDb("music"), 0.02f);
            svc.Advance(AudioCueService.DuckRelease);
            Assert.AreEqual(0f, svc.DuckDb("music"), 0.02f);
            Assert.AreEqual(0f, svc.DuckDb("ambient"), 0.02f);
            Assert.IsFalse(svc.IsPlaying("line"));
            Assert.IsTrue(svc.IsPlaying("bed"));
        }

        [Test]
        public void VoiceLine_ReplacesTheOneAlreadyPlaying()
        {
            AudioCueService svc = New(Join(
                One("vo.a", "voice", 0, 0f, false, "Voice/a.mp3"),
                One("vo.b", "voice", 0, 0f, false, "Voice/b.mp3")));
            Assert.IsTrue(svc.Play("vo.a"));
            Assert.IsTrue(svc.Play("vo.b"));
            Assert.IsFalse(svc.IsPlaying("vo.a"));
            Assert.IsTrue(svc.IsPlaying("vo.b"));
            Assert.AreEqual(1, svc.ActiveVoices);
        }

        [Test]
        public void Variants_DoNotRepeatTheSameClipWithinThreeSeconds()
        {
            AudioCueService svc = New(Join(One("pluck", "interaction", 2, 0f, false, "Sfx/a.mp3", "Sfx/b.mp3", "Sfx/c.mp3")));
            Assert.IsTrue(svc.Play("pluck"));
            Assert.AreEqual("Sfx/a.mp3", svc.LastClip);
            Assert.IsTrue(svc.Play("pluck"));
            Assert.AreEqual("Sfx/b.mp3", svc.LastClip);
            Assert.IsTrue(svc.Play("pluck"));
            Assert.AreEqual("Sfx/c.mp3", svc.LastClip);
            Assert.IsFalse(svc.Play("pluck"));
            svc.Advance(AudioCueService.IdenticalWindow);
            Assert.IsTrue(svc.Play("pluck"));
            Assert.AreEqual("Sfx/a.mp3", svc.LastClip);
        }

        [Test]
        public void Mute_OutranksPlayback_AndPersists()
        {
            AudioCueService svc = New(Join(One("bed", "ambient", 3, 0f, true, "Sfx/bed.mp3")));
            Assert.IsTrue(svc.Play("bed"));
            svc.Mute = true;
            Assert.AreEqual(0, svc.ActiveVoices);
            Assert.IsFalse(svc.Play("bed"));
            AudioCueService again = New(Join(One("bed", "ambient", 3, 0f, true, "Sfx/bed.mp3")));
            Assert.IsTrue(again.Mute);
            Assert.IsFalse(again.Play("bed"));
            again.Mute = false;
            Assert.IsTrue(again.Play("bed"));
            Assert.AreEqual(1, again.ActiveVoices);
        }

        [Test]
        public void Placeholder_IsSilent_AndLoggedOnce()
        {
            AudioCueService svc = New(Join(One("gone", "interaction", 2, 0f, false)));
            Assert.IsFalse(svc.Play("gone"));
            Assert.IsTrue(svc.Announced("gone"));
            Assert.AreEqual(0, svc.ActiveVoices);
            int announced = svc.PlaceholderAnnouncements;
            Assert.IsFalse(svc.Play("gone"));
            Assert.AreEqual(announced, svc.PlaceholderAnnouncements);
            Assert.IsFalse(svc.Play("no.such"));
            Assert.IsTrue(svc.Announced("no.such"));
        }

        [Test]
        public void FullPool_YieldsTheLowestBandFirst()
        {
            var parts = new string[10];
            for (int i = 0; i < 8; i++)
                parts[i] = One("pad" + i, "music", 4, 0f, true, "Music/pad" + i + ".mp3");
            parts[8] = One("line", "voice", 0, 0f, false, "Voice/line.mp3");
            parts[9] = One("extra", "music", 4, 0f, true, "Music/extra.mp3");
            AudioCueService svc = New(Join(parts));
            for (int i = 0; i < 8; i++)
                Assert.IsTrue(svc.Play("pad" + i), "pad " + i);
            Assert.AreEqual(AudioCueService.MaxVoices, svc.ActiveVoices);
            Assert.IsTrue(svc.Play("line"));
            Assert.AreEqual(AudioCueService.MaxVoices, svc.ActiveVoices);
            Assert.IsTrue(svc.IsPlaying("line"));
            int pads = 0;
            for (int i = 0; i < 8; i++)
            {
                if (svc.IsPlaying("pad" + i)) pads++;
            }
            Assert.AreEqual(7, pads);
            Assert.IsFalse(svc.Play("extra"));
        }

        AudioCueService New(string json)
        {
            var go = new GameObject("cue-test");
            _owned.Add(go);
            var svc = go.AddComponent<AudioCueService>();
            svc.UseSyntheticClips(5f);
            svc.LoadManifest(json);
            return svc;
        }

        static string Join(params string[] cues)
        {
            var sb = new StringBuilder();
            sb.Append("{\"app\":\"test\",\"version\":1,\"cues\":[");
            for (int i = 0; i < cues.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(cues[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string One(string id, string bus, int band, float cooldown, bool loop, params string[] clips)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(id).Append("\",\"clips\":[");
            for (int i = 0; i < clips.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(clips[i]).Append('"');
            }
            sb.Append("],\"bus\":\"").Append(bus);
            sb.Append("\",\"space\":\"2d\",\"priorityBand\":").Append(band);
            sb.Append(",\"maxConcurrent\":8,\"cooldownS\":").Append(cooldown.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"pitchJitter\":0,\"gainDb\":0,\"loop\":").Append(loop ? "true" : "false");
            sb.Append(",\"placeholder\":").Append(clips.Length == 0 ? "true" : "false").Append('}');
            return sb.ToString();
        }
    }
}
