// E1.S8 batch 3, the second half (C13 to C20, C22): links, plan ids, heading levels, sample triggers, the reading block in tables and fences, the text of a sentence, values. Same shape as
// conformance-batch3.test.mjs: one change planted in a copy of a conforming spec, the unchanged spec ok for the check, the change a FAIL line naming it. Controls show what must stay ok.
import assert from 'node:assert/strict';
import { chmodSync } from 'node:fs';
import { test } from 'node:test';
import { check, failsOf, fixtureRepo, isOk, phrase, recordFile } from './support/conformance-fixtures.mjs';
import { run } from '../lib/conformance.mjs';
import { argsOf } from './support/conformance-fixtures.mjs';

const R2 = ['**R2.** For n = 1', null];
const inR2 = text => ({ spec: [[R2[0], `**R2.** ${text} For n = 1`]] });
const KEYS = ['env', 'file', 'start', 'connect', 'send', 'request', 'recv', 'expect', 'close'].map(word => `\`${word}\``).join(' ');
const STDERR = 'Standard error is not compared and may hold anything; it is recorded, not judged. Only its size is bounded: a run that writes more than 256 MiB to standard error is stopped, and that test fails.';
const OFFLINE = 'The tests run offline. They use a clean copy of the workspace as you left it: build output and installed packages are removed, and the frozen inputs are put back as they were given. Every other file is yours, including the project files that declare or lock your packages, and is built as you left it, by `bash build.sh`. The program must not depend on the network, on files outside the workspace, or on anything written during an earlier run.';

const GATE_OFF = '| Gate verdict | not yet gated |\n| Freeze tag | none yet |';
const GATED = '| Gate verdict | `proceed` · 2026-11-04 |\n';

