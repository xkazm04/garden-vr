# Habit Garden: art-style storyboard, 20 direction pairs for Leonardo

Written 2026-10-02 beside the round-2 spikes of the `habit-garden` contest. Ten directions for each shortlisted variant:
**A/1 The Breathing Terrarium** (Unity + Meta XR SDK v207, a glass jar on your real table, pinch-hold breathing) and
**A/2 The Sundial Bed** (IWSDK + IWER, glasses-first glance-and-pinch dial). These are **look studies**: they ask "what
could this app look like", not promises about frame rate or final assets. No named artist, studio, film or franchise
appears in any prompt.

## How to use this file

1. **Two images per style.** `XR` emulates what the user sees through the headset (mixed reality: the object over a real
   room); `CONCEPT` is store/key art. Generate both before judging: a look that only works as key art is a poster style,
   not an app style.
2. **Fair comparison.** Within a variant, every XR prompt shares one scene and every concept prompt shares one scene; only
   the opening `Style:` block changes. If you rewrite a scene, rewrite it for all ten.
3. **Leonardo settings (suggested).** XR image **16:9**; crop the centre ~70% to preview the Meta VR Glasses' narrower
   ~70-degree view. Concept image **2:3**. Generate 4 per prompt, keep the best; reroll for hands before judging a style on them.
4. **Negative prompt.** Paste the shared negative plus the style's extra line:

```
text, letters, numbers, clock numerals, captions, watermark, logo, signature, UI panels, menus, frame border, extra fingers, six fingers, fused fingers, missing fingers, deformed hands, twisted wrists, game controller, VR headset visible, smartphone, screen, cluttered background, faces, blurry, low resolution, jpeg artifacts
```

### What every XR image is testing (the app's real constraints)

- **Hands first.** The pinching hand must read clearly against the object and the room; a style that swallows the hand fails.
- **Glanceable.** The state of the garden (what grew, what is waiting) reads in about two seconds with no text.
- **Calm, never shaming.** Nothing looks wilted, broken or red-alarm; a missed day is quiet, not sad.
- **Mixed reality.** The object sits over a real room; dark, heavy or full-frame styles fight passthrough.
- **Feasibility rating** per direction, from what the stacks showed on this machine: **H** = cheap, standard technique;
  **M** = needs care or faking; **L** = likely key-art only. A/2's ratings lean on the IWSDK spike's measurements
  (174 draw calls against a 50 target, a 1.69 MB gzipped bundle, flat/lambert materials proven); A/1's are the host's
  engineering judgement for Unity URP on Quest (transparency over passthrough and refraction are the expensive parts).
  Ratings are judgement, not measurements, until a direction is rendered in the engine.

## Variant A/1: The Breathing Terrarium (Unity, Quest-first, mixed reality)

The hero is a palm-sized vessel on the real desk and a frond that uncoils with your breath. The big technical question for every style is **the glass**: real refraction is off the table on Quest, so each direction either fakes glass cheaply, or swaps it for something opaque.

### Shared scene blocks (already included in every prompt below)

**XR scene:**

> First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.

**Concept scene:**

> Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.

### Index

