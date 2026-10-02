# Habit Garden: the owner's art-direction choice

Recorded 2026-10-02, from the storyboard round (`ART-STYLE-STORYBOARD.md` in this folder).

> "Winners we should chase: A/1-03, A/2-05 through its potential to combine hand-drawn/anime style into reality"

| Variant | Chosen direction | Why (owner) | Stack consequence (host) |
|---|---|---|---|
| A/1 The Breathing Terrarium (Unity) | **03 Night Moss Bioluminescence** | chosen | Emissive unlit materials + a few additive particles: the cheapest beautiful route on Quest. Ties the jar to the **wind-down** moment; over passthrough it wants a dimmed room, so decide whether the jar dims the passthrough around it or only glows. |
| A/2 The Sundial Bed (IWSDK) | **05 Field Notebook (paper & ink)**, pushed toward **hand-drawn / anime linework living in the real room** | "its potential to combine hand-drawn/anime style into reality" | Spike-proven as a passing direction. The push needs a toon ramp (2-3 flat shade steps), an ink outline (inverted-hull or edge shader), paper-grain overlay and boiling-line animation (redraw jitter at ~8-12 fps) - all cheap unlit Three.js work, no post-processing. The contrast is the point: drawn object, real room. |

The ratings stay judgement until each look renders in its spike at frame rate (the storyboard's rule).

## Follow-up prompts (paste into Leonardo)

Same shared negative as the storyboard. A follow-up uses a **different moment** than round one so a style is judged on
more than one frame.

### A/1-03 Night Moss - follow-up

**A/1-03-F1 XR: the sixth breath, a frond stays** (16:9)

```
Style: soft bioluminescent night garden. Dim room, the terrarium is the main light source, moss and fern edges glowing gentle mint, tiny drifting golden spores, subtle glass rim light, deep teal shadows, hushed and sleepy, no harsh contrast. First-person view from a seated person's eyes at a real wooden desk in a dark bedroom at night, mixed-reality look, the room barely visible. On the desk within arm's reach a palm-sized glass terrarium jar with a cork lid; inside, a fern frond has just finished unfurling and settles into place, a single bead of glowing dew rolling off its tip, a slow ripple of brighter mint light spreading through the moss. The person's bare right hand is opening from a pinch, fingers relaxing, softly lit by the jar from below. A faint complete ring of light circles the jar's base. The moment of a small reward, quiet and earned; the new frond, the dew bead and the relaxing hand are the focal points.
```

**A/1-03-F2 XR: day 7, the jar at a glance** (16:9)

```
Style: soft bioluminescent night garden. Dim room, the terrarium is the main light source, moss and fern edges glowing gentle mint, tiny drifting golden spores, subtle glass rim light, deep teal shadows, hushed and sleepy, no harsh contrast. First-person view from a seated person's eyes at a real desk at night, mixed-reality look. A palm-sized glass terrarium jar holds a small lush fern garden grown over a week: seven fronds of slightly different sizes, each glowing a little differently, the newest brightest, a first tiny luminous flower opening at the centre, one quieter frond still healthy but unlit where a day was missed. The person's bare hand rests open beside the jar. Calm pride, nothing wilted or sad; the garden's week is readable at a glance.
```

**A/1-03-F3 CONCEPT: store key art** (2:3)

```
Style: soft bioluminescent night garden, luminous mint and gold on deep teal darkness, drifting spores, gentle glass rim light, hushed and sleepy. Key art for an original wind-down ritual app. A palm-sized glass terrarium glowing on a bedside table at night, a fern unfurling inside, a soft halo of light breathing out of the moss, two relaxed human hands cupped around it without touching, warm faint lamp light at the edge of frame, deep calm. Poster composition, strong simple silhouette, generous dark negative space for a title.
```

**Extra negative:** `neon, cyberpunk, harsh bloom, horror, glowing eyes`

### A/2-05 Field Notebook, pushed to hand-drawn / anime linework in a real room - follow-up

The original A/2-05 prompt stays the control. These push the line toward hand-drawn animation: clean confident ink
contours, flat cel colour with one shade step, soft watercolour backgrounds only inside the dial, and the **real room left
photographic** so the drawn object visibly lives in reality.

**A/2-05-F1 XR: the drawn dial in a real kitchen** (16:9)

```
Style: hand-drawn animation linework placed into a real photographed room. The sundial garden is drawn like a frame of a hand-animated film: clean confident ink contours of varying weight, flat cel colours with a single soft shade step, loose watercolour washes filling the three arcs, a few pencil construction lines and tiny tick marks, slight paper grain only on the drawn object; the surrounding kitchen stays a real photograph with natural light and depth. First-person view from a seated person's eyes at a real kitchen table in daylight. Floating just below eye level, a plate-sized round sundial garden bed with three arcs for morning, midday and dusk, a slender inked gnomon casting a soft painted shadow at midday, one small drawn plant in each arc beside seven little tiles, some coloured, some pale. The person's real bare right hand pinches toward the midday plant, and a hand-drawn sparkle ring answers the pinch. The contrast between drawn object and real room is the focal point.
```

**A/2-05-F2 XR: day 7, the week drawn in** (16:9)

```
Style: hand-drawn animation linework placed into a real photographed room. Clean confident ink contours of varying weight, flat cel colours with a single soft shade step, loose watercolour washes inside the drawn object, pencil construction lines, slight paper grain only on the drawn object; the room stays a real photograph. First-person view from a seated person's eyes at a real desk in warm evening light. Floating within arm's reach, a plate-sized drawn sundial garden after one week: the three arcs full of inked plants of different sizes, the dusk plant in bloom drawn with lively animated petals, seven tiles per plant mostly coloured, one pale tile left quiet and unshamed, small hand-drawn motion lines showing the gnomon's shadow sweeping across the week. The person's real hand rests palm-up beside it. Warm, proud, legible at a glance.
```

**A/2-05-F3 CONCEPT: store key art** (2:3)

```
Style: hand-drawn animation key frame composited into a real photographed home. A plate-sized sundial garden drawn in clean ink contours with flat cel colour and soft watercolour washes floats above a real wooden table in golden-hour light; three coloured arcs, a slender inked gnomon casting a long painted shadow, small drawn plants, the dusk plant blooming with animated petals and a few hand-drawn sparkles. A real human hand reaches in with a light pinch toward the bloom. The drawn and the real side by side, optimistic and warm. Poster composition, strong simple silhouette, clean space at the top for a title.
```

**Extra negative:** `3D render, CGI, plastic, photorealistic plants, chibi characters, faces, speech bubbles, manga text, screen tones covering the room`

## Next

1. Generate the three follow-ups for each winner; keep 1-2 frames per winner as the art reference.
2. Hand the reference frames to the next build step so each spike renders its chosen look in-engine (A/1: emissive
   moss + spores in Unity URP; A/2: toon ramp + ink outline + paper grain + boiling line in IWSDK), and measure frame
   rate and draw calls against the targets.
