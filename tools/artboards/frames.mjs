// Artboard frames for the upgrade's style pick (docs/plans/upgrade-2026-10.md section 8).
// Each frame is an overlay drawn on one of the two app renders already in the repo, at its reference framing:
//   Night Moss (Terrarium): tools/fidelity/rungs/JarG1-level2-round3-midbreath.png
//   Field Notebook (Sundial): tools/fidelity/rungs/DialG1-level2-round3-idle.png
// Sprites come from prep.py (the apps' own Art/Source images). No pixel of the owner's reference frames is used.
//
// Usage: node tools/artboards/frames.mjs <build-dir> <out-dir> [font-dir]
//   build-dir: where prep.py wrote the sprites; out-dir: docs/art/artboards
//   font-dir: optional folder holding caveat-latin-400-normal.woff2, caveat-latin-600-normal.woff2 and
//             cormorant-garamond-latin-500-italic.woff2 (npm @fontsource/caveat, @fontsource/cormorant-garamond)

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const [build, out, fontDir] = process.argv.slice(2);
if (!build || !out) { console.error('usage: frames.mjs <build-dir> <out-dir> [font-dir]'); process.exit(2); }

const url = p => pathToFileURL(path.resolve(p)).href;
const repo = (...p) => url(path.join(ROOT, ...p));
const sprite = name => url(path.join(build, name + '.png'));
const W = 1824, H = 1024;

// ---------- geometry ----------
const rad = d => d * Math.PI / 180;
// Angles on a floor ellipse: 0 is screen right, 90 is toward the viewer, 180 left, 270 away.
const pt = (e, deg, grow = 0) => [e.cx + (e.rx + grow) * Math.cos(rad(deg)), e.cy + (e.ry + grow * e.ry / e.rx) * Math.sin(rad(deg))];
function arc(e, a0, a1, grow = 0) {
  const steps = Math.max(4, Math.ceil(Math.abs(a1 - a0) / 3));
  let d = '';
  for (let i = 0; i <= steps; i++) {
    const [x, y] = pt(e, a0 + (a1 - a0) * i / steps, grow);
    d += (i ? 'L' : 'M') + x.toFixed(1) + ' ' + y.toFixed(1);
  }
  return d;
}

// ---------- shared styles ----------
function fontFaces() {
  if (!fontDir) return '';
  const face = (family, file, weight, style) => fs.existsSync(path.join(fontDir, file))
    ? `@font-face{font-family:'${family}';src:url('${url(path.join(fontDir, file))}') format('woff2');font-weight:${weight};font-style:${style}}` : '';
  return face('Caveat', 'caveat-latin-400-normal.woff2', 400, 'normal')
    + face('Caveat', 'caveat-latin-600-normal.woff2', 600, 'normal')
    + face('Cormorant Garamond', 'cormorant-garamond-latin-500-italic.woff2', 500, 'italic');
}

const page = (base, body, extraCss = '') => `<!doctype html><html><head><meta charset="utf-8"><style>
${fontFaces()}
html,body{margin:0;background:#000}
.frame{position:relative;width:${W}px;height:${H}px;overflow:hidden}
.base{position:absolute;left:0;top:0;width:${W}px;height:${H}px}
.abs{position:absolute}
svg.ov{position:absolute;left:0;top:0}
.etched{font-family:'Cormorant Garamond',Georgia,serif;font-style:italic;font-weight:500;fill:#dcfff0}
.ink{font-family:'Caveat','Comic Sans MS',cursive;fill:#2b2420}
.card{position:absolute;background:url('${sprite('paper')}') center/cover;box-shadow:0 10px 22px rgba(60,40,20,.35);border:1px solid rgba(60,40,20,.25)}
${extraCss}
</style></head><body><div class="frame"><img class="base" src="${base}">${body}</div></body></html>`;

const HATCH = { Body: '#6f9e6a', Mind: '#5f84b3', Work: '#b88440', Connection: '#c9707f', active: '#e9b25a' };
const svg = inner => `<svg class="ov" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}"><defs>
<filter id="glow" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="5" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
<filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="14"/></filter>
<filter id="textglow" x="-20%" y="-50%" width="140%" height="200%"><feGaussianBlur stdDeviation="3" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
<filter id="pencil"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed="3"/><feDisplacementMap in="SourceGraphic" scale="2.2"/></filter>
${Object.entries(HATCH).map(([id, c]) => `<pattern id="hatch-${id}" width="7" height="7" patternUnits="userSpaceOnUse" patternTransform="rotate(35)"><line x1="0" y1="0" x2="0" y2="7" stroke="${c}" stroke-width="3.4" opacity=".8"/></pattern>`).join('')}
</defs>${inner}</svg>`;

