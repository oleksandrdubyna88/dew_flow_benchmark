// E1.S8, checks 1 to 6: one failing fixture per deviation, each named. Every case plants one change in a copy of a conforming synthetic spec, and the same test shows that the unchanged spec is
// ok for that check, so a checker that finds nothing (or everything) fails here. `The_checker_still_finds_a_known_deviation` pins one exact line.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { check, failsOf, fixtureRepo, isOk, phrase } from './support/conformance-fixtures.mjs';

const HEADER = '| Kind | a program that reads standard input once and writes standard output once |';
const swapRows = text => text.replace('| Gate verdict | not yet gated |\n| Freeze tag | none yet |', '| Freeze tag | none yet |\n| Gate verdict | not yet gated |');
const moveHeading = text => text.replace('## 5. Build and run — the workspace contract\n\n', '').replace('**R11.**', '## 5. Build and run — the workspace contract\n\n**R11.**');
const moveEnv = text => {
  const env = /Every test gives the program the same base\s+environment[\s\S]*?and\s+`TZ` is `UTC`\./.exec(text)[0];
  return text.replace(env, '').replace('\n\n**R12.**', ` ${env}\n\n**R12.**`);
};

// [name, kind, edits, check, message]
const CASES = [
  ['a_title_with_another_number', 'olympiad', { spec: [['# Task 01 — Count down', '# Task 02 — Count down']] }, 'file', /the title says task 02, and the file name says 01/],
  ['a_title_with_a_hyphen_for_the_em_dash', 'olympiad', { spec: [['# Task 01 — Count down', '# Task 01 - Count down']] }, 'file', /line 1 is not the title/],
  ['a_kind_phrase_that_is_none_of_the_three', 'olympiad', { spec: [[HEADER, '| Kind | a program |']] }, 'header', /value of Kind is "a program"/],
  ['header_rows_in_another_order', 'olympiad', { spec: [swapRows] }, 'header', /exactly the rows Kind, Gate verdict, Freeze tag/],
  ['a_freeze_rule_that_is_not_the_sentence', 'olympiad', { spec: [['before the v2 pre-registration', 'before the pre-registration']] }, 'header', /value of Freeze rule/],
  ['a_gate_verdict_that_is_none_of_the_three', 'olympiad', { spec: [['| Gate verdict | not yet gated |', '| Gate verdict | `maybe` · 2026-11-04 |']] }, 'header', /value of Gate verdict is "`maybe` · 2026-11-04"/],
  ['a_header_table_with_a_filled_first_row', 'olympiad', { spec: [['| | |', '| a | b |']] }, 'header', /header row of the table must be empty/],
  ['a_section_with_another_title', 'olympiad', { spec: [['## 3. Output', '## 3. Results']] }, 'sections', /expected "## 3\. Output", found "## 3\. Results"/],
  ['a_ninth_section', 'olympiad', { spec: [['## 8. Amendments', '## 9. Extra\n\nText.\n\n## 8. Amendments']] }, 'sections', /expected "## 8\. Amendments", found "## 9\. Extra"/],
  ['a_daemon_spec_with_the_olympiad_headings', 'daemon', { spec: [['## 2. Start, readiness and stop', '## 2. Input']] }, 'sections', /expected "## 2\. Start, readiness and stop"/],
  ['amendments_with_a_gap', 'olympiad', { spec: [['None yet.', '- A1 · 2026-11-04 · R1 — changed\n- A3 · 2026-11-04 · R2 — changed'], ['| Amendments | none yet |', '| Amendments | A1–A2 (section 8) |']] }, 'sections', /without a gap/],
  ['an_amendments_row_that_does_not_match_section_8', 'olympiad', { spec: [['None yet.', '- A1 · 2026-11-04 · R1 — changed']] }, 'sections', /Amendments row says "none yet", and section 8 holds 1 item/],
  ['a_line_in_section_8_that_is_no_item', 'olympiad', { spec: [['None yet.', 'Nothing has changed.']] }, 'sections', /neither/],
  ['a_requirement_id_used_twice', 'olympiad', { spec: [['**R6.**', '**R5.**']] }, 'r-ids', /R5 stands more than once/],
  ['a_gap_in_the_requirement_ids', 'olympiad', { spec: [['**R14.**', '**R15.**'], ['**R14**)', '**R15**)']] }, 'r-ids', /R14 is missing/],
  ['ids_that_descend_before_the_freeze', 'olympiad', { spec: [['**R7.**', '**R99.**'], ['**R9.**', '**R7.**'], ['**R99.**', '**R9.**']] }, 'r-ids', /R8 stands after R9/],
  ['an_F_REQ_that_names_the_wrong_highest_id', 'olympiad', { spec: [['**R14**)', '**R13**)']] }, 'r-ids', /F-REQ .* says R13, and the highest requirement is R14/],
  ['an_id_written_without_the_full_stop', 'olympiad', { spec: [['**R2.**', '**R2**']] }, 'r-ids', /looks like a requirement id/],
  ['an_amendment_that_names_a_missing_id', 'olympiad', { spec: [['None yet.', '- A1 · 2026-11-04 · R99 — withdrawn'], ['| Amendments | none yet |', '| Amendments | A1 (section 8) |']] }, 'r-ids', /amendment A1 names R99, which does not exist/],
  ['a_table_that_follows_no_requirement', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n| a | b |\n|---|---|\n| 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_fenced_block_that_follows_no_requirement', 'olympiad', { spec: [['## 2. Input\n', '## 2. Input\n\n```text\nx\n```\n']] }, 'r-ids', /a fenced code block outside any requirement paragraph/],
  ['one_changed_word_in_F_STDERR', 'olympiad', { spec: [phrase('Standard error is not compared', 'Standard error is not judged')] }, 'sentences', /F-STDERR is altered at line \d+/],
  ['F_PROCS_left_out', 'olympiad', { spec: [phrase('The program and every process it starts may hold at most 512 processes and threads together. Beyond that the system refuses to create another, and a run that needs more fails that test.', '')] }, 'sentences', /F-PROCS is missing/],
  ['a_sentence_in_the_wrong_section', 'olympiad', { spec: [moveHeading] }, 'sentences', /F-FROZEN \(line \d+\) stands in section 4, and 2\.1 puts it in section 5/],
  ['F_INVALID_and_F_ERRORS_together', 'olympiad', { spec: [phrase('is unspecified and is never tested.', 'is unspecified and is never tested. Every input that violates R3 is answered as R1 says, and hidden tests check it.')] }, 'sentences', /F-INVALID and F-ERRORS both stand/],
  ['neither_F_INVALID_nor_F_ERRORS', 'olympiad', { spec: [phrase('Input that violates R3 does not occur in any visible or hidden test. What the program does with such input is unspecified and is never tested.', 'Nothing.')] }, 'sentences', /neither F-INVALID nor F-ERRORS stands/],
  ['F_BYTES_O_N_for_this_series', 'olympiad', { spec: [phrase('Nothing is normalized: a missing or extra line feed, a trailing space or a carriage return makes the test fail.', 'Nothing is normalized: a missing or extra line feed, a trailing space or a carriage return makes the test fail. The output is compared with the expected output byte for byte, except that a missing final line feed is ignored. Nothing else is normalized.')] }, 'sentences', /F-BYTES-O-N is refused for this series/],
  ['a_ref_slot_with_any_of_before_one_id', 'olympiad', { spec: [['violates R3 does', 'violates any of R3 does']] }, 'sentences', /F-INVALID is altered/],
  ['a_ref_slot_that_names_a_missing_requirement', 'olympiad', { spec: [['violates R3 does', 'violates R99 does']] }, 'sentences', /\{ref\} names R99, and the spec has requirements R1 to R14 only/],
  ['a_ref_range_that_does_not_ascend', 'olympiad', { spec: [['violates R3 does', 'violates any of R3–R2 does']] }, 'sentences', /does not ascend/],
  ['a_sample_count_that_is_not_the_number_shown', 'olympiad', { spec: [['The same two cases', 'The same five cases']] }, 'sentences', /says five samples, and section 6 shows 2/],
  ['a_F_STOP_which_that_ignores_F_EOF', 'daemon', { spec: [['A test that does not end at the end of its input ends', 'A test ends']] }, 'sentences', /\{which\} is "A test", and it must be "A test that does not end at the end of its input" because F-EOF is present/],
  ['an_override_without_F_TIMING', 'web', { spec: [phrase('The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.', '')] }, 'sentences', /F-TIMING is missing for the grace period: the task uses 8 s and the series default is 5 s/],
  ['an_F_TIMING_for_a_default_value', 'daemon', { spec: [phrase('leaving no process it started still running. A program still running after 5 seconds is killed and fails that test, and so does one that exits with another status or leaves a process running.\n\n**R5.**', 'leaving no process it started still running. A program still running after 5 seconds is killed and fails that test, and so does one that exits with another status or leaves a process running. The series default for the grace period is 5 seconds; this task uses 7 seconds because it must.\n\n**R5.**')] }, 'sentences', /F-TIMING/],
  ['F_HIDDEN_that_does_not_open_the_requirement', 'olympiad', { spec: [['**R14.** Hidden tests exist.', '**R14.** Extra words. Hidden tests exist.']] }, 'sentences', /F-HIDDEN \(line \d+\): the first sentence does not open the requirement/],
  ['F_HIDDEN_that_does_not_close_the_requirement', 'olympiad', { spec: [phrase('a program that passes them has not thereby passed the hidden tests.', 'a program that passes them has not thereby passed the hidden tests. Extra words.')] }, 'sentences', /F-HIDDEN \(line \d+\): the second sentence does not close the requirement/],
  ['F_HIDDEN_with_a_changed_word', 'olympiad', { spec: [phrase('Hidden tests exist.', 'Hidden tests do exist.')] }, 'sentences', /F-HIDDEN is altered/],
  ['F_OFFLINE_twice', 'olympiad', { spec: [phrase('**R14.**', 'The tests run offline, on a pristine copy of the workspace, after the build. The program must not depend on the network, on files outside the workspace, or on anything written during an earlier run.\n\n**R14.**')] }, 'sentences', /F-OFFLINE stands 2 times|F-OFFLINE \(line \d+\) stands outside/],
  ['F_ENV_apart_from_F_RUN', 'olympiad', { spec: [moveEnv] }, 'sentences', /F-ENV .* must stand in the same requirement as F-RUN-O or F-RUN-D/],
  ['a_sample_that_waits_less_than_G', 'daemon', { 's01.in': [['wait-exit within 5s', 'wait-exit within 4s']] }, 'T/G', /^sample s01 waits 4 s, the spec says 5 s$/],
  ['a_sample_that_starts_with_another_T', 'daemon', { 's02.in': [['start ready port within 10s', 'start ready port within 12s']] }, 'T/G', /sample s02 starts within 12 s, the spec says 10 s/],
  ['a_spec_T_that_the_samples_do_not_use', 'daemon', { spec: [['at most 10 seconds', 'at most 12 seconds']] }, 'T/G', /sample s01 starts within 10 s, the spec says 12 s/],
  ['an_F_EOF_with_another_G', 'daemon', { spec: [text => text.replace(/\*\*R5\.\*\*[\s\S]*?(?=\*\*R6\.)/, part => part.replaceAll('5 seconds', '6 seconds'))] }, 'T/G', /F-EOF .* says G is 6 s, and F-STOP says 5 s/],
  ['an_F_EOF_whose_two_G_differ', 'daemon', { spec: [['within 5 seconds of the end of its input', 'within 6 seconds of the end of its input']] }, 'sentences', /F-EOF is altered/],
  ['F_OFFLINE_outside_every_requirement', 'olympiad', { spec: [['## 7. Hidden tests\n', '## 7. Hidden tests\n\nThe tests run offline, on a pristine copy of the workspace, after the build. The program must not depend on the network, on files outside the workspace, or on anything written during an earlier run.\n']] }, 'sentences', /F-OFFLINE \(line \d+\) stands outside every requirement paragraph/],
  ['a_sample_with_no_wait_exit', 'web', { 's01.in': [['wait-exit within 8s\n', '']] }, 'T/G', /sample s01 has no wait-exit within/],
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

test('The_checker_still_finds_a_known_deviation', async t => {
  const result = await check(fixtureRepo(t, 'daemon', { 's01.in': [['wait-exit within 5s', 'wait-exit within 4s']] }));
  assert.ok(result.lines.includes('FAIL T/G — sample s01 waits 4 s, the spec says 5 s'), result.lines.join('\n'));
  assert.equal(result.exit, 1);
});

test('A_sample_that_waits_4_s_while_F_STOP_says_5_s_is_named_with_its_id_the_time_and_the_spec', async t => {
  const result = await check(fixtureRepo(t, 'daemon', { 's02.in': [['wait-exit within 5s', 'wait-exit within 4s']] }));
  assert.deepEqual(failsOf(result, 'T/G'), ['FAIL T/G — sample s02 waits 4 s, the spec says 5 s']);
});
