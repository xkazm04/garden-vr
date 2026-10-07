# 0014 - The lean sweep's judgement calls and the App Master's answers on the asset half, as amendments to 0012 (a)

Date: 2026-10-07. Status: accepted. The App Master's, 2026-10-07, under the owner's brief and 0012; the owner may overrule.

Source: run 95ec7ba8, the code half of the lean sweep, merged at `8de6f64`, and run c4e14328, the asset half, merged at
`b83d233`. The App Master accepted their calls on 2026-10-07. Until now the calls lived only in the two run results and
the App Master's journal. The record of every finding is `docs/health/sweep.json` and the account is
`docs/health/SWEEP.md`, both at `b83d233` (899 records: 842 code, 57 assets; unclassified 0 in each half; nothing under
`docs/assets` or `docs/budgets` changed in `b83d233`). Every number below is quoted from the file named beside it, at the
commit named. "The census" means `tools/assets/census.mjs` and its output in `docs/assets/baseline.json`.

This decision adds items to 0012 (a). It changes no other part of 0012, and it changes no code, plan, budget, asset or
`.meta`. It writes only `docs/decisions/`.

## (a) The code half's four calls

**Constraint.** The code half classified 842 records: 51 fixed, 15 filed as 4 ideas and 776 deliberate, 24 of them staged
for the first Unity session (`docs/health/SWEEP.md` at `b83d233`, the header and the Counts table). Four calls need a
reason on main.

**Decision.** All four are accepted as the run stated them.

1. **CA1707 and CA1861 are deliberate in tests.** CA1707 is 302 test names with underscores, and CA1861 is 6 inline
   expected arrays (`SWEEP.md`, "Deliberate, by rule"). A test is named as a sentence that states the rule it checks, and
   CA1707 targets library API names. An expected array is the value of one assertion that runs once; hoisting it moves the
   expectation away from the assertion it explains. After the fix commit `1a99071` they are the 308 test-code warnings
   left at `latest-recommended` (`SWEEP.md`, "After the fix").
2. **CA1050 was fixed with file-scoped namespaces in 39 test files** (40 sites; `SWEEP.md`, "Fixed", commit `1a99071`).
   This changes the tests' fully qualified names. No tool in the repo filters tests by fully qualified name, and
   `dotnet test shared/core-dotnet` passes 309 of 309 (Core 264, SundialModel 31, TerrariumModel 14), the base count
   (`SWEEP.md`, "After the fix").
3. **Every info-level IDE style finding is deliberate** (`SWEEP.md`, "Deliberate, by rule", the IDE style row: 229
   sites, core 101, app 32, test 96; IDE0059 is a separate row of 14). The reasons are the ones in that row: no
   `.editorconfig` sets a house style; the core compiles at LangVersion 9.0, the Unity level; several rewrites (IDE0300,
   IDE0305, IDE0290) need C# 12; and a style-only commit adds churn to the hotspot score under 0004. IDE0060 (1 site,
   `RitualPause.Continue`) is not in this call; it is staged, as the same table says.
4. **The name-grep "test-only" list is recorded and classified, although the brief asked only for the no-reference
   list.** `SWEEP.md` ("Name grep") lists 3 members with no reference found beyond the declaration and 29 referenced only
   from test code, each marked staged or deliberate. The 29 stay in the record: a later reading needs to know which core
   members wait for a step B and which are rule witnesses.

**Alternatives that lost.** Rename the 302 tests to remove underscores: every test id changes and the names read worse.
Add an `.editorconfig` to enforce a style: it is a house-style choice the owner has not made, and the core's C# 9 ceiling
blocks several of the rewrites. Drop the 29-member list: it is the only record of which core is waiting for its step B.

## (b) The asset half's three calls

**Constraint.** The asset half classified 57 records (55 census findings, 2 scope records) from a census run at `8670dd1`:
0 fixed, 2 ideas, 55 deliberate, 19 of them staged (`SWEEP.md`, "Asset half", "Per class"). Every class count and byte
count equals the "After" table of `docs/assets/M2-READING-2026-10-07.md`.

**Decision.** All three are accepted.

1. **All 21 no-reference-found files are deliberate** (17,079,142 bytes, `SWEEP.md`, "Per class"). For each, a search of
   `.cs`, `.py`, `.mjs`, `.shader`, `.json` and `.txt` found a script that loads or composes the file by name; the
   file:line is in each record of `sweep.json`. **Known limit of the census:** its guid scan reads references by guid and
   cannot see a load by name. A file in the no-reference-found class is therefore a lead, not a finding. The next reading
   starts from the file:line in `sweep.json` and does not raise these 21 again unless a record's loader is gone.
2. **Where they differ, the hand verdicts in `docs/assets/duplicates.json` win** over the census keep rule (8 groups) and
   over the census risk (2 groups). `SWEEP.md` ("Same as M2-READING") records that the census printed both sets of
   differences and that the hand verdict is the one used.
3. **The 3 script inputs over 1024 px are deliberate under their effective import cap of 1024.** `dial_face`,
   `dial_face_s1` and `condensation` are read by scripts at full size, and the census reads the effective cap 1024 from
   their `.meta` (`SWEEP.md`, "Deliberate, by rule", the over-1024 line). The 4 plates in the same class are 0013 (c).

