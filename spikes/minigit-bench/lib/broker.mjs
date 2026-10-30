// The credential broker (E2.S2 D-16, D-17): the only road from the agent's box to the API. It listens on a unix socket the box reaches as
// plain HTTP on its own loopback; it replaces `Authorization` with the REAL token (read by the harness, held here, never in the box),
// forwards every other request header (an open list: anthropic-beta carries the OAuth capability), streams the answer back as it arrives,
// follows no redirect, reads the JSON body first and refuses one that makes the API reach the network for the agent (broker-body.mjs), and enforces the cell's own budget (budget.mjs) so a Bash call that POSTs at the door cannot spend past the cap.
// The log names header NAMES and counts; it never carries a header value, a body or the token.
import { chmodSync, rmSync } from 'node:fs';
import http from 'node:http';
import https from 'node:https';
import { StringDecoder } from 'node:string_decoder';
import { MAX_BODY_BYTES, bodyDecision, encodingDecision } from './broker-body.mjs';
import { routeDecision } from './broker-routes.mjs';
import { OVER_CAP_FACTOR, admit, capStatus, openState, reservationFor, reserve, settle } from './budget.mjs';

export const DRAIN_MS = 120_000;
/** The most bytes of request bodies the broker holds in memory at once, across every request in flight: a request that would pass it is refused at once (503, `busy`). */
export const MAX_BUFFERED_BYTES = 64 * 1024 * 1024;
export const DEFAULT_UPSTREAM = Object.freeze({ host: 'api.anthropic.com', port: 443, tls: true });
const UPSTREAM_IDLE_MS = 120_000;
/**
 * Request headers never forwarded: the credential (replaced), `host` and `content-length` (set from the upstream and the body read), `accept-encoding` (so the usage parser reads
 * plain SSE), and every HOP-BY-HOP header (RFC 9110 7.6.1 and the legacy ones): they are about the client's connection to the broker, not about the request, and `upgrade` /
 * `http2-settings` would try to switch the upstream connection's protocol. A name the `Connection` header lists is hop-by-hop for that message and is dropped too.
 */
const DROPPED = Object.freeze(new Set([
  'authorization', 'x-api-key', 'host', 'content-length', 'accept-encoding',
  'connection', 'keep-alive', 'proxy-connection', 'proxy-authenticate', 'proxy-authorization', 'te', 'trailer', 'transfer-encoding', 'upgrade', 'http2-settings',
]));

/** The headers to forward: the request's minus the closed DROPPED list and every name its `Connection` header lists, `host` set to the upstream. A new object. */
export function forwardHeaders(headers, upstreamHost) {
  const listed = new Set(String(headers.connection ?? headers.Connection ?? '').split(',').map(n => n.trim().toLowerCase()).filter(Boolean));
  return { ...Object.fromEntries(Object.entries(headers).filter(([k]) => !DROPPED.has(k.toLowerCase()) && !listed.has(k.toLowerCase()))), host: upstreamHost };
}

/**
 * A feeder over an SSE response: `feed(chunk)` any slice of the stream, `result()` -> `{ model, usage }`. `message_start` carries the
 * input side and the model; `message_delta` carries the cumulative output (and, on newer APIs, the input side again). Unreadable events are skipped.
 */
export function parseSseUsage() {
  const decoder = new StringDecoder('utf8'); // a character split across two chunks is held back, not decoded as two U+FFFD
  let rest = '';
  const acc = { model: null, usage: null };
  const take = ev => {
    if (ev.type === 'message_start' && ev.message) {
      acc.model = ev.message.model ?? acc.model;
      acc.usage = { ...(acc.usage ?? {}), ...(ev.message.usage ?? {}) };
    }
    if (ev.type === 'message_delta' && ev.usage) acc.usage = { ...(acc.usage ?? {}), ...ev.usage };
  };
  const dataOf = block => block.split('\n').filter(l => l.startsWith('data:')).map(l => l.slice(5).trim());
  return {
    feed(chunk) {
      rest = (rest + (typeof chunk === 'string' ? chunk : decoder.write(chunk))).replaceAll('\r\n', '\n');
      for (let i = rest.indexOf('\n\n'); i >= 0; i = rest.indexOf('\n\n')) {
        const block = rest.slice(0, i);
        rest = rest.slice(i + 2);
        for (const data of dataOf(block)) {
          try { take(JSON.parse(data)); } catch { /* [DONE] or a partial: not usage */ }
        }
      }
    },
    result: () => ({ model: acc.model, usage: acc.usage }),
  };
}

