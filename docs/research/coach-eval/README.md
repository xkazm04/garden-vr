# Coach evaluation

## Relay latency

Measured 2026-10-12 in the Sundial Windows development player on the owner's Windows machine. Local work uses the Claude Code subscription through `COACH_BACKEND=cli`; API measurements are separate.

| Backend | Status | Sample | Budget | p50 | p95 | Gate interpretation |
|---|---|---|---|---|---|---|
| `cli` | measured | 20 calls: 10 onboarding, 10 reflection | 15 s | 4.659 s | 6.334 s | Local development measurement; not the API gate |
| `api` | **needs-owner** | Not run | 4 s | Not measured | Not measured | p95 <= 4 s gate; measure when the owner switches to `api` |

The CLI model was `claude-haiku-5-5`. All 20 relay requests succeeded (HTTP 200, no relay failure). The app accepted 18 model replies: onboarding 10/10, reflection 8/10. Two reflection replies were rejected by the core reply guard and used scripted fallbacks (samples 10 and 12); both remain in the latency statistics. No timeout or rate-limit response occurred. Raw replies were not logged, so the specific guard condition is not known.

| CLI kind | n | p50 | p95 |
|---|---:|---:|---:|
| Onboarding | 10 | 4.667 s | 5.201 s |
| Reflection | 10 | 4.600 s | 7.168 s |

Timing runs from `SundialCoach` invocation through `CoachFlow` completion observed by the player, including transport, guard validation and frame granularity. p50 uses the median; p95 uses nearest rank. Calls ran sequentially, with starts at least 11 seconds apart under one install key. These are repeated synthetic fixtures, not a diverse quality sample. Two preceding live UI calls were excluded from the 20: the player displayed three model-generated onboarding packets and a model reflection card, and checked the journal matches the reflection.

Evidence: [report](../../../orchestration/runs/sundial/T-SUN-068/REPORT.md), [timing table](../../../orchestration/runs/sundial/T-SUN-068/timing-table.md), [CSV](../../../orchestration/runs/sundial/T-SUN-068/timings.csv), [content-free relay lines](../../../orchestration/runs/sundial/T-SUN-068/relay-lines.jsonl), and [player excerpts](../../../orchestration/runs/sundial/T-SUN-068/player-excerpts.txt).

To repeat the CLI check, build with `GardenVR.Sundial.Editor.Build.WindowsDevelopment`, then run `orchestration/runs/sundial/T-SUN-068/run-live.ps1` followed by `python orchestration/runs/sundial/T-SUN-068/verify.py`. The runner checks `backend=cli`, waits for the player and stops its relay. It uses an isolated save and synthetic speech; native dictation is not evaluated. No API key is needed for this path.
