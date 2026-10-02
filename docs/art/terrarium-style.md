# Terrarium style bible - Night Moss (A/1-03)

*Extend reality, add glow and magic on top.* A palm-sized cork-topped glass jar on the real desk at night. The jar is
the room's main light source: moss and fern edges glow a gentle mint, gold spores drift, a faint breath ring circles the
base, condensation fogs the upper glass. Hushed, sleepy, intimate, no harsh contrast.

Sources: `staging/habit-garden/ART-STYLE-STORYBOARD.md` (A1-03 entry: palette, prompt, negatives),
`staging/habit-garden/OWNER-CHOICE.md` (follow-ups F1-F3, extra negatives), the owner's frames
`shared/assets/art-reference/A1-03-night-moss-{1,2}.png`, round 3 (`arena/habit-garden-r2-r3/entries/claude-claude-opus-5-5_high-v1/variant-1/index.html`
sections 01, 04-06). Paths relative to `C:\Users\kazda\kiro\personas\.contest\` unless they start with `shared/`.

## 1. Palette

Two columns: the storyboard's intent palette (what each colour is for) and values measured from ref-1 (what the parity
check compares against; region averages, so darker than the peak colour).

| Role (token) | Intent hex | Measured in ref-1 | Used for |
|---|---|---|---|
| `night.room` | `#0E1A1C` | wall `#0C1A1F`, desk `#111514` | what the room reads as at night; the plate is graded toward it |
| `shadow.teal` | - | `#070F14` | deepest shadow; never pure black |
| `glow.mint` | `#8FF0C8` | frond lit edge region `#58936E` | frond and moss edge emission, the ring line, look rings |
| `moss.body` | `#2C5B45` | moss `#376222` | moss albedo (the moss is greener and warmer than the storyboard hex; parity follows the frame) |
| `fiddlehead.core` | `#A9D46A` | coil region `#799855` | the frond being earned: the brightest, yellowest green in the jar |
| `glass.rim` | `#6FB7C9` | glass mid `#517A7F` | fresnel rim, droplet highlights |
| `spore.gold` | `#F2D27A` | - (sparse) | spores, the completed ring, the first flower |
| `soil.loam` | - | `#0D231D` | soil band under the moss |
| `cork` | `#8A5A3B` | cork top in shade `#1F1913` | cork; lit only by the jar from below and the lamp |
| `lamp.warm` | - | `#D99F67` | the room's one warm accent (on the plate only) |
| `etch.text` | `#BFF5DD` at 70% | - | etched words on the glass; contrast >= 4.5:1 against `night.room` behind the glass |

Never: neon, cyberpunk, harsh bloom, horror, glowing eyes (OWNER-CHOICE extra negative); red anywhere; pure white
emission (cap emission at `#E8FFF4`).

## 2. Materials and shaders (URP, no post-processing)

| Material | Shader | Notes and costs (round 3 cost table) |
|---|---|---|
| Glass | `Glass` 2-pass (back faces first, then front): fresnel rim `glass.rim`, mint inner scatter by height, droplet map, painted streaks, breath fog (bottom-up, driven by `Fog`) | 2 draws, 1 sample (front); premultiplied. No grab pass, no refraction of the room (platform limit) |
| Fronds, fiddlehead, companions | `Glow`: emissive edge from a distance field, vitality scales emission (floor 0.6), self-light gradient | opaque + alpha-to-coverage cards; 3-5 samples |
| Moss | `Glow` triplanar, tip brightening; silhouette skirt of moss cards | the heaviest item; keep the card ring under 40 cards |
| Soil, cork | unlit gradient lit "by the jar" from below | 1 sample |
| Halos (coil, jar, flower), mist, spores, ring | `Card` additive or premultiplied; ring is analytic (no texture) | replaces bloom; the jar halo and desk spill are the largest fill cost: use the lean variant (round 3: half the transparent fill for no visible loss) |
| Room plate (PC only) | `Plate` at the far plane, graded to `night.room` | never ships to Quest |

Overdraw rule: mean transparent layers over the jar <= 1.5 (hard), <= 1.2 (soft); round 3 measured 1.85 full and lean
2.25 over a smaller area: the lean variant plus a smaller mist stack is the starting point.

## 3. Lighting