/** `{ model, usage, apiError }` from a non-streaming JSON body; `apiError` is true for the API's own error shape (`type: "error"`). */
function readJsonBody(chunks) {
  try {
    const j = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    return { model: j.model ?? null, usage: j.usage ?? null, apiError: j.type === 'error' };
  } catch {
    return { model: null, usage: null, apiError: false };
  }
}

/** The reason a finished answer was not billed, or null: an error status with the API's JSON error body (an overloaded or rate-limited call is free), or a 2xx from the free token counter. */
const unbilledAnswer = (status, apiError, countTokens) => {
  if (status >= 400 && apiError) return 'upstream error';
  return countTokens && status >= 200 && status < 300 ? 'count_tokens is free' : null;
};
const isCountTokens = url => String(url).split('?')[0] === '/v1/messages/count_tokens';

/** Refusals by their fixed reason word: `cap` (the cell's budget), `path` (off the route list) and `body` (a body that would make the API reach the network) and `busy` (the bytes of request bodies held at once would pass the bound). A line with another reason is counted under it. */
export const REFUSAL_REASONS = Object.freeze(['cap', 'path', 'body', 'busy']);
const refusedByReason = lines => lines.filter(l => l.event === 'refused')
  .reduce((acc, l) => ({ ...acc, [l.reason]: (acc[l.reason] ?? 0) + 1 }), Object.fromEntries(REFUSAL_REASONS.map(r => [r, 0])));

/** A settled line whose model the price table did not hold: it was charged at the dearest known rates, and says so. */
const isUnpriced = l => l.priced?.known === false && l.priced?.usd !== undefined;

/**
 * The ledger read out of `broker.ndjson` lines: the 4th cost view `B`, beside E, O and T. A ledger that ends with the sealed `summary` line (`seal()`) is read as the seal
 * reported it: `spentUsd` is the settled spend plus the unsettled reservations and `lowerBound` says it may be more, the same two figures as `broker.json` and
 * `meta.broker`. Without one, `spentUsd` is the sum of the settled lines and an unsettled request makes it a lower bound.
 */
export function brokerView(lines) {
  const settled = lines.filter(l => l.event === 'settled');
  const forwarded = lines.filter(l => l.event === 'forwarding');
  const done = new Set(settled.map(l => l.seq));
  const sealed = lines.findLast(l => l.event === 'summary' && l.sealed === true);
  const unsettled = forwarded.filter(l => !done.has(l.seq)).length;
  return {
    present: lines.length > 0,
    forwarded: forwarded.length,
    refused: lines.filter(l => l.event === 'refused').length,
    refusedByReason: refusedByReason(lines),
    unpricedModels: [...new Set(settled.filter(isUnpriced).map(l => l.model))],
    unsettled,
    spentUsd: sealed === undefined ? settled.reduce((a, l) => a + l.chargedUsd, 0) : sealed.spentUsd,
    lowerBound: sealed === undefined ? unsettled > 0 : sealed.lowerBound === true,
    settled: settled.map(l => ({ seq: l.seq, model: l.model, usage: l.usage, chargedUsd: l.chargedUsd, status: l.status, clientClosedEarly: l.clientClosedEarly, ...(isUnpriced(l) ? { unpriced: true } : {}) })),
  };
}

const PATH_ERROR = JSON.stringify({ type: 'error', error: { type: 'permission_error', message: 'broker: this request is not on the broker\'s route list' } });
const BUSY_ERROR = JSON.stringify({ type: 'error', error: { type: 'overloaded_error', message: 'broker: busy, too many request bytes are held at once; retry' } });
const BODY_ERROR = why => JSON.stringify({ type: 'error', error: { type: 'permission_error', message: `broker: this request body is not forwarded (${why})` } });
const API_ERROR = cap => JSON.stringify({ type: 'error', error: { type: 'rate_limit_error', message: `broker: the cell's budget (${cap} USD) is spent` } });
const hostHeader = u => (u.port === 443 || u.port === 80 ? u.host : `${u.host}:${u.port}`);
const listen = (server, socketPath) => new Promise((resolve, reject) => { server.once('error', reject); server.listen(socketPath, resolve); });

