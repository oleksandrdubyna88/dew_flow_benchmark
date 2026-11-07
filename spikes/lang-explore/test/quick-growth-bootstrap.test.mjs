// The growth record and the disk guard before the first measurement (section 2.11): `growth.json` is validated on write and on load, an ordinary scoring run refuses to start without it, and only the bootstrap goes without.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { GIB, MEASURE_FREE_FLOOR_BYTES, MEASURE_WRITE_CEILING_BYTES, growthProblem, loadGrowth, writeGrowth } from '../lib/growth.mjs';
import { dirBytes, freeBytesOf, growthFrom, runMeasurement } from '../lib/measure.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { harness } from './quick-passes-harness.mjs';

const GOOD = Object.freeze({
  measuredOn: '2026-11-07', host: 'invented-host', sessionTreeBytes: 300_000_000, retainedTenPassesBytes: 20_000_000, retainedTenPassesWorstBytes: 60_000_000,
  tenPassesWallMs: Object.freeze({ defective: 400_000, 'reference-csharp': 300_000, 'reference-node': 250_000 }), mostAttempts: 30, pairWorstCaseBytes: 2 * 300_000_000 + 2 * 60_000_000,
});
const fileIn = () => path.join(tmpDir('quick-growth'), 'growth.json');

test('growth_json_is_validated_on_write_every_size_a_positive_whole_number_the_attempt_count_at_most_thirty_and_pairWorstCaseBytes_equal_to_the_formula', () => {
  const file = fileIn();
  const bad = [
    [{ ...GOOD, sessionTreeBytes: 0 }, 'sessionTreeBytes'], [{ ...GOOD, retainedTenPassesBytes: 1.5 }, 'retainedTenPassesBytes'], [{ ...GOOD, retainedTenPassesWorstBytes: undefined }, 'retainedTenPassesWorstBytes'],
    [{ ...GOOD, tenPassesWallMs: { ...GOOD.tenPassesWallMs, defective: undefined } }, 'defective'], [{ ...GOOD, tenPassesWallMs: {} }, 'tenPassesWallMs'],
    [{ ...GOOD, mostAttempts: 31 }, 'mostAttempts'], [{ ...GOOD, pairWorstCaseBytes: GOOD.pairWorstCaseBytes + 1 }, 'pairWorstCaseBytes'], [{ ...GOOD, host: '' }, 'host'],
  ];
  for (const [record, field] of bad) {
    const r = writeGrowth({ file, record });
    assert.equal(r.ok, false, field);
    assert.match(r.reason, new RegExp(field));
    assert.equal(existsSync(file), false, 'nothing is written for a refused result');
  }
  assert.equal(growthProblem(GOOD), null);
  const written = writeGrowth({ file, record: GOOD });
  assert.equal(written.ok, true, written.reason);
  assert.deepEqual(loadGrowth({ file }), { ok: true, value: GOOD });
  assert.deepEqual(readdirSync(path.dirname(file)), ['growth.json'], 'the atomic write leaves no temp file');
});

test('A_growth_file_that_is_missing_or_invalid_is_a_refusal_naming_the_file_and_the_validators_sentence', () => {
  const file = fileIn();
  const missing = loadGrowth({ file });
  assert.equal(missing.ok, false);
  assert.ok(missing.reason.includes(file));
  writeFileSync(file, JSON.stringify({ ...GOOD, mostAttempts: 99 }));
  const invalid = loadGrowth({ file });
  assert.equal(invalid.ok, false);
  assert.ok(invalid.reason.includes(file) && invalid.reason.includes('mostAttempts'));
  writeFileSync(file, 'half a file');
  assert.match(loadGrowth({ file }).reason, new RegExp(file.replaceAll('.', '\\.')));
});

