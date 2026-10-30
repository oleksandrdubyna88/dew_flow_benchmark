import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { renderVerdicts, verdictBlockOf } from '../capture/verdicts.mjs';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';
import { readSecretFile } from '../lib/secrets.mjs';
import { scanSessionDir } from '../lib/session-scan.mjs';
import { loadSmoke } from '../lib/smoke-load.mjs';
import { SMOKE_ASSUMPTIONS, computeSmokeVerdicts } from '../lib/smoke-verdicts.mjs';
import { hostLacks, spikeDir } from './helpers.mjs';

const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
const REQUIRED_HOLDS = ['B-R1', 'B-R2', 'B-N1', 'B-N2', 'B-S2', 'B-S3', 'B-$1', 'B-O1', 'B-T1', 'B-E1', 'B-A1', 'B-A3', 'B-K1', 'B-C1'];

function verdicts(mutate = s => s) {
  const smoke = loadSmoke(FIXTURE);
  assert.equal(smoke.ok, true, smoke.reason);
  const inputs = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(inputs.ok, true, inputs.reason);
  return computeSmokeVerdicts(mutate(smoke.value), inputs.value);
}

test('The_real_bare_smoke_holds', () => {
  const rows = verdicts();
  assert.deepEqual(rows.map(r => r.id), SMOKE_ASSUMPTIONS.map(([id]) => id), 'none blank, none missing');
  const byId = Object.fromEntries(rows.map(r => [r.id, r]));
  for (const id of REQUIRED_HOLDS) assert.equal(byId[id].verdict, 'holds', `${id}: ${byId[id].observed}`);
  assert.equal(byId['B-K2'].verdict, 'not exercised', 'the smoke ended by itself');
  for (const id of ['B-A2', 'B-H1']) assert.ok(['holds', 'refuted'].includes(byId[id].verdict), `${id} is recorded either way: ${byId[id].verdict}`);
  const md = readFileSync(path.join(FIXTURE, 'SMOKE.md'), 'utf8');
  assert.equal(verdictBlockOf(md), renderVerdicts(rows), 'SMOKE.md is the computed table, byte for byte');
});

test('The_smoke_rests_on_the_numbers_the_spec_asks_for', () => {
  const smoke = loadSmoke(FIXTURE).value;
  assert.ok(smoke.meta.envelopeCostUsd <= 0.2, `cost ${smoke.meta.envelopeCostUsd} at most $0.20`);
  assert.equal(Math.abs(smoke.meta.broker.spentUsd - smoke.meta.envelopeCostUsd) <= 0.000001, true, 'B = E');
  assert.ok(smoke.meta.samplers.samples >= 5);
  assert.deepEqual([smoke.meta.samplers.realInCmdline, smoke.meta.samplers.realInEnviron], [0, 0]);
  assert.equal(smoke.meta.outcome.label, 'ok');
  const t = smoke.stamped.map(s => s.t);
  assert.ok(t.every((x, i) => i === 0 || x >= t[i - 1]), 'stamps increase');
});

test('A_token_file_with_anything_but_the_dummy_or_nothing_refutes_B_S3', () => {
  const withTokenTxt = tokenTxt => verdicts(s => ({ ...s, checks: { ...s.checks, tokenTxt } })).find(r => r.id === 'B-S3');
  assert.equal(withTokenTxt({ present: true, equalsDummy: true, bytes: 40 }).verdict, 'holds', 'the dummy');
  assert.equal(withTokenTxt({ present: true, equalsDummy: false, bytes: 0 }).verdict, 'holds', 'empty: the CLI\'s Bash tool scrubbed the variable');
  assert.equal(withTokenTxt({ present: true, equalsDummy: false, bytes: 41 }).verdict, 'refuted', 'anything else');
  assert.equal(withTokenTxt({ present: false }).verdict, 'refuted', 'no file at all: the probe did not run');
  const fewSamples = verdicts(s => ({ ...s, meta: { ...s.meta, samplers: { ...s.meta.samplers, samples: 4 } } })).find(r => r.id === 'B-S3');
  assert.equal(fewSamples.verdict, 'refuted', 'fewer than 5 samples');
  const hit = verdicts(s => ({ ...s, checks: { ...s.checks, realTokenScan: { scanned: 3, hits: [{ source: 'x', kind: 'own-value', offset: 1 }] } } })).find(r => r.id === 'B-S3');
  assert.equal(hit.verdict, 'refuted', 'a real-token hit');
});

test('The_committed_smoke_fixture_holds_neither_the_real_token_nor_the_session_dummy', t => {
  const token = readSecretFile({ home: os.homedir(), parts: TOKEN_FILE_PARTS, label: 'token' });
  if (!token.ok) return hostLacks(t, `the operator's token file is unusable on this host (${token.reason}): the scan for it cannot run`);
  const smoke = loadSmoke(FIXTURE).value;
  const dummyHash = smoke.checks.tokenTxt.dummySha256;
  assert.match(dummyHash, /^[0-9a-f]{64}$/);
  const scan = scanSessionDir(FIXTURE, { real: token.value });
  assert.deepEqual(scan.hits, [], 'no file of the fixture holds the real token or any credential shape');
  assert.ok(scan.scanned >= 10, `${scan.scanned} files scanned`);
  assert.equal(existsSync(path.join(FIXTURE, 'transcripts')), true);
});
