# 0004 - When the art judge may gate, and what it needs to re-run from a clone

Date: 2026-10-05. Status: proposed (the bar is enforced by a test; the label run and the frame tracking are the owner's).

**Context.** Art quality is the owner's main reason for rejecting work, and the F2 pairwise judge
(`tools/fidelity/judge/packet-v1/`) is the only automatic art signal. Its one calibration record
(`tools/fidelity/judge/calibration-2026-10-04.json`) passes the same-framing control gate but has 0 of 30 owner labels,
so it is advisory. Its inputs include 8 frames kept only on the host, so no clone and no CI run can reproduce it.

**Decision: the bar for leaving advisory.** A calibration record may say `"advisory": false` only when it shows all of
these, computed by the lab and written into the record:

| Evidence | Bar | Record field |
|---|---|---|
| Owner labels, total | at least `kappa_min_labels` of the packet (30) | `kappa.n` |
| Owner labels per gate frame | at least 12 for each of JarG1 and DialG1, so one app cannot carry the other | `kappa.per_frame.<frame>` |
| Chance-corrected agreement | Cohen's kappa at least the packet's `kappa_floor` (0.6), and `kappa.pass` true | `kappa.kappa`, `kappa.pass` |
| Small-sample margin | 95% lower confidence bound of kappa at least 0.4; with 30 labels a kappa of 0.6 can be luck | `kappa.ci95_low` |
| Raw agreement | at least 80% of labelled pairs | `kappa.agreement` |
| Order stability | inversion rate at most 10% per frame (the record already writes it) | `frames.<frame>.inversion.rate` |
| Control gate | passes | `gate_pass` |

The labels must be useful ones: pairs drawn from real sweep variants close to the locked frame (`fid.cmd sheet`
`chosen_by: owner`), not only the negative controls, picked by the owner before seeing the judge's verdict.

`shared/core-dotnet/GardenVR.Core.Tests/ArtJudgeGateTests.cs` enforces this table on every committed calibration
record, so a record that drops `advisory` without the evidence fails `dotnet test shared/core-dotnet` locally and in CI.
The lab does not write `ci95_low`, `agreement` or `per_frame` yet, so today no record can leave advisory.

**What gating means once the bar is met.** Veto only. A candidate that loses to the locked frame on `overall` at 9 of 10
agreed draws can be held back. The judge never certifies a frame; only the owner's A1 rating does (registry
`quality-verdict-integrity`: an unprovable verdict may condemn, never elevate).

**Demotion.** A new packet version or a new model starts advisory again with a fresh calibration. A rolling agreement
below 70% over the owner's last 10 labels demotes the judge to advisory until it is recalibrated.

**Proposal: re-running from a clone (owner's call).** The judge reads 8 files no clone has: the two locked gate frames
(`orchestration/runs/terrarium/T-TER-032/jar-g1.png`, `orchestration/runs/sundial/T-SUN-031/dial-g1-nohalo.png`) and
the six re-renders under `orchestration/runs/sundial/T-SUN-040/rerenders/`. `/orchestration/` is ignored because it is
process state, not the app, and that reason still holds for run folders. So copy only these 8 frames, by the sha256
the ladders already record, into a tracked `tools/fidelity/frames/`, as plain binary files like the tracked
`tools/fidelity/rungs/`, and point the ladders and the next calibration record at them. Do not use LFS: it needs a
per-clone install, the same gap the commit hook has, and the files are a few megabytes. Re-tracking
`tools/fidelity/lab/` (byte-identical at f08ff0e^ on backup/pre-rewrite-main) is the second step that makes
`fid.cmd judge --check` runnable in a clone. `ArtJudgeGateTests` lists the 8 paths as host-only; the list may only
shrink, so a new host-only input fails, and each entry comes off as its frame is tracked.

**Why.** A judge that agrees with controls shows it can see a large difference, not that it ranks two close variants
the way the owner does. Agents iterate against whatever signal exists, so the signal must be calibrated before it can
hold work back, and that must be checked by a machine rather than remembered.