// ---------- Night Moss (Terrarium) ----------
const JAR = repo('tools', 'fidelity', 'rungs', 'JarG1-level2-round3-midbreath.png');
const RING = { cx: 914, cy: 836, rx: 304, ry: 94 };
const NM = { Body: '#7dffb4', Mind: '#86e6ff', Work: '#ffd27a', Connection: '#ffb3cf' };
const NM_ZONES = [['Connection', 8, 82], ['Body', 98, 172], ['Mind', 188, 262], ['Work', 278, 352]];
const nmLabel = { Body: [118, 'end', 40, 26], Connection: [62, 'start', 40, 26], Mind: [222, 'end', -24, -10], Work: [318, 'start', -24, -10] };

function zoneRing(fill) {
  let s = '';
  for (const [z, a0, a1] of NM_ZONES) {
    const back = a0 > 180;
    const c = NM[z];
    s += `<path d="${arc(RING, a0, a1)}" stroke="${c}" stroke-width="5" fill="none" opacity="${back ? .28 : .35}" stroke-linecap="round"/>`;
    if (fill != null) {
      const f = fill[z];
      s += `<path d="${arc(RING, a0, a0 + (a1 - a0) * f)}" stroke="${c}" stroke-width="7" fill="none" filter="url(#glow)" opacity="${back ? .75 : 1}" stroke-linecap="round"/>`;
    }
    const [deg, anchor, grow, dy] = nmLabel[z];
    const [x, y] = pt(RING, deg, grow);
    s += `<text class="etched" x="${x}" y="${y + dy}" font-size="27" text-anchor="${anchor}" filter="url(#textglow)" style="fill:${c}">${z}</text>`;
  }
  return s;
}

function etched(lines, x, y, size = 34, gap = 1.25, opacity = 0.92) {
  return lines.map((t, i) => `<text class="etched" x="${x}" y="${y + i * size * gap}" font-size="${size}" text-anchor="middle" filter="url(#textglow)" opacity="${opacity}">${t}</text>`).join('');
}

function seedPod(x, y, title, sub, zone, lit) {
  const c = NM[zone];
  return `<ellipse cx="${x}" cy="${y}" rx="34" ry="12" fill="${c}" opacity=".18" filter="url(#soft)"/>
<path d="M${x - 13} ${y} C${x - 13} ${y - 26} ${x + 13} ${y - 26} ${x + 13} ${y} C${x + 13} ${y + 8} ${x - 13} ${y + 8} ${x - 13} ${y}Z" fill="${c}" opacity="${lit ? .95 : .7}" filter="url(#glow)"/>
<text class="etched" x="${x}" y="${y + 44}" font-size="28" text-anchor="middle" filter="url(#textglow)">${title}</text>
<text class="etched" x="${x}" y="${y + 74}" font-size="22" text-anchor="middle" filter="url(#textglow)" style="fill:${c}">${sub}</text>`;
}

function listeningRings(cx, cy) {
  let s = '';
  for (let i = 0; i < 4; i++)
    s += `<ellipse cx="${cx}" cy="${cy}" rx="${215 + i * 34}" ry="${42 + i * 9}" fill="none" stroke="#bfffe0" stroke-width="${2.4 - i * .45}" opacity="${.7 - i * .15}" filter="url(#glow)"/>`;
  return s;
}

function spores(n, seed, box) {
  let s = '', r = seed;
  const rnd = () => (r = (r * 9301 + 49297) % 233280) / 233280;
  for (let i = 0; i < n; i++)
    s += `<circle cx="${box[0] + rnd() * box[2]}" cy="${box[1] + rnd() * box[3]}" r="${1.2 + rnd() * 2.2}" fill="#ffd98a" opacity="${.45 + rnd() * .5}" filter="url(#glow)"/>`;
  return s;
}