/** The running ledger: the spend state, the counts, and the one writer of ledger lines. The state is replaced, never mutated. */
function makeLedger({ budget, prices, cliVersion, log }) {
  let state = openState();
  let seq = 0;
  const idleWaiters = new Set();
  const counts = { upstreamRequests: 0, refusedByReason: Object.fromEntries(REFUSAL_REASONS.map(r => [r, 0])) };
  const refusedTotal = () => Object.values(counts.refusedByReason).reduce((a, n) => a + n, 0);
  const refuse = reason => { counts.refusedByReason = { ...counts.refusedByReason, [reason]: (counts.refusedByReason[reason] ?? 0) + 1 }; };
  let sealed = false;
  const emit = line => {
    if (sealed) return; // after the seal no ledger line is written: the collector has read the ledger and written broker.json from it
    try { log({ t: new Date().toISOString(), ...line }); } catch (e) { process.stderr.write(`broker: the log sink failed: ${e.message}\n`); }
  };
  return {
    emit,
    /** From now on `emit` writes nothing; state still moves, so a late settlement cannot throw, it just stays out of the record. */
    seal() { sealed = true; },
    summary: () => ({ ...counts, refused: refusedTotal(), ...state, ...capStatus({ spentUsd: state.spentUsd, capUsd: budget.capUsd }), capUsd: budget.capUsd, overCapFactor: OVER_CAP_FACTOR }),
    /** A request off the route list: counted and logged with its own seq, no reservation, nothing forwarded -> the seq. */
    refusePath(req, names, why) {
      seq += 1;
      refuse('path');
      emit({ seq, event: 'refused', reason: 'path', method: String(req.method).slice(0, 20), path: String(req.url).slice(0, 200), headerNames: names, why });
      return seq;
    },
    /** A body the broker will not forward (`body`), or has no room for (`busy`): counted and logged with its own seq (the offending key or type, never a value), no reservation -> the seq. */
    refuseBody(req, names, { why, offending = null }, reason = 'body') {
      seq += 1;
      refuse(reason);
      emit({ seq, event: 'refused', reason, method: String(req.method).slice(0, 20), path: String(req.url).slice(0, 200), headerNames: names, why, ...(offending === null ? {} : { offending }) });
      return seq;
    },
    /** Admit or refuse the next request -> `{ entry } | { refusal: { seq, why } }`. An admitted one holds a reservation until it settles. */
    open() {
      seq += 1;
      const reserveUsd = reservationFor(state, budget.reserveUsd);
      const verdict = admit({ ...state, reserveUsd, capUsd: budget.capUsd });
      if (!verdict.admit) {
        refuse('cap');
        return { refusal: { seq, reason: 'cap', why: verdict.why, spentUsd: state.spentUsd, reservedUsd: state.reservedUsd } };
      }
      state = reserve(state, reserveUsd);
      counts.upstreamRequests += 1;
      return { entry: { seq, reserveUsd, startedAt: Date.now(), settled: false }, inFlight: state.inFlight, reservedUsd: state.reservedUsd };
    },
    /** Resolve `{ drained, inFlight, reservedUsd }` once no request is in flight, or at `timeoutMs` with what is still unsettled. */
    drain(timeoutMs) {
      const now = () => ({ drained: state.inFlight === 0, inFlight: state.inFlight, reservedUsd: state.reservedUsd });
      if (state.inFlight === 0) return Promise.resolve(now());
      return new Promise(resolve => {
        const done = () => { clearTimeout(timer); idleWaiters.delete(done); resolve(now()); };
        const timer = setTimeout(done, timeoutMs);
        idleWaiters.add(done);
      });
    },
    /** Settle once; a second call for the same entry is a no-op. `floor`: charge at least the reservation (see budget.settle). */
    settle(entry, model, usage, note, waive = null, floor = false) {
      if (entry.settled) return;
      entry.settled = true;
      const r = settle({ state, entry, usage, model, table: prices, cliVersion, waive, floor });
      state = r.state;
      if (state.inFlight === 0) { idleWaiters.forEach(wake => wake()); idleWaiters.clear(); }
      emit({ seq: entry.seq, event: 'settled', ...r.line, elapsedMs: Date.now() - entry.startedAt, ...note });
    },
  };
}

