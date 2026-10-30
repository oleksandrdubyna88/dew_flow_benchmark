// The credential broker (E2.S2 D-16, D-17): the only road from the agent's box to the API. It listens on a unix socket the box reaches as
// plain HTTP on its own loopback; it replaces `Authorization` with the REAL token (read by the harness, held here, never in the box),
// forwards every other request header (an open list: anthropic-beta carries the OAuth capability), streams the answer back as it arrives,
// follows no redirect, and enforces the cell's own budget (budget.mjs) so a Bash call that POSTs at the door cannot spend past the cap.
// The log names header NAMES and counts; it never carries a header value, a body or the token.
import { chmodSync, rmSync } from 'node:fs';
import http from 'node:http';
import https from 'node:https';
import { routeDecision } from './broker-routes.mjs';
import { OVER_CAP_FACTOR, admit, capStatus, openState, reserve, settle } from './budget.mjs';

export const DEFAULT_UPSTREAM = Object.freeze({ host: 'api.anthropic.com', port: 443, tls: true });
const UPSTREAM_IDLE_MS = 120_000;
/** Request headers never forwarded: the credential (replaced), the hop-by-hop ones, and accept-encoding (so the usage parser reads plain SSE). */
const DROPPED = Object.freeze(new Set(['authorization', 'x-api-key', 'host', 'connection', 'content-length', 'transfer-encoding', 'accept-encoding']));

/** The headers to forward: the request's minus the closed DROPPED list, `host` set to the upstream. A new object. */
export function forwardHeaders(headers, upstreamHost) {
  return { ...Object.fromEntries(Object.entries(headers).filter(([k]) => !DROPPED.has(k.toLowerCase()))), host: upstreamHost };
}

/**
 * A feeder over an SSE response: `feed(chunk)` any slice of the stream, `result()` -> `{ model, usage }`. `message_start` carries the
 * input side and the model; `message_delta` carries the cumulative output (and, on newer APIs, the input side again). Unreadable events are skipped.
 */
