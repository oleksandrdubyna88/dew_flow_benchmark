// The scan composition's small pure parts.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import * as scan from '../lib/push-scan.mjs';
import { isHistoryCheck, isPlacementCheck, placementEntries } from '../lib/push-scan.mjs';

const tree = new Map([
  ['tasks/in-range.md', { oid: '1'.repeat(40), mode: '100644' }],
  ['tasks/untouched.md', { oid: '2'.repeat(40), mode: '100644' }],
  ['tasks/held.md', { oid: '3'.repeat(40), mode: '100644' }],
  ['sub', { oid: '4'.repeat(40), mode: '160000' }],
]);
const TIP = 'f'.repeat(40);
const files = [{ commit: 'a'.repeat(40), path: 'tasks/in-range.md', oid: '1'.repeat(40), mode: '100644' }];

test('A_placement_entry_always_names_a_commit_the_first_of_the_range_that_touched_the_path_or_else_the_tip', () => {
  const entries = placementEntries(tree, new Set([`tasks/held.md\0${'3'.repeat(40)}`]), files, new Set(), TIP);
  assert.deepEqual(entries.map(e => [e.path, e.commit]), [['tasks/in-range.md', 'a'.repeat(40)], ['tasks/untouched.md', TIP]], 'a held pair and a submodule pointer are not entries; the untouched path falls back to the tip');
  assert.ok(entries.every(e => typeof e.commit === 'string' && e.commit.length === 40), 'no finding can lose its commit');
});

test('The_placement_checks_are_answered_by_a_function_and_the_list_cannot_be_changed_from_outside', () => {
  for (const name of ['run-base-text', 'family-names', 'attribution']) assert.equal(isPlacementCheck(name), true, name);
  for (const name of ['leakcheck', 'secrets', 'tree-shape', 'stack-folders']) assert.equal(isPlacementCheck(name), false, name);
  assert.equal(isHistoryCheck('leakcheck') && isHistoryCheck('secrets') && !isHistoryCheck('run-base-text'), true);
  assert.equal(scan.PLACEMENT_CHECKS, undefined, 'the sets themselves are private: Object.freeze on a Set does not stop .add(), so nothing mutable is exported');
  assert.equal(scan.HISTORY_CHECKS, undefined);
});
