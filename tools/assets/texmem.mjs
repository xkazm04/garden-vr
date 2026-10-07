// Texture memory, the pure part: no git, no file system. census.mjs `--only texmem` feeds it the blobs tracked at one
// commit and writes docs/budgets/TEXTURE-MEMORY.md.
//
// For one texture: source size -> nPOTScale (where Unity applies it) -> the effective max size, aspect kept -> GPU bytes
// at ASTC 6x6, ceil(w/6) x ceil(h/6) x 16 per mip level, summed over the chain when the meta enables mipmaps.
// The effective max size is the Android entry when it is overridden, else the Default platform entry, else the legacy
// top-level maxTextureSize.

export const BLOCK = 6;
export const BLOCK_BYTES = 16;
// UnityEngine.TextureFormat ids an Android override can name. -1 is Automatic. 56 is the legacy ASTC_RGBA_6x6.
export const FORMAT_NAMES = {
  '-1': 'Automatic', 1: 'Alpha8', 3: 'RGB24', 4: 'RGBA32', 7: 'RGB565', 13: 'RGBA4444', 34: 'ETC_RGB4', 45: 'ETC2_RGB',
  47: 'ETC2_RGBA8', 48: 'ASTC_4x4', 49: 'ASTC_5x5', 50: 'ASTC_6x6', 51: 'ASTC_8x8', 52: 'ASTC_10x10', 53: 'ASTC_12x12',
  56: 'ASTC_RGBA_6x6', 63: 'R8', 66: 'ASTC_HDR_4x4', 68: 'ASTC_HDR_6x6',
};
const ASTC_6X6 = new Set([50, 56]);
const ASTC_BLOCK = { 48: 4, 49: 5, 50: 6, 51: 8, 52: 10, 53: 12, 56: 6, 66: 4, 68: 6 };
// Bytes per pixel of the uncompressed formats an override can name.
const BPP = { 1: 1, 3: 3, 4: 4, 7: 2, 13: 2, 63: 1 };
// Unity ignores nPOTScale for these TextureImporterType values: 2 GUI (Editor GUI and Legacy GUI), 8 Sprite.
const NPOT_IGNORED = new Set([2, 8]);

// TextureImporter .meta: the fields the size and format rules read. Platform entries are the `platformSettings` list.
export function parseImporter(text) {
  const lines = text.split(/\r?\n/);
  const top = k => { const m = text.match(new RegExp(`^  ${k}: (-?\\d+)\\s*$`, 'm')); return m ? Number(m[1]) : null; };
  const importer = (text.match(/^([A-Za-z]+Importer):\s*$/m) || [])[1] || null;
  const mip = text.match(/^ {4}enableMipMap: (\d+)\s*$/m);
  const platforms = {};
  let i = lines.indexOf('  platformSettings:');
  if (i >= 0) {
    let cur = null;
    for (i++; i < lines.length; i++) {
      const l = lines[i];
      if (l.startsWith('  - ')) cur = {};
      else if (!l.startsWith('    ')) break;
      const m = l.match(/^  (?:- | {2})([A-Za-z0-9_]+): ?(.*)$/);
      if (m && cur) { cur[m[1]] = m[2].trim(); if (m[1] === 'buildTarget') platforms[cur.buildTarget] = cur; }
    }
  }
  const plat = p => (p ? {
    maxTextureSize: Number(p.maxTextureSize), textureFormat: Number(p.textureFormat),
    textureCompression: Number(p.textureCompression), overridden: Number(p.overridden || 0),
  } : null);
  return {
    importer,
    textureType: top('textureType') ?? 0,
    textureShape: top('textureShape') ?? 1,
    nPOTScale: top('nPOTScale') ?? 0,
    mipmaps: mip ? Number(mip[1]) === 1 : true,
    legacyMaxTextureSize: top('maxTextureSize'),
    default: plat(platforms.DefaultTexturePlatform),
    android: plat(platforms.Android),
  };
}

// The settings Unity uses on Android: the Android entry when overridden, else the Default entry, else the legacy fields.
export function androidSettings(imp) {
  if (imp.android && imp.android.overridden) return { from: 'Android override', ...imp.android };
  if (imp.default) return { from: 'Default', ...imp.default };
  return { from: 'legacy', maxTextureSize: imp.legacyMaxTextureSize, textureFormat: -1, textureCompression: null, overridden: 0 };
}

