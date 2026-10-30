// A stub `claude` for the bare runner's end-to-end test: a script that does what the real CLI does at its edges (the init event, one request to the broker, an OTel record,
// a transcript, the result envelope) and nothing else. Its own copy: the launch test's stub is local to that file, which another branch is rewriting.
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { apiAttrs, otlpPayload } from './synth.mjs';

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
  say({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.009232, result: 'status ' + r.status });
})();
`;

/** Write the stub into `dir` and return its path (executable). */
export function stubClaude(dir, name = 'claude-stub') {
  const file = path.join(dir, name);
  writeFileSync(file, STUB(JSON.stringify(otlpPayload([{ name: 'api_request', tsMs: Date.now(), attrs: apiAttrs({ request_id: 'req_stub_1' }) }]))), { mode: 0o755 });
  return file;
}