- The jar is the light. No realtime light loop on the jar; emission and a baked-in self-light gradient do the work.
- The room is dim: the plate is graded down; one warm lamp accent stays in the background (upper left in ref-1/2).
- A small desk light pool under the jar (a card) and a mint rim on the near desk edge; nothing lights the hands on PC
  (the reference's green-lit hands are image-model light, a platform limit on Quest).

## 4. Motion (follows the breath, not a clock)

| Motion | Spec | Reduced motion |
|---|---|---|
| Uncoil | advances only while PinchHold is held; ease-out toward this breath's share over the ideal inhale (4 s); never curls back past earned breaths | shows the earned state at once (breath timing unchanged) |
| Fog | peaks at release, clears bottom-up over the exhale (decay 2.5 s in the core; visible fog <= 6 s) | opacity step, no sweep |
| Ring | progress arc fills with the hold; re-syncs on every pinch; turns gold at completion | static ring with a fill number of dots |
| Spores | 20-40 points drifting up 1-3 cm/s, gentle curl; brighten for 1 s at the answer | 50% count, no curl |
| Mist | 5 loop cards rising from the cork, 6-10 s loops, slow | still |
| The answer | 0.0 s frond settles (0.8 s) -> 0.3 s chime -> 0.4 s dew bead rolls tip to base (1.2 s) -> 0.6 s ripple through the moss (1.5 s) -> ring closes gold (0.6 s) | all end states at once; the chime still plays |
| Jar arrival | drops the last 8 cm in 0.6 s, eased, one tink | appears in place |
| Missed day | glow at vitality (0.85 / 0.70 / floor 0.60); lean <= 6 deg; the waiting fiddlehead does not pulse | same values |

Ambient travel stays within a few millimetres at 40 cm (under 1 degree of view); entrances under 1 s; one expressive
easing (the uncoil) reserved for the earned moment.

## 5. Reference frames and the framing to capture them at

All parity captures: **1824 x 1024**, the jar camera measured in round 3 (`RUNBOOK.md` section 2,
`FidelityTools.JarCamera`): **jar base at pixel (915, 830), 4550 px per metre at the jar, about 13 degrees elevation,
28.2 degrees vertical FOV**, over `shared/assets/room-plates/plate-jar.png` (ref-1 with the jar inpainted out; the hands
stay in the plate). A second **seated POV** capture (eye 0.30 m above and 0.40 m back from the jar, 90 degrees FOV)
shows the true headset scale and is reviewed but not scored.

| Gate frame | Source | State to render | Status |
|---|---|---|---|
| **G1 mid-breath** | `A1-03-night-moss-1.png` | breath 3 of 6 held: ring half full, coil a third open, fog starting, two settled fronds + one seedling | anchor now |
| **G2 the answer** | `A1-03-night-moss-2.png` (centred jar, gold ring; closest look to "the frond stays") | frond stays, dew bead, ring closed gold; rendered at the G1 framing over `plate-jar.png` (as round 3 did) and compared with ref-2 for look (glow, ring, glass), not for composition | anchor now (look only) |
| **G3 the sixth breath** | A1-03-F1 prompt (`OWNER-CHOICE.md`), generated with `image_gen` in T-TER-006 | relaxing hand, dew bead rolling, moss ripple | **provisional** until the owner picks one at R2 |
| **G4 day 7** | A1-03-F2 prompt, generated in T-TER-006 | seven fronds, newest brightest, first flower, one quieter frond | provisional until R2 |

## 6. Spec anchor (measured tolerances, gate A2)

| Quantity | Target at the G1 framing | Tolerance |
|---|---|---|
| Jar height incl. cork | 14.0 cm (about 635 px) | +/- 5% |
| Jar diameter | 8.7 cm (about 395 px) | +/- 5% |
| Cork height | 1.5 cm | +/- 10% |
| Breath ring radius | 6.0 cm from the jar axis | +/- 8% |
| Region colours (moss, frond edge, glass mid, soil, wall) | measured hexes in section 1 | mean CIEDE2000 <= 8 per region |
| Fiddlehead coil diameter at G1 | 1.6 cm | +/- 15% |
| Spore count in frame | 20-40 | - |

## 7. Judge rubric (gate A3; anchored, scored per frame and per region; the minimum counts)

| Level | Anchor |
|---|---|
| 1 | procedural primitives, sawtooth leaves (round-2 "Night Lantern" stills) |
| 2 | right silhouette and glow, CG materials (round-3 moss dome, regular frond lace) |
| 3 | authored materials everywhere, one region visibly weaker than the reference |
| 4 | every region (glass, fronds, moss, cork, mist, ring) reads as the reference's material at a 2 s glance |
| 5 | indistinguishable from the reference's look at a 2 s glance |

Capped disqualifiers (frame scores at most 2): magenta or placeholder material, transparent sorting pop or z-fighting
in the glass, pixels taken from the reference frame, neon / harsh bloom, any text other than the etched hints, a
pure-black or pure-white region larger than the dew bead.

Regions excluded from parity (platform limits, round-3 gap ledger): the room seen through the glass is not refracted;
hands are not lit by the jar.
