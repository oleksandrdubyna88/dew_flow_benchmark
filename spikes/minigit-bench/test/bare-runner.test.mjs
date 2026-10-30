// The CLI of the bare runner (E3b.S1): the re-exec onto the harness core, `status`, the dry runs that write and spend nothing, and the refusals.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { afterStopRefusal, main, parseWhat } from '../bare-runner.mjs';
import { ridOf, maskFor } from '../lib/bare-plan.mjs';
import { openSeries, writeRunMap } from '../lib/run-map.mjs';
import { appendLedger } from '../lib/ledger.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { promptsText, stacksText } from './prereg-fixtures.mjs';

const SEED = 'mg-0123456789abcdef';
const RUNNER = path.join(spikeDir, 'bare-runner.mjs');

function series({ ledger = [] } = {}) {
  const dir = path.join(tmpDir('rsd'), 's-20261030-ab12');
  mkdirSync(dir, { recursive: true });
  const map = openSeries({
    seriesId: 's-20261030-ab12', seedSha256: 'b'.repeat(64), harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', preregCommit: 'c'.repeat(40), pins: { claude: '2.1.310' },
    stacksSha256: 'd', pricesFile: 'prices-2026-10-29.json', pricesSha256: 'e', snapshots: { csharp: 'f', node: '1' }, snapshotDirs: { csharp: '/nowhere/cs', node: '/nowhere/nd' },
    claude: { path: '/nowhere/claude-2.1.310', sha256: 'x', version: '2.1.310' }, caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, bareBudgetUsd: 40, ceilingUsd: 500, spin: { medianMs: 20 },
  });
  writeRunMap(path.join(dir, 'run-map.json'), map);
  for (const l of ledger) appendLedger(path.join(dir, 'ledger.ndjson'), l);
  return dir;
}

function inputsDir() {
  const dir = tmpDir('inputs');
  for (const f of ['stacks.json', 'prices-2026-10-29.json', 'crosscheck-policy.json', 'quiet.json', 'package-flags.json']) cpSync(path.join(spikeDir, 'inputs', f), path.join(dir, f));
  writeFileSync(path.join(dir, 'cache-snapshots.json'), JSON.stringify({ csharp: { treeSha256: 'f'.repeat(64), parts: { nuget: 'a' }, packages: [] }, node: { treeSha256: '1'.repeat(64), parts: { npm: 'b' }, packages: [] } }));
  writeFileSync(path.join(dir, 'seed.txt'), `${SEED}\n`);
  return dir;
}
const run = async argv => { const lines = []; const code = await main(argv, l => lines.push(l)); return { code, out: lines.join('\n') }; };

test('The_runner_reexecs_onto_the_harness_core', () => {
  const dir = series();
  const env = { ...process.env };
  delete env.BENCH_RUNNER_PINNED;
  const pinned = spawnSync(process.execPath, [RUNNER, 'status', '--series', dir], { env, encoding: 'utf8' });
  assert.equal(pinned.status, 0, pinned.stderr);
  assert.match(pinned.stdout, /Cpus_allowed_list 6-7 \(re-exec marker set\)/, 'started without the marker: re-executed under taskset -c 6,7, and not a second time');
  const control = spawnSync(process.execPath, [RUNNER, 'status', '--series', dir], { env: { ...env, BENCH_RUNNER_PINNED: '1' }, encoding: 'utf8' });
  assert.doesNotMatch(control.stdout, /Cpus_allowed_list 6-7/, 'control: with the marker set there is no re-exec and the process keeps the full list');
  assert.match(control.stdout, /re-exec marker set/);
});

test('Status_lists_the_sessions_and_the_spend_of_both_limits', async () => {
  const dir = series({ ledger: [{ kind: 'prior', usd: 0.43 }, { kind: 'aa', usd: 3 }] });
  const r = await run(['status', '--series', dir]);
  assert.equal(r.code, 0);
  assert.match(r.out, /ceiling-side \$3\.4300 of \$500, bare \$3\.0000 of \$40/);
  assert.match(r.out, /series s-20261030-ab12/);
});

