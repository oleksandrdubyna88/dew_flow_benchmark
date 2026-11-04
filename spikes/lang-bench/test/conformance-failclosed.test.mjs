// E1.S8 batch 3, the fail-closed items: a runner that exits 4 and says nothing (C1), a FORMAT that is not the recorded one or whose table or blocks do not fit what the reader expects (C2, C11),
// and the unreadable inputs that end in one sentence and exit 4 (C22). Nothing here may end in a pass.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { createHash } from 'node:crypto';
import { check, fixtureRepo } from './support/conformance-fixtures.mjs';

/** A stand-in runner (a Python script) written next to the fixture. */
function standIn(fixture, body) {
  const file = path.join(fixture.repo, 'fake_runner.py');
  writeFileSync(file, body);
  return file;
}

const SILENT = [
  ['says_nothing', 'import sys\nsys.exit(4)\n'],
  ['writes_only_to_standard_output', 'import sys\nprint("5 pairs parsed")\nsys.exit(4)\n'],
  ['writes_only_whitespace_to_standard_error', 'import sys\nsys.stderr.write("  \\n\\n \\t\\n")\nsys.exit(4)\n'],
];
for (const [name, body] of SILENT) {
  test(`A_runner_that_exits_4_and_${name}_is_exit_4_never_a_pass`, async t => {
    const fixture = fixtureRepo(t, 'daemon');
    const result = await check(fixture, [], { runner: standIn(fixture, body) });
    assert.equal(result.exit, 4, result.lines.join('\n'));
    assert.deepEqual(result.lines, []);
    assert.match(result.errors.join('\n'), /the runner exited 4 and named no problem/);
  });
}

// ---- C2: the FORMAT must be the recorded one, and its table and blocks must fit what the reader names
import { readFileSync } from 'node:fs';
import { run } from '../lib/conformance.mjs';
import { argsOf, recordFile } from './support/conformance-fixtures.mjs';
import { edit } from './support/format-fixtures.mjs';

/** The fixture with its FORMAT edited by `steps` (pairs or functions), the record made after the edit. */
function withFormat(t, steps) {
  const fixture = fixtureRepo(t, 'daemon', { format: steps });
  return { fixture, record: recordFile(fixture) };
}
const runWith = ({ fixture, record }, seams = {}) => run(argsOf(fixture, ['--record', record]), seams);

test('A_FORMAT_that_is_not_in_the_record_is_exit_4_and_the_default_record_does_not_hold_the_stand_in', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  const result = await run(argsOf(fixture));
  assert.equal(result.exit, 4);
  assert.deepEqual(result.lines, []);
  assert.match(result.errors.join('\n'), /is not the FORMAT the record names/);
});

test('A_FORMAT_in_the_record_is_read_and_its_sha256_is_printed_on_an_ok_line_for_the_gate_record', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  const result = await run(argsOf(fixture, ['--record', recordFile(fixture)]));
  assert.equal(result.exit, 0, result.errors.join('\n'));
  const sha = createHash('sha256').update(readFileSync(fixture.format)).digest('hex');
  assert.equal(result.lines[0], `ok format sha256 ${sha} (record A, gate none)`);
});

test('A_record_that_cannot_be_read_or_holds_no_sha256_is_exit_4', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  const bad = path.join(fixture.repo, 'bad.json');
  writeFileSync(bad, '{ "A": {} }');
  for (const file of [bad, path.join(fixture.repo, 'none.json')]) {
    const result = await run(argsOf(fixture, ['--record', file]));
    assert.equal(result.exit, 4);
    assert.match(result.errors.join('\n'), /record/);
  }
});

