// Growth and the disk rule (section 2.5), and the bootstrap's own guard constants. Pure but for the growth file.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { GIB, MEASURE_FREE_FLOOR_BYTES, MEASURE_WRITE_CEILING_BYTES, diskRuleVerdict, measureGuard, pairWorstCaseBytes } from '../lib/growth.mjs';

test('The_pairs_worst_case_is_two_sessions_trees_plus_twice_the_measured_retained_size_of_ten_passes_with_every_re_run', () => {
  const r = pairWorstCaseBytes({ sessionTreeBytes: 300, retainedTenPassesWorstBytes: 70 });
  assert.equal(r.ok, true);
  assert.equal(r.value, 2 * 300 + 2 * 70);
  for (const [bad, field] of [[{ retainedTenPassesWorstBytes: 70 }, 'sessionTreeBytes'], [{ sessionTreeBytes: 300 }, 'retainedTenPassesWorstBytes'], [{ sessionTreeBytes: 0, retainedTenPassesWorstBytes: 70 }, 'sessionTreeBytes'], [{ sessionTreeBytes: 300, retainedTenPassesWorstBytes: 'x' }, 'retainedTenPassesWorstBytes']]) {
    const refused = pairWorstCaseBytes(bad);
    assert.equal(refused.ok, false, field);
    assert.match(refused.reason, new RegExp(field), 'the refusal names the field and is never a default of 0');
  }
});

test('The_disk_rule_halts_below_twice_the_pairs_measured_worst_case_and_goes_at_or_above_it', () => {
  const worst = 1000;
  const halted = diskRuleVerdict({ freeBytes: 2 * worst - 1, pairWorstCaseBytes: worst });
  assert.equal(halted.go, false);
  assert.ok(halted.reason.includes(String(2 * worst - 1)) && halted.reason.includes(String(2 * worst)), 'the sentence names both numbers');
  assert.deepEqual(diskRuleVerdict({ freeBytes: 2 * worst, pairWorstCaseBytes: worst }), { go: true });
  assert.deepEqual(diskRuleVerdict({ freeBytes: 2 * worst + 1, pairWorstCaseBytes: worst }), { go: true });
  assert.equal(diskRuleVerdict({ freeBytes: 5000 }).go, false, 'a missing worst case is a refusal, never a free pass');
  assert.match(diskRuleVerdict({ freeBytes: 5000 }).reason, /pairWorstCaseBytes/);
});

test('The_bootstrap_guard_has_the_registered_floor_and_ceiling_and_refuses_one_under_the_floor_and_one_over_the_ceiling', () => {
  assert.equal(MEASURE_FREE_FLOOR_BYTES, 60 * GIB);
  assert.equal(MEASURE_WRITE_CEILING_BYTES, 20 * GIB);
  assert.equal(measureGuard({ freeBytes: MEASURE_FREE_FLOOR_BYTES - 1, folderBytes: 0 }).go, false);
  assert.deepEqual(measureGuard({ freeBytes: MEASURE_FREE_FLOOR_BYTES, folderBytes: MEASURE_WRITE_CEILING_BYTES }), { go: true });
  const over = measureGuard({ freeBytes: MEASURE_FREE_FLOOR_BYTES, folderBytes: MEASURE_WRITE_CEILING_BYTES + 1 });
  assert.equal(over.go, false);
  assert.ok(over.reason.includes(String(MEASURE_WRITE_CEILING_BYTES + 1)) && over.reason.includes(String(MEASURE_WRITE_CEILING_BYTES)));
});
