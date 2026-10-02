// The drawn dial's art, authored as SVG (ink paths + turbulence-displaced watercolour washes)
// and rasterised by Chrome through playwright-core:
//   dial_face.png         2048 px painted face: paper grain, pencil ruler, three watercolour arcs, soil wash
//   plant_<id>_<k>.png    three hand-inked plants x 3 "boiling line" frames (re-seeded wobble)
// usage: node art/tex/dial_svg.mjs   (from spike/)
import { chromium } from 'playwright-core';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { mkdirSync } from 'node:fs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const OUT = path.join(ROOT, 'public/hero/tex');
mkdirSync(OUT, { recursive: true });

let seed = 1;
const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
const r2 = (n) => Math.round(n * 100) / 100;

// ---------------------------------------------------------------- the face
function face() {
  const S = 2048, C = S / 2, R = S / 2 - 6;
  const pol = (r, deg) => [C + r * Math.cos((deg * Math.PI) / 180), C - r * Math.sin((deg * Math.PI) / 180)];
  const arc = (r0, r1, a0, a1) => {
    const [x0, y0] = pol(r1, a0), [x1, y1] = pol(r1, a1), [x2, y2] = pol(r0, a1), [x3, y3] = pol(r0, a0);
    const large = Math.abs(a1 - a0) > 180 ? 1 : 0;
    return `M${x0},${y0} A${r1},${r1} 0 ${large} 1 ${x1},${y1} L${x2},${y2} A${r0},${r0} 0 ${large} 0 ${x3},${y3} Z`;
  };
  // arcs run clockwise in screen space from a0 (larger angle) to a1
  const washes = [
    { id: 'sunrise', a0: 212, a1: 112, c0: '#f6c24a', c1: '#f08a3c', edge: '#d9762a' },
    { id: 'midday', a0: 106, a1: 22, c0: '#f39a8a', c1: '#e8625a', edge: '#c9483f' },
    { id: 'dusk', a0: 16, a1: -76, c0: '#b9a0e0', c1: '#8f74c9', edge: '#6f55ad' },
  ];
  let ticks = '';
  for (let d = 0; d < 360; d += 2) {
    const long = d % 30 === 0, mid = d % 10 === 0;
    const [x0, y0] = pol(R * 0.985, d), [x1, y1] = pol(R * (long ? 0.925 : mid ? 0.945 : 0.962), d);
    ticks += `<line x1="${r2(x0)}" y1="${r2(y0)}" x2="${r2(x1)}" y2="${r2(y1)}" stroke-width="${long ? 3.2 : mid ? 2.4 : 1.6}" />`;
  }
  let constr = '';
  for (const d of [0, 45, 90, 135]) {
    const [x0, y0] = pol(R * 0.9, d), [x1, y1] = pol(R * 0.9, d + 180);
    constr += `<line x1="${r2(x0)}" y1="${r2(y0)}" x2="${r2(x1)}" y2="${r2(y1)}" />`;
  }
  let pebbles = '';
  seed = 41;
  for (let i = 0; i < 70; i++) {
    const a = rnd() * 360, rr = Math.sqrt(rnd()) * R * 0.55;
    const [x, y0] = pol(rr, a);
    const y = y0 + R * 0.1;
    const s = 3 + rnd() * 9;
    pebbles += `<ellipse cx="${r2(x)}" cy="${r2(y)}" rx="${r2(s)}" ry="${r2(s * 0.7)}" transform="rotate(${r2(rnd() * 180)} ${r2(x)} ${r2(y)})" />`;
  }
  let hatch = '';
  seed = 91;
  for (let i = 0; i < 520; i++) {
    const a = -76 + rnd() * 288, rr = R * (0.5 + rnd() * 0.36);
    const [x, y] = pol(rr, a);
    const L = 18 + rnd() * 30;
    hatch += `<line x1="${r2(x)}" y1="${r2(y)}" x2="${r2(x + L)}" y2="${r2(y - L * 0.9)}"/>`;
  }
  let stipple = '';
  seed = 77;
  for (let i = 0; i < 6000; i++) {
    const a = rnd() * 360, rr = Math.pow(rnd(), 0.38) * R * 0.57;
    const [x0, y0] = pol(rr, a);
    const x = x0, y = y0 + R * 0.1;
    stipple += `<circle cx="${r2(x)}" cy="${r2(y)}" r="${r2(0.8 + rnd() * rnd() * 3.2)}"/>`;
  }
  const washDefs = washes
    .map(
      (w) => `<radialGradient id="g-${w.id}" cx="${C}" cy="${C}" r="${R * 0.86}" gradientUnits="userSpaceOnUse">
        <stop offset="0.5" stop-color="${w.c0}" stop-opacity="0.12"/><stop offset="0.72" stop-color="${w.c0}" stop-opacity="0.45"/><stop offset="1" stop-color="${w.c1}" stop-opacity="0.7"/></radialGradient>`,
    )
    .join('');
  const washPaths = washes
    .map(
      (w) => `<g>
        <path d="${arc(R * 0.47, R * 0.875, w.a0, w.a1)}" fill="url(#g-${w.id})" filter="url(#wc)"/>
        <path d="${arc(R * 0.47, R * 0.875, w.a0, w.a1)}" fill="none" stroke="${w.edge}" stroke-opacity="0.22" stroke-width="8" filter="url(#wcEdge)"/>
        <path d="${arc(R * 0.47, R * 0.875, w.a0, w.a1)}" fill="${w.c1}" opacity="0.07" filter="url(#gran)"/>
      </g>`,
    )
    .join('');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${S}" height="${S}" viewBox="0 0 ${S} ${S}">
  <defs>
    ${washDefs}
    <filter id="paper" x="0" y="0" width="1" height="1">
      <feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="3" seed="3" result="n"/>
      <feColorMatrix type="matrix" values="0 0 0 0 0.45  0 0 0 0 0.40  0 0 0 0 0.32  0 0 0 -1.1 0.62" />
    </filter>
    <filter id="fibres" x="0" y="0" width="1" height="1">
      <feTurbulence type="fractalNoise" baseFrequency="0.004 0.06" numOctaves="2" seed="8"/>
      <feColorMatrix type="matrix" values="0 0 0 0 0.5  0 0 0 0 0.44  0 0 0 0 0.35  0 0 0 -1.4 0.75" />
    </filter>
    <filter id="wc" x="-5%" y="-5%" width="110%" height="110%">
      <feTurbulence type="fractalNoise" baseFrequency="0.012" numOctaves="4" seed="5" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="34" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="0.004 0.012" numOctaves="2" seed="9" result="t2"/>
      <feColorMatrix in="t2" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1.2 -0.1" result="blot"/>
      <feComposite in="d" in2="blot" operator="arithmetic" k1="0.5" k2="0.55" k3="0" k4="0" result="m"/>
      <feGaussianBlur in="m" stdDeviation="2.5"/>
    </filter>
    <filter id="wcEdge" x="-5%" y="-5%" width="110%" height="110%">
      <feTurbulence type="fractalNoise" baseFrequency="0.012" numOctaves="4" seed="5" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="34" xChannelSelector="R" yChannelSelector="G"/>
      <feGaussianBlur stdDeviation="3"/>
    </filter>
    <filter id="gran" x="-5%" y="-5%" width="110%" height="110%">
      <feTurbulence type="fractalNoise" baseFrequency="0.012" numOctaves="4" seed="5" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="34" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="0.55" numOctaves="2" seed="12" result="g"/>
      <feColorMatrix in="g" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 3 -1.5" result="gm"/>
      <feComposite in="d" in2="gm" operator="in"/>
    </filter>
    <filter id="soil" x="-5%" y="-5%" width="110%" height="110%">
      <feTurbulence type="fractalNoise" baseFrequency="0.018" numOctaves="4" seed="21" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="40" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="0.08" numOctaves="4" seed="22" result="t2"/>
      <feColorMatrix in="t2" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 2.2 -0.7" result="blot"/>
      <feComposite in="d" in2="blot" operator="arithmetic" k1="0.7" k2="0.5" k3="0" k4="0"/>
    </filter>
    <filter id="pencil">
      <feTurbulence type="fractalNoise" baseFrequency="0.7" numOctaves="2" seed="4" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="2.2" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feTurbulence type="fractalNoise" baseFrequency="1.4" numOctaves="1" seed="6" result="g"/>
      <feColorMatrix in="g" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 2.4 -0.6" result="gm"/>
      <feComposite in="d" in2="gm" operator="in"/>
    </filter>
    <filter id="ink">
      <feTurbulence type="fractalNoise" baseFrequency="0.05" numOctaves="2" seed="2" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="3" xChannelSelector="R" yChannelSelector="G"/>
    </filter>
    <clipPath id="disc"><circle cx="${C}" cy="${C}" r="${R}"/></clipPath>
  </defs>
  <g clip-path="url(#disc)">
    <rect width="${S}" height="${S}" fill="#f1e8d6"/>
    <rect width="${S}" height="${S}" filter="url(#fibres)" opacity="0.18"/>
    ${washPaths}
    <g stroke="#6a5644" stroke-opacity="0.22" stroke-width="2.2" filter="url(#pencil)">${hatch}</g>
    <ellipse cx="${C}" cy="${C + R * 0.1}" rx="${R * 0.6}" ry="${R * 0.56}" fill="#6e4e34" opacity="0.85" filter="url(#wc)"/>
    <ellipse cx="${C}" cy="${C + R * 0.14}" rx="${R * 0.48}" ry="${R * 0.42}" fill="#4a3322" opacity="0.5" filter="url(#wc)"/>
    <g fill="#2f2117" opacity="0.75">${stipple}</g>
    <g fill="#d8cfc0" stroke="#4a3d30" stroke-width="2" opacity="0.9" filter="url(#ink)">${pebbles}</g>
    <g stroke="#7d7466" stroke-opacity="0.45" stroke-width="1.6" filter="url(#pencil)">${constr}</g>
    <g stroke="#3b3329" stroke-opacity="0.85" stroke-linecap="round" filter="url(#pencil)">${ticks}</g>
    <g fill="none" stroke="#2f281f" filter="url(#ink)">
      <circle cx="${C}" cy="${C}" r="${R * 0.99}" stroke-width="4"/>
      <circle cx="${C}" cy="${C}" r="${R * 0.905}" stroke-width="1.6" stroke-opacity="0.6"/>
      <circle cx="${C}" cy="${C}" r="${R * 0.878}" stroke-width="3"/>
    </g>
    <rect width="${S}" height="${S}" filter="url(#paper)" opacity="0.55"/>
  </g>
</svg>`;
}

// ---------------------------------------------------------------- plants
const INK = '#2e2a1f';
function leafPath(x, y, ang, len, w, j) {
  const c = Math.cos(ang), s = Math.sin(ang);
  const P = (u, v) => `${r2(x + u * c - v * s)},${r2(y + u * s + v * c)}`;
  const jj = () => (rnd() - 0.5) * j;
  return {
    d: `M${P(0, 0)} Q${P(len * 0.35 + jj(), -w + jj())} ${P(len + jj(), jj())} Q${P(len * 0.55 + jj(), w * 0.9 + jj())} ${P(0, 0)} Z`,
    rib: `M${P(len * 0.05, 0)} Q${P(len * 0.5, -w * 0.12)} ${P(len * 0.88, 0)}`,
  };
}
function stemPath(pts, j) {
  let d = `M${r2(pts[0][0])},${r2(pts[0][1])}`;
  for (let i = 1; i < pts.length - 1; i += 2) d += ` Q${r2(pts[i][0] + (rnd() - 0.5) * j)},${r2(pts[i][1])} ${r2(pts[i + 1][0])},${r2(pts[i + 1][1])}`;
  return d;
}
function plant(kind, frame) {
  const W = 512, H = 768;
  seed = 100 + frame * 7 + (kind === 'sunrise' ? 0 : kind === 'midday' ? 1000 : 2000);
  const jit = 3.2; // boiling: control points re-jittered per frame
  let fills = '', inks = '', extras = '';
  const leafCol = { sunrise: '#7aa556', midday: '#6f9c58', dusk: '#8da38a' }[kind];
  const addLeaf = (x, y, ang, len, w) => {
    const L = leafPath(x, y, ang, len, w, jit);
    fills += `<path d="${L.d}" fill="${leafCol}" transform="translate(${r2(2 + rnd() * 3)},${r2(1 + rnd() * 2)})"/>`;
    inks += `<path d="${L.d}" stroke-width="3.4"/><path d="${L.rib}" stroke-width="1.8"/>`;
  };
  const baseY = H - 70, cx = W / 2;
  if (kind === 'sunrise') {
    const stems = [[cx, baseY, -0.05, 470], [cx - 6, baseY, -0.42, 300], [cx + 6, baseY, 0.38, 320]];
    for (const [x, y, lean, h] of stems) {
      const pts = [];
      for (let i = 0; i <= 8; i++) pts.push([x + Math.sin(lean) * h * (i / 8) + Math.sin(i) * 3, y - Math.cos(lean) * h * (i / 8)]);
      inks += `<path d="${stemPath(pts, jit)}" stroke-width="4"/>`;
      for (let i = 1; i < 8; i++) {
        const [px, py] = pts[i], sz = 1 - i / 10;
        addLeaf(px, py, -Math.PI / 2 + lean - 0.8, 105 * sz, 24 * sz);
        addLeaf(px, py, -Math.PI / 2 + lean + 0.8, 98 * sz, 22 * sz);
      }
      addLeaf(pts[8][0], pts[8][1], -Math.PI / 2 + lean, 46, 11);
    }
  } else if (kind === 'midday') {
    const heads = [[cx - 70, 150], [cx + 60, 110], [cx + 20, 240], [cx - 95, 300], [cx + 95, 270]];
    for (const [hx, hy] of heads) {
      const pts = [[cx, baseY], [(cx + hx) / 2 + (rnd() - 0.5) * 20, (baseY + hy) / 2], [hx, hy]];
      inks += `<path d="${stemPath(pts, jit)}" stroke-width="2.6"/>`;
    }
    for (let i = 0; i < 9; i++) {
      const y = baseY - 30 - i * 38, x = cx + (rnd() - 0.5) * 30;
      addLeaf(x, y, i % 2 ? -0.5 : Math.PI + 0.5, 92 - i * 4, 22);
    }
    for (const [hx, hy] of heads) {
      const R = 46 + rnd() * 10, rot = rnd() * 72;
      for (let p = 0; p < 5; p++) {
        const a = ((rot + p * 72) * Math.PI) / 180;
        const px = hx + Math.cos(a) * R * 0.55, py = hy + Math.sin(a) * R * 0.55;
        const pd = `M${r2(hx)},${r2(hy)} Q${r2(hx + Math.cos(a - 0.5) * R * 1.1)},${r2(hy + Math.sin(a - 0.5) * R * 1.1)} ${r2(px + Math.cos(a) * R * 0.5 + (rnd() - 0.5) * jit)},${r2(py + Math.sin(a) * R * 0.5)} Q${r2(hx + Math.cos(a + 0.5) * R * 1.1)},${r2(hy + Math.sin(a + 0.5) * R * 1.1)} ${r2(hx)},${r2(hy)} Z`;
        fills += `<path d="${pd}" fill="#ee8fb4" transform="translate(2,2)"/><path d="${pd}" fill="#f7c3d6" opacity="0.6" transform="scale(1)"/>`;
        inks += `<path d="${pd}" stroke-width="2.8"/><path d="M${r2(hx)},${r2(hy)} L${r2(px)},${r2(py)}" stroke-width="1.2" stroke="#b0587d"/>`;
      }
      extras += `<circle cx="${r2(hx)}" cy="${r2(hy)}" r="9" fill="#f2c94c" stroke="${INK}" stroke-width="2.2"/>`;
    }
  } else {
    for (let k = 0; k < 5; k++) {
      const lean = (k - 2) * 0.13 + (rnd() - 0.5) * 0.05, h = 380 + rnd() * 120;
      const x = cx + (k - 2) * 12;
      const pts = [];
      for (let i = 0; i <= 6; i++) pts.push([x + Math.sin(lean) * h * (i / 6), baseY - Math.cos(lean) * h * (i / 6)]);
      inks += `<path d="${stemPath(pts, jit)}" stroke-width="3"/>`;
      for (let f = 0; f < 14; f++) {
        const t = 0.55 + (f / 14) * 0.45;
        const fx = x + Math.sin(lean) * h * t + (f % 2 ? 7 : -7), fy = baseY - Math.cos(lean) * h * t;
        const rr = 13 - f * 0.45;
        const col = ['#9b7fd0', '#8466c2', '#b49ce0'][f % 3];
        fills += `<ellipse cx="${r2(fx + 2)}" cy="${r2(fy + 1)}" rx="${r2(rr * 0.75)}" ry="${r2(rr)}" fill="${col}" transform="rotate(${f % 2 ? 20 : -20} ${r2(fx)} ${r2(fy)})"/>`;
        inks += `<ellipse cx="${r2(fx)}" cy="${r2(fy)}" rx="${r2(rr * 0.7)}" ry="${r2(rr * 0.95)}" stroke-width="1.6" transform="rotate(${f % 2 ? 20 : -20} ${r2(fx)} ${r2(fy)})"/>`;
      }
    }
    for (let i = 0; i < 8; i++) addLeaf(cx + (rnd() - 0.5) * 20, baseY - 10 - i * 12, (i % 2 ? -0.7 : Math.PI + 0.7) + (rnd() - 0.5) * 0.3, 120 - i * 7, 12);
  }
  // a little mound of drawn soil at the base
  let soil = `<path d="M${cx - 95},${baseY + 8} Q${cx},${baseY - 30} ${cx + 95},${baseY + 8} Z" fill="#6b4c33" opacity="0.75" filter="url(#wc)"/>`;
  for (let i = 0; i < 26; i++) soil += `<circle cx="${r2(cx + (rnd() - 0.5) * 170)}" cy="${r2(baseY - rnd() * 14)}" r="${r2(1 + rnd() * 2.6)}" fill="${INK}"/>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
  <defs>
    <filter id="wc" x="-10%" y="-10%" width="120%" height="120%">
      <feTurbulence type="fractalNoise" baseFrequency="0.03" numOctaves="3" seed="${frame + 3}" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="9" xChannelSelector="R" yChannelSelector="G" result="d"/>
      <feGaussianBlur in="d" stdDeviation="0.8"/>
    </filter>
    <filter id="ink" x="-10%" y="-10%" width="120%" height="120%">
      <feTurbulence type="fractalNoise" baseFrequency="0.06" numOctaves="2" seed="${frame * 13 + 1}" result="t"/>
      <feDisplacementMap in="SourceGraphic" in2="t" scale="3.5" xChannelSelector="R" yChannelSelector="G"/>
    </filter>
  </defs>
  ${soil}
  <g opacity="0.82" filter="url(#wc)">${fills}</g>
  <g fill="none" stroke="${INK}" stroke-linecap="round" stroke-linejoin="round" filter="url(#ink)">${inks}</g>
  ${extras}
</svg>`;
}

const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
const render = async (svg, w, h, file) => {
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  await page.setContent(`<html><body style="margin:0;background:transparent">${svg}</body></html>`);
  await page.screenshot({ path: path.join(OUT, file), omitBackground: true });
  await page.close();
  console.log('wrote', file);
};
try {
  await render(face(), 2048, 2048, 'dial_face.png');
  for (const kind of ['sunrise', 'midday', 'dusk']) for (let f = 0; f < 3; f++) await render(plant(kind, f), 512, 768, `plant_${kind}_${f}.png`);
} finally {
  await browser.close();
}
