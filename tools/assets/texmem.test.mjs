// node --test tools/assets/texmem.test.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import { astcBytes, astcLevelBytes, capSize, mipChain, nearestPot, npotSize, parseImporter, textureMemory } from './texmem.mjs';

const meta = ({ nPOTScale = 0, mip = 1, legacy = 2048, def = 2048, android = 2048, overridden = 0, format = -1, defCompression = 1 } = {}) => [
  'fileFormatVersion: 2', 'guid: 0123456789abcdef0123456789abcdef', 'TextureImporter:', '  mipmaps:', `    enableMipMap: ${mip}`,
  `  maxTextureSize: ${legacy}`, `  nPOTScale: ${nPOTScale}`, '  textureType: 0', '  textureShape: 1', '  platformSettings:',
  '  - serializedVersion: 4', '    buildTarget: DefaultTexturePlatform', `    maxTextureSize: ${def}`, '    textureFormat: -1',
  `    textureCompression: ${defCompression}`, '    overridden: 0',
  '  - serializedVersion: 4', '    buildTarget: Android', `    maxTextureSize: ${android}`, `    textureFormat: ${format}`,
  '    textureCompression: 1', `    overridden: ${overridden}`, '  spriteSheet:', '',
].join('\n');

test('NPOT: nearest rounds per side, a tie goes up; None and power-of-two sources are left alone', () => {
  assert.deepEqual(npotSize(600, 300, 1), { width: 512, height: 256, applied: true });
  assert.equal(nearestPot(768), 1024);
  assert.deepEqual(npotSize(600, 300, 2), { width: 1024, height: 512, applied: true });
  assert.deepEqual(npotSize(600, 300, 3), { width: 512, height: 256, applied: true });
  assert.deepEqual(npotSize(600, 300, 0), { width: 600, height: 300, applied: false });
  assert.deepEqual(npotSize(600, 300, 1, 8), { width: 600, height: 300, applied: false });
  const r = textureMemory({ width: 1000, height: 600 }, parseImporter(meta({ nPOTScale: 1, mip: 0 })));
  assert.deepEqual([r.npot, r.imported], [[1024, 512], [1024, 512]]);
  assert.equal(r.bytes, 171 * 86 * 16);
});

test('a 2048 source is capped to 1024, aspect kept; the Android override wins only when overridden', () => {
  assert.deepEqual(capSize(2048, 1024, 1024), { width: 1024, height: 512 });
  assert.deepEqual(capSize(1500, 2048, 1024), { width: 750, height: 1024 });
  assert.deepEqual(capSize(512, 512, 1024), { width: 512, height: 512 });
  const off = textureMemory({ width: 2048, height: 2048 }, parseImporter(meta({ def: 1024, android: 512 })));
  assert.deepEqual([off.imported, off.maxFrom], [[1024, 1024], 'Default']);
  const on = textureMemory({ width: 2048, height: 2048 }, parseImporter(meta({ def: 1024, android: 512, overridden: 1, format: 50 })));
  assert.deepEqual([on.imported, on.maxFrom, on.androidOverrideFormat], [[512, 512], 'Android override', null]);
  const legacy = parseImporter(meta({ legacy: 256 }).replace(/  platformSettings:[\s\S]*  spriteSheet:/, '  spriteSheet:'));
  assert.deepEqual(textureMemory({ width: 2048, height: 2048 }, legacy).imported, [256, 256]);
});

test('mip chain: levels sum down to 1x1, each level rounded up to whole 6x6 blocks', () => {
  assert.deepEqual(mipChain(8, 2), [[8, 2], [4, 1], [2, 1], [1, 1]]);
  assert.equal(mipChain(1024, 1024).length, 11);
  // 1024: 171x171 blocks; 512: 86; 256: 43; 128: 22; 64: 11; 32: 6; 16: 3; 8, 4, 2, 1: 2, 1, 1, 1.
  const blocks = [171, 86, 43, 22, 11, 6, 3, 2, 1, 1, 1];
  assert.equal(astcBytes(1024, 1024), blocks.reduce((s, b) => s + b * b * 16, 0));
  assert.equal(astcBytes(1024, 1024), 626288);
  assert.equal(astcBytes(1024, 1024, false), 171 * 171 * 16);
});

test('the 1x1 tail: a level smaller than a block still costs one whole block', () => {
  assert.equal(astcLevelBytes(1, 1), 16);
  assert.equal(astcBytes(1, 1), 16);
  assert.equal(astcBytes(2, 1), 32);
  assert.equal(astcLevelBytes(7, 1), 32);
});

test('a non-6x6 override is named; automatic with compression off is flagged at 4 bytes per pixel', () => {
  const r = textureMemory({ width: 64, height: 64 }, parseImporter(meta({ overridden: 1, format: 48, android: 64, mip: 0 })));
  assert.deepEqual([r.androidOverrideFormat, r.bytes, r.ownFormatBytes], ['ASTC_4x4', 11 * 11 * 16, 16 * 16 * 16]);
  const u = textureMemory({ width: 64, height: 64 }, parseImporter(meta({ defCompression: 0, mip: 0 })));
  assert.deepEqual([u.androidOverrideFormat, u.ownFormatBytes], [null, 64 * 64 * 4]);
  assert.match(u.androidFormat, /compression off/);
});
