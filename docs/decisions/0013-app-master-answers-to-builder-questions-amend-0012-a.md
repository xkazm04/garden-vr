# 0013 - The App Master's answers to six builder questions, as amendments to 0012 (a)

Date: 2026-10-07. Status: accepted. The App Master's, 2026-10-07, under the owner's brief and 0012; the owner may overrule.

Source: the App Master's answers of 2026-10-07 to questions from three merged runs, which until now lived only in the App
Master's journal note: run c7ec0752 (merged `a9a2315`), run 748fb132 (merged `e5c0aa6`) and run 0796cf1b (merged
`b7ed44f`); and the design conformance read `docs/design/CONFORMANCE-sundial.md` (commit `00c2e4c`), which adds the sixth
question, S9. Every number below is quoted from the file named beside it, at the commit named. The texture figures are in
`docs/budgets/TEXTURE-MEMORY.md` and `docs/budgets/texture-memory.json`, both unchanged between `7a43894` (the commit
that added them) and `a9a2315`. They are a reading of commit `5cea8f4` ("budgets: milestone 4 budgets ...") that
`7a43894`'s tool wrote. The ceiling policy is in `docs/budgets/README.md` ("Why texture memory gets a quarter", from
`7a43894`) and `docs/budgets/sundial.json` and `terrarium.json` (from `5cea8f4`). The rows are `docs/PLAN.md` D8 and D10
(line 282 and 284 at `00c2e4c`).

This decision adds items to 0012 (a). It changes no other part of 0012, and it changes no code, plan, budget or `.meta`.

## (a) Android texture overrides become item 6 of 0012 (a)

**Constraint.** The reading at `a9a2315` found 107 textures with no Android override: 77 in Sundial and 30 in Terrarium
(`docs/budgets/TEXTURE-MEMORY.md`, the "Automatic format with compression off on Android" paragraph of each app;
`texture-memory.json`, `apps[].uncompressed`, 77 and 30 entries). Their Default entry says `textureCompression: 0` with
Automatic format, so Unity imports them uncompressed. The same file gives the figures at 4 bytes per pixel (RGBA32; a
texture without alpha can import as RGB24, so this is an upper figure). Counted textures only (state `referenced` or
`resources`; 75 in Sundial, 24 in Terrarium, from the same `uncompressed` list):

| App | Counted at ASTC 6x6 (TEXTURE-MEMORY.md, Totals) | Counted as RGBA32 (same file, the paragraph above) |
| --- | ---: | ---: |
| sundial | 21.75 MiB (22,808,032 bytes, all 160 counted textures) | 83,973,556 bytes, about 80 MiB, for 75 textures |
| terrarium | 11.57 MiB (12,136,864 bytes, all 32 counted textures) | 93,187,176 bytes, about 89 MiB, for 24 textures |

The two columns do not cover the same textures (the right column is the uncompressed subset only), so they are not
subtracted from each other. The Totals row is what 0012 and the ceiling check read, and it counts every texture at ASTC
6x6. None of this was measured by Unity or on a headset; H2 is the first reading (`TEXTURE-MEMORY.md`, the paragraph under "Trees read").

**Decision.** Android overrides at ASTC 6x6, set per texture class, become **item 6** of 0012 (a), for the first Unity
session or Phase 2 from 2026-10-24 (`docs/PLAN.md:63`), whichever comes first.

- They are set through the importer, by an editor script or the inspector. Nobody hand-edits a `.meta`. The repo already
  has one importer-setting precedent, `apps/sundial/Assets/Editor/DialSetup.cs:87-125` (ASTC Ink Check), which sets
  `TextureImporterFormat.ASTC_6x6` through `SetPlatformTextureSettings`.
- The list of 107 is `apps[].uncompressed` of `docs/budgets/texture-memory.json` at `a9a2315`. It includes the 4 plates
  and the textures with state `no-reference-found` (Sundial 2, Terrarium 6), because code can load those by a name the
  scan cannot see. The 4 plates are covered by (c) below.
- The command that regenerates the list, read from `tools/assets/census.mjs:633-657` and `tools/assets/texmem.mjs`:
  `node tools/assets/census.mjs --only texmem --rev a9a2315 --out <a directory outside the repo>`. Without `--out` it
  writes `docs/budgets/TEXTURE-MEMORY.md` and `texture-memory.json` in place (`census.mjs:653-654`, default
  `docs/budgets`). **Never run `census.mjs` or `texmem.mjs` without `--out` pointing outside the repo**; the files in
  `docs/budgets/` are another run's output.
- Item 6 covers the 107 with no override, and the two textures of (b) that move to 6x6. The four of (b) that keep 4x4
  stay as they are.
- Evidence the item must leave: the importer-change script or the inspector settings it used, a census rerun with
  `--out` outside the repo showing 0 textures in `uncompressed`, and the render compare of 0012 (a) item 5 for every
  texture of (b) that changes format.