test('Run_dry_run_prints_the_phase_and_spends_nothing', async () => {
  const dir = series({ ledger: [{ kind: 'prior', usd: 0.43 }] });
  const before = readdirSync(dir).sort();
  const r = await run(['run', '--series', dir, '--what', 'pair:1', '--dry-run', '--inputs-dir', inputsDir()]);
  assert.equal(r.code, 0, r.out);
  for (const stack of ['csharp', 'node']) {
    assert.ok(r.out.includes(`rid ${ridOf({ seed: SEED, pair: 1, stack, phase: 'v1', attempt: 1 })}, mask ${maskFor({ seed: SEED, pair: 1, stack })}`), `${stack}'s rid and mask are printed`);
  }
  assert.match(r.out, /cap \$6, wall 60 min, stall 45 min/);
  assert.match(r.out, /spend so far \$0\.43 \+ caps \$13\.2 stays within the ceiling \$500/);
  assert.match(r.out, /the bare budget/);
  assert.match(r.out, /nothing was claimed, started or spent/);
  assert.deepEqual(readdirSync(dir).sort(), before, 'no file was written');
  assert.equal(existsSync(path.join(dir, 's')), false, 'no layout was claimed');
});

test('A_dry_run_of_the_fixed_lists_names_the_sessions_and_the_gate', async () => {
  const dir = series();
  const aa = await run(['run', '--series', dir, '--what', 'aa', '--dry-run', '--inputs-dir', inputsDir()]);
  assert.equal(aa.code, 0, aa.out);
  assert.match(aa.out, /aa: would launch/);
  assert.match(aa.out, /caps \$13\.2/);
  const pilot = await run(['run', '--series', dir, '--what', 'pilot-node', '--dry-run', '--inputs-dir', inputsDir()]);
  assert.match(pilot.out, /caps \$6\.6/);
});

test('The_refusals_name_what_to_do', async () => {
  assert.equal((await run(['bogus'])).code, 4);
  assert.match((await run([])).out, /the commands are/);
  assert.equal((await run(['run', '--what', 'pair:1'])).code, 4, 'no --series');
  const dir = series();
  const bad = await run(['run', '--series', dir, '--what', 'pair:9', '--dry-run']);
  assert.equal(bad.code, 4);
  assert.match(bad.out, /--what aa \| pilot-node \| rehearse-v2 \| pair/);
  assert.equal(parseWhat('pair:3').pair, 3);
  assert.equal(parseWhat('pair:4'), null);
  assert.deepEqual(parseWhat('aa'), { kind: 'aa' });
});

test('A_series_stop_cannot_be_passed_with_after_stop_and_a_halt_needs_a_reason', async () => {
  for (const rule of ['stop: security', 'stop: auth', 'stop: alias drift']) assert.match(afterStopRefusal({ 'after-stop': rule, reason: 'x' }), /series stop cannot be passed/, rule);
  assert.match(afterStopRefusal({ 'after-stop': 'void: rate limit' }), /needs --reason/);
  assert.equal(afterStopRefusal({ 'after-stop': 'void: rate limit', reason: 'the window reset at 05:00' }), null);
  assert.equal(afterStopRefusal({}), null);
  const r = await run(['resume', '--series', series(), '--after-stop', 'stop: auth', '--reason', 'it was a typo']);
  assert.equal(r.code, 4);
  assert.match(r.out, /series stop cannot be passed/);
});

test('The_seed_commands_write_the_hash_to_inputs_and_the_seed_outside_git', async () => {
  const inputs = tmpDir('inputs');
  writeFileSync(path.join(inputs, 'stacks.json'), stacksText('10.0.114'));
  writeFileSync(path.join(inputs, 'prompts-bare.json'), promptsText('10.0.114'));
  const seedFile = path.join(tmpDir('seeds'), 'bare.seed');
  const c = await run(['seed-commit', '--seed-file', seedFile, '--inputs-dir', inputs]);
  assert.equal(c.code, 0, c.out);
  assert.equal(existsSync(path.join(inputs, 'seed.sha256')), true);
  assert.equal(existsSync(path.join(inputs, 'seed.txt')), false);
  const r = await run(['seed-reveal', '--seed-file', seedFile, '--inputs-dir', inputs]);
  assert.equal(r.code, 0, r.out);
  assert.equal(existsSync(path.join(inputs, 'seed.txt')), true);
});
