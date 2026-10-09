// The coach relay's HTTP server. Run: ANTHROPIC_API_KEY=... node tools/relay/server.mjs
// Env: PORT (8787), RELAY_MODEL (claude-haiku-5-5), RELAY_SYSTEMS (allowed-systems.json beside this file).
// POST /v1/coach with header x-install-key and the JSON body RelayProtocol.Encode writes. GET /healthz answers ok.

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import Anthropic from '@anthropic-ai/sdk';
import { createHandler, DEFAULT_MODEL } from './relay.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const MAX_BODY = 64 * 1024;

export function createServer(handle) {
  return http.createServer((req, res) => {
    const send = (status, body) => {
      res.writeHead(status, { 'content-type': 'application/json', 'cache-control': 'no-store' });
      res.end(JSON.stringify(body));
    };
    if (req.method === 'GET' && req.url === '/healthz') return send(200, { ok: true });
    if (req.method !== 'POST' || req.url !== '/v1/coach') return send(404, { ok: false, failure: 'Rejected' });
    let size = 0;
    const chunks = [];
    req.setTimeout(15_000, () => { req.destroy(); });
    req.on('data', chunk => {
      size += chunk.length;
      if (size > MAX_BODY) { send(413, { ok: false, failure: 'Rejected' }); req.destroy(); return; }
      chunks.push(chunk);
    });
    req.on('end', async () => {
      if (res.writableEnded) return;
      let body;
      try { body = JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { return send(400, { ok: false, failure: 'Rejected' }); }
      const result = await handle({ body, installKey: req.headers['x-install-key'], ip: req.socket.remoteAddress });
      send(result.status, result.body);
    });
  });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const systemsFile = process.env.RELAY_SYSTEMS || path.join(here, 'allowed-systems.json');
  const allowedSystems = JSON.parse(fs.readFileSync(systemsFile, 'utf8')).systems;
  const handle = createHandler({
    client: new Anthropic(),
    model: process.env.RELAY_MODEL || DEFAULT_MODEL,
    allowedSystems,
    log: entry => console.log(JSON.stringify(entry))
  });
  const port = Number(process.env.PORT || 8787);
  createServer(handle).listen(port, () => console.log(JSON.stringify({ listening: port, model: process.env.RELAY_MODEL || DEFAULT_MODEL, systems: allowedSystems.length })));
}
