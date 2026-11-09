// The record of the FORMAT the checker may read (`inputs/specs.d/format.json`) and which entry answers for a task: the entry whose `tasks` list names the task and whose sha256 is the FORMAT's, the sha256 alone
// only for a task in no list and only when exactly one entry has it. After F2's `B` entry, `A` and `B` share one sha256, so the sha256 alone no longer names a group.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { DEFAULT_RECORD, readRecord, recordedLabel } from '../lib/conformance-record.mjs';
import { preCheck } from '../lib/workspace-build.mjs';
import { check, fixtureRepo } from './support/conformance-fixtures.mjs';
import { folder, sha256 } from './support/format-fixtures.mjs';
import { inputsWith } from './support/workspace-fixtures.mjs';

const SHA_ONE = 'a'.repeat(64);
const SHA_TWO = 'b'.repeat(64);
const SIX = Object.freeze(['04', '05', '06', '07', '08', '10']);
const OLYMPIAD = Object.freeze(['01', '02', '03', '09']);
const realRecord = () => JSON.parse(readFileSync(DEFAULT_RECORD, 'utf8'));

/** The entries `readRecord` reads from a record made of `rows` (`{ label: { sha256, tasks, gate? } }`), in a file of this test's own. */
function entriesOf(t, rows) {
  const file = path.join(folder(t, 'rec'), 'format.json');
  writeFileSync(file, JSON.stringify(rows));
  const read = readRecord(file);
  assert.equal(read.ok, true, read.reason);
  return { entries: read.value, file };
}

const labelOf = (record, sha, task) => recordedLabel(record.entries, sha, record.file, task);

/** A record file in the fixture's folder: `rows` is `{ label: { sha256, tasks, gate } }`. */
function recordIn(fixture, rows) {
  const file = path.join(fixture.repo, 'two-entry-record.json');
  writeFileSync(file, JSON.stringify(Object.fromEntries(Object.entries(rows).map(([label, row]) => [label, { version: 2, gate: null, ...row }]))));
  return file;
}

const shaOfFormat = fixture => sha256(readFileSync(fixture.format));