**Alternatives that lost.**

- Leave the Default entry as is and trust Automatic: it is the uncompressed import the reading found.
- Hand-edit the `.meta` files: no Unity session exists here to import them and show the edit took, and the importer is
  the owner of that file.
- Set one override for every texture regardless of class: (b) shows a class that reads a fine line at arm's length.

## (b) The 6 Sundial ASTC_4x4 overrides

**Constraint.** Six Sundial textures already have an Android override at ASTC_4x4, which costs more than the 6x6 the
budget reading counts (`TEXTURE-MEMORY.md`, "Android override with a format other than ASTC 6x6 or Automatic"). Each
`.meta` was read read-only (`textureFormat: 48`, `overridden: 1`, `buildTarget: Android`, max size and mipmaps as in the
table); each user is found by guid grep under `apps/sundial`, `shared` and the shader.

**Decision.** Keep 4x4 only where a fine line or text is read at arm's length. No text is in any of the six. The fine
lines are the painted arc and its ticks on the dial face, and the pencil hatch in the control texture; the dial is the
hero object, plate-sized and about 55 cm ahead (`docs/PLAN.md:41`). Those four stay at 4x4. The other two are a
paper-grain map and a light cookie, which carry no line, and they move to 6x6 as part of item 6.

| Texture | Path | Use | 4x4 or 6x6 | Reason | Cost difference (bytes, mips as in the `.meta`) |
| --- | --- | --- | --- | --- | ---: |
| `dial_face_s1` | `apps/sundial/Assets/Art/Textures/dial_face_s1.png` | the S1 dial face colour; `Dial_Face_S1.mat`; made by `apps/sundial/Art/Scripts/watercolour_s1.py:388` | **4x4** | the painted arc and the clipped dusk band, read at arm's length on the hero dial | 6x6 would save 771,840 (1,398,128 to 626,288) |
| `dial_control` | `apps/sundial/Assets/Art/Textures/dial_control.png` | `_ControlTex` of `FToon.shader`: R arc distance, G density, B pencil, A wash id read with thresholds 0.16, 0.50 and 0.83 (`FToon.shader:32`, `:301-308`; `watercolour_s1.py:389`); used by `Dial_Face_S1.mat` | **4x4** | B carries the pencil hatch, a fine line; A is a stepped id whose bands 6x6 blocks can smear across a band edge | 6x6 would save 771,840 |
| `dial_face_layout_s1` | `apps/sundial/Assets/Resources/Layout/dial_face_layout_s1.png` | the polar remap of `dial_face_s1` (`layout_t052_face.py --s1`); `Dial_Face_Layout_S1.mat`, loaded by `Resources.Load` (`DialView.cs:1031`) | **4x4** | a remap of the same painted arc and ticks, so the same reason as `dial_face_s1` | 6x6 would save 771,840 |
| `dial_control_layout` | `apps/sundial/Assets/Resources/Layout/dial_control_layout.png` | the same remap of `dial_control`; `Dial_Face_Layout_S1.mat`; read by `SprintVariantTests.cs:118` | **4x4** | the same channels as `dial_control` | 6x6 would save 771,840 |
| `dial_paper_h` | `apps/sundial/Assets/Art/Textures/dial_paper_h.png` | `_PaperH`, a tileable 512 px paper-tooth height map, 8-bit grey (`FToon.shader:33`, `:94-95`); `Dial_Face_S1.mat`, `Dial_Face_Layout_S1.mat`, `Soil_Mound.mat` | **6x6** | grain sampled as a tiled height, not a line or text; no edge is read from it by eye | saves 191,120 (349,552 to 158,432) |
| `room_cookie` | `apps/sundial/Assets/Art/Textures/room_cookie.png` | the PC room-light cookie, `_GvrRoomCookie`; bound in `Dial.prefab`; `RoomLight.cs:7` says Quest reads the room itself and this is only the seam | **6x6** | a soft 256 px light mask, no mipmaps, read as a gradient; it is a PC stand-in | saves 35,952 (65,536 to 29,584) |

The byte figures are the `ownFormatBytes` and `bytes` fields of `texture-memory.json` (`apps[0].otherFormat`, commit
`7a43894`). The 6x6 move of the last two rows saves 227,072 bytes in total; the four kept rows cost 3,087,360 bytes more
than at 6x6, which is 2.94 MiB of the 1472 MiB ceiling. The decision rests on a reading of the shader and the
provenance, not on a render. Item 5 of 0012 (a) is where a 6x6 render of any kept row is compared with its 4x4 render,
and a row may move to 6x6 only if that compare shows no line lost.

## (c) The four room plates leave the Quest build in Phase 2