const nmOnboarding = page(JAR, svg(`
${listeningRings(914, 236)}
${etched(['What would you', 'like to grow?'], 914, 352, 36, 1.2)}
${etched(['say it, or pinch a seed'], 914, 150, 24, 1, .8)}
${zoneRing(null)}
${seedPod(700, 628, '', '', 'Body', true)}
<text class="etched" x="520" y="440" font-size="30" text-anchor="middle" filter="url(#textglow)">Walk after lunch</text>
<text class="etched" x="520" y="472" font-size="23" text-anchor="middle" filter="url(#textglow)" style="fill:${NM.Body}">Body · weekdays</text>
${seedPod(1390, 420, 'Water, 8 glasses', 'Body · every day', 'Body', false)}
${seedPod(1590, 300, 'Call mum', 'Connection · twice a week', 'Connection', false)}
<path d="M706 640 C 690 720, 690 820, 712 896" stroke="${NM.Body}" stroke-width="2.5" fill="none" stroke-dasharray="3 9" stroke-linecap="round" filter="url(#glow)"/>
`));

const nmBreaks = page(JAR, svg(`
<path d="M914 190 C 1050 60, 1300 40, 1478 150" stroke="#cfffe8" stroke-width="2" fill="none" stroke-dasharray="2 12" stroke-linecap="round" opacity=".8" filter="url(#glow)"/>
<circle cx="1478" cy="150" r="60" fill="#bfffe0" opacity=".16" filter="url(#soft)"/>
<circle cx="1478" cy="150" r="13" fill="#effff7" filter="url(#glow)"/>
<path d="${(() => { const r = 34, a0 = -90, a1 = -90 + 360 * .65; const p = a => [1478 + r * Math.cos(rad(a)), 150 + r * Math.sin(rad(a))]; const [x0, y0] = p(a0), [x1, y1] = p(a1); return `M${x0} ${y0} A${r} ${r} 0 1 1 ${x1} ${y1}`; })()}" stroke="#effff7" stroke-width="2.5" fill="none" opacity=".85" filter="url(#glow)"/>
<text class="etched" x="1478" y="232" font-size="26" text-anchor="middle" filter="url(#textglow)">rest your eyes here</text>
<text class="etched" x="1478" y="262" font-size="21" text-anchor="middle" filter="url(#textglow)" opacity=".75">13 s</text>
${etched(['Now let your eyes', 'travel to the far light'], 914, 352, 31, 1.25)}
${[[430, 380, true], [1395, 330, true], [1530, 610, false]].map(([x, y, done]) => `
<circle cx="${x}" cy="${y}" r="30" fill="none" stroke="${NM.Body}" stroke-width="2.5" opacity="${done ? .45 : .95}" filter="url(#glow)"/>
<circle cx="${x}" cy="${y}" r="${done ? 7 : 5}" fill="${NM.Body}" opacity="${done ? .6 : 1}" filter="url(#glow)"/>`).join('')}
<text class="etched" x="1530" y="672" font-size="22" text-anchor="middle" filter="url(#textglow)" style="fill:${NM.Body}" opacity=".85">stretch 2 of 3</text>
${zoneRing({ Body: .35, Mind: 0, Work: 0, Connection: 0 })}
`));

const nmReflection = page(JAR, `<div class="abs" style="inset:0;background:radial-gradient(ellipse at 50% 55%, rgba(0,0,0,0) 30%, rgba(0,6,14,.55) 85%)"></div>` + svg(`
${(() => { let s = ''; for (let a = 0; a < 360; a += 4) { const amp = 8 + 22 * Math.abs(Math.sin(rad(a * 3.1))) * (0.4 + 0.6 * Math.abs(Math.cos(rad(a * 1.7)))); const [x0, y0] = pt(RING, a, 6), [x1, y1] = pt(RING, a, 6 + amp); s += `<line x1="${x0}" y1="${y0}" x2="${x1}" y2="${y1}" stroke="#bfffe0" stroke-width="2.2" opacity="${a > 190 && a < 350 ? .3 : .8}" stroke-linecap="round" filter="url(#glow)"/>`; } return s; })()}
${etched(['A slow day,', 'and you still walked.'], 914, 356, 38, 1.2)}
${etched(['kept in the jar tonight'], 914, 985, 24, 1, .8)}
${spores(26, 7, [700, 160, 430, 380])}
<text class="etched" x="520" y="455" font-size="28" text-anchor="middle" filter="url(#textglow)" opacity=".9">hold to speak</text>
`));

