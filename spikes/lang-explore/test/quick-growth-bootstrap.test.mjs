// The growth record and the disk guard before the first measurement (section 2.11): `growth.json` is validated on write and on load, an ordinary scoring run refuses to start without it, and only the bootstrap goes without.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { growthProblem, loadGrowth, writeGrowth } from '../lib/growth.mjs';

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