export function parseSseUsage() {
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
      rest = (rest + chunk.toString('utf8')).replaceAll('\r\n', '\n');
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

/** The reason a finished answer was not billed, or null: an error status with the API's JSON error body (an overloaded or rate-limited call is free). */
const unbilledAnswer = (status, apiError) => (status >= 400 && apiError ? 'upstream error' : null);

/** The ledger read out of `broker.ndjson` lines: the 4th cost view `B`, beside E, O and T. */
export function brokerView(lines) {
  const settled = lines.filter(l => l.event === 'settled');
  const forwarded = lines.filter(l => l.event === 'forwarding');
  const done = new Set(settled.map(l => l.seq));
  return {
    present: lines.length > 0,
    forwarded: forwarded.length,
    refused: lines.filter(l => l.event === 'refused').length,
    pathRefused: lines.filter(l => l.event === 'path-refused').length,
    unsettled: forwarded.filter(l => !done.has(l.seq)).length,
    spentUsd: settled.reduce((a, l) => a + l.chargedUsd, 0),
    settled: settled.map(l => ({ seq: l.seq, model: l.model, usage: l.usage, chargedUsd: l.chargedUsd, status: l.status, clientClosedEarly: l.clientClosedEarly })),
  };
}

const PATH_ERROR = JSON.stringify({ type: 'error', error: { type: 'permission_error', message: 'broker: this request is not on the broker\'s route list' } });
const API_ERROR = cap => JSON.stringify({ type: 'error', error: { type: 'rate_limit_error', message: `broker: the cell's budget (${cap} USD) is spent` } });
const hostHeader = u => (u.port === 443 || u.port === 80 ? u.host : `${u.host}:${u.port}`);
const listen = (server, socketPath) => new Promise((resolve, reject) => { server.once('error', reject); server.listen(socketPath, resolve); });

/** The running ledger: the spend state, the counts, and the one writer of ledger lines. The state is replaced, never mutated. */
function makeLedger({ budget, prices, cliVersion, log }) {
  let state = openState();
  let seq = 0;
  const counts = { upstreamRequests: 0, refused: 0, pathRefused: 0 };
  const emit = line => {
    try { log({ t: new Date().toISOString(), ...line }); } catch (e) { process.stderr.write(`broker: the log sink failed: ${e.message}\n`); }
  };
  return {
    emit,
    summary: () => ({ ...counts, ...state, ...capStatus({ spentUsd: state.spentUsd, capUsd: budget.capUsd }), capUsd: budget.capUsd, overCapFactor: OVER_CAP_FACTOR }),
    /** A request off the route list: counted and logged with its own seq, no reservation, nothing forwarded -> the seq. */
    refusePath(req, names, why) {
      seq += 1;
      counts.pathRefused += 1;
      emit({ seq, event: 'path-refused', method: String(req.method).slice(0, 20), path: String(req.url).slice(0, 200), headerNames: names, why });
      return seq;
    },
    /** Admit or refuse the next request -> `{ entry } | { refusal: { seq, why } }`. An admitted one holds a reservation until it settles. */
    open() {
      seq += 1;
      const verdict = admit({ ...state, reserveUsd: budget.reserveUsd, capUsd: budget.capUsd });
      if (!verdict.admit) {
        counts.refused += 1;
        return { refusal: { seq, why: verdict.why, spentUsd: state.spentUsd, reservedUsd: state.reservedUsd } };
      }
      state = reserve(state, budget.reserveUsd);
      counts.upstreamRequests += 1;
      return { entry: { seq, reserveUsd: budget.reserveUsd, startedAt: Date.now(), settled: false }, inFlight: state.inFlight, reservedUsd: state.reservedUsd };
    },
    /** Settle once; a second call for the same entry is a no-op. */
    settle(entry, model, usage, note, waive = null) {
      if (entry.settled) return;
      entry.settled = true;
      const r = settle({ state, entry, usage, model, table: prices, cliVersion, waive });
      state = r.state;
      emit({ seq: entry.seq, event: 'settled', ...r.line, elapsedMs: Date.now() - entry.startedAt, ...note });
    },
  };
}

/** Forward one admitted request to the upstream, signed, and relay the answer. */
function relay({ ledger, upstream, token, timeoutMs }, req, res, opened, names) {
  const { entry } = opened;
  const headers = { ...forwardHeaders(req.headers, hostHeader(upstream)), authorization: `Bearer ${token}` };
  ledger.emit({ seq: entry.seq, event: 'forwarding', method: req.method, path: req.url, headerNames: names, inFlight: opened.inFlight, reservedUsd: opened.reservedUsd });
  const up = (upstream.tls ? https : http).request({ host: upstream.host, port: upstream.port, method: req.method, path: req.url, headers, timeout: timeoutMs },
    upRes => answer(upRes, res, entry, ledger));
  up.on('timeout', () => up.destroy(new Error('ETIMEDOUT')));
  let sent = false; // the request is fully handed to the socket: from here the API may have it whole and may bill it
  up.on('finish', () => { sent = true; });
  up.on('error', e => {
    ledger.emit({ seq: entry.seq, event: 'upstream-error', code: e.code ?? e.message, requestSent: sent });
    ledger.settle(entry, null, null, { error: e.code ?? e.message }, sent ? null : 'no byte was sent upstream');
    if (!res.headersSent) res.writeHead(502, { connection: 'close' });
    res.end();
  });
  req.on('error', () => up.destroy());
  req.pipe(up);
}

/** Relay the upstream's answer: a 3xx is refused (502); otherwise status, headers and body as they arrive, usage parsed on the way. */
function answer(upRes, res, entry, ledger) {
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
  let clientClosedEarly = false;
  res.on('close', () => { if (!res.writableFinished) clientClosedEarly = true; }); // the upstream is billed whatever the client does: keep reading it
  res.writeHead(status, upRes.headers);
  upRes.on('data', chunk => {
    if (sse) parser.feed(chunk); else chunks.push(chunk);
    if (!clientClosedEarly) res.write(chunk);
  });
  upRes.on('end', () => {
    if (!clientClosedEarly) res.end();
    const { model, usage, apiError = false } = sse ? parser.result() : readJsonBody(chunks);
    ledger.settle(entry, model, usage, { status, sse, clientClosedEarly }, unbilledAnswer(status, apiError));
  });
  upRes.on('error', e => {
    ledger.settle(entry, null, null, { status, error: e.code ?? e.message, clientClosedEarly });
    if (!res.writableFinished) res.destroy();
  });
}

/**
 * Start the broker -> `{ server, summary(), close() }`. `token` is a value; `budget` is `{ capUsd, reserveUsd }`; `prices` a loaded table;
 * `log(line)` receives every ledger line. `upstream` is injectable so tests run a local stub; the smoke runs the real one.
 */
export async function brokerServer({ socketPath, token, upstream = DEFAULT_UPSTREAM, budget, prices, cliVersion, log, timeoutMs = UPSTREAM_IDLE_MS }) {
  const ledger = makeLedger({ budget, prices, cliVersion, log });
  const server = http.createServer((req, res) => {
    const names = Object.keys(req.headers).sort();
    const route = routeDecision(req.method, req.url);
    if (!route.allow) {
      ledger.refusePath(req, names, route.why);
      req.resume();
      res.writeHead(403, { 'content-type': 'application/json', connection: 'close' });
      res.end(PATH_ERROR);
      return;
    }
    const opened = ledger.open();
    if (opened.refusal) {
      ledger.emit({ ...opened.refusal, event: 'refused', method: req.method, path: req.url, headerNames: names });
      req.resume();
      res.writeHead(429, { 'content-type': 'application/json', connection: 'close' });
      res.end(API_ERROR(budget.capUsd));
      return;
    }
    relay({ ledger, upstream, token, timeoutMs }, req, res, opened, names);
  });
  rmSync(socketPath, { force: true });
  await listen(server, socketPath);
  chmodSync(socketPath, 0o600);
  let closed = false;
  return {
    server, summary: ledger.summary,
    close: async () => {
      if (closed) return;
      closed = true;
      ledger.emit({ event: 'summary', ...ledger.summary() });
      await new Promise(resolve => { server.close(() => resolve()); server.closeAllConnections(); });
      rmSync(socketPath, { force: true });
    },
  };
}
