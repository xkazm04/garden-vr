// Painted dial face, gnomon albedo, and gnomon-shadow decal.
// Adapted from tools/blender/from-iwsdk/dial_svg.mjs. Washes are graded to the
// measured column in docs/art/sundial-style.md, with wet-edge pooling and granulation.
// Rasterised by system Chrome through playwright-core (pinned in package.json).
//   node apps/sundial/Art/Scripts/dial_face.mjs
import { chromium } from 'playwright-core';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { mkdirSync } from 'node:fs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.resolve(HERE, '../../Assets/Art/Textures');
mkdirSync(OUT, { recursive: true });

const CHROME = process.env.CHROME_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe';

let seed = 1;
const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
const r2 = (n) => Math.round(n * 100) / 100;

// Measured pales, intent mids, wet-edge pools. See sundial-style.md section 1.
// Wider than the first SVG so each wash fills its sector. About 18 degrees of
// bare paper stays at the near rim, between dusk at -100 and morning at 242.
const WASHES = [
  // The near rim (about 250 deg) is the front of the plate. The two arcs meet there
  // so the camera does not see a bare paper wedge. Tile spans stay narrower.
  { id: 'sunrise', a0: 270, a1: 108, pale: '#F6DEB7', pale2: '#F0E0BE', mid: '#E2B866', wet: '#D9762A', deep: '#A85A1C' },
  { id: 'midday', a0: 112, a1: 18, pale: '#F4B6A1', pale2: '#EFBC9B', mid: '#E39C82', wet: '#C9483F', deep: '#8E3028' },
  { id: 'dusk', a0: 22, a1: -128, pale: '#CAB0C5', pale2: '#BFA1BC', mid: '#A79AD6', wet: '#6F55AD', deep: '#4A3480' },
];