const isPot = n => n > 0 && (n & (n - 1)) === 0;
export function nextPot(n) { let p = 1; while (p < n) p *= 2; return p; }
export function prevPot(n) { let p = 1; while (p * 2 <= n) p *= 2; return p; }
// Mathf.ClosestPowerOfTwo: a tie goes to the larger.
export function nearestPot(n) { const hi = nextPot(n), lo = hi / 2; return lo >= 1 && n - lo < hi - n ? lo : hi; }

// TextureImporterNPOTScale: 0 None, 1 ToNearest, 2 ToLarger, 3 ToSmaller. Applied per side, only to a non power of two
// texture, and not to the importer types that ignore it.
export function npotSize(w, h, mode, textureType = 0) {
  if (!mode || NPOT_IGNORED.has(textureType) || (isPot(w) && isPot(h))) return { width: w, height: h, applied: false };
  const f = mode === 1 ? nearestPot : mode === 2 ? nextPot : mode === 3 ? prevPot : null;
  if (!f) return { width: w, height: h, applied: false };
  return { width: f(w), height: f(h), applied: true };
}

// Downscale so the longer side fits maxSize, aspect kept; the shorter side rounds to the nearest pixel, at least 1.
export function capSize(w, h, maxSize) {
  const side = Math.max(w, h);
  if (!maxSize || side <= maxSize) return { width: w, height: h };
  const s = maxSize / side;
  return { width: w >= h ? maxSize : Math.max(1, Math.round(w * s)), height: h >= w ? maxSize : Math.max(1, Math.round(h * s)) };
}

// Mip levels from w x h down to 1 x 1, each side halving (floored, at least 1).
export function mipChain(w, h, mipmaps = true) {
  const out = [[w, h]];
  if (!mipmaps) return out;
  while (w > 1 || h > 1) { w = Math.max(1, w >> 1); h = Math.max(1, h >> 1); out.push([w, h]); }
  return out;
}

export const astcLevelBytes = (w, h, block = BLOCK) => Math.ceil(w / block) * Math.ceil(h / block) * BLOCK_BYTES;
export const astcBytes = (w, h, mipmaps = true, block = BLOCK) => mipChain(w, h, mipmaps).reduce((s, [x, y]) => s + astcLevelBytes(x, y, block), 0);

// Imported size and GPU bytes of one texture. src: { width, height }; imp: parseImporter's result.
export function textureMemory(src, imp) {
  const settings = androidSettings(imp);
  const npot = npotSize(src.width, src.height, imp.nPOTScale, imp.textureType);
  const imported = capSize(npot.width, npot.height, settings.maxTextureSize);
  const levels = mipChain(imported.width, imported.height, imp.mipmaps).length;
  const bytes = astcBytes(imported.width, imported.height, imp.mipmaps);
  const fmt = settings.textureFormat;
  // A note on what Android really gets when it is not ASTC 6x6: an override naming another format, or Automatic with
  // compression off (Unity then imports uncompressed, RGBA32).
  let androidFormat = null, ownFormatBytes = null;
  if (settings.from === 'Android override' && fmt !== -1 && !ASTC_6X6.has(fmt)) {
    androidFormat = FORMAT_NAMES[fmt] || `TextureFormat ${fmt}`;
    if (ASTC_BLOCK[fmt]) ownFormatBytes = astcBytes(imported.width, imported.height, imp.mipmaps, ASTC_BLOCK[fmt]);
    else if (BPP[fmt]) ownFormatBytes = mipChain(imported.width, imported.height, imp.mipmaps).reduce((s, [x, y]) => s + x * y * BPP[fmt], 0);
  } else if (fmt === -1 && settings.textureCompression === 0) {
    androidFormat = `Automatic, compression off (${settings.from}): RGBA32`;
    ownFormatBytes = mipChain(imported.width, imported.height, imp.mipmaps).reduce((s, [x, y]) => s + x * y * 4, 0);
  }
  return {
    source: [src.width, src.height], npot: npot.applied ? [npot.width, npot.height] : null,
    maxTextureSize: settings.maxTextureSize, maxFrom: settings.from,
    imported: [imported.width, imported.height], mipmaps: imp.mipmaps, levels, bytes,
    androidOverrideFormat: settings.from === 'Android override' && fmt !== -1 && !ASTC_6X6.has(fmt) ? androidFormat : null,
    androidFormat, ownFormatBytes,
  };
}

// TGA header: width and height are little-endian at bytes 12 and 14.
export function tgaHeader(b) {
  if (b.length < 18) return { error: 'tga shorter than its header' };
  return { width: b.readUInt16LE(12), height: b.readUInt16LE(14) };
}
