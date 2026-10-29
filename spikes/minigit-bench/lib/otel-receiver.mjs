// The OTLP/HTTP JSON receiver for a live session: 127.0.0.1 only, JSON only, bounded. The one I/O module besides capture/*.
// It keeps every body raw (`otel-raw.ndjson`, the file sink's line shape), the decoded records (`otel.ndjson`) and a running cost
// sum that E2's cost watchdog reads: `cost_usd` per request is final and includes children.
import { appendFileSync, mkdirSync } from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { isJson, rawBodyLine, readBody } from './http-body.mjs';
import { apiRequests, decodeOtlpLogs } from './otel-decode.mjs';

const PATHS = Object.freeze(['/v1/logs', '/v1/metrics']);
const HOST = '127.0.0.1';

/** The five exporter variables of the capture plus the tool-details switch, for one receiver port. The caller adds them AFTER any env scrub. */
export const otelEnv = port => Object.freeze({
  CLAUDE_CODE_ENABLE_TELEMETRY: '1', OTEL_LOGS_EXPORTER: 'otlp', OTEL_METRICS_EXPORTER: 'otlp', OTEL_EXPORTER_OTLP_PROTOCOL: 'http/json',
  OTEL_EXPORTER_OTLP_ENDPOINT: `http://${HOST}:${port}`, OTEL_LOGS_EXPORT_INTERVAL: '1000', OTEL_METRIC_EXPORT_INTERVAL: '1000',
  OTEL_LOG_TOOL_DETAILS: '1', OTEL_LOG_USER_PROMPTS: '0',
});

/** The running sum over decoded record lines (`{ receivedAt, name, tsMs, attrs }`): cost of `api_request`s whose cost is known, each request id once. */
export function liveSumOf(records) {
  const all = apiRequests(records.filter(r => r.name === 'api_request'));
  const seen = new Set();
  const reqs = all.filter(r => r.requestId === null || (!seen.has(r.requestId) && seen.add(r.requestId)));
  const lastAt = records.filter(r => r.name === 'api_request').reduce((m, r) => Math.max(m, r.receivedAt ?? 0), 0);
  return {
    usd: reqs.reduce((a, r) => a + (r.costUsd ?? 0), 0), requests: reqs.length, duplicates: all.length - reqs.length, costUnknown: reqs.filter(r => r.costUsd === null).length, lastAt,
  };
}

const reply = (res, status, body = {}) => {
  res.writeHead(status, { 'content-type': 'application/json', connection: 'close' });
  res.end(JSON.stringify(body));
};

/** The request handler over shared state: `append(file, obj)` writes a line, `counters` and `store.records` are the running totals. */
function makeHandler({ append, counters, store }) {
  const refuse = (res, status, req, reason) => {
    if (status === 415) {
      counters.misconfigured += 1;
      append('otel-misconfigured.ndjson', { receivedAt: Date.now(), path: req.url, contentType: req.headers['content-type'] ?? null, reason });
    }
    reply(res, status, { error: reason });
  };

  function accept(req, res, text) {
    const receivedAt = Date.now();
    append('otel-raw.ndjson', rawBodyLine(req, text, receivedAt));
    if (req.url === '/v1/logs') {
      const decoded = decodeOtlpLogs(text);
      if (!decoded.ok) {
        counters.undecodable += 1;
        return refuse(res, 400, req, decoded.reason);
      }
      counters.rejectedRecords += decoded.rejected.length;
      decoded.rejected.forEach(x => append('otel-rejected.ndjson', { receivedAt, ...x }));
      const lines = decoded.value.map(r => ({ receivedAt, ...r }));
      lines.forEach(l => append('otel.ndjson', l));
      store.records = [...store.records, ...lines];
    }
    return reply(res, 200);
  }

  return async (req, res) => {
    if (req.method !== 'POST') return refuse(res, 405, req, 'POST only');
    if (!PATHS.includes(req.url)) return refuse(res, 404, req, 'unknown path');
    if (!isJson(req.headers['content-type'])) {
      req.resume();
      return refuse(res, 415, req, 'content-type must be application/json: set OTEL_EXPORTER_OTLP_PROTOCOL=http/json');
    }
    const body = await readBody(req);
    if (!body.ok) return refuse(res, body.reason === 'too large' ? 413 : 400, req, body.reason);
    return accept(req, res, body.value.toString('utf8'));
  };
}

/** Start the receiver in `dir`. `port` 0 = any free one. Resolves once listening. */
export async function startReceiver({ dir, port = 0 }) {
  mkdirSync(dir, { recursive: true });
  const counters = { misconfigured: 0, undecodable: 0, rejectedRecords: 0 };
  const store = { records: [] };
  const append = (name, obj) => appendFileSync(path.join(dir, name), `${JSON.stringify(obj)}\n`);
  const handle = makeHandler({ append, counters, store });
  const server = http.createServer((req, res) => {
    handle(req, res).catch(e => {
      process.stderr.write(`otel-receiver: ${e.stack}\n`);
      if (!res.headersSent) reply(res, 500, { error: 'receiver failure' });
    });
  });
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(port, HOST, resolve); });
  return {
    server, port: server.address().port, dir,
    liveSum: () => liveSumOf(store.records),
    get misconfigured() { return counters.misconfigured; },
    get undecodable() { return counters.undecodable; },
    get rejectedRecords() { return counters.rejectedRecords; },
    close: () => new Promise(resolve => { server.close(() => resolve()); server.closeAllConnections(); }),
  };
}
