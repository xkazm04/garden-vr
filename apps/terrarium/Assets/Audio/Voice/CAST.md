# Cast - Terrarium narration

| | |
|---|---|
| Voice | Lumi (added to the account as "GV Lumi") |
| voice_id | `3Tw8C9N2okjMpXowdRPp` |
| Model | `eleven_multilingual_v2` (pinned) |
| Settings | stability 0.65, similarity 0.75, style 0.1, speaker boost on (the tool's default) |
| Chosen by | the owner, from the audition rounds 1 to 3 (`docs/audio/CHOICES.md`) |
| Rendered | 2026-10-02 by the audio agent through `tools/audio/elevenlabs.mjs tts`, 3 takes per line |

## Licence note

Lumi is a **shared voice-library voice** added to this ElevenLabs account, not a voice cloned or designed by this project.
Use of the rendered clips is governed by the ElevenLabs terms for the account's plan (tier "starter" at render time) and by
the voice owner's terms for shared library voices. The audio agent could not read those terms from here and did not
verify commercial-use or attribution conditions: **the owner must confirm them before a store release**. The rendered
clips are baked files; no API call is needed to play them.

## How the files were made

- Each line was rendered 3 times with the settings above. Trim: 50 ms of silence before and 250 ms after the speech
  (found by sample level, not by guess), mono. Loudness: two-pass ffmpeg loudnorm to -20 LUFS integrated, true peak
  <= -1.5 dBTP, verified on the shipped mp3 at 44.1 kHz.
- The kept take is the **median-duration take among those that fit the window** (an outlier take is the rushed or the
  dragged one). Nobody listened to the takes: selection is by measured duration and loudness only. The owner should
  listen first to vo.ter.in.02 (1.77 s of its 1.8 s window, the only take that fit) and to the whole set for pace.
- `measured_s` is the whole file (head and tail silence included), compared with `window_s`: the check is conservative,
  the speech itself ends about 0.25 s earlier.
- Copy rules held: no medical or therapeutic words, no promises, no em dash.

## Lines

| id | text | window s | measured s | take | LUFS | dBTP |
|---|---|---|---|---|---|---|
| vo.ter.open.01 | Let's take six slow breaths together. | 4 | 2.78 | 1 of 3 | -19.9 | -4.9 |
| vo.ter.open.02 | Settle in. The jar will follow you. | 4 | 2.95 | 3 of 3 | -20 | -2.3 |
| vo.ter.in.01 | Breathe in. | 1.8 | 1.26 | 2 of 3 | -20 | -7.1 |
| vo.ter.in.02 | In, slowly. | 1.8 | 1.77 | 2 of 3 | -19.9 | -2.4 |
| vo.ter.in.03 | Fill up gently. | 1.8 | 1.56 | 3 of 3 | -19.9 | -4.4 |
| vo.ter.out.01 | And let go. | 2.5 | 1.31 | 3 of 3 | -20 | -4.2 |
| vo.ter.out.02 | Let it out, softly. | 2.5 | 2.09 | 3 of 3 | -20 | -1.7 |
| vo.ter.out.03 | All the way out. | 2.5 | 1.54 | 2 of 3 | -20 | -5.8 |
| vo.ter.mid.01 | Halfway. The fern is listening. | 4 | 3.01 | 1 of 3 | -19.9 | -3.3 |
| vo.ter.last.01 | One last breath. | 1.8 | 1.54 | 2 of 3 | -19.9 | -6.2 |
| vo.ter.close.01 | Six breaths. This frond stays. | 4 | 3.07 | 2 of 3 | -19.8 | -2.5 |
| vo.ter.close.02 | Rest here as long as you like. | 4 | 2.25 | 3 of 3 | -20 | -4.9 |
| vo.ter.return.01 | Welcome back. Everything you grew is still here. | 4 | 3.72 | 1 of 3 | -19.9 | -6.2 |
| vo.ter.resume.01 | Whenever you are ready. | 4 | 1.75 | 2 of 3 | -20 | -7.3 |