const READER_FAULTS = [
  ['a_version_other_than_2', [['Format version: 2', 'Format version: 3']], /reads Format version 2 only/],
  ['a_deleted_row_whose_block_stays', [text => text.replace(/^\| F-OFFLINE \|.*\n/m, '')], /a text block labelled F-OFFLINE and no row/],
  ['a_row_the_checker_names_deleted_with_its_block', [text => text.replace(/^\| F-PORT \|.*\n/m, '').replace(/F-PORT:\n\n```text\n[^]*?\n```\n/, '')], /no row for F-PORT/],
  ['an_added_block_with_no_row', [['F-OFFLINE:\n', 'F-EXTRA:\n\n```text\nExtra.\n```\n\nF-OFFLINE:\n']], /a text block labelled F-EXTRA and no row/],
  ['a_second_block_under_one_id', [text => text.replace(/(F-OFFLINE:\n\n```text\n[^]*?\n```\n)/, '$1\n```text\nA second block.\n```\n')], /F-OFFLINE has 2 text blocks, and 2\.1 expects 1/],
  ['F_HIDDEN_with_one_block', [text => text.replace(/\n```text\nThe visible samples are a small part[^]*?\n```\n/, '\n')], /F-HIDDEN has 1 text block, and 2\.1 expects 2/],
  ['a_second_lead_for_one_id_with_the_same_kinds', [text => text.replace(/(F-OFFLINE:\n\n```text\n[^]*?\n```\n)/, '$1\nF-OFFLINE:\n\n```text\nAnother.\n```\n')], /F-OFFLINE has two text blocks for the same kinds/],
  ['a_kinds_cell_the_reader_cannot_read_exactly', [['| F-PORT | daemon and web that listen on a port |', '| F-PORT | daemon and web sometimes |']], /the kinds of F-PORT/],
  ['a_condition_the_reader_does_not_know', [['| F-PORT | daemon and web that listen on a port |', '| F-PORT | daemon and web, unless |']], /the kinds of F-PORT/],
  ['a_kinds_cell_of_olympiad_with_a_stray_condition', [['| F-EXIT-O | olympiad |', '| F-EXIT-O | olympiad, whenever |']], /the kinds of F-EXIT-O/],
];
for (const [name, steps, message] of READER_FAULTS) {
  test(`A_FORMAT_with_${name}_is_exit_4_even_when_the_record_names_it`, async t => {
    const result = await runWith(withFormat(t, steps));
    assert.equal(result.exit, 4, result.lines.join('\n'));
    assert.deepEqual(result.lines, []);
    assert.match(result.errors.join('\n'), message);
  });
}

test('The_edit_helper_throws_when_a_needle_is_missing_so_a_fault_case_cannot_pass_by_editing_nothing', () => {
  assert.throws(() => edit('abc', 'x', 'y'));
});

// ---- C11: the phrases the slots take are read from FORMAT's prose; a phrase that is no longer there is exit 4
const PHRASE_FAULTS = [
  ['readiness_phrase', [['`{readiness}` is one of `it accepts a TCP connection on that port`', '`{readiness}` is one of `it accepts a connection on that port`']], /the phrase `it accepts a TCP connection on that port`/],
  ['line_readiness_phrase', [['or `it has written the line {line} to', 'or `it has printed the line {line} to']], /the phrase `it has written the line \{line\} to standard output`/],
  ['which_phrase', [['`A test`, or, in a task', '`The test`, or, in a task']], /the phrase `A test`/],
  ['which_phrase_with_F_EOF', [['`A test that does not end at the end of its\ninput`', '`A test that does not end at the end of its\nstream`']], /the phrase `A test that does not end at the end of its input`/],
  ['what_phrase', [['`{what}` is `readiness` or `the grace period`', '`{what}` is `readiness` or `the grace time`']], /the phrase `the grace period`/],
];
for (const [name, steps, message] of PHRASE_FAULTS) {
  test(`A_FORMAT_that_changed_the_${name}_is_exit_4_though_the_spec_keeps_the_old_one`, async t => {
    const result = await runWith(withFormat(t, steps));
    assert.equal(result.exit, 4, result.lines.join('\n'));
    assert.deepEqual(result.lines, []);
    assert.match(result.errors.join('\n'), message);
  });
}