// Nine habits across the four zones, grown in the moss: [x, base y, height, zone, mirror]
const NM_SPRIGS = [
  [772, 690, 88, 'Body', false], [826, 712, 70, 'Body', true], [878, 718, 96, 'Body', false],
  [770, 640, 64, 'Mind', true], [832, 620, 84, 'Mind', false],
  [1000, 622, 74, 'Work', true], [1056, 646, 58, 'Work', false],
  [968, 716, 92, 'Connection', false], [1040, 700, 66, 'Connection', true]
];
const NM_HUE = { Body: 'hue-rotate(0deg) saturate(1.1) brightness(1.35)', Mind: 'hue-rotate(70deg) saturate(1.2) brightness(1.4)', Work: 'hue-rotate(-55deg) saturate(1.4) brightness(1.45)', Connection: 'hue-rotate(-130deg) saturate(.9) brightness(1.5)' };
const sprigs = NM_SPRIGS.map(([x, y, h, z, m]) => {
  const w = h * 567 / 975;
  return `<img class="abs" src="${sprite('fern')}" style="left:${x - w / 2}px;top:${y - h}px;height:${h}px;mix-blend-mode:screen;filter:${NM_HUE[z]} drop-shadow(0 0 6px ${NM[z]});transform:${m ? 'scaleX(-1) ' : ''}rotate(${m ? -8 : 8}deg);transform-origin:50% 100%;opacity:.95">`;
}).join('');

const nmRecord = page(JAR, sprigs + svg(`
${zoneRing({ Body: .72, Mind: .55, Work: .81, Connection: .4 })}
<circle cx="878" cy="660" r="36" fill="none" stroke="#effff7" stroke-width="2" opacity=".9" filter="url(#glow)"/>
<path d="M906 640 C 1000 560, 1150 520, 1250 520" stroke="#effff7" stroke-width="1.6" fill="none" opacity=".7"/>
<text class="etched" x="1262" y="512" font-size="30" filter="url(#textglow)">Water, 8 glasses</text>
<text class="etched" x="1262" y="548" font-size="24" filter="url(#textglow)" style="fill:${NM.Body}">kept 18 days this month</text>
${etched(['October'], 914, 258, 26, 1, .85)}
`));

// ---------- Field Notebook (Sundial) ----------
const DIAL = repo('tools', 'fidelity', 'rungs', 'DialG1-level2-round3-idle.png');
const SOIL = { cx: 790, cy: 560, rx: 272, ry: 168 };
const FN = { Body: '#6f9e6a', Mind: '#5f84b3', Work: '#b88440', Connection: '#c9707f' };
const TABS = [['Body', 520, 6], ['Mind', 672, 2.5], ['Work', 902, -2.5], ['Connection', 1060, -6]];

function tabs(fill) {
  return TABS.map(([z, x, rot]) => {
    const yTop = 615 + 310 * Math.sqrt(Math.max(0, 1 - ((x - 785) / 495) ** 2)) - 8;
    const w = z === 'Connection' ? 128 : 96, h = 62, c = FN[z];
    const f = fill ? fill[z] : 0;
    return `<g transform="rotate(${rot} ${x} ${yTop})" filter="url(#pencil)">
<path d="M${x - w / 2} ${yTop} L${x - w / 2 + 6} ${yTop + h} Q${x} ${yTop + h + 4} ${x + w / 2 - 6} ${yTop + h} L${x + w / 2} ${yTop}Z" fill="#f4e8d0" stroke="#2b2420" stroke-width="2"/>
${f > 0 ? `<rect x="${x - w / 2 + 8}" y="${yTop + 6}" width="${(w - 16) * f}" height="${h - 14}" fill="url(#hatch-${z})"/>` : ''}
<text class="ink" x="${x}" y="${yTop + 42}" font-size="30" font-weight="600" text-anchor="middle">${z}</text></g>`;
  }).join('');
}

function inkCircle(x, y, r, opts = '') {
  return `<path d="M${x + r} ${y} C${x + r} ${y - r * 1.05} ${x - r * 1.02} ${y - r * 1.02} ${x - r} ${y + 2} C${x - r * .98} ${y + r * 1.04} ${x + r * 1.06} ${y + r * .98} ${x + r * .96} ${y - 4}" fill="none" stroke="#2b2420" stroke-width="2.4" stroke-linecap="round" ${opts}/>`;
}

const fnPlant = (name, x, baseY, h) => {
  const ratio = { plant_sunrise: 699 / 1049, plant_sunrise_bloom: 699 / 1049, plant_midday: 681 / 955, plant_midday_bloom: 699 / 960, plant_dusk: 659 / 1044, plant_dusk_bloom: 696 / 1048 }[name];
  const w = h * ratio;
  return `<img class="abs" src="${sprite(name)}" style="left:${x - w / 2}px;top:${baseY - h}px;height:${h}px;mix-blend-mode:multiply">`;
};