function face() {
  const S = 2048, C = S / 2, R = S / 2 - 8;
  const pol = (r, deg) => [C + r * Math.cos((deg * Math.PI) / 180), C - r * Math.sin((deg * Math.PI) / 180)];
  const arc = (r0, r1, a0, a1) => {
    const [x0, y0] = pol(r1, a0), [x1, y1] = pol(r1, a1), [x2, y2] = pol(r0, a1), [x3, y3] = pol(r0, a0);
    const large = Math.abs(a1 - a0) > 180 ? 1 : 0;
    return `M${r2(x0)},${r2(y0)} A${r2(r1)},${r2(r1)} 0 ${large} 1 ${r2(x1)},${r2(y1)} L${r2(x2)},${r2(y2)} A${r2(r0)},${r2(r0)} 0 ${large} 0 ${r2(x3)},${r2(y3)} Z`;
  };
  const arcStroke = (r, a0, a1) => {
    const [x0, y0] = pol(r, a0), [x1, y1] = pol(r, a1);
    const large = Math.abs(a1 - a0) > 180 ? 1 : 0;
    return `M${r2(x0)},${r2(y0)} A${r2(r)},${r2(r)} 0 ${large} 1 ${r2(x1)},${r2(y1)}`;
  };

  // Varying line weight: short strokes, width wanders along the circle.
  seed = 3;
  let rim = '';
  const rimN = 96;
  for (let i = 0; i < rimN; i++) {
    const a0 = (i / rimN) * 360;
    const a1 = ((i + 1) / rimN) * 360 + 0.4;
    const wOuter = 7.5 + 5.5 * Math.sin(i * 0.47) + rnd() * 3.2;
    const wInner = 4.2 + 2.6 * Math.sin(i * 0.63 + 1.1) + rnd() * 1.8;
    const [x0, y0] = pol(R * 0.992, a0);
    const [x1, y1] = pol(R * 0.992, a1);
    const [u0, v0] = pol(R * 0.888, a0);
    const [u1, v1] = pol(R * 0.888, a1);
    rim += `<line x1="${r2(x0)}" y1="${r2(y0)}" x2="${r2(x1)}" y2="${r2(y1)}" stroke-width="${r2(wOuter)}" stroke-linecap="round"/>`;
    rim += `<line x1="${r2(u0)}" y1="${r2(v0)}" x2="${r2(u1)}" y2="${r2(v1)}" stroke-width="${r2(wInner)}" stroke-linecap="round"/>`;
  }

  let ticks = '';
  seed = 11;
  for (let d = 0; d < 360; d += 2) {
    const long = d % 15 === 0;
    const mid = d % 5 === 0;
    const len = long ? 0.062 : mid ? 0.040 : 0.022;
    const w = (long ? 4.6 : mid ? 3.1 : 2.05) + rnd() * 0.8;
    const [x0, y0] = pol(R * 0.978, d);
    const [x1, y1] = pol(R * (0.978 - len), d);
    ticks += `<line x1="${r2(x0)}" y1="${r2(y0)}" x2="${r2(x1)}" y2="${r2(y1)}" stroke-width="${r2(w)}" stroke-linecap="round"/>`;
  }

  // Pencil construction: radials, compass arcs, a centre cross. No lettering.
  let constr = '';
  seed = 17;
  for (let d = 0; d < 180; d += 15) {
    const [x0, y0] = pol(R * 0.16, d);
    const [x1, y1] = pol(R * 0.90, d);
    const [x2, y2] = pol(R * 0.16, d + 180);
    const [x3, y3] = pol(R * 0.90, d + 180);
    const w = 1.15 + rnd() * 0.9;
    constr += `<line x1="${r2(x0)}" y1="${r2(y0)}" x2="${r2(x1)}" y2="${r2(y1)}" stroke-width="${r2(w)}"/>`;
    constr += `<line x1="${r2(x2)}" y1="${r2(y2)}" x2="${r2(x3)}" y2="${r2(y3)}" stroke-width="${r2(w)}"/>`;
  }
  for (const rr of [0.30, 0.48, 0.70]) {
    const n = 40;
    for (let i = 0; i < n; i++) {
      if (i % 2 === 0 && rr < 0.6) continue;
      const a0 = (i / n) * 360;
      const a1 = ((i + 0.72) / n) * 360;
      constr += `<path d="${arcStroke(R * rr, a0, a1)}" fill="none" stroke-width="${r2(1.1 + rnd())}"/>`;
    }
  }
  constr += `<line x1="${r2(C - 28)}" y1="${r2(C)}" x2="${r2(C + 28)}" y2="${r2(C)}" stroke-width="1.4"/>`;
  constr += `<line x1="${r2(C)}" y1="${r2(C - 28)}" x2="${r2(C)}" y2="${r2(C + 28)}" stroke-width="1.4"/>`;

  // Brush direction across the whole sector, not a thin crescent at the rim.
  let brushes = '';
  seed = 23;
  for (const w of WASHES) {
    const span = ((w.a0 - w.a1) + 360) % 360;
    for (let s = 0; s < 16; s++) {
      const t = s / 15;
      const a0 = w.a0 - span * (0.02 + rnd() * 0.06);
      const a1 = w.a0 - span * (0.62 + rnd() * 0.34);
      const rr = R * (0.26 + t * 0.60);
      const col = s % 3 === 0 ? w.wet : s % 3 === 1 ? w.mid : w.pale2;
      brushes += `<path d="${arcStroke(rr, a0, a1)}" fill="none" stroke="${col}" stroke-width="${r2(26 + rnd() * 36)}" stroke-opacity="${r2(0.24 + rnd() * 0.22)}" stroke-linecap="round"/>`;
    }
  }

  // The mound mesh is about 0.46 of the face radius. Paint a lighter earth disc
  // on that footprint, with stipple and pebbles, and let the washes fill outside it.
  seed = 41;
  const sx = C;
  const sy = C + R * 0.012;
  const soilRx = R * 0.48;
  const soilRy = R * 0.45;

  let stipple = '';
  seed = 77;
  for (let i = 0; i < 7400; i++) {
    const a = rnd() * Math.PI * 2;
    const u = Math.pow(rnd(), 0.62);
    const x = sx + Math.cos(a) * soilRx * u;
    const y = sy + Math.sin(a) * soilRy * u;
    const rad = 0.55 + rnd() * rnd() * 2.6;
    const pick = rnd();
    const fill = pick > 0.86 ? '#E7D7C6' : pick > 0.55 ? '#5E4636' : '#8A6E58';
    stipple += `<circle cx="${r2(x)}" cy="${r2(y)}" r="${r2(rad)}" fill="${fill}" opacity="${r2(0.32 + rnd() * 0.5)}"/>`;
  }
  let pebbles = '';
  seed = 53;
  for (let i = 0; i < 78; i++) {
    const a = rnd() * Math.PI * 2;
    const u = Math.sqrt(rnd()) * 0.92;
    const x = sx + Math.cos(a) * soilRx * u;
    const y = sy + Math.sin(a) * soilRy * u;
    const s = 7 + rnd() * 20;
    const fill = rnd() > 0.35 ? '#E6D5C4' : '#CDBBA8';
    pebbles += `<ellipse cx="${r2(x)}" cy="${r2(y)}" rx="${r2(s)}" ry="${r2(s * (0.5 + rnd() * 0.28))}" fill="${fill}" stroke="#3A3228" stroke-width="1.5" transform="rotate(${r2(rnd() * 180)} ${r2(x)} ${r2(y)})"/>`;
  }

  const washDefs = WASHES.map((w) => `<radialGradient id="g-${w.id}" cx="${C}" cy="${C}" r="${r2(R * 0.94)}" gradientUnits="userSpaceOnUse">
        <stop offset="0.16" stop-color="${w.wet}" stop-opacity="0.96"/>
        <stop offset="0.38" stop-color="${w.mid}" stop-opacity="0.92"/>
        <stop offset="0.64" stop-color="${w.pale2}" stop-opacity="0.88"/>
        <stop offset="0.90" stop-color="${w.pale}" stop-opacity="0.84"/>
      </radialGradient>`).join('');

  const washPaths = WASHES.map((w) => {
    const band = arc(R * 0.16, R * 0.905, w.a0, w.a1);
    const side0 = (() => {
      const [x0, y0] = pol(R * 0.20, w.a0), [x1, y1] = pol(R * 0.90, w.a0);
      return `M${r2(x0)},${r2(y0)} L${r2(x1)},${r2(y1)}`;
    })();
    const side1 = (() => {
      const [x0, y0] = pol(R * 0.20, w.a1), [x1, y1] = pol(R * 0.90, w.a1);
      return `M${r2(x0)},${r2(y0)} L${r2(x1)},${r2(y1)}`;
    })();
    return `<g>
        <path d="${band}" fill="url(#g-${w.id})" filter="url(#wc)"/>
        <path d="${band}" fill="${w.mid}" opacity="0.18" filter="url(#gran)"/>
        <path d="${side0}" fill="none" stroke="${w.wet}" stroke-width="10" stroke-opacity="0.18" filter="url(#wcEdge)"/>
        <path d="${side1}" fill="none" stroke="${w.wet}" stroke-width="10" stroke-opacity="0.18" filter="url(#wcEdge)"/>
      </g>`;
  }).join('');

  return `<svg xmlns="http://www.w3.org/2000/svg" width="2048" height="2048" viewBox="0 0 2048 2048">
  <defs>
    ${washDefs}
    <filter id="paper" x="0" y="0" width="100%" height="100%">
      <feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="3" seed="3" result="n"/>
      <feColorMatrix type="matrix" values="0 0 0 0 0.45  0 0 0 0 0.40  0 0 0 0 0.32  0 0 0 -1.1 0.55"/>
    </filter>
    <filter id="fibres" x="0" y="0" width="100%" height="100%">
      <feTurbulence type="fractalNoise" baseFrequency="0.004 0.07" numOctaves="2" seed="8"/>
      <feColorMatrix type="matrix" values="0 0 0 0 0.48  0 0 0 0 0.40  0 0 0 0 0.32  0 0 0 -1.3 0.7"/>
    </filter>
    <filter id="wc" x="-8%" y="-8%" width="116%" height="116%">
      <feTurbulence type="fractalNoise" baseFrequency="0.008 0.02" numOctaves="4" seed="5" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="46" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="0.003 0.014" numOctaves="2" seed="9" result="t2"/>
      <feColorMatrix in="t2" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1.4 -0.15" result="blot"/>
      <feComposite in="d" in2="blot" operator="arithmetic" k1="0.55" k2="0.6" k3="0" k4="0" result="m"/>
      <feGaussianBlur in="m" stdDeviation="1.6"/>
    </filter>
    <filter id="wcEdge" x="-8%" y="-8%" width="116%" height="116%">
      <feTurbulence type="fractalNoise" baseFrequency="0.01 0.022" numOctaves="3" seed="5" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="28" xChannelSelector="R" yChannelSelector="G"/>
      <feGaussianBlur stdDeviation="2.2"/>
    </filter>
    <filter id="gran" x="-8%" y="-8%" width="116%" height="116%">
      <feTurbulence type="fractalNoise" baseFrequency="0.012" numOctaves="3" seed="5" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="30" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="0.62" numOctaves="2" seed="12" result="g"/>
      <feColorMatrix in="g" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 3.2 -1.6" result="gm"/>
      <feComposite in="d" in2="gm" operator="in"/>
    </filter>
    <filter id="soil" x="-6%" y="-6%" width="112%" height="112%">
      <feTurbulence type="fractalNoise" baseFrequency="0.012" numOctaves="3" seed="21" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="16" xChannelSelector="R" yChannelSelector="G"/>
    </filter>
    <filter id="pencil">
      <feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="4" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="1.8" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="1.6" numOctaves="1" seed="6" result="g"/>
      <feColorMatrix in="g" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 2.2 -0.45" result="gm"/>
      <feComposite in="d" in2="gm" operator="in"/>
    </filter>
    <filter id="ink">
      <feTurbulence type="fractalNoise" baseFrequency="0.045" numOctaves="2" seed="2" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="2.4" xChannelSelector="R" yChannelSelector="G"/>
    </filter>
    <clipPath id="disc"><circle cx="${C}" cy="${C}" r="${R}"/></clipPath>
  </defs>
  <g clip-path="url(#disc)">
    <rect width="2048" height="2048" fill="#F3EEE2"/>
    <rect width="2048" height="2048" filter="url(#fibres)" opacity="0.22"/>
    <circle cx="${C}" cy="${C}" r="${r2(R * 0.97)}" fill="none" stroke="#DAC9B9" stroke-width="46" opacity="0.85"/>
    ${washPaths}
    <g filter="url(#wc)" opacity="0.95">${brushes}</g>
    <ellipse cx="${sx}" cy="${sy}" rx="${r2(soilRx)}" ry="${r2(soilRy)}" fill="#A0836C" filter="url(#soil)"/>
    <ellipse cx="${sx}" cy="${sy + R * 0.02}" rx="${r2(soilRx * 0.42)}" ry="${r2(soilRy * 0.36)}" fill="#7A624E" opacity="0.22"/>
    <g>${stipple}</g>
    <g filter="url(#ink)">${pebbles}</g>
    <rect width="2048" height="2048" filter="url(#paper)" opacity="0.12"/>
    <g stroke="#6A6158" stroke-opacity="0.34" fill="none" stroke-linecap="round" filter="url(#pencil)">${constr}</g>
    <g stroke="#3A332C" stroke-opacity="0.92" filter="url(#pencil)">${ticks}</g>
    <g fill="none" stroke="#2A2622" stroke-linecap="round" filter="url(#ink)">${rim}</g>
  </g>
</svg>`;
}

