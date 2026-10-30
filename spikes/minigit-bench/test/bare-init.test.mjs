// `init` (E3b.S1 step 1): everything the series is pinned to, and a dry run that writes nothing.
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { applyInit, describePlan, initPlan } from '../lib/bare-init.mjs';
import { treeHash } from '../lib/cache-snapshot.mjs';
import { readLedger } from '../lib/ledger.mjs';
import { readRunMap } from '../lib/run-map.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const put = (p, t) => { mkdirSync(path.dirname(p), { recursive: true }); writeFileSync(p, t); };

function setup() {
  const inputsDir = tmpDir('inputs');
  for (const f of ['stacks.json', 'prices-2026-10-29.json', 'spend-prior.json', 'quiet.json', 'package-flags.json']) cpSync(path.join(spikeDir, 'inputs', f), path.join(inputsDir, f));
  writeFileSync(path.join(inputsDir, 'seed.sha256'), `${'b'.repeat(64)}\n`);
  const toolchainsDir = tmpDir('toolchains');
  const registered = {};
  for (const [stack, part] of [['csharp', 'nuget'], ['node', 'npm']]) {
    const stage = tmpDir(`stage-${stack}`);
    put(path.join(stage, part, 'f'), stack);
    const hash = treeHash(stage);
    cpSync(stage, path.join(toolchainsDir, `cache-${stack}-${hash.slice(0, 12)}`), { recursive: true });
    registered[stack] = { treeSha256: hash, parts: { [part]: treeHash(path.join(stage, part)) }, sizeBytes: 1 };
  }
  writeFileSync(path.join(inputsDir, 'cache-snapshots.json'), JSON.stringify(registered));
  const versionsDir = tmpDir('versions');
  put(path.join(versionsDir, '2.1.310'), '#!/bin/sh\necho fake claude\n');
  const seriesRoot = tmpDir('rsd');
  return { inputsDir, toolchainsDir, versionsDir, seriesRoot, seriesId: 's-20261030-ab12', facts: { preregTag: 'prereg/minigit-bare-20261030', preregCommit: 'c'.repeat(40), harnessIdentity: 'a'.repeat(64) } };
}

test('Init_pins_the_cli_copy_and_dry_run_writes_nothing', () => {
  const s = setup();
  const planned = initPlan(s);
  assert.equal(planned.ok, true, planned.reason);
  const lines = describePlan(planned.value).join('\n');
  assert.match(lines, /pinned CLI 2\.1\.310/);
  const priorCount = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'spend-prior.json'), 'utf8')).length; // the registered list, not a number typed here
  assert.match(lines, new RegExp(`ledger seeded with ${priorCount} prior line`));
  assert.deepEqual(readdirSync(s.seriesRoot), [], 'the dry run (initPlan + describePlan) writes no file');
  assert.equal(existsSync(path.join(s.toolchainsDir, 'claude-2.1.310')), false);

  const done = applyInit({ plan: planned.value, spin: { medianMs: 20, spreadMs: 1 } });
  assert.equal(done.ok, true, done.reason);
  const copy = path.join(s.toolchainsDir, 'claude-2.1.310');
  assert.equal(statSync(copy).mode & 0o777, 0o555, 'the pinned copy is read-only and executable');
  assert.equal(readFileSync(copy, 'utf8'), readFileSync(path.join(s.versionsDir, '2.1.310'), 'utf8'));
  const map = readRunMap(path.join(s.seriesRoot, s.seriesId, 'run-map.json')).value;
  assert.deepEqual([map.seriesId, map.seedSha256, map.pins.claude, map.spin.medianMs, map.stopped], ['s-20261030-ab12', 'b'.repeat(64), '2.1.310', 20, null]);
  assert.equal(map.claude.sha256.length, 64);
  for (const [field, file] of [['quietSha256', 'quiet.json'], ['packageFlagsSha256', 'package-flags.json']]) assert.equal(map[field], createHash('sha256').update(readFileSync(path.join(s.inputsDir, file))).digest('hex'), `${field} is pinned at init: the thresholds that applied are named by the run-map`);
  assert.deepEqual(Object.keys(map.snapshots).sort(), ['csharp', 'node']);
  assert.deepEqual([map.caps.capUsd, map.bareBudgetUsd, map.ceilingUsd], [6, 40, 500]);
  const ledger = readLedger(path.join(s.seriesRoot, s.seriesId, 'ledger.ndjson'));
  assert.equal(ledger.length, priorCount);
  assert.ok(ledger.some(l => l.approx === true), 'an approximate prior figure stays marked in the ledger');
  assert.ok(ledger.every(l => l.kind === 'prior'));
  for (const d of ['s', 'pairs', 'canaries']) assert.ok(existsSync(path.join(s.seriesRoot, s.seriesId, d)));
  assert.equal(applyInit({ plan: planned.value, spin: null }).ok, false, 'a series is initialised once');
});

test('Init_refuses_what_is_missing_or_does_not_match_naming_it', () => {
  const s = setup();
  assert.match(initPlan({ ...s, seriesId: 'series-1' }).reason, /s-yyyymmdd/);
  assert.match(initPlan({ ...s, versionsDir: tmpDir('empty') }).reason, /auto-update/);
  writeFileSync(path.join(s.inputsDir, 'cache-snapshots.json'), JSON.stringify({ csharp: { treeSha256: 'd'.repeat(64), parts: {} }, node: { treeSha256: 'e'.repeat(64), parts: {} } }));
  assert.match(initPlan(s).reason, /snapshot directory/);
  const t = setup();
  writeFileSync(path.join(t.toolchainsDir, readdirSync(t.toolchainsDir).find(n => n.startsWith('cache-node-')), 'npm', 'f'), 'tampered');
  assert.match(initPlan(t).reason, /no longer hashes/);
  const u = setup();
  cpSync(path.join(u.versionsDir, '2.1.310'), path.join(u.toolchainsDir, 'claude-2.1.310'));
  writeFileSync(path.join(u.toolchainsDir, 'claude-2.1.310'), 'not the pinned cli');
  const planned = initPlan(u);
  assert.match(applyInit({ plan: planned.value, spin: null }).reason, /not the pinned CLI/);
  const v = setup();
  assert.match(initPlan({ ...v, inputsDir: tmpDir('bare-inputs') }).reason, /stacks\.json does not exist/, 'an inputs dir with no stacks.json is a refusal, never a default');
});