test('An_ordinary_scoring_run_refuses_to_start_without_a_valid_growth_json_and_only_the_bootstrap_flag_goes_without_it', async () => {
  const file = fileIn();
  const loaded = () => loadGrowth({ file });
  const missing = harness({ a: { growth: { loaded } } });
  const first = await scorePasses(missing.a);
  assert.equal(first.kind, 'environment');
  assert.ok(first.reason.includes(file));
  assert.equal(missing.calls.build.length, 0, 'nothing is built');
  writeFileSync(file, JSON.stringify({ ...GOOD, mostAttempts: 99 }));
  const second = await scorePasses(missing.a);
  assert.equal(second.kind, 'environment');
  assert.ok(second.reason.includes('mostAttempts'));
  assert.equal(missing.calls.build.length, 0);
  const before = readFileSync(file, 'utf8');
  const boot = await scorePasses({ ...missing.a, bootstrap: true });
  missing.release();
  assert.equal(boot.kind, 'scored', boot.reason);
  assert.equal(readFileSync(file, 'utf8'), before, 'the driver writes nothing to growth.json itself');
});

test('The_measurement_stops_resumably_after_the_attempt_that_crossed_the_write_ceiling_records_a_ceiling_event_and_leaves_every_attempt_file', async () => {
  const h = harness();
  let attempts = 0;
  const afterAttempt = () => { attempts += 1; return attempts >= 2 ? 'the measurement folder holds 20971521 bytes, past its ceiling of 20971520 bytes' : null; };
  const r = await scorePasses({ ...h.a, bootstrap: true, afterAttempt });
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /past its ceiling/);
  assert.equal(readdirSync(path.join(h.work, 'passes')).length, 2, 'every attempt file is kept and no third is started');
  assert.ok(readdirSync(path.join(h.work, 'events')).some(f => f.endsWith('-ceiling.json')));
});

// ---- the bootstrap measurement as a whole -----------------------------------------------------------------------------------------------------------

/** The `score` closure of a measurement over the fake driver: one subject, its own `score/` folder, `severe` making the first two attempts of every pass environment ones. */
function scoreOver(h, calls) {
  return async (subject, { work, afterAttempt, severe }) => {
    calls.push([subject.name, severe]);
    const inner = h.a.runPass;
    const runPass = severe ? async spec => (spec.attempt < 3 ? { kind: 'environment', reason: 'the first two attempts of every pass are made environment ones, through the same code path' } : inner(spec)) : inner;
    return scorePasses({ ...h.a, work, runPass, bootstrap: true, afterAttempt });
  };
}
const SUBJECTS = [{ name: 'defective', stack: 'csharp' }, { name: 'accepted-csharp', stack: 'csharp' }, { name: 'accepted-node', stack: 'node' }];

test('The_measurement_refuses_to_start_below_the_free_disk_floor_and_copies_nothing', async () => {
  const folder = path.join(tmpDir('quick-measure'), 'measure');
  const calls = [];
  const r = await runMeasurement({ folder, subjects: SUBJECTS, score: async () => { calls.push(1); }, io: { freeBytes: () => MEASURE_FREE_FLOOR_BYTES - 1, folderBytes: () => 0, say: () => {} } });
  assert.equal(r.exit, 4);
  assert.ok(r.message.includes(String(MEASURE_FREE_FLOOR_BYTES - 1)) && r.message.includes(String(MEASURE_FREE_FLOOR_BYTES)), 'both numbers are named');
  assert.deepEqual(calls, []);
  assert.equal(existsSync(folder), false, 'not even the folder is made');
});

test('The_measurement_stops_resumably_when_its_folder_passes_the_write_ceiling_and_writes_no_growth_json', async () => {
  const h = harness();
  const growthFile = fileIn();
  let reads = 0;
  const io = { freeBytes: () => MEASURE_FREE_FLOOR_BYTES + GIB, folderBytes: () => { reads += 1; return reads >= 2 ? MEASURE_WRITE_CEILING_BYTES + 1 : 1; }, say: () => {}, growthFile };
  const r = await runMeasurement({ folder: path.join(tmpDir('quick-measure'), 'measure'), subjects: SUBJECTS, score: scoreOver(h, []), io });
  h.release();
  assert.equal(r.exit, 3);
  assert.match(r.message, /past its ceiling/);
  assert.equal(existsSync(growthFile), false, 'growth.json is the last step and is not written');
});

