# Garden VR Audio (`com.gardenvr.audio`)

Cue service shared by Terrarium and Sundial. App code plays a cue id. It never holds an `AudioClip`.

`CueManifest` reads `apps/<app>/Assets/Audio/cues.json` (see `docs/audio/CHOICES.md`). `AudioCueService.Play(id, at, pitchSemitones)` applies the rules in `docs/audio/AUDIO-BIBLE.md` section 2: eight voices, lowest band yields, round-robin variants, no identical clip within 3 seconds, cooldowns drop, voice ducks music 6 dB and ambient 3 dB, one voice line at a time, mute persists and outranks everything. A missing clip logs `PLACEHOLDER cue <id>` once and stays silent.

`CueLog` appends `{t, cue, clip, bus, gainDb, pos}` in development builds and tests. `tools/audio/mixdown.mjs` renders that log offline.

Owner: terrarium. Sundial consumes the same package.
