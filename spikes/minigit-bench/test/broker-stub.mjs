// Test plumbing for the credential broker: a stub upstream that records what it saw and replays a scripted answer, a client over the
// broker's unix socket, and the recorded SSE body. Nothing here talks to the real API.
import { Buffer } from 'node:buffer';
import http from 'node:http';
import path from 'node:path';
import { loadPrices } from '../lib/prices.mjs';
import { brokerServer } from '../lib/broker.mjs';
import { readFileSync } from 'node:fs';
import { spikeDir, tmpDir } from './helpers.mjs';

export const sleep = ms => new Promise(r => setTimeout(r, ms));
export const prices = () => loadPrices(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'prices-2026-10-29.json'), 'utf8'))).value;
export const fakeToken = () => `sk-ant-fake-${Math.random().toString(36).slice(2, 14)}`;

/**
 * The SSE body of the 2026-10-29 stage 2b run, rebuilt from the usage its ledger recorded (the broker never logged bodies): a
 * message_start with 2 input / 2,297 cache-write-1h tokens, pings, content, a message_delta with 4 output tokens. Priced at the
 * 2.1.310 table it is exactly 0.009232.
 */
export function sseBody() {
  const ev = (type, data) => `event: ${type}\ndata: ${JSON.stringify({ type, ...data })}\n\n`;
  const usage = { input_tokens: 2, cache_creation_input_tokens: 2297, cache_read_input_tokens: 0, cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 2297 }, output_tokens: 1 };
  return [
    ev('message_start', { message: { id: 'msg_1', type: 'message', role: 'assistant', model: 'claude-sonnet-5-5', content: [], usage } }),
    ev('ping', {}),
    ev('content_block_start', { index: 0, content_block: { type: 'text', text: '' } }),
    ev('content_block_delta', { index: 0, delta: { type: 'text_delta', text: 'OK' } }),
    ev('content_block_stop', { index: 0 }),
    ev('ping', {}),
    ev('message_delta', { delta: { stop_reason: 'end_turn' }, usage: { output_tokens: 4 } }),
    ev('message_stop', {}),
  ].join('');
}

/** Split a string into n roughly equal chunks, on arbitrary byte boundaries (an SSE event may straddle two). */
export const chunksOf = (text, n) => {
  const size = Math.ceil(text.length / n);
  return Array.from({ length: n }, (_, i) => text.slice(i * size, (i + 1) * size)).filter(Boolean);
};

/** A stub upstream. `answer(req, res, body)` scripts the reply; every request is recorded in `seen`. */
export async function stubUpstream(answer) {
  const seen = [];
  const server = http.createServer((req, res) => {
    const chunks = [];
    req.on('data', c => chunks.push(c));
    req.on('end', () => {
      const body = Buffer.concat(chunks).toString('utf8');
      seen.push({ method: req.method, url: req.url, headers: req.headers, body, at: Date.now() });
      answer(req, res, body);
    });
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  return {
    seen, upstream: { host: '127.0.0.1', port: server.address().port, tls: false },
    close: () => new Promise(resolve => { server.close(() => resolve()); server.closeAllConnections(); }),
  };
}

/** Replays `chunks` with `gapMs` between them as a 200 text/event-stream; records when each chunk was written. */
export const sseAnswer = (chunks, gapMs, writes = []) => async (_req, res) => {
  res.writeHead(200, { 'content-type': 'text/event-stream' });
  for (const [i, c] of chunks.entries()) {
    if (i > 0) await sleep(gapMs);
    writes.push(Date.now());
    res.write(c);
  }
  res.end();
};

/** A broker over a stub upstream in a fresh short dir; `lines` collects every log line. */
export async function startBroker({ stub, token = fakeToken(), capUsd = 0.2, reserveUsd = 0.05, cliVersion = '2.1.310' } = {}) {
  const dir = tmpDir('bk');
  const socketPath = path.join(dir, 'api.sock');
  const lines = [];
  const broker = await brokerServer({ socketPath, token, upstream: stub.upstream, budget: { capUsd, reserveUsd }, prices: prices(), cliVersion, log: l => lines.push(l) });
  return { broker, socketPath, lines, token, stub, dir, close: () => broker.close() };
}

/** One request to the broker's socket -> { status, headers, body }. `onFirstChunk` fires when the first body chunk arrives. */
export function viaSocket(socketPath, { method = 'POST', path: p = '/v1/messages?beta=true', headers = {}, body = '{}', onFirstChunk, abortAfterFirstChunk = false } = {}) {
  return new Promise((resolve, reject) => {
    const req = http.request({ socketPath, path: p, method, headers: { 'content-type': 'application/json', ...headers } }, res => {
      const parts = [];
      res.on('data', c => {
        if (parts.length === 0) onFirstChunk?.(Date.now());
        parts.push(c);
        if (abortAfterFirstChunk) req.destroy();
      });
      res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(parts).toString('utf8') }));
      res.on('close', () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(parts).toString('utf8'), closedEarly: true }));
    });
    req.on('error', e => (abortAfterFirstChunk ? undefined : reject(e)));
    req.end(body);
  });
}