/** The bytes of request bodies held at once, across every request in flight, against one bound. */
function makeByteGate(limit) {
  let held = 0;
  return {
    tryHold(n) { if (held + n > limit) return false; held += n; return true; },
    release(n) { held = Math.max(0, held - n); },
  };
}

/**
 * The request's whole body, at most `limit` bytes -> `{ body, release } | { tooLarge: true } | { busy: true } | { aborted: true }`. A declared length is held in `gate` at once
 * (a body that does not fit is `busy`, before a byte is read); an undeclared one is held as it arrives. `release()` gives the bytes back, once: the caller calls it when the body
 * has been handed on (or refused); every other answer has already given them back.
 */
function readBody(req, limit, gate) {
  return new Promise(resolve => {
    const declared = Number(req.headers['content-length']);
    if (declared > limit) return resolve({ tooLarge: true });
    let held = 0;
    if (Number.isFinite(declared) && declared > 0) {
      if (!gate.tryHold(declared)) return resolve({ busy: true });
      held = declared;
    }
    const give = () => { gate.release(held); held = 0; };
    const chunks = [];
    let size = 0;
    const done = r => { req.off('data', onData); if (!r.body) give(); resolve(r); };
    const onData = chunk => {
      size += chunk.length;
      if (size > limit) return done({ tooLarge: true });
      if (size > held && !gate.tryHold(size - held)) return done({ busy: true });
      held = Math.max(held, size);
      chunks.push(chunk);
    };
    req.on('data', onData);
    req.once('end', () => done({ body: Buffer.concat(chunks), release: give }));
    req.once('error', () => done({ aborted: true }));
    req.once('close', () => { if (!req.complete) done({ aborted: true }); });
  });
}

/** `{ allow: true } | { allow: false, why, offending? }`: the encoding, then the body. */
function refusalOf(req, body) {
  const enc = encodingDecision(req.headers);
  return enc.allow ? bodyDecision(body) : enc;
}

/** Forward one admitted request to the upstream, signed, and relay the answer. */
function relay({ ledger, upstream, token, timeoutMs, live }, req, res, opened, names, body, release) {
  const { entry } = opened;
  const headers = { ...forwardHeaders(req.headers, hostHeader(upstream)), authorization: `Bearer ${token}`, 'content-length': String(body.length) };
  ledger.emit({ seq: entry.seq, event: 'forwarding', method: req.method, path: req.url, headerNames: names, reserveUsd: entry.reserveUsd, inFlight: opened.inFlight, reservedUsd: opened.reservedUsd });
  const progress = { partial: null }; // set once the answer has started: reads what the usage parser has so far
  const up = (upstream.tls ? https : http).request({ host: upstream.host, port: upstream.port, method: req.method, path: req.url, headers, timeout: timeoutMs },
    upRes => answer(upRes, res, entry, ledger, progress, isCountTokens(req.url)));
  live.add(up);
  up.on('close', () => { live.delete(up); release(); });
  up.on('finish', release); // the body is on the socket: its bytes are no longer held here
  up.on('timeout', () => up.destroy(new Error('ETIMEDOUT')));
  let sent = false; // the request is fully handed to the socket: from here the API may have it whole and may bill it
  up.on('finish', () => { sent = true; });
  const wire = watchWire(up, upstream);
  up.on('error', e => {
    const proof = wire.noByteSent();
    ledger.emit({ seq: entry.seq, event: 'upstream-error', code: e.code ?? e.message, requestSent: sent, noByteSent: proof });
    settleOnError(ledger, entry, progress, { error: e.code ?? e.message }, proof ? 'no byte was sent upstream' : null);
    if (!res.headersSent) res.writeHead(502, { connection: 'close' });
    res.end();
  });
  up.end(body); // the exact bytes that were read and checked
}

