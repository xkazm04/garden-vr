# Garden VR

Two calm, hands-first daily-ritual apps that grow a living garden from real habits. Built for the
Meta VR Start Developer Competition 2026 (Productivity track), **PC-first**: mechanics, art and audio are mastered on
Windows with keyboard and mouse standing in for hands; Quest integration comes later, only once the app proves useful,
the mechanics feel seamless and the art is beautiful.

| App | Folder | Direction | One line |
|---|---|---|---|
| **Terrarium** | `apps/terrarium/` | Night Moss: extend reality, add glow and magic on top | A glass jar on your real desk; pinch and hold to breathe, and a glowing fern uncoils with your breath. |
| **Sundial** | `apps/sundial/` | Field Notebook: hand-drawn art mixed into reality | A watercolour-and-ink sundial on your real table; read your day in two seconds, tend it in twenty. |

Both are Unity 6.6 (URP) projects that share code and assets through local UPM packages in `shared/packages/`.

## Layout

```
apps/
  terrarium/            Unity project - Terrarium (app-specific scenes, art, audio, tests)
  sundial/              Unity project - Sundial
shared/
  packages/
    com.gardenvr.core     habits, ledger, growth rules, breath ritual, clock, persistence (pure C#, no UnityEngine)
    com.gardenvr.input    hand-intent abstraction (pinch, hold, poke, look, palm) + keyboard/mouse provider
    com.gardenvr.audio    cue-based audio service and manifest loader
    com.gardenvr.fx       shared shaders and effects: halo cards, glow, shadow catcher, toon ramp, ink outline
    com.gardenvr.room     PC stand-in for passthrough: room plates, desk anchor, seated camera rig
    com.gardenvr.capture  batchmode screenshot/video capture and test helpers
  core-dotnet/          .NET test harness compiling com.gardenvr.core sources (fast `dotnet test`, no Unity)
  assets/               audio, room plates and art references both apps can use
tools/
  audio/                ElevenLabs generation (sfx, narration, music) with a credit guard
  blender/              scripted asset generation (Blender 4.2, `-b -P`)
  capture/              render/capture scripts
  orchestrate/          agent task runner and queue tooling
docs/
  PLAN.md               the programme: phases, gates, schedule
  plans/                one detailed plan per app (MVP = the competition scope)
  decisions/            architecture decision records
  art/ audio/           style bibles
  contest/              where the design came from (links and copies of the deciding reports)
  knowledge/            registry consults and lessons to forge
orchestration/          task queue per agent, run logs, STATUS.md
```

## Quick start

```bash
# fast core tests, no Unity
dotnet test shared/core-dotnet

# open an app
"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe" -projectPath apps/terrarium
```

Agents: read `AGENTS.md` before doing anything.
