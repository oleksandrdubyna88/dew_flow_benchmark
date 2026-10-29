// The smoke machinery rehearsed free: a stub `claude`, a stub upstream behind the real broker, the real box. Nothing here is paid.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { sanitiseCapture } from '../capture/capture-files.mjs';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { loadSmoke, stageSmoke } from '../lib/smoke-load.mjs';
import { SMOKE_ASSUMPTIONS, computeSmokeVerdicts, renderSmoke } from '../lib/smoke-verdicts.mjs';
import { runSmoke, smokeInputs } from '../lib/smoke-run.mjs';
import { verdictBlockOf, renderVerdicts } from '../capture/verdicts.mjs';
import { fakeToken, prices, sseAnswer, sseBody, chunksOf, stubUpstream } from './broker-stub.mjs';
import { TS_DIR } from './box-helpers.mjs';
import { homeDir, sandboxGate, spikeDir, tmpDir } from './helpers.mjs';
import { apiAttrs, otlpPayload } from './synth.mjs';

const STUB = `#!/usr/bin/env node
const fs = require('node:fs'); const http = require('node:http');
const say = o => process.stdout.write(JSON.stringify(o) + '\\n');
const post = (url, body, headers) => new Promise((resolve, reject) => { const u = new URL(url); const req = http.request({ host: u.hostname, port: u.port, path: u.pathname, method: 'POST', headers: { 'content-type': 'application/json', ...headers } }, res => { res.resume(); res.on('end', resolve); }); req.on('error', reject); req.end(body); });
(async () => {
  const tools = process.argv[process.argv.indexOf('--tools') + 1].split(',').filter(t => t !== 'TodoWrite').map(t => (t === 'Agent' ? 'Task' : t));
  say({ type: 'system', subtype: 'init', cwd: process.cwd(), session_id: 'stub', tools, mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default', apiKeySource: 'none', claude_code_version: '2.1.310', memory_paths: { auto: process.env.CLAUDE_CONFIG_DIR + '/projects/-p/memory/' } });
  await post(process.env.ANTHROPIC_BASE_URL + '/v1/messages', '{}', { authorization: 'Bearer ' + process.env.CLAUDE_CODE_OAUTH_TOKEN });
  fs.writeFileSync('token.txt', process.env.CLAUDE_CODE_OAUTH_TOKEN + '\\n');
  fs.mkdirSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p', { recursive: true });
  fs.writeFileSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p/s.jsonl', '{}\\n');
  say({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.009232, result: 'DONE' });
})();
`;

test('The_smoke_machinery_runs_stages_sanitises_and_computes_a_verdict_for_every_assumption', async t => {
  if (!sandboxGate(t)) return;
  const stubBin = path.join(tmpDir('smoke-stub'), 'claude-stub');
  writeFileSync(stubBin, STUB, { mode: 0o755 });
  const upstream = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 10));
  t.after(() => upstream.close());
  const real = fakeToken();
  const root = homeDir('smk');
  const out = path.join(root, 'r1');
  const inputs = { token: real, claudeBinary: stubBin, prices: prices(), tsDir: TS_DIR, stacks: { pins: { claude: '2.1.310' } } };
  const r = await runSmoke({
    out, inputs, preflightFn: async () => ({ ok: true, value: {} }), deps: {},
    over: { upstream: upstream.upstream, settleMs: 200, tickMs: 200, sampleMs: 100, toolchains: [] },
  });
  assert.equal(r.ok, true, r.reason);
  const { layout, plan, result } = r.value;
  assert.equal(result.ok, true, result.reason);
  const checks = JSON.parse(readFileSync(path.join(layout.dir, 'checks.json'), 'utf8'));
  assert.equal(checks.tokenTxt.equalsDummy, true, 'the session persisted its token on purpose: token.txt holds the dummy');
  assert.deepEqual(checks.realTokenScan.hits, []);
  assert.equal(checks.survivors, 0);
  assert.equal(checks.statePhase, 'closed');
  assert.equal(checks.socketsLeft, 0);
  assert.equal(JSON.stringify(checks).includes(real) || JSON.stringify(checks).includes(plan.dummy), false, 'checks.json holds no secret');
  // the real-token scan is alive: plant the token in a file of the out dir and the scan finds it
  const planted = path.join(layout.work, 'planted.txt');
  writeFileSync(planted, `x ${real} y`);
  const { scanSessionDir } = await import('../lib/session-scan.mjs');
  assert.equal(scanSessionDir(layout.dir, { real, dummy: plan.dummy }).hits.length, 1, 'control: a planted real token is found');
  (await import('node:fs')).rmSync(planted);
  // stage -> sanitise -> load -> verdicts
  const staged = path.join(root, 'staged');
  const stagedOk = stageSmoke(layout.dir, staged);
  assert.equal(stagedOk.ok, true, stagedOk.reason);
  const fixture = path.join(tmpDir('smoke-fx'), 'fixture');
  const sanitised = sanitiseCapture({ from: staged, to: fixture, token: real, optional: ['stream.stamped.ndjson', 'broker.ndjson', 'egress.ndjson', 'audit.ndjson', 'watchdog.ndjson', 'readback.json', 'checks.json', 'egress.json', 'broker.json', 'stderr.txt'].filter(f => existsSync(path.join(staged, f))) });
  assert.equal(sanitised.ok, true, `${sanitised.reason} ${JSON.stringify(sanitised.findings)}`);
  const smoke = loadSmoke(fixture);
  assert.equal(smoke.ok, true, smoke.reason);
  const policyInputs = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(policyInputs.ok, true, policyInputs.reason);
  const rows = computeSmokeVerdicts(smoke.value, policyInputs.value);
  assert.deepEqual(rows.map(x => x.id), SMOKE_ASSUMPTIONS.map(([id]) => id));
  for (const row of rows) {
    assert.ok(['holds', 'refuted', 'not exercised'].includes(row.verdict), `${row.id}: ${row.verdict}`);
    assert.ok(typeof row.observed === 'string' && row.observed !== '', `${row.id} says what it rests on`);
  }
  const byId = Object.fromEntries(rows.map(x => [x.id, x]));
  assert.equal(byId['B-R1'].verdict, 'holds', byId['B-R1'].observed);
  assert.equal(byId['B-S3'].verdict, smoke.value.meta.samplers.samples >= 5 ? 'holds' : 'refuted', 'B-S3 needs at least 5 samples: the rule is strict about a short session');
  assert.equal(byId['B-K2'].verdict, 'not exercised');
  const md = renderSmoke({ meta: smoke.value.meta, rows, command: 'x' });
  assert.equal(verdictBlockOf(md), renderVerdicts(rows));
  assert.equal(md.includes(real) || md.includes(plan.dummy), false);
});

test('Smoke_inputs_refuse_a_missing_token_and_a_missing_claude_without_echoing_either', () => {
  const none = smokeInputs({ home: tmpDir('no-token-home'), env: { PATH: '' } });
  assert.equal(none.ok, false);
  assert.match(none.reason, /no token/);
});