| # | Style | Family | Feasibility | One line |
|---|---|---|---|---|
| A1-01 | [Botanical Glass Study (round-1 baseline)](#a1-01-botanical-glass-study-round-1-baseline) | Naturalist | M | The round-1 report's look made real: cream light, fern green, teal-edged glass. |
| A1-02 | [Wardian Case Apothecary](#a1-02-wardian-case-apothecary) | Naturalist | M | A brass-and-bevelled-glass fern case from a curio cabinet: collected, cherished, slightly antique. |
| A1-03 | [Night Moss Bioluminescence](#a1-03-night-moss-bioluminescence) | Atmosphere | H | The wind-down jar: moss and fronds glow softly in the dark, breath makes the glow swell. |
| A1-04 | [Celadon Cloche (opaque porcelain)](#a1-04-celadon-cloche-opaque-porcelain) | Crafted | H | Trade the glass for a pale celadon porcelain bell: the plants live in a glazed, softly translucent ceramic dome. |
| A1-05 | [Layered Paper Diorama](#a1-05-layered-paper-diorama) | Crafted | H | The garden as cut-paper layers inside the jar; a breath lifts the next paper frond into place. |
| A1-06 | [Herbarium Watercolour](#a1-06-herbarium-watercolour) | Painterly | M | A pressed-specimen watercolour world: loose washes, pencil underdrawing, the frond painted in as you breathe. |
| A1-07 | [Blown-Glass Garden](#a1-07-blown-glass-garden) | Magical | L | The plants themselves are glass sculptures: every new frond is pulled glass catching the lamp light. |
| A1-08 | [Ink & Rice Paper](#a1-08-ink-rice-paper) | Painterly | H | Brush-ink fern on rice paper: each breath lets the ink bleed one more stroke. |
| A1-09 | [Stop-Motion Clay Jar](#a1-09-stop-motion-clay-jar) | Crafted | H | Plasticine moss and a clay fern with thumbprints: handmade, funny-warm, very approachable. |
| A1-10 | [Sunbeam Soft Cel](#a1-10-sunbeam-soft-cel) | Stylised 3D | H | Clean cel-shaded plants in a warm shaft of light: bold, cheerful, instantly readable. |

<a id="a1-01-botanical-glass-study-round-1-baseline"></a>
### A1-01. Botanical Glass Study (round-1 baseline)

*The round-1 report's look made real: cream light, fern green, teal-edged glass.*

- **Palette:** cream #F4EEDF, fern #3F6B35, moss #6E8F3A, glass teal #5FA8A0, cork #B08355
- **Feasibility (Unity / Quest):** **M** - URP on Quest: fake the glass with an unlit fresnel rim + one transparent pass (no refraction, no screen-space effects); fern as alpha-tested cards with vertex wind. Transparency over passthrough is the cost to watch.
- **What to judge:** Does the jar read as glass with only a rim and a fog layer? Is the fern still the hero in the Glasses' centre crop?

**A1-01-XR** (16:9)

```
Style: refined naturalist illustration rendered as soft stylised 3D. Clean glass with a thin teal edge highlight and a light frosted fog band, matte cream light, crisp fern fronds with gentle vertex-colour gradients from moss to fresh green, warm cork, soft contact shadows on the real desk, quiet and precise, botanical-study clarity. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-01-CONCEPT** (2:3)

```
Style: refined naturalist illustration rendered as soft stylised 3D. Clean glass with a thin teal edge highlight and a light frosted fog band, matte cream light, crisp fern fronds with gentle vertex-colour gradients from moss to fresh green, warm cork, soft contact shadows on the real desk, quiet and precise, botanical-study clarity. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `photorealistic glass refraction, heavy reflections`

<a id="a1-02-wardian-case-apothecary"></a>
### A1-02. Wardian Case Apothecary

*A brass-and-bevelled-glass fern case from a curio cabinet: collected, cherished, slightly antique.*

- **Palette:** aged brass #A07A3C, bevel glass #CFE3DC, deep fern #2F5A33, ink label #2B2420, linen #E8DFCC
- **Feasibility (Unity / Quest):** **M** - Brass via a baked matcap (cheap on Quest), bevels as normal-mapped edges, glass panels as flat transparent quads. Small mesh, high charm per triangle.
- **What to judge:** Is it too precious for a daily habit? Does the brass frame clutter the pinch target?

**A1-02-XR** (16:9)

```
Style: antique naturalist's curio, a miniature brass-framed glass fern case with bevelled panes, warm aged brass with soft patina, ferns and mosses arranged like a collector's specimen, warm reading-lamp light, gentle vignette, careful handcrafted detail, calm scholarly mood. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-02-CONCEPT** (2:3)

```
Style: antique naturalist's curio, a miniature brass-framed glass fern case with bevelled panes, warm aged brass with soft patina, ferns and mosses arranged like a collector's specimen, warm reading-lamp light, gentle vignette, careful handcrafted detail, calm scholarly mood. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `steampunk gears, rust, grime`

<a id="a1-03-night-moss-bioluminescence"></a>
### A1-03. Night Moss Bioluminescence

*The wind-down jar: moss and fronds glow softly in the dark, breath makes the glow swell.*

- **Palette:** night #0E1A1C, glow mint #8FF0C8, spore gold #F2D27A, moss #2C5B45, glass rim #6FB7C9
- **Feasibility (Unity / Quest):** **H** - Emissive unlit materials plus a few additive particle spores: about the cheapest beautiful thing on Quest. Works dimmed over passthrough at night.
- **What to judge:** Is the glow calming or toy-like? Does the pinching hand stay visible in low light?

**A1-03-XR** (16:9)

```
Style: soft bioluminescent night garden. Dim room, the terrarium is the main light source, moss and fern edges glowing gentle mint, tiny drifting golden spores, subtle glass rim light, deep teal shadows, the breath ring a faint luminous line, hushed and sleepy, no harsh contrast. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-03-CONCEPT** (2:3)

```
Style: soft bioluminescent night garden. Dim room, the terrarium is the main light source, moss and fern edges glowing gentle mint, tiny drifting golden spores, subtle glass rim light, deep teal shadows, the breath ring a faint luminous line, hushed and sleepy, no harsh contrast. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `neon, cyberpunk, harsh bloom`

<a id="a1-04-celadon-cloche-opaque-porcelain"></a>
### A1-04. Celadon Cloche (opaque porcelain)

*Trade the glass for a pale celadon porcelain bell: the plants live in a glazed, softly translucent ceramic dome.*

- **Palette:** celadon #A9C8B4, porcelain #F1EFE8, glaze shadow #7E9C8C, leaf #4E7A4B, kiln gold #C9A45C
- **Feasibility (Unity / Quest):** **H** - Avoids the expensive transparency problem entirely: opaque matcap glaze with a cut-away window. The most Quest-friendly way to keep the 'precious vessel' idea.
- **What to judge:** Do you lose the 'see the breath fog the glass' moment? Is the window enough to watch the frond?

**A1-04-XR** (16:9)

```
Style: glazed celadon porcelain craft. A pale green-grey porcelain bell dome with an open arched window, soft crackle glaze, plants sculpted in matte ceramic with subtle glaze pooling, gentle diffuse daylight, minimal and serene, museum-quality ceramics. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-04-CONCEPT** (2:3)

```
Style: glazed celadon porcelain craft. A pale green-grey porcelain bell dome with an open arched window, soft crackle glaze, plants sculpted in matte ceramic with subtle glaze pooling, gentle diffuse daylight, minimal and serene, museum-quality ceramics. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `cracked damaged pottery, busy pattern`

<a id="a1-05-layered-paper-diorama"></a>
### A1-05. Layered Paper Diorama

*The garden as cut-paper layers inside the jar; a breath lifts the next paper frond into place.*

- **Palette:** paper cream #F3ECDD, sage #9CB48A, fern #4F7A45, shadow #6B5E4C, accent coral #E08A6A
- **Feasibility (Unity / Quest):** **H** - Flat unlit cards with baked edge shadows; trivially cheap, and the 'growth' is just another layer animating up. Very agent-buildable.
- **What to judge:** Does flat paper still feel alive and earned? Is the depth readable in stereo?

**A1-05-XR** (16:9)

```
Style: handmade layered papercraft. Every frond, moss tuft and pebble cut from textured coloured paper, visible paper fibres and torn edges, soft shadows between layers, the jar outline a thin curl of translucent vellum, warm natural light, tactile and gentle. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-05-CONCEPT** (2:3)

```
Style: handmade layered papercraft. Every frond, moss tuft and pebble cut from textured coloured paper, visible paper fibres and torn edges, soft shadows between layers, the jar outline a thin curl of translucent vellum, warm natural light, tactile and gentle. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `glossy plastic, photographic leaves`

<a id="a1-06-herbarium-watercolour"></a>
### A1-06. Herbarium Watercolour

*A pressed-specimen watercolour world: loose washes, pencil underdrawing, the frond painted in as you breathe.*

- **Palette:** paper #F5F0E4, wash green #8DB07A, sap #3E6A3A, sepia line #6B4E36, sky wash #A9C6D6
- **Feasibility (Unity / Quest):** **M** - A painterly shader (paper texture + edge darkening + stepped light) is Shader Graph work; fine on Quest if unlit. Paint-in reveal via a dissolve mask.
- **What to judge:** Does the wash style hold up at headset resolution, or look muddy? Is the jar edge readable?

**A1-06-XR** (16:9)

```
Style: botanical herbarium watercolour. Loose transparent washes with blooms and soft edges, fine sepia pencil underdrawing visible, white paper showing through highlights, the fern painted with delicate dry-brush strokes, gentle and airy, like a page from a naturalist's sketchbook brought into 3D. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-06-CONCEPT** (2:3)

```
Style: botanical herbarium watercolour. Loose transparent washes with blooms and soft edges, fine sepia pencil underdrawing visible, white paper showing through highlights, the fern painted with delicate dry-brush strokes, gentle and airy, like a page from a naturalist's sketchbook brought into 3D. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `heavy saturated colour, thick outlines`

<a id="a1-07-blown-glass-garden"></a>
### A1-07. Blown-Glass Garden

*The plants themselves are glass sculptures: every new frond is pulled glass catching the lamp light.*

- **Palette:** clear #E6F2F0, aqua #7CC8C0, amber #E2A64B, smoke #6F7F86, lamp #FFE6B0
- **Feasibility (Unity / Quest):** **L** - Real refraction and caustics are off the table on Quest; fake with matcaps and baked highlight textures. Key-art risk: may look great in Leonardo and cheap in headset.
- **What to judge:** Is it beautiful enough to justify the faking work? Does it lose 'living garden' warmth?

**A1-07-XR** (16:9)

```
Style: art-glass sculpture garden. Fronds, moss and seedling all made of hand-blown coloured glass with soft amber and aqua tints, bright specular glints, gentle caustic light on the desk, the jar and its plants glowing from the lamp, delicate and luxurious, studio craft photography feel. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-07-CONCEPT** (2:3)

```
Style: art-glass sculpture garden. Fronds, moss and seedling all made of hand-blown coloured glass with soft amber and aqua tints, bright specular glints, gentle caustic light on the desk, the jar and its plants glowing from the lamp, delicate and luxurious, studio craft photography feel. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `plastic, cartoon outlines`

<a id="a1-08-ink-rice-paper"></a>
### A1-08. Ink & Rice Paper

*Brush-ink fern on rice paper: each breath lets the ink bleed one more stroke.*

- **Palette:** rice paper #F2EDE1, ink #1E1E1C, ink wash #6E6E6A, seal red #B33A2E, moss grey-green #7D8E6E
- **Feasibility (Unity / Quest):** **H** - Two-tone unlit materials and a stroke-reveal mask; cheap, very legible, beautiful on the Glasses' narrower view.
- **What to judge:** Is monochrome too austere for a morning habit? Does the single red accent carry the reward?

**A1-08-XR** (16:9)

```
Style: expressive black ink brushwork on warm rice paper. The fern drawn in confident wet strokes with soft ink bleeds and dry-brush texture, moss as dotted ink, the jar a single fine line with a pale wash, one small red seal-like accent marking the newest frond, generous negative space, meditative. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-08-CONCEPT** (2:3)

```
Style: expressive black ink brushwork on warm rice paper. The fern drawn in confident wet strokes with soft ink bleeds and dry-brush texture, moss as dotted ink, the jar a single fine line with a pale wash, one small red seal-like accent marking the newest frond, generous negative space, meditative. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `colourful, cluttered, digital gradients`

<a id="a1-09-stop-motion-clay-jar"></a>
### A1-09. Stop-Motion Clay Jar

*Plasticine moss and a clay fern with thumbprints: handmade, funny-warm, very approachable.*

- **Palette:** clay moss #6F9A4E, clay fern #4C7E3E, cork #B98A5A, jar tint #CFE6E2, pebble #9A8F86
- **Feasibility (Unity / Quest):** **H** - Matte lit materials with a thumbprint normal map; frame-stepped animation (12 fps) is a style choice that also saves CPU.
- **What to judge:** Does handmade charm read as 'productivity', or as a kids' toy?

**A1-09-XR** (16:9)

```
Style: stop-motion plasticine model. Moss, fern and pebbles sculpted from soft clay with visible thumbprints and tool marks, slightly wobbly handmade proportions, the jar a thick slightly imperfect glass prop, warm studio lighting, playful but tidy. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-09-CONCEPT** (2:3)

```
Style: stop-motion plasticine model. Moss, fern and pebbles sculpted from soft clay with visible thumbprints and tool marks, slightly wobbly handmade proportions, the jar a thick slightly imperfect glass prop, warm studio lighting, playful but tidy. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `glossy CGI, realistic plants`

<a id="a1-10-sunbeam-soft-cel"></a>
### A1-10. Sunbeam Soft Cel

*Clean cel-shaded plants in a warm shaft of light: bold, cheerful, instantly readable.*

- **Palette:** sun #FFE3A3, leaf light #8CCB6A, leaf shade #3E7B4A, glass edge #7FC1C8, desk #B07B4F
- **Feasibility (Unity / Quest):** **H** - Two-step toon ramp + outline pass: standard on Quest at 72 Hz. Light shaft as an additive card. The safest high-polish route.
- **What to judge:** Is it distinctive, or generic stylised? Does it survive next to a real room in passthrough?

**A1-10-XR** (16:9)

```
Style: soft cel-shaded stylised 3D. Clean two-tone shading with crisp but gentle outlines, a warm shaft of sunlight across the desk, saturated fresh greens, the glass jar a simple shape with one bright edge highlight, cheerful animated-feature clarity, uncluttered. First-person view from a seated person's eyes at a real wooden desk in an ordinary home room, evening lamp light, mixed-reality look. On the desk within arm's reach stands a palm-sized glass terrarium jar with a cork lid; inside, a young fern fiddlehead is uncoiling above a bed of moss beside two settled fronds and one small seedling. In the lower foreground, the person's own bare right hand holds a gentle pinch of thumb and index finger near the jar; the left hand rests open on the desk. A faint ring of light circles the jar's base, half filled, pacing a slow breath; soft condensation fogs the upper glass. Calm, intimate, uncluttered; the jar, the uncoiling frond and the pinching hand are the focal points.
```

**A1-10-CONCEPT** (2:3)

```
Style: soft cel-shaded stylised 3D. Clean two-tone shading with crisp but gentle outlines, a warm shaft of sunlight across the desk, saturated fresh greens, the glass jar a simple shape with one bright edge highlight, cheerful animated-feature clarity, uncluttered. Key art for an original calm daily-ritual app. A palm-sized glass terrarium jar with a cork lid sits on a sunlit wooden desk beside a cup of tea; inside, a lush small fern garden with one new frond unfurling and a single tiny flower. Two cupped human hands rest either side of the jar, not touching it. A thin halo of soft light rises from the moss like a held breath. Quiet morning, gentle depth of field, inviting, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `gritty, realistic textures`


## Variant A/2: The Sundial Bed (IWSDK / WebXR, glasses-first)

The hero is a plate-sized dial read in two seconds. WebXR favours **flat, unlit or lambert materials, vertex colour, few textures, no post-processing**, and every extra mesh is a draw call until instancing is in; styles built from lines, flat print or one painted texture are native to this stack.

### Shared scene blocks (already included in every prompt below)

**XR scene:**

> First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.

**Concept scene:**

> Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.

### Index

| # | Style | Family | Feasibility | One line |
|---|---|---|---|---|
| A2-01 | [Daylight Instrument (round-1 baseline)](#a2-01-daylight-instrument-round-1-baseline) | Instrument | H | The spike's chosen look, pushed: matte terracotta rim, warm cream bed, flat honest colour. |
| A2-02 | [Brass Astrolabe](#a2-02-brass-astrolabe) | Instrument | M | A scientific instrument you tend: engraved brass rings, enamel arcs, a gnomon like a sextant arm. |
| A2-03 | [Majolica Plate](#a2-03-majolica-plate) | Crafted | H | The dial is a hand-painted tin-glazed plate: blue-and-ochre brushwork, glossy glaze, plants as painted motifs that pop up. |
| A2-04 | [Risograph Print](#a2-04-risograph-print) | Flat graphic | H | Three spot inks, grain and slight misregistration: a printed object that changes with the day. |
| A2-05 | [Field Notebook (paper & ink)](#a2-05-field-notebook-paper-ink) | Painterly | H | The spike's third direction, refined: a naturalist's notebook page curled into a dial, ink plants, watercolour arcs. |
| A2-06 | [Celadon Lantern at Dusk](#a2-06-celadon-lantern-at-dusk) | Atmosphere | H | The spike's porcelain direction: a pale celadon dial glowing from within as evening falls. |
| A2-07 | [Cyanotype Sun-Print](#a2-07-cyanotype-sun-print) | Flat graphic | H | A sun-print dial: the plants appear as white botanical silhouettes on Prussian blue, exposed by the day. |
| A2-08 | [Light-Line Diagram (glasses-native)](#a2-08-light-line-diagram-glasses-native) | Magical | H | Built for see-through glasses: the dial is drawn in thin luminous lines over the real table, almost weightless. |
| A2-09 | [Knot-Garden Parterre](#a2-09-knot-garden-parterre) | Naturalist | M | A formal miniature hedge garden: clipped box hedges trace the three arcs, gravel paths, topiary gnomon. |
| A2-10 | [Terracotta & Moss](#a2-10-terracotta-moss) | Crafted | H | A hand-thrown clay planter dial with moss filling each arc: earthy, tactile, warm. |

<a id="a2-01-daylight-instrument-round-1-baseline"></a>
### A2-01. Daylight Instrument (round-1 baseline)

*The spike's chosen look, pushed: matte terracotta rim, warm cream bed, flat honest colour.*

- **Palette:** cream #F4EDE1, terracotta #C2643F, sunrise #F2C766, coral #E58A6B, lavender #9C8FD6, soil #7A5A43
- **Feasibility (IWSDK / WebXR):** **H** - Spike-proven in IWSDK: flat/lambert materials, vertex colours, one shadow. Needs instancing to get from the measured 174 draw calls toward the 50 target.
- **What to judge:** Does it look designed, or like the spike's primitives? Is the shadow readable as 'now'?

**A2-01-XR** (16:9)

```
Style: modern product-design instrument, matte and precise. A terracotta rim with fine engraved hour ticks, warm cream soil plane, three flat colour arcs, simple elegant plants with faceted leaves, a single crisp soft-edged shadow, soft daylight, calm Scandinavian clarity, nothing decorative that is not information. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-01-CONCEPT** (2:3)

```
Style: modern product-design instrument, matte and precise. A terracotta rim with fine engraved hour ticks, warm cream soil plane, three flat colour arcs, simple elegant plants with faceted leaves, a single crisp soft-edged shadow, soft daylight, calm Scandinavian clarity, nothing decorative that is not information. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `glossy, cluttered ornament`

<a id="a2-02-brass-astrolabe"></a>
### A2-02. Brass Astrolabe

*A scientific instrument you tend: engraved brass rings, enamel arcs, a gnomon like a sextant arm.*

- **Palette:** brass #B8913F, dark brass #6E5326, enamel sunrise #E9B949, enamel coral #D9735A, enamel lavender #8A7CC8, verdigris #5E9C8C
- **Feasibility (IWSDK / WebXR):** **M** - Brass via matcap texture (no env-map PBR); engraving baked into one atlas. Watch texture memory in the bundle.
- **What to judge:** Is metal too cold for a habit garden? Do the plants still feel alive against brass?

**A2-02-XR** (16:9)

```
Style: antique scientific instrument. Engraved polished brass rings and a sextant-like gnomon, inlaid enamel arcs in warm sunrise, coral and lavender, small living plants growing from brass planters, fine engraved lines, soft studio light with gentle glints, precise and heirloom-like. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-02-CONCEPT** (2:3)

```
Style: antique scientific instrument. Engraved polished brass rings and a sextant-like gnomon, inlaid enamel arcs in warm sunrise, coral and lavender, small living plants growing from brass planters, fine engraved lines, soft studio light with gentle glints, precise and heirloom-like. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `steampunk gears, rust`

<a id="a2-03-majolica-plate"></a>
### A2-03. Majolica Plate

*The dial is a hand-painted tin-glazed plate: blue-and-ochre brushwork, glossy glaze, plants as painted motifs that pop up.*

- **Palette:** tin white #F6F1E6, cobalt #2F4F9C, ochre #D79A2B, copper green #4F8E5A, manganese #6B3F3A
- **Feasibility (IWSDK / WebXR):** **H** - One painted texture on a disc plus a few low-poly plants: the cheapest rich-looking option in WebXR. Glaze sheen via a cheap specular.
- **What to judge:** Is it charming or kitsch? Does the glaze highlight fight the 'now' shadow?

**A2-03-XR** (16:9)

```
Style: hand-painted tin-glazed majolica ceramic. The dial a glossy white-glazed plate with loose cobalt, ochre and copper-green brushwork bands for morning, midday and dusk, plants painted as raised glazed motifs, slight glaze pooling, warm daylight with one soft highlight, Mediterranean craft. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-03-CONCEPT** (2:3)

```
Style: hand-painted tin-glazed majolica ceramic. The dial a glossy white-glazed plate with loose cobalt, ochre and copper-green brushwork bands for morning, midday and dusk, plants painted as raised glazed motifs, slight glaze pooling, warm daylight with one soft highlight, Mediterranean craft. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `chipped, dirty, photorealistic`

<a id="a2-04-risograph-print"></a>
### A2-04. Risograph Print

*Three spot inks, grain and slight misregistration: a printed object that changes with the day.*

- **Palette:** riso pink #FF48B0, riso yellow #FFE800, riso teal #00838A, paper #F4EFE6, ink black #232020
- **Feasibility (IWSDK / WebXR):** **H** - Unlit flat shapes + a single grain overlay texture; extremely cheap and very readable on the Glasses' 70-degree view.
- **What to judge:** Does flat print lose the sense of a living garden? Is it a style or a gimmick?

**A2-04-XR** (16:9)

```
Style: risograph print brought into 3D. Flat shapes in three spot inks, fluorescent pink, sunny yellow and deep teal, visible grain and slight misregistration offsets, plants as simple cut silhouettes, paper-white base, playful editorial design energy, crisp and graphic. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-04-CONCEPT** (2:3)

```
Style: risograph print brought into 3D. Flat shapes in three spot inks, fluorescent pink, sunny yellow and deep teal, visible grain and slight misregistration offsets, plants as simple cut silhouettes, paper-white base, playful editorial design energy, crisp and graphic. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `gradients, realistic shading`

<a id="a2-05-field-notebook-paper-ink"></a>
### A2-05. Field Notebook (paper & ink)

*The spike's third direction, refined: a naturalist's notebook page curled into a dial, ink plants, watercolour arcs.*

- **Palette:** page #F3EEE2, ink #2A2622, wash ochre #E2B866, wash coral #E39C82, wash lilac #A79AD6, pencil #8A8178
- **Feasibility (IWSDK / WebXR):** **H** - Unlit textured planes and line meshes; spike-proven as a passing art direction. Text-free annotation marks only.
- **What to judge:** Is a notebook a fitting metaphor for 'tend in twenty seconds'? Does line art stay legible at arm's length?

**A2-05-XR** (16:9)

```
Style: naturalist field-notebook page curled into a round dial. Fine ink linework plants, loose watercolour washes filling the three arcs, pencil construction lines and tiny tick marks, the gnomon drawn like a fountain-pen stroke casting a pale wash shadow, warm paper texture, thoughtful and handmade. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-05-CONCEPT** (2:3)

```
Style: naturalist field-notebook page curled into a round dial. Fine ink linework plants, loose watercolour washes filling the three arcs, pencil construction lines and tiny tick marks, the gnomon drawn like a fountain-pen stroke casting a pale wash shadow, warm paper texture, thoughtful and handmade. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `handwriting, legible words`

<a id="a2-06-celadon-lantern-at-dusk"></a>
### A2-06. Celadon Lantern at Dusk

*The spike's porcelain direction: a pale celadon dial glowing from within as evening falls.*

- **Palette:** dusk navy #121A2B, celadon #A9CDB8, lantern gold #F5C878, coral glow #E9937A, lilac glow #B4A6EC
- **Feasibility (IWSDK / WebXR):** **H** - Emissive unlit materials on a dark background; cheap, and the night variant of the daylight look. Needs care over passthrough (dark-on-real-room).
- **What to judge:** Do you want the app to have a time-of-day look switch? Is the glow calm or gaudy?

**A2-06-XR** (16:9)

```
Style: glowing celadon porcelain lantern at dusk. The dial a pale green translucent porcelain disc lit softly from within, warm lantern light pooling in the dusk arc, plants with gently luminous leaf edges, deep blue evening around it, quiet and contemplative. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-06-CONCEPT** (2:3)

```
Style: glowing celadon porcelain lantern at dusk. The dial a pale green translucent porcelain disc lit softly from within, warm lantern light pooling in the dusk arc, plants with gently luminous leaf edges, deep blue evening around it, quiet and contemplative. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `neon, hard bloom, sci-fi`

<a id="a2-07-cyanotype-sun-print"></a>
### A2-07. Cyanotype Sun-Print

*A sun-print dial: the plants appear as white botanical silhouettes on Prussian blue, exposed by the day.*

- **Palette:** prussian #1D3F7A, cyan mid #3F6FAE, print white #F2F4F6, paper edge #E9E2D3, sun gold #F1C25B
- **Feasibility (IWSDK / WebXR):** **H** - Two-tone unlit, a 'developing' reveal shader for growth; tiny bundle and few draw calls. Strong concept fit: sunlight makes the record.
- **What to judge:** Is monochrome blue warm enough for morning? Can the three arcs be told apart?

**A2-07-XR** (16:9)

```
Style: cyanotype sun-print botanical. The dial a disc of deep Prussian blue paper, plants and fern shapes as crisp white photogram silhouettes, three arcs as subtle tonal blues, one warm gold sun marker, soft paper texture and slightly uneven exposure edges, elegant and scientific. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-07-CONCEPT** (2:3)

```
Style: cyanotype sun-print botanical. The dial a disc of deep Prussian blue paper, plants and fern shapes as crisp white photogram silhouettes, three arcs as subtle tonal blues, one warm gold sun marker, soft paper texture and slightly uneven exposure edges, elegant and scientific. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `full colour, cartoon`

<a id="a2-08-light-line-diagram-glasses-native"></a>
### A2-08. Light-Line Diagram (glasses-native)

*Built for see-through glasses: the dial is drawn in thin luminous lines over the real table, almost weightless.*

- **Palette:** line white #F7F5EE, sunrise line #FFD57A, coral line #FF9C82, lilac line #BDAEFF, glow #FFFFFF
- **Feasibility (IWSDK / WebXR):** **H** - Line meshes and a few emissive fills: minimal occlusion of the real room, smallest draw-call budget, perfect for the Glasses' passthrough. The most 'glasses-first' look.
- **What to judge:** Is it beautiful or just a HUD? Does a line garden still feel like a garden?

**A2-08-XR** (16:9)

```
Style: luminous line drawing floating in a real room. The dial, its three arcs, the gnomon and the plants drawn in thin glowing vector lines with soft light falloff, a few translucent colour fills only where a day was kept, the real table visible through it, elegant, airy, almost weightless. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-08-CONCEPT** (2:3)

```
Style: luminous line drawing floating in a real room. The dial, its three arcs, the gnomon and the plants drawn in thin glowing vector lines with soft light falloff, a few translucent colour fills only where a day was kept, the real table visible through it, elegant, airy, almost weightless. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `thick UI panels, sci-fi hologram clutter`

<a id="a2-09-knot-garden-parterre"></a>
### A2-09. Knot-Garden Parterre

*A formal miniature hedge garden: clipped box hedges trace the three arcs, gravel paths, topiary gnomon.*

- **Palette:** box green #3E6B3A, light hedge #6F9A52, gravel #D8CBB2, sunrise bloom #F3C14E, coral bloom #E57C5C, lavender #9C88D3
- **Feasibility (IWSDK / WebXR):** **M** - Many small hedge pieces need GPU instancing (proven necessary by the spike's draw-call count); otherwise feasible.
- **What to judge:** Does a formal garden feel like tending or like admiring? Does it read at the Glasses' scale?

**A2-09-XR** (16:9)

```
Style: miniature formal knot garden parterre. Clipped box hedges forming the round dial and its three arcs, raked pale gravel paths, small flowering beds in sunrise yellow, coral and lavender, a topiary gnomon casting a neat shadow, tilt-shift miniature look, bright morning light, orderly and serene. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-09-CONCEPT** (2:3)

```
Style: miniature formal knot garden parterre. Clipped box hedges forming the round dial and its three arcs, raked pale gravel paths, small flowering beds in sunrise yellow, coral and lavender, a topiary gnomon casting a neat shadow, tilt-shift miniature look, bright morning light, orderly and serene. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `overgrown, wild, messy`

<a id="a2-10-terracotta-moss"></a>
### A2-10. Terracotta & Moss

*A hand-thrown clay planter dial with moss filling each arc: earthy, tactile, warm.*

- **Palette:** terracotta #B8643E, clay light #D99A6C, moss #5F8A3E, moss light #8DB45A, sunrise glaze #EEC15A, dusk glaze #8E7CC3
- **Feasibility (IWSDK / WebXR):** **H** - Matte lit clay with a thumb-tool normal map; moss as instanced tufts. Close to the spike's look, with more tactility for little cost.
- **What to judge:** Is it too earthy/brown for a glanceable instrument? Do the three arcs separate clearly?

**A2-10-XR** (16:9)

```
Style: hand-thrown terracotta planter shaped like a sundial, visible throwing rings and thumb marks, soft green moss cushions filling the arcs, small glazed ceramic markers in sunrise, coral and dusk lavender, a clay gnomon with a warm soft shadow, natural light, earthy and tactile. First-person view from a seated person's eyes at a real kitchen table in daylight, mixed-reality look. Floating just below eye level, within arm's reach, a small round garden bed shaped like a sundial, the size of a dinner plate. Its rim is divided into three arcs: a warm sunrise arc, a midday coral arc and a dusk lavender arc. A slender central gnomon casts a soft shadow pointing at the midday arc. In each arc grows one small plant beside a row of seven little tiles, some filled with colour, some pale. A gentle glowing ring marks the plant the person is looking at, and the person's bare right hand makes a light pinch of thumb and index finger toward it. Calm, legible at a glance; the dial, its shadow and the pinching hand are the focal points.
```

**A2-10-CONCEPT** (2:3)

```
Style: hand-thrown terracotta planter shaped like a sundial, visible throwing rings and thumb marks, soft green moss cushions filling the arcs, small glazed ceramic markers in sunrise, coral and dusk lavender, a clay gnomon with a warm soft shadow, natural light, earthy and tactile. Key art for an original daily-habit app. A small round sundial garden bed seen from a three-quarter angle at golden hour, three coloured arcs around its rim for morning, midday and dusk, a slender gnomon casting a long clean shadow across it, one small plant in each arc, the dusk plant just blooming. A single human hand reaches in from the side with a light pinch, almost touching the bloom. Clean, warm, optimistic, poster composition, strong simple silhouette.
```

**Negative (shared + extra):** `dirty, cracked, dull grey`


## Scoring sheet

Score 1-5. **Hands** = is the pinching hand clear? **Glance** = can you read the garden's state in two seconds?
**Calm** = soothing and never shaming? **Original** = would a judge remember it? **Feasible** = the H/M/L above, adjusted by
what you see. **Glasses** = does the centre-70% crop still work?

### A/1 The Breathing Terrarium

| # | Style | Hands | Glance | Calm | Original | Feasible | Glasses | Keep? | Note |
|---|---|---|---|---|---|---|---|---|---|
| A1-01 | Botanical Glass Study (round-1 baseline) | | | | | M | | | |
| A1-02 | Wardian Case Apothecary | | | | | M | | | |
| A1-03 | Night Moss Bioluminescence | | | | | H | | | |
| A1-04 | Celadon Cloche (opaque porcelain) | | | | | H | | | |
| A1-05 | Layered Paper Diorama | | | | | H | | | |
| A1-06 | Herbarium Watercolour | | | | | M | | | |
| A1-07 | Blown-Glass Garden | | | | | L | | | |
| A1-08 | Ink & Rice Paper | | | | | H | | | |
| A1-09 | Stop-Motion Clay Jar | | | | | H | | | |
| A1-10 | Sunbeam Soft Cel | | | | | H | | | |

### A/2 The Sundial Bed

| # | Style | Hands | Glance | Calm | Original | Feasible | Glasses | Keep? | Note |
|---|---|---|---|---|---|---|---|---|---|
| A2-01 | Daylight Instrument (round-1 baseline) | | | | | H | | | |
| A2-02 | Brass Astrolabe | | | | | M | | | |
| A2-03 | Majolica Plate | | | | | H | | | |
| A2-04 | Risograph Print | | | | | H | | | |
| A2-05 | Field Notebook (paper & ink) | | | | | H | | | |
| A2-06 | Celadon Lantern at Dusk | | | | | H | | | |
| A2-07 | Cyanotype Sun-Print | | | | | H | | | |
| A2-08 | Light-Line Diagram (glasses-native) | | | | | H | | | |
| A2-09 | Knot-Garden Parterre | | | | | M | | | |
| A2-10 | Terracotta & Moss | | | | | H | | | |

## After the round

Shortlist 2-3 directions per variant, then run one follow-up pair per finalist with a different moment (A/1: the sixth
breath, when a frond stays for good; A/2: day 7, the look-back over the week) before choosing. Fusions are welcome (for
example, A/2-08's line language on A/2-03's plate). The finalists then go back into the engine: a direction counts as
chosen only once it renders in the spike at the target frame rate.