// [name, kind, edits, check, message]
const CASES = [
  // C13: a link of any kind
  ...['HTTP://example.org/x', 'Https://example.org', 'ftp://host/file', 'www.example.org', '[the page](page.md)', '[the page][ref]', '<mailto:a@b.example>'].map(link => [`the_link_${link.replace(/[^A-Za-z0-9]/g, '_')}`, 'olympiad', inR2(`See ${link}.`), 'no-needle', /: link$/]),
  ['a_reference_definition', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1\n\n[ref]: page.md']] }, 'no-needle', /: link$/],
  // C14: plan ids in any case
  ...['E1.S8', 'e5', 'E12', 'd41', 'D123', 'T-L56', 't-l5', 'S3', 'CP2', 'OP-K', 'V12', 'v12', 'v2', 'F10'].map(id => [`the_plan_id_${id.replace(/[^A-Za-z0-9]/g, '_')}`, 'olympiad', inR2(`Note ${id} here.`), 'no-needle', /: plan id /]),
  // C15: heading levels
  ['a_level_1_heading_in_the_middle', 'olympiad', { spec: [['## 3. Output\n', '# Another title\n\n## 3. Output\n']] }, 'sections', /a # heading at line \d+/],
  ['a_level_4_heading_ends_a_span', 'olympiad', { spec: [['**R2.** For n = 1 the output is the single term 1.', '**R2.** For n = 1 the output is the single term 1.\n\n#### Detail\n\n| a | b |\n|---|---|\n| 1 | 2 |']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_level_3_heading_in_section_4', 'olympiad', { spec: [['## 4. Limits\n', '## 4. Limits\n\n### Detail\n']] }, 'sections', /a ### heading in section 4/],
  // C16: the triggers of a sample against the spec
  ['a_sample_ready_on_a_line_while_F_READY_says_the_port', 'daemon', { 's01.in': [['start ready port within 10s', 'start ready stdout "READY" within 10s']] }, 'T/G', /sample s01 is ready on a line of standard output, and F-READY says the port/],
  ['a_sample_ready_on_the_port_while_F_READY_says_a_line', 'daemon', { spec: [['when it accepts a TCP connection on that port', 'when it has written the line `READY` to standard output']] }, 'T/G', /sample s01 is ready on the port, and F-READY says a line of standard output/],
  ['a_sample_ended_by_close_stdin_without_F_EOF', 'web', { 's01.in': [['signal TERM', 'close stdin']] }, 'T/G', /sample s01 ends with close stdin, and the spec has no F-EOF/],
  // C17: the reading block in a table or in a fence
  ['a_reading_block_in_a_table', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** For n = 1\n\n| steps |\n|---|\n| ${KEYS} |`]] }, 'no-needle', /names 9 keywords of the scenario script/],
  ['a_reading_block_in_a_fence', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** The steps follow.\n\n\`\`\`text\nThe steps: ${KEYS}.\n\`\`\`\n\nFor n = 1`]] }, 'no-needle', /names 9 keywords of the scenario script/],
  // C18: the text of a sentence
  ['raw_inline_HTML_in_a_requirement', 'olympiad', inR2('Use <b>bold</b> text.'), 'file', /raw inline HTML/],
  ['a_sentence_inside_a_code_span', 'olympiad', { spec: [phrase(STDERR, `\`${STDERR}\``)] }, 'sentences', /F-STDERR is (missing|altered)/],
  ['a_sentence_inside_a_table_cell', 'olympiad', { spec: [phrase(OFFLINE, `Text.\n\n| a |\n|---|\n| ${OFFLINE} |`)] }, 'sentences', /F-OFFLINE is (missing|altered)/],
  ['a_no_break_space_in_a_sentence', 'olympiad', { spec: [phrase('Standard error is not compared', 'Standard error is not compared')] }, 'sentences', /F-STDERR is altered/],
  ['a_sentence_followed_by_a_letter', 'olympiad', { spec: [phrase('written during an earlier run.', 'written during an earlier run.X')] }, 'sentences', /F-OFFLINE is altered/],
  // C19: a sentence in the preamble
  ['a_second_F_REQ_in_the_preamble', 'olympiad', { spec: [['is tested.\n', 'is tested.\n\nEvery numbered sentence (**R1** … **R14**) is a requirement. Hidden tests refer to these numbers; nothing outside them is tested.\n']] }, 'sentences', /F-REQ stands 2 times/],
  ['another_sentence_in_the_preamble', 'olympiad', { spec: [['is tested.\n', `is tested.\n\n${OFFLINE}\n`]] }, 'sentences', /F-OFFLINE \(line \d+\) stands outside every requirement paragraph/],
  // C20: values
  ['a_gate_date_that_is_no_calendar_date', 'olympiad', { spec: [['| Gate verdict | not yet gated |', '| Gate verdict | `proceed` · 2026-02-30 |']] }, 'header', /2026-02-30 is not a real date/],
  ['an_amendment_date_that_is_no_calendar_date', 'olympiad', { spec: [['None yet.', '- A1 · 2026-13-01 · R1 — changed'], ['| Amendments | none yet |', '| Amendments | A1 (section 8) |']] }, 'sections', /2026-13-01 is not a real date/],
  ['a_freeze_tag_without_a_gated_verdict', 'olympiad', { spec: [['| Freeze tag | none yet |', '| Freeze tag | `spec-frozen-A-20261110` |']] }, 'header', /a freeze tag needs a gated verdict/],
  ['a_freeze_tag_with_a_group_letter_outside_A_and_B', 'olympiad', { spec: [['| Gate verdict | not yet gated |\n| Freeze tag | none yet |', '| Gate verdict | `proceed` · 2026-11-04 |\n| Freeze tag | `spec-frozen-C-20261110` |']] }, 'header', /group letter/],
  ['a_freeze_tag_without_a_group_letter', 'olympiad', { spec: [[GATE_OFF, `${GATED}| Freeze tag | \`spec-frozen-20261105\` |`]] }, 'header', /Freeze tag is "`spec-frozen-20261105`", which is neither its placeholder "none yet" nor its later form/],
  ['a_freeze_tag_with_the_group_letter_C', 'olympiad', { spec: [[GATE_OFF, `${GATED}| Freeze tag | \`spec-frozen-C-20261105\` |`]] }, 'header', /Freeze tag is "`spec-frozen-C-20261105`", which is neither its placeholder/],
  ['a_freeze_tag_with_an_invalid_date', 'olympiad', { spec: [['| Gate verdict | not yet gated |\n| Freeze tag | none yet |', '| Gate verdict | `proceed` · 2026-11-04 |\n| Freeze tag | `spec-frozen-A-20261310` |']] }, 'header', /20261310 is not a real date/],
  ['a_withdrawn_requirement_without_its_amendment', 'olympiad', { spec: [['**R2.** For n = 1 the output is the single term 1.', '**R2.** Withdrawn by amendment A2.']] }, 'r-ids', /Withdrawn by amendment A2, which section 8 does not hold/],
  ['a_sample_count_with_a_leading_zero', 'olympiad', { spec: [['The same two cases', 'The same 02 cases']] }, 'sentences', /F-SAMPLES-O is altered/],
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

test('A_sample_id_in_prose_is_no_plan_id', async t => {
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', inR2('The samples s01 and s2 are listed.'))), 'no-needle'));
});

test('A_freeze_tag_with_the_group_letter_A_or_B_is_ok_for_the_header', async t => {
  for (const letter of ['A', 'B']) {
    const result = await check(fixtureRepo(t, 'olympiad', { spec: [[GATE_OFF, `${GATED}| Freeze tag | \`spec-frozen-${letter}-20261105\` |`]] }));
    assert.ok(isOk(result, 'header'), `${letter}: ${result.lines.join('\n')}`);
  }
});

test('Code_spans_and_sample_files_are_no_link_and_no_plan_id_and_a_gated_spec_with_its_amendment_is_ok', async t => {
  const prose = inR2('A pattern like `[a-z](x)` and a sample `s01`, a word D1234 and Day5 and E are no links and no ids.');
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', prose)), 'no-needle'));
  const gated = { spec: [['| Gate verdict | not yet gated |\n| Freeze tag | none yet |', '| Gate verdict | `proceed` · 2026-11-04 |\n| Freeze tag | `spec-frozen-A-20261110` |'], ['None yet.', '- A1 · 2026-11-05 · R2 — withdrawn'], ['| Amendments | none yet |', '| Amendments | A1 (section 8) |'], ['**R2.** For n = 1 the output is the single term 1.', '**R2.** Withdrawn by amendment A1.']] };
  const result = await check(fixtureRepo(t, 'olympiad', gated));
  assert.ok(isOk(result, 'header') && isOk(result, 'sections') && isOk(result, 'r-ids'), result.lines.join('\n'));
});

// ---- C22: a sentence and exit 4, never a stack trace
test('A_kind_label_FORMAT_does_not_know_is_exit_4_with_a_sentence', async t => {
  const fixture = fixtureRepo(t, 'olympiad', { format: [['F-INVALID (olympiad):', 'F-INVALID (cobol):']] });
  const result = await run(argsOf(fixture, ['--record', recordFile(fixture)]));
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /the kind label `cobol` of F-INVALID is none of olympiad, daemon, web/);
});

test('A_sample_that_cannot_be_read_is_exit_4_with_a_sentence_and_no_stack_trace', async t => {
  if (typeof process.getuid === 'function' && process.getuid() === 0) return t.skip('root reads any file');
  const fixture = fixtureRepo(t, 'olympiad');
  chmodSync(`${fixture.samples}/s01.in`, 0o000);
  const result = await check(fixture);
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /samples\/s01\.in cannot be read/);
  assert.ok(!result.errors.join('\n').includes('    at '), 'no stack trace');
});