test('Each_of_the_six_tasks_resolves_to_B_and_each_olympiad_task_to_A', t => {
  const record = realRecord();
  const sha = record.A.sha256;
  const real = { entries: readRecord(DEFAULT_RECORD).value, file: DEFAULT_RECORD };
  const inputs = inputsWith(record);
  const route = (task, group) => preCheck({ stack: 'csharp', kind: 'daemon', task, group, out: path.join(folder(t, 'out'), 'w'), inputs, helloFixture: false });
  for (const task of SIX) {
    assert.equal(labelOf(real, sha, task).value.label, 'B', `${task} by its number`);
    assert.equal(labelOf(real, sha, `${task}-a-slug`).value.label, 'B', `${task} by its folder name`);
    assert.equal(route(task, 'B').ok, true, `${task} is built under B`);
    assert.match(route(task, 'A').reason, /refuses to write into B's tasks/, `${task} is refused under A`);
  }
  for (const task of OLYMPIAD) {
    assert.equal(labelOf(real, sha, `${task}-a-slug`).value.label, 'A', `${task} by its folder name`);
    assert.equal(route(task, 'A').ok, true, `${task} is built under A`);
    assert.match(route(task, 'B').reason, /refuses to write into A's tasks/, `${task} is refused under B`);
  }
});

test('The_checker_prints_record_B_for_a_daemon_task_although_A_has_the_same_sha256', async t => {
  const daemon = fixtureRepo(t, 'daemon');
  const same = shaOfFormat(daemon);
  const record = recordIn(daemon, { A: { sha256: same, tasks: ['01'] }, B: { sha256: same, tasks: ['02'] } });
  const result = await check(daemon, ['--record', record]);
  assert.equal(result.lines[0], `ok format sha256 ${same} (record B, gate none)`);
  const olympiad = fixtureRepo(t, 'olympiad');
  const first = await check(olympiad, ['--record', recordIn(olympiad, { A: { sha256: same, tasks: ['01'] }, B: { sha256: same, tasks: ['04'] } })]);
  assert.equal(first.lines[0], `ok format sha256 ${same} (record A, gate none)`);
});

test('A_listed_task_whose_FORMAT_hash_differs_gets_no_record', async t => {
  const record = entriesOf(t, { A: { sha256: SHA_ONE, tasks: ['01'] }, B: { sha256: SHA_TWO, tasks: ['04'] } });
  const answer = labelOf(record, SHA_ONE, '04-a-slug');
  assert.equal(answer.ok, false, 'task 04 is listed under B, whose sha256 is another: not A by the hash alone');
  assert.match(answer.reason, /04-a-slug/);
  assert.match(answer.reason, /B/);
  assert.equal(labelOf(record, SHA_TWO, '04-a-slug').value.label, 'B');
  const daemon = fixtureRepo(t, 'daemon');
  const same = shaOfFormat(daemon);
  const file = recordIn(daemon, { A: { sha256: same, tasks: ['01'] }, B: { sha256: SHA_TWO, tasks: ['02'] } });
  const result = await check(daemon, ['--record', file]);
  assert.equal(result.exit, 4, 'the checker cannot run: no record');
  assert.equal(result.lines.length, 0, 'no first line, so no `record A` for a listed task');
  assert.match(result.errors.join('\n'), /02/);
});

test('An_unlisted_task_whose_hash_matches_two_entries_is_unresolved_not_A', async t => {
  const record = entriesOf(t, { A: { sha256: SHA_ONE, tasks: ['01'] }, B: { sha256: SHA_ONE, tasks: ['04'] } });
  const answer = labelOf(record, SHA_ONE, '11-a-slug');
  assert.equal(answer.ok, false);
  assert.match(answer.reason, /11-a-slug/, 'the sentence names the task');
  assert.match(answer.reason, /A, B|A and B/, 'and the entries that match');
  const daemon = fixtureRepo(t, 'daemon');
  const same = shaOfFormat(daemon);
  const file = recordIn(daemon, { A: { sha256: same, tasks: ['01'] }, B: { sha256: same, tasks: ['04'] } });
  const result = await check(daemon, ['--record', file]);
  assert.equal(result.exit, 4);
  assert.equal(result.lines.length, 0, 'never `record A` for a task in no list');
  assert.match(result.errors.join('\n'), /02-echo-line/);
});

test('An_unlisted_task_falls_back_to_the_sha256_when_exactly_one_entry_has_it', t => {
  const record = entriesOf(t, { A: { sha256: SHA_ONE, tasks: ['01'] }, B: { sha256: SHA_TWO, tasks: ['04'], gate: 'g.md' } });
  assert.deepEqual(labelOf(record, SHA_TWO, '11-a-slug').value, { label: 'B', gate: 'g.md' });
  assert.equal(labelOf(record, SHA_ONE, undefined).value.label, 'A', 'no task at all is a task in no list');
  assert.equal(labelOf(record, 'c'.repeat(64), '11-a-slug').ok, false, 'a sha256 no entry has is refused');
});

test('A_task_is_named_by_a_list_entry_that_is_its_number_or_its_number_before_a_dash', t => {
  const record = entriesOf(t, { A: { sha256: SHA_ONE, tasks: ['01'] }, B: { sha256: SHA_ONE, tasks: ['04'] } });
  assert.equal(labelOf(record, SHA_ONE, '04').value.label, 'B');
  assert.equal(labelOf(record, SHA_ONE, '04-echo').value.label, 'B');
  assert.equal(labelOf(record, SHA_ONE, '040-echo').ok, false, 'a longer number is another task');
  assert.equal(labelOf(record, SHA_ONE, '4').ok, false);
});