function zoneDot(x, y, z) {
  return `<circle cx="${x}" cy="${y}" r="9" fill="${FN[z]}" stroke="#2b2420" stroke-width="1.6" filter="url(#pencil)"/>`;
}

const packet = (name, x, y, h, rot) => `<img class="abs" src="${sprite(name)}" style="left:${x - h * .45}px;top:${y}px;height:${h}px;transform:rotate(${rot}deg);filter:drop-shadow(0 8px 10px rgba(60,40,20,.35))">`;

const slip = (x, y, w, lines, rot = 0) => `<div class="card" style="left:${x - w / 2}px;top:${y}px;width:${w}px;padding:8px 10px 10px;transform:rotate(${rot}deg);text-align:center;font-family:'Caveat',cursive;color:#2b2420;line-height:1.05">${lines.map(([t, s, c]) => `<div style="font-size:${s}px;${c ? `color:${c};` : ''}font-weight:600">${t}</div>`).join('')}</div>`;

const fnOnboarding = page(DIAL,
  `<div class="card" style="left:36px;top:330px;width:300px;height:250px;transform:rotate(-4deg)"></div>`
  + packet('packet-midday', 450, 52, 140, -6) + packet('packet-morning', 700, 30, 140, 4) + packet('packet-winddown', 1030, 44, 140, -3)
  + slip(450, 200, 220, [['Walk after lunch', 29], ['Body · weekdays', 23, FN.Body]], -3)
  + slip(700, 178, 220, [['Water, 8 glasses', 29], ['Body · every day', 23, FN.Body]], 3)
  + slip(1040, 192, 250, [['Call mum', 29], ['Connection · twice a week', 22, FN.Connection]], -2)
  + svg(`
<g transform="rotate(-4 186 455)">
<text class="ink" x="186" y="400" font-size="40" font-weight="600" text-anchor="middle">What would you</text>
<text class="ink" x="186" y="444" font-size="40" font-weight="600" text-anchor="middle">like to grow?</text>
${[0, 1, 2].map(i => `<path d="M${96 + i * 4} ${500 + i * 14} q 22 -16 44 0 t 44 0 t 44 0 t 44 0" stroke="#2b2420" stroke-width="${2.4 - i * .5}" fill="none" opacity="${.85 - i * .2}" filter="url(#pencil)"/>`).join('')}
<text class="ink" x="186" y="566" font-size="26" text-anchor="middle" opacity=".75">say it, or pinch a packet</text>
</g>
${tabs(null)}
<path d="M450 270 C 410 520, 460 760, 515 900" stroke="#2b2420" stroke-width="2.2" fill="none" stroke-dasharray="2 10" stroke-linecap="round"/>
`));

const fnBreaks = page(DIAL,
  slip(470, 128, 280, [['rest your eyes here', 30], ['13 s', 24, '#7b6aa8']], 2)
  + `<div class="card" style="left:40px;top:640px;width:240px;height:120px;transform:rotate(-3deg)"></div>`
  + svg(`
<path d="M857 236 C 800 120, 640 60, 480 74" stroke="#2b2420" stroke-width="2" fill="none" stroke-dasharray="2 10" stroke-linecap="round"/>
<circle cx="470" cy="70" r="30" fill="#b49ad8" opacity=".55" filter="url(#pencil)"/>
${inkCircle(470, 70, 30)}
${[0, 45, 90, 135, 180, 225, 270, 315].map(a => `<line x1="${470 + 40 * Math.cos(rad(a))}" y1="${70 + 40 * Math.sin(rad(a))}" x2="${470 + 52 * Math.cos(rad(a))}" y2="${70 + 52 * Math.sin(rad(a))}" stroke="#2b2420" stroke-width="2" stroke-linecap="round"/>`).join('')}
${[[240, 470, true], [1200, 250, true], [1132, 330, false]].map(([x, y, done]) => `
${done ? '' : `<circle cx="${x}" cy="${y}" r="34" fill="url(#hatch-active)" opacity=".8"/>`}
${inkCircle(x, y, 24)}
${done ? `<path d="M${x - 10} ${y} l7 8 l14 -16" stroke="#2b2420" stroke-width="3" fill="none" stroke-linecap="round"/>` : `<line x1="${x - 8}" y1="${y}" x2="${x + 8}" y2="${y}" stroke="#2b2420" stroke-width="2.4"/><line x1="${x}" y1="${y - 8}" x2="${x}" y2="${y + 8}" stroke="#2b2420" stroke-width="2.4"/>`}`).join('')}
${[0, 1, 2].map(i => `<path d="M${1060 + i * 9} ${380 - i * 16} q 10 -12 2 -26" stroke="#2b2420" stroke-width="1.8" fill="none" opacity=".7"/>`).join('')}
<g transform="rotate(-3 160 700)">
<text class="ink" x="160" y="694" font-size="34" font-weight="600" text-anchor="middle">reach to each mark</text>
<text class="ink" x="160" y="736" font-size="28" text-anchor="middle" opacity=".8">stretch 2 of 3</text>
</g>
${tabs({ Body: .35, Mind: 0, Work: 0, Connection: 0 })}
`));

