// The fakes of the smoke tests: the stub `claude` (a script that speaks the CLI's stream-json init and result events, calls the broker once with the credential it was given and writes the token file on purpose)
// and a home that holds a fake token the way v1's reader wants it. `version` is what the stub's init event says (`claude_code_version`): the one thing a test of a pinned CLI changes.
import { chmodSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';
import { runSmoke, smokeInputs } from '../lib/smoke-run.mjs';
import { fakeToken, prices, sseAnswer, sseBody, chunksOf, stubUpstream } from './broker-stub.mjs';
import { DOTNET_PIN, TS_DIR } from './box-helpers.mjs';
import { tmpDir } from './helpers.mjs';

export const smokeStub = (version = '2.1.310') => `#!/usr/bin/env node
const fs = require('node:fs'); const http = require('node:http');
const say = o => process.stdout.write(JSON.stringify(o) + '\\n');
const post = (url, body, headers) => new Promise((resolve, reject) => { const u = new URL(url); const req = http.request({ host: u.hostname, port: u.port, path: u.pathname, method: 'POST', headers: { 'content-type': 'application/json', ...headers } }, res => { res.resume(); res.on('end', resolve); }); req.on('error', reject); req.end(body); });
(async () => {
  const tools = process.argv[process.argv.indexOf('--tools') + 1].split(',').filter(t => t !== 'TodoWrite').map(t => (t === 'Agent' ? 'Task' : t));
  say({ type: 'system', subtype: 'init', cwd: process.cwd(), session_id: 'stub', tools, mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default', apiKeySource: 'none', claude_code_version: '${version}', memory_paths: { auto: process.env.CLAUDE_CONFIG_DIR + '/projects/-p/memory/' } });
  await post(process.env.ANTHROPIC_BASE_URL + '/v1/messages', '{}', { authorization: 'Bearer ' + process.env.CLAUDE_CODE_OAUTH_TOKEN });
  fs.writeFileSync('token.txt', 'CLAUDE_CODE_OAUTH_TOKEN=' + process.env.CLAUDE_CODE_OAUTH_TOKEN + '\\n');
  await new Promise(r => setTimeout(r, 1500)); // long enough for the sampler to take its five samples
  fs.mkdirSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p', { recursive: true });
  fs.writeFileSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p/s.jsonl', '{}\\n');
  say({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.009232, result: 'DONE' });
})();
`;

/** A home that holds a token file the way v1's reader wants it (mode 600 in a directory of mode 700): the fake token a test gives, never a real one. */
export function homeWithToken(token, prefix = 'smoke-home') {
  const home = tmpDir(prefix);
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  mkdirSync(path.dirname(file), { recursive: true });
  chmodSync(path.dirname(file), 0o700);
  writeFileSync(file, `${token}\n`, { mode: 0o600 });
  return home;
}

/** An executable file named `name` in a folder of its own: the stub's source, or an empty script when the test never runs it. */
export function binaryNamed(name, source = '#!/bin/sh\n') {
  const file = path.join(tmpDir('smoke-bin'), name);
  writeFileSync(file, source, { mode: 0o755 });
  return file;
}

/**
 * One whole stub smoke session in the real box (the caller has passed `sandboxGate`): a stub `claude` that reports `version`, a stub upstream behind the real broker, `smokeInputs` given that binary and that version, `runSmoke` with a preflight that
 * accepts. -> `{ made, r, home, token, binary }`: `r.value.layout.dir` is the raw capture, the folder the sanitise reads. The upstream is closed before it returns.
 */
export async function stubSmokeSession({ version }) {
  const token = fakeToken();
  const home = homeWithToken(token);
  const binary = binaryNamed(`claude-${version}`, smokeStub(version));
  const upstream = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 10));
  try {
    const made = smokeInputs({ home, env: { PATH: '' }, claude: binary, cliPin: version });
    const inputs = { ...made.value, prices: prices(), tsDir: TS_DIR, stacks: { ...made.value.stacks, pins: { ...made.value.stacks.pins, dotnetSdk: DOTNET_PIN } } };
    const r = await runSmoke({
      out: path.join(tmpDir('smoke-run'), 'r1'), inputs, home, preflightFn: async () => ({ ok: true, value: {} }), deps: {},
      over: { upstream: upstream.upstream, settleMs: 200, tickMs: 200, sampleMs: 50, toolchains: [] },
    });
    return { made, r, home, token, binary };
  } finally {
    upstream.close();
  }
}
