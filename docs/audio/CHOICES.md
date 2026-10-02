# Audio choices (owner, 2026-10-02) - these outrank AUDIO-BIBLE.md where they differ

| Decision | Choice | Source sample | Note |
|---|---|---|---|
| Terrarium reward (`answer.chime`) | **E - two soft low harp plucks, long natural decay** | `audition/r2/ter-chime-E-harp.mp3` | variants must stay in this family; subtler than glass, kalimba or shimmer (all rejected) |
| Terrarium night bed | **E - warm bowed low strings + a tender celesta melody that repeats and evolves, distant glass tones, wide reverb, hypnotic, no beat** | `audition/r2/ter-music-E-celesta-strings.mp3` | **overrides the bible's "no melody, unpulsed" rule**: the owner wants a galactic/hypnotic direction *with* a melody; round-1 strings+glass was "too monotone" |
| Sundial tend (`tend.tock`) | **C - a single soft low marimba note, warm and round** | `audition/sun-tock-C-marimba.mp3` | |
| Sundial beds | **D - a fingerpicked nylon guitar melody, simple and singable, warm soft Rhodes, distant glockenspiel echo, unhurried and optimistic, no drums** | `audition/r3/sun-music-D-nylon-guitar-sketchbook.mp3` | rejected: r1 A monotone without aim, r1 B too aggressive, r3 C; the three arc beds (morning/midday/dusk) are variations of D in one family |
| Narration voice (both apps) | **Lumi - `3Tw8C9N2okjMpXowdRPp`** (shared library, added to the account as "GV Lumi"), model `eleven_multilingual_v2`, stability 0.65, similarity 0.75, style 0.1 | `audition/r3/voice-lumi-*.mp3` | rejected: George, Lily, Brian, Victoria, Talia, Grace, Kimberly, Annabel |

## Manifest contract (shared by the audio agent, which writes it, and the cue service, which reads it)

`apps/<app>/Assets/Audio/cues.json`:

```json
{ "app": "terrarium", "version": 1,
  "cues": [ { "id": "answer.chime", "clips": ["Sfx/answer.chime.v1.mp3", "Sfx/answer.chime.v2.mp3"],
              "bus": "reward", "space": "3d", "priorityBand": 1, "maxConcurrent": 1, "cooldownS": 0,
              "pitchJitter": 0.03, "gainDb": 0, "loop": false, "placeholder": false } ] }
```

Buses and bands: `voice` 0, `reward` 1, `interaction` 2, `ambient` 3, `music` 4. Paths are relative to `Assets/Audio/`.
Loudness is baked into the files (section 5 of the bible), so `gainDb` is a trim, normally 0.

`apps/<app>/Assets/Audio/Voice/lines.json`:

```json
{ "voice_id": "3Tw8C9N2okjMpXowdRPp", "model": "eleven_multilingual_v2", "settings": {"stability":0.65,"similarity":0.75,"style":0.1},
  "lines": [ { "id": "vo.ter.in.01", "text": "...", "trigger": "PinchHold", "window_s": 1.8,
               "file": "Voice/vo.ter.in.01.mp3", "measured_s": 1.62, "take": 2 } ] }
```