**Constraint.** The four plates `plate-dial.png`, `plate-dial-seated.png` (Sundial) and `plate-jar.png`,
`plate-jar-seated.png` (Terrarium) import at 2048x1024 (`TEXTURE-MEMORY.md`, "Imported with a side over the PLAN A5 cap
of 1024 px"), over the A5 cap of 1024 px. Per `docs/PLAN.md` D8, room plates are never shipped on Quest, because
passthrough replaces them.

**Decision.** They leave the Quest build in Phase 2 and are not downscaled. 0012 (a) items 2 and 3 already stage their
cut and their compare (item 2 keeps one copy under `shared/assets/room-plates/`; item 3 names the same four paths). No
line of `docs/PLAN.md` names how they leave the build, so the mechanism is **to be chosen in Phase 2**. None is invented
here. They stay at 2048x1024 for PC, where D8 keeps them for development and parity. They are the one place the A5 1024
px cap is not applied, and they are not counted against it on Quest.

**Alternatives that lost.**

- Downscale them to 1024 px: they never ship on Quest, so the cut loses PC fidelity for nothing.
- Name a build mechanism now (a define, a scene variant, an asset bundle): the plan names none, and no Unity session can
  test one.

## (d) The 25% texture share is a policy

**Constraint.** `docs/budgets/README.md` ("Why texture memory gets a quarter", at `7a43894`) sets the texture ceiling
at 25% of the 5,888 MiB PSS ceiling, 1,472 MiB. Its reasoning is that four resident classes each get an equal quarter
because no device reading exists for any of them. The 5,888 MiB is sourced (`meta-memory-ram`, page updated 2025-08-12,
in `docs/budgets/sundial.json`); the 25% is not.

**Decision.** The 25% is a reasoned policy, not a measurement. It is revisited at H2 (2026-11-03, `docs/PLAN.md:67`),
the first headset reading, where measured PSS replaces the four quarters. Until then the 1024 px cap (PLAN A5) and the
texture formats of item 6 bind, because they are what a session can check without a headset. Today's reading passes
under either figure: 21.75 MiB and 11.57 MiB counted (`TEXTURE-MEMORY.md`, Totals) against 1,472 MiB.

## (e) SundialSave drops unknown Settings and Focus members: a step B in item 4

**Constraint.** Part 2 of the `SundialSave.cs` finding in `docs/health/findings.json` (the blob
`d1bc93ab602caa11172826a712ace613b38b855c` entry, at `a9a2315`/`e5c0aa6`): `ReadSettings` and `WriteSettings`, and
`ReadFocus` and `WriteFocus`, keep only the members this build knows, so an unknown Settings or Focus member is gone
after the next save, against the promise in the file's header and against `TerrariumSave`'s `SettingsExtra`
(`TerrariumSave.cs:55`). A Focus that is not an object is dropped too. Keeping them needs a new public field on
`SundialSave`, which changes the surface that `PublicSurface.approved.txt` pins, so 0012 (c) does not cover it
(condition 1).

Part 1 is done: `b7ed44f` ("SundialSave: habit and tend row members call LedgerJson with Sundial's choices") makes a
habit row or tend row with no id keep its unknown members. A separate test, `An_unknown_member_of_a_habit_row_and_a_tend_row_with_no_id_is_kept`
(`shared/core-dotnet/GardenVR.SundialModel.Tests/SundialCodecGoldenTests.cs:34`), pins it, and the golden input in that
file deliberately carries no unknown member on a row with no id.

**Decision.** Keeping unknown Settings and Focus members becomes a **step B added to item 4** of 0012 (a): a public
extras field for Settings and one for Focus in `SundialSave`, shaped like `TerrariumSave`'s, with the approved surface
list updated in the same change and a test that writes and reads back an unknown member of each. It runs with the other
step Bs of item 4, in the first Unity session.

## (f) Conformance idea S9 is decision 0007 step B

**Constraint.** `docs/design/CONFORMANCE-sundial.md` (at `00c2e4c`), rows DC3 and NC7, and idea S9, "draw plants from
`PlantMonotone`": the app draws from `SundialRules.Plant` with a per-session floor (`SundialController.cs:87`,
`:631-634`), so a clock set-back and a restart can shrink a plant. The idea's own text says "This is decision 0007 step
B, already staged; file only if the staged step is not already tracked."

**Decision.** S9 is the same work as 0007 step B ("switch `SundialController` to `PlantMonotone` and delete the floor").
0012 (a) item 4, proposal 3, already stages the deletion of the floor. S9 is not filed as a separate idea; there is one
piece of work with two names, and it stays under 0007 and item 4.

## Consequences

0012 gains one line under its Status naming what this decision adds to (a): item 6 (Android ASTC 6x6 overrides and the
format table of (b)), a Phase 2 exit for the plates under item 3, and a step B under item 4 (SundialSave Settings and
Focus extras). Nothing in `docs/PLAN.md`, `docs/budgets/`, `docs/health/` or `docs/design/` is edited; the first
Unity session reads this file as its list, beside 0012. `dotnet test shared/core-dotnet` counts 309 tests (264 core, 31
Sundial model, 14 Terrarium model) at `00c2e4c`, all passing.
