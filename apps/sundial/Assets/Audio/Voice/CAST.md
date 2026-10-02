# Cast - Sundial narration

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
  listen to the whole set for pace and warmth.
- `measured_s` is the whole file (head and tail silence included), compared with `window_s`: the check is conservative,
  the speech itself ends about 0.25 s earlier.
- Copy rules held: no medical or therapeutic words, no promises, no em dash.

## Lines

| id | text | window s | measured s | take | LUFS | dBTP |
|---|---|---|---|---|---|---|
| vo.sun.open.01 | Three breaths to close the day. | 4 | 2.33 | 2 of 3 | -20 | -5 |
| vo.sun.in.01 | Breathe in. | 1.8 | 1.24 | 1 of 3 | -19.9 | -5.4 |
| vo.sun.in.02 | In, slowly. | 1.8 | 1.54 | 3 of 3 | -19.9 | -6.6 |
| vo.sun.out.01 | And out. | 2.5 | 1.18 | 3 of 3 | -19.9 | -5.8 |
| vo.sun.out.02 | Let it go. | 2.5 | 1.27 | 3 of 3 | -19.9 | -2.5 |
| vo.sun.last.01 | One more. | 1.8 | 1.08 | 2 of 3 | -20 | -8.4 |
| vo.sun.close.01 | That's the day. Goodnight. | 4 | 1.96 | 3 of 3 | -20 | -1.7 |
| vo.sun.first.01 | Here is your day. | 4 | 1.54 | 3 of 3 | -19.9 | -6.2 |