const fnReflection = page(DIAL,
  `<div class="abs" style="inset:0;background:linear-gradient(180deg, rgba(70,50,110,.38), rgba(40,30,70,.45));mix-blend-mode:multiply"></div>`
  + `<div class="card" style="left:30px;top:300px;width:380px;height:300px;transform:rotate(-3deg)"></div>`
  + svg(`
${[0, 1, 2, 3].map(i => `<ellipse cx="985" cy="440" rx="${60 + i * 28}" ry="${30 + i * 13}" fill="none" stroke="#2b2420" stroke-width="${2.2 - i * .35}" stroke-dasharray="${i % 2 ? '6 8' : '14 6'}" opacity="${.85 - i * .17}" filter="url(#pencil)"/>`).join('')}
<g transform="rotate(-3 220 450)">
<text class="ink" x="72" y="360" font-size="28" opacity=".75">Tue 13 Oct</text>
<text class="ink" x="72" y="430" font-size="44" font-weight="600">A slow day,</text>
<text class="ink" x="72" y="482" font-size="44" font-weight="600">and you still walked.</text>
<path d="M72 510 q 40 -10 80 0 t 80 0 t 80 0" stroke="#2b2420" stroke-width="1.8" fill="none" opacity=".6"/>
<text class="ink" x="72" y="566" font-size="26" opacity=".75">kept in the notebook</text>
</g>
<text class="ink" x="985" y="360" font-size="30" font-weight="600" text-anchor="middle" style="fill:#f6efe2">hold to speak</text>
`));

// Nine plants, three per arc, each with its zone dot: [sprite, angle on the soil rim, height, zone]
const FN_NEW = [
  ['plant_sunrise', 232, 118, 'Body', -26],
  ['plant_midday', 250, 112, 'Work', -26], ['plant_midday_bloom', 264, 104, 'Body', -26], ['plant_midday', 292, 116, 'Connection', -26],
  ['plant_dusk', 340, 116, 'Mind', 8], ['plant_dusk_bloom', 28, 112, 'Work', 8]
];
const FN_OLD = [[510, 560, 'Body'], [985, 515, 'Connection'], [1100, 630, 'Mind']];
const fnRecordPlants = FN_NEW.map(([s, a, h, , g]) => { const [x, y] = pt(SOIL, a, g); return fnPlant(s, x, y, h); }).join('');
const fnRecord = page(DIAL, fnRecordPlants
  + slip(1350, 120, 300, [['Plan tomorrow\'s top 3', 30], ['kept 18 days this month', 26, FN.Work]], 2)
  + svg(`
${FN_NEW.map(([, a, , z, g]) => { const [x, y] = pt(SOIL, a, g); return zoneDot(x, y + 8, z); }).join('')}
${FN_OLD.map(([x, y, z]) => zoneDot(x, y, z)).join('')}
${(() => { const [x, y] = pt(SOIL, 250, -26); return inkCircle(x, y - 56, 62) + `<path d="M${x + 60} ${y - 80} C ${x + 220} ${y - 230}, 1100 160, 1196 150" stroke="#2b2420" stroke-width="2" fill="none"/>`; })()}
<text class="ink" x="785" y="902" font-size="30" font-weight="600" text-anchor="middle">October</text>
${tabs({ Body: .72, Mind: .55, Work: .81, Connection: .4 })}
`));

