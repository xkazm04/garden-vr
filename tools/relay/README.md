# Coach relay

Holds the Claude API key for the Garden VR coach (upgrade plan C9, `docs/plans/upgrade-2026-10.md` sections 6.5 and 7).
The app never carries a key: it POSTs the prompt that core built (`RelayProtocol.Encode`) and reads one line back
(`RelayProtocol.Decode`).

- **Model:** Claude Haiku 5.5 (`claude-haiku-5-5`), effort `low`, `max_tokens` 2048 (room for adaptive thinking on top
  of a reply of one line or one small JSON object). The relay is the only place that names the model (`RELAY_MODEL`).
- **What it accepts:** `POST /v1/coach` with header `x-install-key` (8 to 64 letters, digits or hyphens) and
  `{ kind, system, user, maxTokens }`. `kind` is `Onboarding` or `Reflection`. `system` must be one of the prompts in
  `allowed-systems.json`. `user` is at most 8,000 characters, and `maxTokens` at most 600. Anything else gets a 400
  before any call is made.
- **Limits:** 6 requests a minute and 40 a day per install key, and 300 a day per IP address, kept in memory.
- **Failures:** a refusal returns `{ ok: false, failure: "Refused" }`. Haiku has no server-side fallback model, so the
  app shows the scripted line. Upstream errors, cut-off replies and empty replies return `"Error"`, and a rate limit
  returns `"RateLimited"` (429).
- **Logs:** one JSON line per request with time, kind, status, failure, milliseconds and token counts. It never holds
  what the user said, the reply, or the install key.

## Run

```bash
cd tools/relay && npm ci
ANTHROPIC_API_KEY=... node server.mjs          # PORT=8787 by default
curl -s localhost:8787/healthz                  # {"ok":true}
npm test                                        # node --test, with a fake client
```

## Keep it in step with core

`allowed-systems.json` is written from core and must not be edited by hand:

```bash
dotnet run --project shared/core-dotnet/GardenVR.CoachCli -- systems tools/relay/allowed-systems.json
```

`GardenVR.CoachCli.Tests/AllowedSystemsTests` fails when a prompt change in core was not followed by that command.

## Task W5 on the Windows machine

1. Set `ANTHROPIC_API_KEY` and start the relay.
2. Make one live call from the PC build, and time 20 calls for the section 7 latency mark (p95 4 s or less).

Where the relay runs for the competition build (reachable by judges' headsets from 11-17 through judging) is still open
question 5 in the plan.
