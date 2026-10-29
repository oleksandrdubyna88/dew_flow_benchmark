#!/usr/bin/env node
// The capture's FILE sink: accepts POST /v1/logs and /v1/metrics on 127.0.0.1, appends the body RAW to <dir>/otlp-raw.ndjson and parses
// nothing - the raw payload is what the decoder is later tested on. `node otlp-sink.mjs <dir>` prints {"port":n} and runs until killed.
import { createWriteStream, mkdirSync } from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { rawBodyLine, readBody } from '../lib/http-body.mjs';

const PATHS = ['/v1/logs', '/v1/metrics'];

/** Start the sink in `dir`. One append-mode write stream serves every request and is closed by `close()`; a request is answered after its line is flushed. */
export async function startSink({ dir, port = 0 }) {
  mkdirSync(dir, { recursive: true });
  const file = path.join(dir, 'otlp-raw.ndjson');
  const lines = createWriteStream(file, { flags: 'a' });
  let writeError = null;
  lines.on('error', e => { writeError = e; process.stderr.write(`otlp-sink: cannot write ${file}: ${e.message}\n`); });
  const append = text => new Promise((resolve, reject) => {
    if (writeError) { reject(writeError); return; }
    lines.write(text, e => (e ? reject(e) : resolve()));
  });
  const reply = (res, status, body = {}) => { res.writeHead(status, { 'content-type': 'application/json', connection: 'close' }); res.end(JSON.stringify(body)); };
  const handle = async (req, res) => {
    if (req.method !== 'POST' || !PATHS.includes(req.url)) { req.resume(); return reply(res, req.method === 'POST' ? 404 : 405); }
    const body = await readBody(req);
    if (!body.ok) return reply(res, body.reason === 'too large' ? 413 : 400, { error: body.reason });
    try {
      await append(`${JSON.stringify(rawBodyLine(req, body.value.toString('utf8')))}\n`);
    } catch (e) {
      return reply(res, 500, { error: `the sink could not write: ${e.message}` });
    }
    return reply(res, 200);
  };
  const server = http.createServer((req, res) => {
    handle(req, res).catch(e => {
      process.stderr.write(`otlp-sink: ${e.stack}\n`);
      if (!res.headersSent) reply(res, 500, { error: 'sink failure' });
    });
  });
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(port, '127.0.0.1', resolve); });
  const stopServer = () => new Promise(resolve => { server.close(() => resolve()); server.closeAllConnections(); });
  const closeFile = () => new Promise(resolve => {
    if (lines.destroyed) { resolve(); return; } // a failed stream is already closed: `end` would never call back
    lines.once('close', resolve);
    lines.end();
  });
  return { server, port: server.address().port, file, close: async () => { await stopServer(); await closeFile(); } };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const sink = await startSink({ dir: process.argv[2] ?? '.' });
  process.stdout.write(`${JSON.stringify({ port: sink.port })}\n`);
}