const FRAMES = [
  ['night-moss', '1-onboarding-zones', nmOnboarding], ['night-moss', '2-breaks', nmBreaks],
  ['night-moss', '3-reflection', nmReflection], ['night-moss', '4-record', nmRecord],
  ['field-notebook', '1-onboarding-zones', fnOnboarding], ['field-notebook', '2-breaks', fnBreaks],
  ['field-notebook', '3-reflection', fnReflection], ['field-notebook', '4-record', fnRecord]
];

const MOMENTS = [
  ['1-onboarding-zones', 'AI onboarding and zones', 'The coach proposes three habits; each lands in its zone'],
  ['2-breaks', 'Break moments', 'Stretch to three marks, then rest the eyes on a far point in the room'],
  ['3-reflection', 'Evening reflection', 'Hold to speak; one line is kept'],
  ['4-record', 'Week and month record', 'Nine habits across four zones, read at a glance']
];

function board() {
  const thumb = (style, id) => url(path.join(out, style, id + '.jpg'));
  const rows = MOMENTS.map(([id, title, note]) => `<div class="row"><div class="label"><b>${title}</b><span>${note}</span><em>R1 __ R2 __ R3 __ R4 __ R5 __</em></div>
<img src="${thumb('night-moss', id)}"><img src="${thumb('field-notebook', id)}"></div>`).join('');
  return `<!doctype html><html><head><meta charset="utf-8"><style>${fontFaces()}
body{margin:0;background:#f4efe6;font-family:Georgia,serif;color:#2b2420;width:2240px}
h1{font-size:40px;margin:36px 40px 4px;font-weight:600}
p.sub{margin:0 40px 22px;font-size:20px;color:#6a5e52}
.cols{display:grid;grid-template-columns:330px 912px 912px;gap:18px;padding:0 40px;font-size:24px;font-weight:600}
.row{display:grid;grid-template-columns:330px 912px 912px;gap:18px;padding:14px 40px;align-items:center}
.row img{width:912px;height:512px;border-radius:6px;box-shadow:0 6px 16px rgba(40,30,20,.25)}
.label b{display:block;font-size:26px;margin-bottom:8px}.label span{display:block;font-size:19px;color:#6a5e52;margin-bottom:16px}
.label em{font-style:normal;font-size:18px;letter-spacing:.5px;color:#8a7c6c}
.foot{padding:18px 40px 36px;font-size:18px;color:#6a5e52;line-height:1.5}
</style></head><body><h1>Garden VR upgrade: which style carries it?</h1>
<p class="sub">Composited concept frames on the round-3 app renders (layout, scale and reading, not final fidelity). Score each frame 1 to 5 on R1 to R5. Under 3 on R1 or R2 disqualifies a style.</p>
<div class="cols"><div></div><div>Night Moss (Terrarium)</div><div>Field Notebook (Sundial)</div></div>${rows}
<div class="foot">R1 nine habits across four zones read in 2 s · R2 the break is clear in the room · R3 the voice moment has a visible listener and stays calm · R4 the month reads as growth, never a score · R5 art cost to reach</div>
</body></html>`;
}

const { chromium } = require(path.join(process.env.PLAYWRIGHT_NODE_MODULES || '/opt/node22/lib/node_modules', 'playwright'));
const browser = await chromium.launch();
const pageCtx = await browser.newPage({ viewport: { width: W, height: H } });
const tmp = path.join(build, 'html');
fs.mkdirSync(tmp, { recursive: true });
for (const [style, id, html] of FRAMES) {
  const file = path.join(tmp, `${style}-${id}.html`);
  fs.writeFileSync(file, html);
  fs.mkdirSync(path.join(out, style), { recursive: true });
  await pageCtx.goto(url(file));
  await pageCtx.evaluate(() => document.fonts.ready);
  await pageCtx.waitForTimeout(150);
  await pageCtx.screenshot({ path: path.join(out, style, id + '.jpg'), type: 'jpeg', quality: 92 });
  console.log('wrote', path.join(out, style, id + '.jpg'));
}
const boardFile = path.join(tmp, 'board.html');
fs.writeFileSync(boardFile, board());
await pageCtx.setViewportSize({ width: 2240, height: 1200 });
await pageCtx.goto(url(boardFile));
await pageCtx.evaluate(() => document.fonts.ready);
await pageCtx.screenshot({ path: path.join(out, 'board.jpg'), fullPage: true, type: 'jpeg', quality: 90 });
console.log('wrote', path.join(out, 'board.jpg'));
await browser.close();
