// E1.S8 batch 3, the structure items C5 to C10, C12 and C15 to C20: each case plants one change in a copy of a conforming synthetic spec (as conformance-checks.test.mjs does) and shows that the
// unchanged spec is ok for the check and that the change gives a FAIL line naming it. The threat model of the batch is an honest author who makes mistakes in the ordinary forms of Markdown.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { check, failsOf, fixtureRepo, isOk, phrase } from './support/conformance-fixtures.mjs';

// [name, kind, edits, check, message]
const CASES = [
  // C5: section 8
  ['an_empty_section_8', 'olympiad', { spec: [['None yet.', '']] }, 'sections', /section 8 is empty/],
  ['a_fenced_block_in_section_8', 'olympiad', { spec: [['None yet.', 'None yet.\n\n```text\nx\n```']] }, 'sections', /section 8 holds a fenced code block/],
  ['a_table_in_section_8', 'olympiad', { spec: [['None yet.', 'None yet.\n\n| a | b |\n|---|---|\n| 1 | 2 |']] }, 'sections', /section 8 holds a table/],
  // C6: a table is found by its delimiter row, whatever stands around it
  ['a_table_right_under_a_heading', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n| a | b |\n|---|---|\n| 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_after_a_text_line', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\nSome text.\n| a | b |\n|---|---|\n| 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_followed_by_text', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n| a | b |\n|---|---|\n| 1 | 2 |\nTrailing text.\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['an_indented_table', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n  | a | b |\n  |---|---|\n  | 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_without_outer_pipes', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\na | b\n--|--\n1 | 2\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_in_a_block_quote', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n> | a | b |\n> |---|---|\n> | 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  // C7: requirement ids in any form
  ['an_id_in_a_list_item', 'olympiad', { spec: [['**R6.** The output', '- **R6.** The output']] }, 'r-ids', /looks like a requirement id/],
  ['an_indented_id', 'olympiad', { spec: [['**R6.** The output', '  **R6.** The output']] }, 'r-ids', /looks like a requirement id/],
  ['an_id_in_the_middle_of_a_line', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 and **R3.** the rest']] }, 'r-ids', /looks like a requirement id/],
  ['a_plain_id_at_the_start_of_a_line', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1\nR3. Another sentence']] }, 'r-ids', /looks like a requirement id/],
  ['an_id_glued_to_the_paragraph_above', 'olympiad', { spec: [['\n\n**R6.** The output', '\n**R6.** The output']] }, 'r-ids', /does not open a paragraph/],
  ['a_reference_to_a_requirement_that_does_not_exist', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (see R99).']] }, 'r-ids', /names R99, and the spec has requirements R1 to R14 only/],
  ['a_reference_to_R0', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (see R0).']] }, 'r-ids', /names R0/],
  ['a_reference_with_a_leading_zero', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (see R02).']] }, 'r-ids', /names R02/],
  // C8: a sentence the table does not list for this kind
  ['F_PORT_in_an_olympiad_spec', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1. The environment variable `PORT` holds a TCP port number; the program listens on it, and the tests connect to it at 127.0.0.1.']] }, 'sentences', /F-PORT \(line \d+\) is carried by this olympiad spec, and 2\.1 lists it for daemon, web only/],
  ['F_BYTES_W_in_a_daemon_spec', 'daemon', { spec: [phrase('Nothing else is normalized.', 'Nothing else is normalized. Every reply is compared on its status code, on each response header that a requirement of this spec names, and on its body; other headers are not compared. The body is compared byte for byte, except where a requirement of this spec names another comparison. Nothing else is normalized.')] }, 'sentences', /F-BYTES-W \(line \d+\) is carried by this daemon spec, and 2\.1 lists it for web only/],
  ['a_sentence_whose_kinds_cell_was_narrowed', 'daemon', { format: [['| F-STDERR | all |', '| F-STDERR | olympiad |']] }, 'sentences', /F-STDERR \(line \d+\) is carried by this daemon spec, and 2\.1 lists it for olympiad only/],
  // C9: F-PORT is required for a web task and for a daemon that uses the port
  ['F_PORT_left_out_of_a_web_spec', 'web', { spec: [phrase('The environment variable `PORT` holds a TCP port number; the program listens on it, and the tests connect to it at 127.0.0.1.', '')] }, 'sentences', /F-PORT is missing: a web task listens on a port/],
  ['F_PORT_left_out_of_a_daemon_spec_that_uses_the_port', 'daemon', { spec: [phrase('The environment variable `PORT` holds a TCP port number; the program listens on it, and the tests connect to it at 127.0.0.1.', '')] }, 'sentences', /F-PORT is missing: .*uses the port/],
  // C10: every F-TIMING is checked, paired with its own value, and stands with the sentence it bounds
  ['an_F_TIMING_for_the_grace_period_in_the_requirement_of_F_READY', 'web', { spec: [['readiness; a program that is not ready by then fails that test.', 'readiness; a program that is not ready by then fails that test. The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.']] }, 'sentences', /F-TIMING \(line \d+\) for the grace period must stand in the same requirement as F-STOP/],
  ['an_F_TIMING_moved_away_from_F_STOP', 'web', { spec: [phrase('The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.', ''), ['readiness; a program that is not ready by then fails that test.', 'readiness; a program that is not ready by then fails that test. The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.']] }, 'sentences', /F-TIMING \(line \d+\) for the grace period must stand in the same requirement as F-STOP/],
  ['an_F_TIMING_said_twice', 'web', { spec: [phrase('The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.', 'The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits. The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.')] }, 'sentences', /F-TIMING for the grace period stands 2 times/],
  // C12: section 6 of an olympiad spec
  ['a_second_table_in_section_6', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |\n', '| `s02` | `1⏎` | `1⏎` |\n\n| x | y |\n|---|---|\n| 1 | 2 |\n']] }, 'samples', /section 6 holds more than one table/],
  ['a_fenced_block_in_section_6_of_an_olympiad_spec', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |\n', '| `s02` | `1⏎` | `1⏎` |\n\n```text\nx\n```\n']] }, 'samples', /section 6 of an olympiad spec holds a fenced code block/],
  ['a_subsection_in_section_6_of_an_olympiad_spec', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |\n', '| `s02` | `1⏎` | `1⏎` |\n\n### s01\n\nText.\n']] }, 'samples', /section 6 of an olympiad spec holds a ### heading/],
  ['a_251_byte_sample_shown_by_its_bytes', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |', `| \`s02\` | \`${'a'.repeat(250)}⏎\` | \`1⏎\` |`]], 's02.in': [['1\n', `${'a'.repeat(250)}\n`]] }, 'samples', /samples\/s02\.in has 251 bytes and can be shown only by its size/],
  ['a_sample_of_spaces_only_shown_by_its_bytes', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |', '| `s02` | ``     `` | `1⏎` |']], 's02.in': [['1\n', '   ']] }, 'samples', /samples\/s02\.in has only spaces and can be shown only by its size/],
  ['a_sample_with_two_backticks_in_a_row_shown_by_its_bytes', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |', '| `s02` | ` a``b ` | `1⏎` |'.replace('` a``b `', '`` a``b ``')]], 's02.in': [['1\n', 'a``b']] }, 'samples', /samples\/s02\.in holds two backticks in a row and can be shown only by its size/],
];

for (const [name, kind, edits, name_, message] of CASES) {
  test(`${name}_is_found_by_the_${name_}_check`, async t => {
    assert.ok(isOk(await check(fixtureRepo(t, kind)), name_), 'the unchanged spec is ok for this check');
    const result = await check(fixtureRepo(t, kind, edits));
    const lines = failsOf(result, name_).map(line => line.slice(`FAIL ${name_} — `.length));
    assert.ok(lines.some(line => message.test(line)), `${message} not in:\n${result.lines.join('\n')}`);
    assert.equal(result.exit, 1);
  });
}

test('The_phrase_helper_is_available_to_the_batch_cases', () => assert.equal(typeof phrase, 'function'));

test('A_table_without_outer_pipes_inside_a_requirement_is_no_finding', async t => {
  const spec = [['| n | output |\n|---|---|\n| 1 | `1` |\n| 2 | `2 1` |', 'n | output\n--|--\n1 | `1`\n2 | `2 1`']];
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', { spec })), 'r-ids'));
});

test('Plain_references_in_prose_and_the_F_REQ_paragraph_are_no_finding', async t => {
  const spec = [['**R2.** For n = 1', '**R2.** For n = 1, as R1 says; see R3–R5 and R14. A word like CR3 or R2D2 is no reference.']];
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', { spec })), 'r-ids'));
});

// ---- C9 on a context made by hand: a daemon that is ready on a line of standard output and connects to nothing needs no F-PORT
const readyBy = readiness => new Map([['F-READY', { hits: [{ slots: { readiness } }] }]]);
const scenario = text => new Map([['s01', Buffer.from(text)]]);
const LINE_READY = 'it has written the line `READY` to standard output';
const PORT_READY = 'it accepts a TCP connection on that port';

test('F_PORT_is_not_required_of_a_daemon_that_is_ready_on_a_line_and_uses_no_connection', async () => {
  const { portRequired } = await import('../lib/conformance-check-sentences.mjs');
  const lines = scenario('scenario 2\nstart ready stdout "READY" within 10s\nsend stdin "x\\n"\nrecv stdout until "\\n"\nsignal TERM\nwait-exit within 5s\n');
  assert.equal(portRequired({ kind: 'daemon', found: readyBy(LINE_READY), scenarios: lines }), false);
  assert.equal(portRequired({ kind: 'daemon', found: readyBy(PORT_READY), scenarios: lines }), true, 'the port phrase of F-READY');
  assert.equal(portRequired({ kind: 'daemon', found: readyBy(LINE_READY), scenarios: scenario('scenario 2\nstart ready port within 10s\n') }), true, 'a sample that is ready on the port');
  assert.equal(portRequired({ kind: 'daemon', found: readyBy(LINE_READY), scenarios: scenario('scenario 2\nstart ready stdout "R" within 10s\nconnect c1 tcp\n') }), true, 'a sample that connects');
  assert.equal(portRequired({ kind: 'web', found: readyBy(LINE_READY), scenarios: lines }), true, 'a web task');
  assert.equal(portRequired({ kind: 'olympiad', found: new Map(), scenarios: new Map() }), false);
});
