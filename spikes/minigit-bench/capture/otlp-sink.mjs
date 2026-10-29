#!/usr/bin/env node
// The capture's FILE sink: accepts POST /v1/logs and /v1/metrics on 127.0.0.1, appends the body RAW to <dir>/otlp-raw.ndjson and parses
// nothing - the raw payload is what the decoder is later tested on. `node otlp-sink.mjs <dir>` prints {"port":n} and runs until killed.
import { appendFileSync, mkdirSync } from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readBody } from '../lib/http-body.mjs';

const PATHS = ['/v1/logs', '/v1/metrics'];

export async function startSink({ dir, port = 0 }) {
  mkdirSync(dir, { recursive: true });
  const file = path.join(dir, 'otlp-raw.ndjson');
  const reply = (res, status, body = {}) => { res.writeHead(status, { 'content-type': 'application/json', connection: 'close' }); res.end(JSON.stringify(body)); };
  const server = http.createServer(async (req, res) => {
    if (req.method !== 'POST' || !PATHS.includes(req.url)) { req.resume(); return reply(res, req.method === 'POST' ? 404 : 405); }
    const body = await readBody(req);
    if (!body.ok) return reply(res, body.reason === 'too large' ? 413 : 400, { error: body.reason });
    appendFileSync(file, `${JSON.stringify({ receivedAt: Date.now(), path: req.url, contentType: req.headers['content-type'] ?? null, body: body.buffer.toString('utf8') })}\n`);
    return reply(res, 200);
  });
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(port, '127.0.0.1', resolve); });
  return { server, port: server.address().port, file, close: () => new Promise(resolve => { server.close(() => resolve()); server.closeAllConnections(); }) };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const sink = await startSink({ dir: process.argv[2] ?? '.' });
  process.stdout.write(`${JSON.stringify({ port: sink.port })}\n`);
}
