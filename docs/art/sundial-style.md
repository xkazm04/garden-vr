# Sundial style bible - Field Notebook, pushed to hand-drawn linework in a real room (A/2-05)

*Mix hand-drawn art and reality.* A plate-sized sundial garden drawn like a frame of a hand-animated film: clean,
confident ink contours of varying weight, flat cel colour with a single soft shade step, loose watercolour washes only
inside the three arcs, pencil construction lines and tiny ticks, paper grain only on the drawn object. **The room stays
photographic.** The contrast between drawn object and real room is the point (`OWNER-CHOICE.md`).

Sources: `staging/habit-garden/ART-STYLE-STORYBOARD.md` (A2-05 entry), `staging/habit-garden/OWNER-CHOICE.md`
(A/2-05-F1..F3 and the extra negative), the owner's frames `shared/assets/art-reference/A2-05-field-notebook-{1,2}.png`,
round 3 Unity seat section 02 and IWSDK seat "Asset pipeline" / "How each effect is drawn". Paths relative to
`C:\Users\kazda\kiro\personas\.contest\` unless they start with `shared/` or `tools/`.

## 1. Palette

| Role (token) | Intent hex (storyboard) | Measured in ref-1 / ref-2 | Used for |
|---|---|---|---|
| `paper.page` | `#F3EEE2` | rim `#DAC9B9`, page `#DBCDC4` (in shade) | the dial face and rim paper |
| `ink.line` | `#2A2622` | - | every contour; never pure black |
| `pencil` | `#8A8178` | - | construction lines, ticks, late-tile hatch |
| `wash.morning` | `#E2B866` | `#F6DEB7` / `#F0E0BE` | morning arc wash (pale at the outer edge, pooled ochre `#D9762A` at the wet edge) |
| `wash.midday` | `#E39C82` | `#F4B6A1` / `#EFBC9B` | midday arc; wet edge `#C9483F` used sparingly |
| `wash.dusk` | `#A79AD6` | `#BFA1BC` / `#CAB0C5` | dusk arc; wet edge `#6F55AD` |
| `soil` | - | `#A0836C` | the soil mound and its stipple |
| `halo.gold` | `#F5C76A` | warm white core `#F3D2AB` | the inked look / pinch halo |
| `tile.kept` | the arc's wash at full strength | sage `#B2BBA1` (ref-1) | kept tiles |
| `tile.missed` | `#EFE8DA` + pencil outline | - | missed: pale paper, quiet |
| `tile.late` | `pencil` hatch over the arc wash at 50% | - | logged late: hatched forever |
| `tile.today` | dashed `ink.line` outline | - | today, still open |

The IWSDK seat's SVG washes (`tools/blender/from-iwsdk/dial_svg.mjs`: `#f6c24a -> #f08a3c`, `#f39a8a -> #e8625a`,
`#b9a0e0 -> #8f74c9`) are more saturated than the references; T-SUN-006 grades them toward the measured column.

Never: 3D render, CGI, plastic, photorealistic plants, chibi characters, faces, speech bubbles, manga text, screen tones
covering the room (OWNER-CHOICE extra negative); red for a missed day; legible handwriting in MVP (labels are cut-line
item 3).

## 2. Materials and shaders (URP, no post-processing)

| Material | Shader | Notes |
|---|---|---|
| Dial body, gnomon, soil | `Toon`: 2-step ramp (lit 1.0, shade 0.78 with a warm shift), painted albedo, paper grain overlay in object space | main light direction taken from the plate's window (ref-1: from the upper left), not a realtime light loop |
| Ink outline | inverted hull, back faces pushed along **smoothed** normals stored in a vertex colour channel (hard shading normals stay for the toon ramp); width 2.5-3.5 px at framing | the hull boils with a 10 fps UV/vertex jitter |
| Dial face | unlit painted 2048 source (1024 ASTC 6x6 on Quest) from the SVG pipeline | the face itself does not boil (memory), the rim line does |
| Plants | `Card` with alpha-to-coverage; boil = UV jitter at 10 fps (amplitude <= 1.2 px at framing); halo derived from the silhouette | no per-frame textures |
| Tiles | one instanced raised tile; state drawn by the shader (fill, hatch, pale, dashed) | 1 draw for 21 tiles |
| Gnomon shadow | painted wash decal, rotated by `GnomonAngleDeg` | replaces the realtime shadow pass (round 3 measured 4 shadow casters) |
| Table contact | `ShadowCatcher` darkening the real desk under the dial | Quest: on the scene desk plane |
| Room plate (PC only) | `Plate` | never ships to Quest |

