# 0011 - Lossless PNG recompression happens at ingest, not retroactively

Date: 2026-10-07. Status: accepted (App Master, 2026-10-07).

Source: `tools/assets/lossless.py` at 7da8c9e; `docs/assets/LOSSLESS.md` and `docs/assets/M2-READING-2026-10-07.md`
section 4 at 58d8719 (the ledger was measured at c3b8e3e).

**Constraint.** Git keeps every old blob, so rewriting a tracked png adds its new bytes to history and removes only the
difference from the checkout. The repo is public and its history is not rewritten (the owner's no-push rule in
`AGENTS.md`).

**Evidence.** Of the 307 measured pngs, 17 clear 10 percent and 64 KiB. They would save 6,574,653 bytes at HEAD and add
18,630,921 bytes of history. 0 of them clear saved > newBytes, and total repo bytes would rise by 12,056,268. Snapshot
files (`docs/assets/baseline.json`, `docs/health/`) record blob ids but do not keep a blob alive in git, so since 7da8c9e
they no longer exclude a file. The ingest probe from run 502844b2 (not committed): a copy of `plate-dial-seated.png` went
from 2,831,538 to 1,547,397 bytes with the proof ok.

**Decision.**

- (a) A tracked png is recompressed only when the gate of `lossless.py --apply` passes: 10 percent, 64 KiB, and
  saved > newBytes.
- (b) Every new or changed png goes through `python tools/assets/lossless.py --ingest --apply <paths>` before its first
  commit. That mode has no threshold, proves each file by a decoded-pixel hash, exits nonzero when a proof fails, and
  writes no ledger.

**Alternatives that lost.**

- Recompress the 17 now: the repo grows by about 12 MB to save 6.3 MiB at HEAD.
- Recompress all 230 pngs that shrink at all (10.29 MiB at HEAD): more history added still.
- Rewrite history to drop the old blobs: needs a force push of a public repo.

**Consequences.** `AGENTS.md` gains one line in its image-generation bullets that makes (b) a rule. The M2 footprint
goal is met at HEAD through moves and cuts, not recompression.