/**
 * Watch the upstream socket of one request: `noByteSent()` is true only when it is PROVEN that no byte of the request reached the upstream: no socket ever
 * existed; or the socket was new and never finished connecting (Node holds a write until the connection is made, over TLS until the handshake is done); or it
 * is a plain socket that has written nothing since the request took it. Anything else (a reset after part of the request was written, a handshake that
 * finished, a reused connection over TLS) may have been read by the API in part, so it is charged at the reservation, never waived.
 */
function watchWire(up, upstream) {
  const w = { socket: null, fresh: false, established: false, base: 0 };
  up.on('socket', s => {
    w.socket = s;
    w.base = s.bytesWritten;
    w.fresh = s.connecting === true || s.pending === true;
    if (w.fresh) s.once(upstream.tls ? 'secureConnect' : 'connect', () => { w.established = true; });
  });
  return { noByteSent: () => w.socket === null || (w.fresh && !w.established) || (!upstream.tls && w.socket.bytesWritten === w.base) };
}

/**
 * Settle a request that failed. Before any answer there is nothing parsed: the reservation (or 0 for a request provably never sent). After the answer began, the
 * usage the parser already holds is priced and charged, never less than the reservation: the API bills what it read, and a reset must not erase it.
 */
function settleOnError(ledger, entry, progress, note, waive = null) {
  const partial = progress.partial?.() ?? null;
  if (partial === null) return ledger.settle(entry, null, null, note, waive);
  return ledger.settle(entry, partial.model, partial.usage, { ...note, partialUsage: partial.usage !== null }, null, true);
}

/** Relay the upstream's answer: a 3xx is refused (502); otherwise status, headers and body as they arrive, usage parsed on the way. */
function answer(upRes, res, entry, ledger, progress, countTokens) {
  const status = upRes.statusCode ?? 0;
  if (status >= 300 && status < 400) {
    ledger.emit({ seq: entry.seq, event: 'redirect-refused', status, location: upRes.headers.location ?? null });
    res.writeHead(502, { connection: 'close' });
    res.end('broker: upstream redirect refused');
    upRes.resume();
    upRes.on('end', () => ledger.settle(entry, null, null, { status, redirect: true }));
    return;
  }
  const sse = /text\/event-stream/.test(upRes.headers['content-type'] ?? '');
  const parser = parseSseUsage();
  const chunks = [];
  progress.partial = () => (sse ? parser.result() : { model: null, usage: null });
  let clientClosedEarly = false;
  res.on('close', () => { if (!res.writableFinished) clientClosedEarly = true; }); // the upstream is billed whatever the client does: keep reading it
  res.on('error', () => { clientClosedEarly = true; });
  res.writeHead(status, upRes.headers);
  upRes.on('data', chunk => {
    if (sse) parser.feed(chunk); else chunks.push(chunk);
    if (!clientClosedEarly) res.write(chunk);
  });
  upRes.on('end', () => {
    if (!clientClosedEarly) res.end();
    const { model, usage, apiError = false } = sse ? parser.result() : readJsonBody(chunks);
    ledger.settle(entry, model, usage, { status, sse, clientClosedEarly }, unbilledAnswer(status, apiError, countTokens));
  });
  const failed = e => {
    settleOnError(ledger, entry, progress, { status, error: e.code ?? e.message, clientClosedEarly });
    if (!res.writableFinished) res.destroy();
  };
  upRes.on('error', failed);
  upRes.on('aborted', () => failed(new Error('aborted')));
}

/**
 * Start the broker -> `{ server, summary(), drain(timeoutMs), seal(), close() }`. `drain` waits until nothing is in flight: the upstream bills a request
 * whatever the client does, so a collector must wait for it before it records what the cell spent; `seal()` then ends the ledger (see there), and `close()` seals if the collector did not. `token` is a value; `budget` is `{ capUsd, reserveUsd }`; `prices` a loaded table;
 * `log(line)` receives every ledger line. `upstream` is injectable so tests run a local stub; the smoke runs the real one.
 */
