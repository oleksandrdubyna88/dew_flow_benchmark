// Test plumbing for the credential broker: a stub upstream that records what it saw and replays a scripted answer, a client over the
// broker's unix socket, and the recorded SSE body. Nothing here talks to the real API.
import { Buffer } from 'node:buffer';
import http from 'node:http';
import path from 'node:path';
import { loadPrices } from '../lib/prices.mjs';
import { brokerServer } from '../lib/broker.mjs';
import { readFileSync } from 'node:fs';
import { spikeDir, tmpDir } from './helpers.mjs';

export const prices = () => loadPrices(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'prices-2026-10-29.json'), 'utf8'))).value;
export const fakeToken = () => `sk-ant-fake-${Math.random().toString(36).slice(2, 14)}`;

export { chunksOf, sleep, sseAnswer, sseBody, stubUpstream } from '../lib/stub-upstream.mjs';

/** A broker over a stub upstream in a fresh short dir; `lines` collects every log line. */
export async function startBroker({ stub, token = fakeToken(), capUsd = 0.2, reserveUsd = 0.05, cliVersion = '2.1.310', ...more } = {}) {
  const dir = tmpDir('bk');
  const socketPath = path.join(dir, 'api.sock');
  const lines = [];
  const broker = await brokerServer({ socketPath, token, upstream: stub.upstream, budget: { capUsd, reserveUsd }, prices: prices(), cliVersion, log: l => lines.push(l), ...more });
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
