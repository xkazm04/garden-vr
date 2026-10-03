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
const WASHES = [
  { id: 'sunrise', a0: 212, a1: 112, pale: '#F6DEB7', pale2: '#F0E0BE', mid: '#E2B866', wet: '#D9762A', deep: '#A85A1C' },
  { id: 'midday', a0: 106, a1: 22, pale: '#F4B6A1', pale2: '#EFBC9B', mid: '#E39C82', wet: '#C9483F', deep: '#8E3028' },
  { id: 'dusk', a0: 16, a1: -76, pale: '#CAB0C5', pale2: '#BFA1BC', mid: '#A79AD6', wet: '#6F55AD', deep: '#4A3480' },
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

  // Brush direction inside each arc: a handful of long strokes, not a flat fill.
  let brushes = '';
  seed = 23;
  for (const w of WASHES) {
    const span = ((w.a0 - w.a1) + 360) % 360;
    for (let s = 0; s < 9; s++) {
      const t = 0.08 + (s / 8) * 0.84;
      const a0 = w.a0 - span * (0.04 + rnd() * 0.08);
      const a1 = w.a0 - span * (0.55 + rnd() * 0.4);
      const rr = R * (0.52 + t * 0.32);
      brushes += `<path d="${arcStroke(rr, a0, a1)}" fill="none" stroke="${s % 2 ? w.mid : w.pale2}" stroke-width="${r2(18 + rnd() * 22)}" stroke-opacity="${r2(0.16 + rnd() * 0.12)}" stroke-linecap="round"/>`;
    }
  }

  // Soil mound UV radius is about 0.59 of R. A solid ellipse covers that disc.
  // A few low-frequency lobes spill past it onto the paper, toward the near rim.
  // Mound UV radius is ~0.59 R. The covering ellipse stays inside that, plus a margin
  // the soil filter can nibble. Spill lobes sit outside it, mostly toward the near rim.
  seed = 41;
  const sx = C;
  const sy = C;

  let stipple = '';
  seed = 77;
  for (let i = 0; i < 5200; i++) {
    const a = rnd() * Math.PI * 2;
    const rr = Math.pow(rnd(), 0.72) * R * 0.70;
    const x = sx + Math.cos(a) * rr;
    const y = sy + Math.sin(a) * rr;
    const rad = 0.7 + rnd() * rnd() * 3.4;
    const dark = rnd() > 0.72;
    stipple += `<circle cx="${r2(x)}" cy="${r2(y)}" r="${r2(rad)}" fill="${dark ? '#24180F' : '#3A291C'}" opacity="${r2(0.35 + rnd() * 0.5)}"/>`;
  }
  let pebbles = '';
  seed = 53;
  for (let i = 0; i < 46; i++) {
    const a = rnd() * Math.PI * 2;
    const rr = Math.sqrt(rnd()) * R * 0.58;
    const x = sx + Math.cos(a) * rr;
    const y = sy + Math.sin(a) * rr;
    const s = 4 + rnd() * 14;
    const fill = rnd() > 0.5 ? '#C4B5A4' : '#A89884';
    pebbles += `<ellipse cx="${r2(x)}" cy="${r2(y)}" rx="${r2(s)}" ry="${r2(s * (0.55 + rnd() * 0.3))}" fill="${fill}" stroke="#3A3228" stroke-width="1.6" transform="rotate(${r2(rnd() * 180)} ${r2(x)} ${r2(y)})"/>`;
  }

  const washDefs = WASHES.map((w) => `<radialGradient id="g-${w.id}" cx="${C}" cy="${C}" r="${r2(R * 0.90)}" gradientUnits="userSpaceOnUse">
        <stop offset="0.42" stop-color="${w.wet}" stop-opacity="0.92"/>
        <stop offset="0.58" stop-color="${w.mid}" stop-opacity="0.78"/>
        <stop offset="0.74" stop-color="${w.pale2}" stop-opacity="0.72"/>
        <stop offset="0.90" stop-color="${w.pale}" stop-opacity="0.55"/>
      </radialGradient>`).join('');

  const washPaths = WASHES.map((w) => {
    const band = arc(R * 0.46, R * 0.875, w.a0, w.a1);
    const inner = arcStroke(R * 0.66, w.a0, w.a1);
    const side0 = (() => {
      const [x0, y0] = pol(R * 0.46, w.a0), [x1, y1] = pol(R * 0.875, w.a0);
      return `M${r2(x0)},${r2(y0)} L${r2(x1)},${r2(y1)}`;
    })();
    const side1 = (() => {
      const [x0, y0] = pol(R * 0.46, w.a1), [x1, y1] = pol(R * 0.875, w.a1);
      return `M${r2(x0)},${r2(y0)} L${r2(x1)},${r2(y1)}`;
    })();
    return `<g>
        <path d="${band}" fill="url(#g-${w.id})" filter="url(#wc)"/>
        <path d="${band}" fill="${w.mid}" opacity="0.18" filter="url(#gran)"/>
        <path d="${inner}" fill="none" stroke="${w.deep}" stroke-width="28" stroke-opacity="0.62" stroke-linecap="round" filter="url(#wcEdge)"/>
        <path d="${side0}" fill="none" stroke="${w.wet}" stroke-width="16" stroke-opacity="0.35" filter="url(#wcEdge)"/>
        <path d="${side1}" fill="none" stroke="${w.wet}" stroke-width="16" stroke-opacity="0.35" filter="url(#wcEdge)"/>
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
    <g filter="url(#wc)" opacity="0.9">${brushes}</g>
    <ellipse cx="${C}" cy="${C}" rx="${r2(R * 0.62)}" ry="${r2(R * 0.60)}" fill="#4A3324" filter="url(#soil)"/>
    <ellipse cx="${C}" cy="${C + R * 0.03}" rx="${r2(R * 0.34)}" ry="${r2(R * 0.30)}" fill="#3A281C" opacity="0.45"/>
    <g>${stipple}</g>
    <g filter="url(#ink)">${pebbles}</g>
    <rect width="2048" height="2048" filter="url(#paper)" opacity="0.28"/>
    <g stroke="#6A6158" stroke-opacity="0.62" fill="none" stroke-linecap="round" filter="url(#pencil)">${constr}</g>
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
  <rect y="230" width="256" height="150" fill="url(#gold)"/>
  <rect y="226" width="256" height="8" fill="#2A2622"/>
  <rect y="374" width="256" height="8" fill="#2A2622"/>
  <rect y="900" width="256" height="124" fill="#4A433C"/>
  <polygon points="108,900 148,900 128,1020" fill="#1A1613"/>
  <rect width="256" height="1024" filter="url(#grain)" opacity="0.45"/>
</svg>`;
}

// Soft painted shadow. UV v grows away from the gnomon. Transparent outside the wash.
function shadow() {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
  <defs>
    <linearGradient id="fade" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#1A100C" stop-opacity="0.88"/>
      <stop offset="0.45" stop-color="#2A1A12" stop-opacity="0.62"/>
      <stop offset="1" stop-color="#2A1A12" stop-opacity="0"/>
    </linearGradient>
    <filter id="soft" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">
      <feGaussianBlur stdDeviation="18"/>
    </filter>
    <filter id="speck" x="-8%" y="-8%" width="116%" height="116%">
      <feTurbulence type="fractalNoise" baseFrequency="0.45" numOctaves="2" seed="3" result="n"/>
      <feColorMatrix in="n" type="matrix" values="0 0 0 0 0.28  0 0 0 0 0.2  0 0 0 0 0.16  0 0 0 0.4 -0.14" result="g"/>
      <feComposite in="g" in2="SourceGraphic" operator="in"/>
    </filter>
  </defs>
  <g filter="url(#soft)">
    <polygon points="470,70 554,70 820,900 204,900" fill="url(#fade)"/>
    <polygon points="496,120 528,120 700,780 324,780" fill="#140E0A" opacity="0.45"/>
  </g>
  <polygon points="500,180 524,180 640,700 384,700" fill="#5A4538" filter="url(#speck)" opacity="0.55"/>
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
