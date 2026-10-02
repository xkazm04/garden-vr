import { chromium } from 'playwright';
const b = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe' });
const p = await b.newPage({ viewport: { width: 1280, height: 800 } });
const url = 'file:///C:/Users/kazda/kiro/personas/.contest/arena/habit-garden-r2-r3/entries/claude-claude-opus-5-5_high-v1/variant-1/index.html';
await p.goto(url); await p.addStyleTag({ content: 'html{scroll-behavior:auto !important}' }); await p.waitForTimeout(800);
await p.screenshot({ path: 'C:/hgspike/fidelity/logs/page-top.png' });
for (const id of ['cost', 'gaps', 'headset']) { await p.evaluate(i => document.getElementById(i).scrollIntoView(), id); await p.waitForTimeout(300); await p.screenshot({ path: `C:/hgspike/fidelity/logs/page-${id}.png` }); }
const broken = await p.evaluate(() => [...document.images].filter(i => !i.complete || i.naturalWidth === 0).map(i => i.src));
const small = await p.evaluate(() => [...document.querySelectorAll('body *')].filter(e => e.childNodes.length && [...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()) && parseFloat(getComputedStyle(e).fontSize) < 12).length);
console.log('broken images', broken, 'text<12px', small);
await b.close();