test('A_whole_measurement_runs_every_subject_then_the_worst_case_and_writes_growth_json_last_that_reads_back_equal', async () => {
  const h = harness();
  const growthFile = fileIn();
  const calls = [];
  const folder = path.join(tmpDir('quick-measure'), 'measure');
  const io = { freeBytes: () => MEASURE_FREE_FLOOR_BYTES + 5 * GIB, folderBytes: () => dirBytes(folder), say: () => {}, growthFile, now: () => new Date('2026-11-07T12:00:00.000Z') };
  const r = await runMeasurement({ folder, subjects: SUBJECTS, score: scoreOver(h, calls), io });
  h.release();
  assert.equal(r.exit, 0, r.message);
  assert.deepEqual(calls, [['defective', false], ['accepted-csharp', false], ['accepted-node', false], ['defective', true]]);
  const growth = loadGrowth({ file: growthFile }).value;
  assert.equal(growth.measuredOn, '2026-11-07');
  assert.deepEqual(Object.keys(growth.tenPassesWallMs), ['defective', 'accepted-csharp', 'accepted-node']);
  assert.ok(Object.values(growth.tenPassesWallMs).every(ms => ms === 10 * 1500), 'ten attempts of 1500 ms each (the fake clock)');
  assert.equal(growth.mostAttempts, 30);
  assert.equal(growth.pairWorstCaseBytes, 2 * growth.sessionTreeBytes + 2 * growth.retainedTenPassesWorstBytes);
  assert.ok(growth.retainedTenPassesWorstBytes >= growth.retainedTenPassesBytes);
  assert.equal(readdirSync(path.join(folder, 'defective-worst', 'score', 'passes')).length, 30, 'the worst case really made thirty attempts');
  assert.ok(existsSync(path.join(folder, 'defective', 'result.json')) && existsSync(path.join(folder, 'defective-worst', 'result.json')), 'each subject keeps its numbers in the private folder');
  assert.equal(JSON.parse(readFileSync(path.join(folder, 'defective', 'result.json'), 'utf8')).verdict, 'accepted');
});

test('The_free_bytes_of_a_folder_are_the_available_blocks_times_the_block_size', () => {
  assert.equal(freeBytesOf('/anywhere', () => ({ bavail: 7n, bsize: 4096n })), 28672);
  assert.ok(freeBytesOf(tmpDir('quick-free')) > 0);
});

test('The_growth_record_takes_the_largest_tree_and_retained_size_of_any_subject_and_a_worst_case_never_below_the_ordinary_figure', () => {
  const results = [{ name: 'a', treeBytes: 300, retainedBytes: 70, tenPassesWallMs: 5000 }, { name: 'b', treeBytes: 900, retainedBytes: 40, tenPassesWallMs: 7000 }];
  const g = growthFrom({ results, worst: { retainedBytes: 50 }, host: 'invented host', measuredOn: '2026-11-07' });
  assert.deepEqual([g.sessionTreeBytes, g.retainedTenPassesBytes, g.retainedTenPassesWorstBytes], [900, 70, 70], 'a worst case measured under the ordinary is lifted to it');
  assert.equal(g.pairWorstCaseBytes, 2 * 900 + 2 * 70);
  assert.deepEqual(g.tenPassesWallMs, { a: 5000, b: 7000 });
  assert.equal(growthProblem(g), null);
  assert.equal(growthFrom({ results, worst: { retainedBytes: 500 }, host: 'h', measuredOn: '2026-11-07' }).retainedTenPassesWorstBytes, 500);
});