// Lathe albedo. v=0 is the butt, v=1 is the nib. Gold collar sits on the mesh UV 0.22..0.40.
function gnomon() {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="1024" viewBox="0 0 256 1024">
  <defs>
    <linearGradient id="inkBody" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#1E1A16"/>
      <stop offset="0.38" stop-color="#3A342C"/>
      <stop offset="0.5" stop-color="#5C5348"/>
      <stop offset="0.62" stop-color="#2E2924"/>
      <stop offset="1" stop-color="#161310"/>
    </linearGradient>
    <linearGradient id="gold" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#8A6A32"/>
      <stop offset="0.45" stop-color="#E2C27A"/>
      <stop offset="0.55" stop-color="#F5C76A"/>
      <stop offset="1" stop-color="#6E5428"/>
    </linearGradient>
    <filter id="grain">
      <feTurbulence type="fractalNoise" baseFrequency="0.8" numOctaves="2" seed="4"/>
      <feColorMatrix type="matrix" values="0 0 0 0 0.3  0 0 0 0 0.25  0 0 0 0 0.18  0 0 0 0.35 0"/>
    </filter>
  </defs>
  <rect width="256" height="1024" fill="url(#inkBody)"/>
  <!-- Image top is UV v=1 (the nib). The wide collar is mesh v 0.22..0.42, image y 594..799. -->
  <polygon points="108,8 148,8 128,150" fill="#1A1613"/>
  <rect y="594" width="256" height="205" fill="url(#gold)"/>
  <rect y="590" width="256" height="8" fill="#2A2622"/>
  <rect y="794" width="256" height="8" fill="#2A2622"/>
  <rect y="900" width="256" height="124" fill="#3A342C"/>
  <rect width="256" height="1024" filter="url(#grain)" opacity="0.45"/>
</svg>`;
}

// Soft painted shadow. UV v grows away from the gnomon. Transparent outside the wash.
function shadow() {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
  <defs>
    <linearGradient id="fade" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#6B5344" stop-opacity="0.42"/>
      <stop offset="0.42" stop-color="#8A7362" stop-opacity="0.28"/>
      <stop offset="1" stop-color="#A08B78" stop-opacity="0"/>
    </linearGradient>
    <filter id="soft" x="-40%" y="-40%" width="180%" height="180%" color-interpolation-filters="sRGB">
      <feGaussianBlur stdDeviation="22"/>
    </filter>
    <filter id="speck" x="-8%" y="-8%" width="116%" height="116%">
      <feTurbulence type="fractalNoise" baseFrequency="0.45" numOctaves="2" seed="3" result="n"/>
      <feColorMatrix in="n" type="matrix" values="0 0 0 0 0.28  0 0 0 0 0.2  0 0 0 0 0.16  0 0 0 0.4 -0.14" result="g"/>
      <feComposite in="g" in2="SourceGraphic" operator="in"/>
    </filter>
  </defs>
  <g filter="url(#soft)">
    <polygon points="455,40 569,40 860,960 164,960" fill="url(#fade)"/>
    <polygon points="492,90 532,90 690,820 334,820" fill="#7A6556" opacity="0.28"/>
  </g>
  <polygon points="500,150 524,150 620,740 404,740" fill="#C4A890" filter="url(#speck)" opacity="0.35"/>
</svg>`;
}

const browser = await chromium.launch({ executablePath: CHROME, headless: true });
const render = async (svg, w, h, file) => {
  const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
  await page.setContent(`<!doctype html><html><body style="margin:0;overflow:hidden;background:transparent">${svg}</body></html>`);
  await page.screenshot({
    path: path.join(OUT, file),
    omitBackground: true,
    clip: { x: 0, y: 0, width: w, height: h },
  });
  await page.close();
  console.log('wrote', file, w + 'x' + h);
};
try {
  await render(face(), 2048, 2048, 'dial_face.png');
  await render(gnomon(), 256, 1024, 'gnomon.png');
  await render(shadow(), 1024, 1024, 'gnomon_shadow.png');
} finally {
  await browser.close();
}