export async function brokerServer({ socketPath, token, upstream = DEFAULT_UPSTREAM, budget, prices, cliVersion, log, timeoutMs = UPSTREAM_IDLE_MS, maxBufferedBytes = MAX_BUFFERED_BYTES }) {
  const ledger = makeLedger({ budget, prices, cliVersion, log });
  const live = new Set(); // the upstream requests in flight
  const gate = makeByteGate(maxBufferedBytes);
  const server = http.createServer(async (req, res) => {
    // a client that resets mid-write makes the response emit `error` (a write to a destroyed socket before it closed); unhandled it would end the broker, and with
    // it the cell's budget. `answer` also marks the client gone; the upstream is billed either way and stays read to its end.
    res.on('error', () => {});
    const names = Object.keys(req.headers).sort();
    const route = routeDecision(req.method, req.url);
    if (!route.allow) {
      ledger.refusePath(req, names, route.why);
      req.resume();
      res.writeHead(403, { 'content-type': 'application/json', connection: 'close' });
      res.end(PATH_ERROR);
      return;
    }
    const read = await readBody(req, MAX_BODY_BYTES, gate);
    if (read.aborted) { ledger.emit({ event: 'body-aborted', method: req.method, path: req.url }); return; }
    if (read.busy) {
      ledger.refuseBody(req, names, { why: `the broker already holds request body bytes up to its bound (${maxBufferedBytes}): this request was not read` }, 'busy');
      res.writeHead(503, { 'content-type': 'application/json', connection: 'close', 'retry-after': '1' });
      res.end(BUSY_ERROR, () => req.destroy());
      return;
    }
    const verdict = read.tooLarge ? { allow: false, why: `the body is over ${MAX_BODY_BYTES} bytes` } : refusalOf(req, read.body);
    if (!verdict.allow) {
      read.release?.();
      ledger.refuseBody(req, names, verdict);
      res.writeHead(read.tooLarge ? 413 : 403, { 'content-type': 'application/json', connection: 'close' });
      res.end(BODY_ERROR(verdict.why), () => (read.tooLarge ? req.destroy() : undefined));
      return;
    }
    const opened = ledger.open();
    if (opened.refusal) {
      read.release();
      ledger.emit({ ...opened.refusal, event: 'refused', method: req.method, path: req.url, headerNames: names });
      res.writeHead(429, { 'content-type': 'application/json', connection: 'close' });
      res.end(API_ERROR(budget.capUsd));
      return;
    }
    relay({ ledger, upstream, token, timeoutMs, live }, req, res, opened, names, read.body, read.release);
  });
  rmSync(socketPath, { force: true });
  await listen(server, socketPath);
  chmodSync(socketPath, 0o600);
  let closed = false;
  let sealedAs = null;
  const round9 = n => Math.round(n * 1e9) / 1e9;
  /**
   * The end of the ledger: what was settled, and what is still in flight at the drain limit counted at its reservation, so B = settled spend + unsettled
   * reservations and `lowerBound` says it may be more (the API bills what it read of a request nobody waited for). The summary line is the last line written;
   * then the ledger is silent and every request still in flight is destroyed, so nothing settles into a record that was already written.
   */
  const seal = () => {
    if (sealedAs !== null) return sealedAs;
    const s = ledger.summary();
    const unsettled = { count: s.inFlight, reservedUsd: s.reservedUsd };
    const boundUsd = round9(s.spentUsd + s.reservedUsd);
    const summary = { ...s, spentUsd: boundUsd, ...capStatus({ spentUsd: boundUsd, capUsd: budget.capUsd }) };
    ledger.emit({ event: 'summary', ...summary, settledUsd: s.spentUsd, unsettled, lowerBound: unsettled.count > 0, sealed: true });
    ledger.seal();
    live.forEach(up => up.destroy());
    sealedAs = Object.freeze({ unsettled, summary, lowerBound: unsettled.count > 0 });
    return sealedAs;
  };
  return {
    server, summary: ledger.summary, drain: ledger.drain, seal,
    close: async () => {
      if (closed) return;
      closed = true;
      seal();
      await new Promise(resolve => { server.close(() => resolve()); server.closeAllConnections(); });
      rmSync(socketPath, { force: true });
    },
  };
}
