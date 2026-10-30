// A stub `claude` and the OTLP records it posts (E3b review m4, the v2 rehearsal; also the bare runner's end-to-end test). The stub does what the real CLI does at its edges (the init event,
// one request to the broker, an OTel record, a transcript, the result envelope) and nothing else: no model, no network but the loopback ones the box gives it, no cost. Written by the harness
// into a directory it owns, run in the real box behind the real broker and a loopback stub upstream (`stub-upstream.mjs`).
import { writeFileSync } from 'node:fs';
import path from 'node:path';

// ---- OTLP/HTTP JSON payloads (SYNTHETIC: the OTLP JSON encoding, with the attribute names of the plan's A-O1 row) --------------

const anyValue = v => {
  if (typeof v === 'string') return { stringValue: v };
  if (typeof v === 'boolean') return { boolValue: v };
  if (Number.isInteger(v)) return { intValue: String(v) };
  return { doubleValue: v };
};

/** One OTLP log record; `attrs` is a plain object (an `undefined` value omits the attribute), `name` goes in the body, ints are encoded as strings as OTLP JSON does. */
export function otlpRecord({ name, tsMs, attrs }) {
  return {
    timeUnixNano: String(BigInt(tsMs) * 1_000_000n),
    severityNumber: 9,
    body: { stringValue: `claude_code.${name}` },
    attributes: [{ key: 'event.name', value: { stringValue: name } }, ...Object.entries(attrs).filter(([, v]) => v !== undefined).map(([key, v]) => ({ key, value: anyValue(v) }))],
  };
}

export const otlpPayload = records => ({
  resourceLogs: [{
    resource: { attributes: [{ key: 'service.name', value: { stringValue: 'claude-code' } }] },
    scopeLogs: [{ scope: { name: 'com.anthropic.claude_code.events' }, logRecords: records.map(otlpRecord) }],
  }],
});

/** A complete api_request attribute set; override what a test varies. */
export const apiAttrs = (over = {}) => ({
  model: 'claude-opus-5-5', input_tokens: 10, output_tokens: 20, cache_read_tokens: 300, cache_creation_tokens: 40, cost_usd: 0.01,
  duration_ms: 1500, query_source: 'main', request_id: 'req_1', 'session.id': 'sess-1', ...over,
});

const STUB = otlpBody => `#!/usr/bin/env node
const fs = require('node:fs'); const http = require('node:http');
const argv = process.argv.slice(2);
const tools = argv[argv.indexOf('--tools') + 1].split(',').filter(t => t !== 'TodoWrite').map(t => (t === 'Agent' ? 'Task' : t));
const say = o => process.stdout.write(JSON.stringify(o) + '\\n');
const post = (url, body, headers) => new Promise((resolve, reject) => {
  const u = new URL(url);
  const req = http.request({ host: u.hostname, port: u.port, path: u.pathname, method: 'POST', headers: { 'content-type': 'application/json', ...headers } }, res => { const c = []; res.on('data', d => c.push(d)); res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(c).toString() })); });
  req.on('error', reject); req.end(body);
});
(async () => {
  say({ type: 'system', subtype: 'init', cwd: process.cwd(), session_id: 'stub-session', tools, mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default', apiKeySource: 'none', claude_code_version: '2.1.310', memory_paths: { auto: process.env.CLAUDE_CONFIG_DIR + '/projects/-p/memory/' } });
  const r = await post(process.env.ANTHROPIC_BASE_URL + '/v1/messages?beta=true', '{"model":"claude-sonnet-5-5","stream":true}', { authorization: 'Bearer ' + process.env.CLAUDE_CODE_OAUTH_TOKEN, 'anthropic-beta': 'oauth-2025-04-20' });
  await post(process.env.OTEL_EXPORTER_OTLP_ENDPOINT + '/v1/logs', ${JSON.stringify(otlpBody)}, {});
  fs.mkdirSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p', { recursive: true });
  fs.writeFileSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p/stub-session.jsonl', '{"type":"assistant"}\\n');
  say({ type: 'assistant', message: { id: 'm1', model: 'claude-sonnet-5-5', usage: { input_tokens: 2, output_tokens: 4 } } });
  // a COMPLETE envelope (usage and modelUsage, the numbers the stub upstream answers with): T1 can run on it, so the series' fail-closed rule (review MINOR-7: a completed session with an unreadable envelope stops a series whose t1 policy is stop) has nothing to stop on
  say({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.009232, duration_ms: 50, duration_api_ms: 46, num_turns: 1, result: 'status ' + r.status,
    usage: { input_tokens: 2, output_tokens: 4, cache_read_input_tokens: 0, cache_creation_input_tokens: 2297, cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 2297 } },
    modelUsage: { 'claude-sonnet-5-5': { inputTokens: 2, outputTokens: 4, cacheReadInputTokens: 0, cacheCreationInputTokens: 2297, costUSD: 0.009232 } } });
})();
`;

/** Write the stub into `dir` and return its path (executable). */
export function stubClaude(dir, name = 'claude-stub') {
  const file = path.join(dir, name);
  writeFileSync(file, STUB(JSON.stringify(otlpPayload([{ name: 'api_request', tsMs: Date.now(), attrs: apiAttrs({ request_id: 'req_stub_1' }) }]))), { mode: 0o755 });
  return file;
}