## 3. Lighting

Daylight from the room: warm, soft, from the window side of the plate. The drawn object is lit by its toon ramp only
(two steps); its shadow on the real desk is the catcher plus the painted contact shadow. The gnomon's shadow is a
painted wash, never a hard realtime shadow (round-3 gap: "thin and hard").

## 4. Motion

| Motion | Spec | Reduced motion |
|---|---|---|
| Boiling line | 10 fps held clock, jitter <= 1.2 px at framing on plants and rim | off |
| Dial appears | pencil construction 0.4 s -> ink 0.4 s -> washes bleed in 0.4 s | appears drawn |
| Shadow sweep | on launch 06:00 -> now in 1.5 s; then real time, 15 degrees per hour | in place |
| Plant sway | +/- 2 deg, 4 s loop, phase-offset per plant (round-1 motion table) | still |
| Waiting glow | 2.6 s opacity breath on the due plant's halo | static ring |
| Bloom pulse (tend) | scale 1 -> 1.3 -> 1 in 700 ms plus a ring ripple | colour step only |
| Breath circle (dusk ritual) | an ink circle swells while held, shrinks on release; never auto-paced | a fill ring |
| Tile fill | ink floods the tile in 300 ms from the nib side | instant |

## 5. Reference frames and the framing to capture them at

Parity captures: **1824 x 1024**, the dial camera measured in round 3 (`FidelityTools.DialCamera`): **dial ellipse
centre at pixel (785, 615), about 37 degrees elevation, 30 degrees vertical FOV**, over
`shared/assets/room-plates/plate-dial.png` (the kitchen with the dial inpainted out), with
`shared/assets/room-plates/dial-hand-matte.png` laid back on top for the pinch frames (dev captures only; on Quest the
real hand needs occlusion). A seated POV capture (eye 0.35 m above, 0.55 m back, 90 degrees FOV) is reviewed, not scored.

| Gate frame | Source | State to render | Status |
|---|---|---|---|
| **G1 pinch halo** | `A2-05-field-notebook-1.png` | halo on the midday plant, shadow at about 13:00, three plants, tiles mixed | anchor now |
| **G2 idle** | same framing | no halo, shadow at 14:20, waiting glow on the midday plant | anchor now (spec anchor only for the halo-free regions) |
| **G3 drawn dial in a real kitchen** | A/2-05-F1 prompt, generated with `image_gen` in T-SUN-006 | the anime-pushed linework target | **provisional** until the owner picks one at R2 |
| **G4 day 7** | A/2-05-F2 prompt, generated in T-SUN-006 | week drawn in, dusk plant in bloom, one pale tile | provisional until R2 |

`A2-05-field-notebook-2.png` shows the dial held upright with hand-lettered arcs; it is a palette and tile-layout
reference only.

## 6. Spec anchor (gate A2)

| Quantity | Target at the G1 framing | Tolerance |
|---|---|---|
| Dial diameter | 30 cm (outer rim) | +/- 5% |
| Rim ink line | 3 px | 2.5-3.5 px |
| Plant contour | 1.5-2.5 px, varying weight along the stroke | no constant-width contour over 60% of a plant |
| Pencil lines | <= 1 px, `pencil` colour | - |
| Wash colours per arc (pale region) | measured hexes in section 1 | mean CIEDE2000 <= 8 |
| Tiles | 7 per plant, raised 2 mm, 12 x 9 mm | count exact |
| Gnomon | 11 cm, leaning toward the viewer as in ref-1 | +/- 10% |
| Boil | 10 fps +/- 0.5, jitter <= 1.2 px | measured on a 3 s capture |

## 7. Judge rubric (gate A3; per frame and per region, minimum counts)

| Level | Anchor |
|---|---|
| 1 | flat-shaded 3D primitives (round-2 IWSDK "174 draw calls" look) |
| 2 | drawn materials but plants or soil read as cut-outs or CG |
| 3 | every element drawn and painted; one region (plants, soil, tiles, washes) visibly plainer than the reference |
| 4 | reads as a hand-drawn film frame placed into the photographed room at a 2 s glance, in every region |
| 5 | indistinguishable from the reference's look at a 2 s glance |

Capped disqualifiers (at most 2): plants keyed from the reference frame; the room stylised (screen tones, outlines on
real objects); text or legible handwriting; a constant-width "vector" contour on all plants; boil frames drifting colour;
missing outline segments at silhouettes.
