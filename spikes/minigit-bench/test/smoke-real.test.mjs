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
// B-S3 and B-S3b are NOT here on purpose: this fixture is the 2026-10-30 smoke, taken before the link scan and the in-box probe existed, so its
// checks.json has no `links` (B-S3: 'not scanned') and its token.txt is empty (B-S3b: 'not exercised'). The re-smoke that replaces it must hold both.
const REQUIRED_HOLDS = ['B-R1', 'B-R2', 'B-N1', 'B-N2', 'B-S2', 'B-$1', 'B-O1', 'B-T1', 'B-E1', 'B-A1', 'B-A3', 'B-K1', 'B-C1'];

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
  assert.equal(byId['B-S3'].verdict, 'refuted', 'the old fixture has no link scan, and a scan that did not look is not clean');
  assert.match(byId['B-S3'].observed, /not scanned/);
  assert.equal(byId['B-S3b'].verdict, 'not exercised', 'token.txt is empty in the old fixture');
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

const verdictOf = (id, mutate) => verdicts(mutate).find(r => r.id === id);
const withChecks = over => s => ({ ...s, checks: { ...s.checks, ...over } });
const withSamplers = over => s => ({ ...s, meta: { ...s.meta, samplers: { ...s.meta.samplers, ...over } } });
const CLEAN_SCAN = { scanned: 3, hits: [], tooLarge: [], links: [] };

test('B_S3_holds_only_with_enough_samples_no_real_token_in_cmdline_or_environ_the_positive_control_and_a_clean_scan_with_no_link', () => {
  const holds = mutate => verdictOf('B-S3', mutate);
  const base = s => withChecks({ realTokenScan: CLEAN_SCAN })(withSamplers({ samples: 9, realInCmdline: 0, realInEnviron: 0, dummyInEnviron: 3 })(s));
  assert.equal(holds(base).verdict, 'holds');
  assert.match(holds(base).observed, /dummy in environ 3/);
  assert.equal(holds(s => withSamplers({ samples: 4 })(base(s))).verdict, 'refuted', 'fewer than 5 samples');
  assert.equal(holds(s => withSamplers({ realInCmdline: 1 })(base(s))).verdict, 'refuted', 'the real token in a cmdline');
  assert.equal(holds(s => withSamplers({ realInEnviron: 2 })(base(s))).verdict, 'refuted', 'the real token in an environ');
  assert.equal(holds(s => withSamplers({ dummyInEnviron: 0 })(base(s))).verdict, 'refuted', 'no positive control: a sampler that never saw the dummy proves nothing about the real token');
  assert.equal(holds(s => withChecks({ realTokenScan: { ...CLEAN_SCAN, hits: [{ source: 'x', kind: 'own-value', offset: 1 }] } })(base(s))).verdict, 'refuted', 'a real-token hit');
  assert.equal(holds(s => withChecks({ realTokenScan: { ...CLEAN_SCAN, links: [{ rel: 'box/out/audit.ndjson', kind: 'symlink', severity: 'high' }] } })(base(s))).verdict, 'refuted', 'a link anywhere in the tree');
  assert.equal(holds(s => withChecks({ realTokenScan: { scanned: 3, hits: [], tooLarge: [] } })(base(s))).verdict, 'refuted', 'a scan that did not look for links is not clean');
});

test('B_S3_does_not_read_the_in_box_token_file_at_all_because_that_half_is_B_S3b', () => {
  const clean = s => withChecks({ realTokenScan: CLEAN_SCAN })(withSamplers({ samples: 9, realInCmdline: 0, realInEnviron: 0, dummyInEnviron: 3 })(s));
  for (const tokenTxt of [{ present: false }, { present: true, equalsDummy: false, bytes: 41 }, { present: true, equalsDummy: true, bytes: 40 }]) {
    assert.equal(verdictOf('B-S3', s => withChecks({ tokenTxt })(clean(s))).verdict, 'holds', JSON.stringify(tokenTxt));
  }
});

test('B_S3b_holds_only_when_the_in_box_probe_wrote_the_dummy_and_is_not_exercised_when_it_wrote_nothing', () => {
  const b = tokenTxt => verdictOf('B-S3b', withChecks({ tokenTxt }));
  assert.equal(b({ present: true, equalsDummy: true, bytes: 70 }).verdict, 'holds', 'the dummy');
  assert.equal(b({ present: true, equalsDummy: false, bytes: 0 }).verdict, 'not exercised', 'empty: the probe read nothing');
  assert.equal(b({ present: false, equalsDummy: false }).verdict, 'not exercised', 'no file: the probe did not run');
  assert.equal(b({ present: true, equalsDummy: false, bytes: 41 }).verdict, 'refuted', 'anything else, the real token included');
  assert.equal(b({ present: true, unreadable: 'symlink', equalsDummy: false }).verdict, 'refuted', 'a link is never the dummy');
  assert.match(b({ present: true, equalsDummy: true, bytes: 70 }).observed, /dummy/);
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