**Alternatives that lost.** Treat the 21 as unused and cut them: a script loads each, and a cut breaks the script. Make
the census read names too: that is a tool change outside this decision. Let the census keep rule overrule the hand
verdicts: the census prints its own differences as notes, not corrections.

## (c) Item 7 of 0012 (a): `dial_paper.png` is requantised only if the render compare shows no change

**Constraint.** `apps/sundial/Assets/Art/Textures/dial_paper.png` is 16 bits per sample (128 x 128, 89,783 bytes; the
`png-16-bit` row of `docs/assets/baseline.json`: rank 129, `bytesSaved` 44,892 by estimate, risk `visible-change`, cut
"requantise to 8 bits per sample"). Its import is `textureFormat -1`, `textureCompression 0`, so Unity can import it at 16
bits. 0011 covers lossless rewrites, not a requantise (`SWEEP.md`, idea I5). `Dial_Rim.mat` uses it.

**Decision.** It becomes **item 7** of 0012 (a). At the first Unity session, `dial_paper.png` is requantised to 8 bits
only if item 5's render compare shows no visible change at headset scale. Otherwise it stays at 16 bits, and the compare
is recorded as the reason. Either result leaves evidence under `orchestration/runs/`. A requantised file goes through
`python tools/assets/lossless.py --ingest --apply`, as `AGENTS.md` requires.

**Alternatives that lost.** Requantise now: no Unity session can run the compare, and the file is `visible-change`.
Requantise without the compare: the saving is 44,892 bytes by estimate, too small to risk a visible change.

## (d) Item 8 of 0012 (a): `moss-repaint.png` is cut at the first Unity session

**Constraint.** `apps/terrarium/Assets/Art/Textures/moss-repaint.png` is 4,110,297 bytes by the census (`docs/assets/baseline.json`,
the `no-reference-found` row, rank 1, `bytesSaved`; the blob alone is 4,106,871 bytes, the `bytes` field of its row in
the same file), 1024 x 1024, 16 bits per sample. Grounds:

- Its row in `apps/terrarium/Assets/Art/Textures/PROVENANCE.md` (at `b83d233`) says: "kept, not bound: no .mat or .cs
  names it and `Jar_Moss.mat` still uses `moss_macro`". A grep of `.mat`, `.cs` and `.unity` under `apps` finds no name
  for it.
- Its only binding is `tools/fidelity/sweeps/jar-moss.json`, whose variants `jar-moss-tex-repaint` (all 3 slots) and
  `jar-moss-tex-repaint-main` (albedo only) name it. The closed T-SUN-043 sweep ran them. That run record,
  `orchestration/runs/sundial/T-SUN-043/jar-moss/sweep.json`, is untracked on main (`f08ff0e` stopped tracking
  orchestration runs) and was read in the main checkout. It keeps the file's sha256,
  `321f5bf9bdc2fd3bc8a05ab881f70ef3cf8514d95aea97a4fb7cd4cc97ae45c4`, in the `textureFiles` of the first variant.
- The F3 sweep moved every region bar within the noise band: `docs/knowledge/lessons.md`, the 2026-10-04 entry
  "Parameter sweeps confirm the plateau; technique changes break it". The repaint was one of its 11 variants.
- Git history keeps the blob from `fea50b2` ("terrarium: add the moss-repaint variant from the JarG1 bake"), so the cut is
  reversible.
- A player build ships only referenced assets, so waiting for the session costs no build bytes.

**Decision.** It becomes **item 8** of 0012 (a). At the first Unity session, together with item 2, the blob
`apps/terrarium/Assets/Art/Textures/moss-repaint.png` and its `moss-repaint.png.meta` are cut. **The same commit**
removes the variants `jar-moss-tex-repaint` and `jar-moss-tex-repaint-main` from `tools/fidelity/sweeps/jar-moss.json`,
so that no spec names a missing file. Idea I5 (`SWEEP.md`, "Ideas") is then moot for this file: nothing is left to
requantise. Evidence the item leaves: the commit, and a grep showing no spec names the path.

Not part of this item, and left as they are: `apps/terrarium/Assets/Art/Textures/moss-repaint.prompt.txt` and its
`.meta`; `apps/terrarium/Art/Source/moss-repaint.png` (73,877 bytes at `b83d233`, a different blob from the Assets file)
and its `.prompt.txt`; and `tools/blender/project_bake.py` and `tools/blender/run_f4.py`, which name the file. The first
Unity session reads those two scripts before the cut and decides whether they need a line changed.

**Alternatives that lost.**

- Keep it: 4.1 MiB of tracked weight for a variant that never won.
- Move it to `Art/Source`: it is the source of no shipped texture.
- Cut it now without Unity: 0012 stages asset cuts for one verified session.

## Consequences

0012 gains one line under its Status naming 0014 and items 7 and 8, the way 0013 did. With 0013, the first Unity session
reads items 1 to 8 of 0012 (a). Idea I5 is closed by this decision: `dial_paper.png` is item 7 and `moss-repaint.png` is
item 8. Nothing in `docs/health/`, `docs/assets/`, `docs/budgets/`, `docs/knowledge/`, `tools/` or `docs/PLAN.md` is
edited here. The `dotnet test shared/core-dotnet` count stays 309 (`SWEEP.md`, "After the fix").
