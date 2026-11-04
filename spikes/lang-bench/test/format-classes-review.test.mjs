// T-L56 frozen against candidate, fix batch 1 (E1.S9): the holes the code review found, each built as a synthetic fixture. 2.1's rows are the contiguous block under the
// `| Id |` header only; an F-id has one row; the header rows keep their place and a strict value; renaming an unprotected part passes; a lead text may not be added as common;
// a free or added row may not name `all` or `olympiad`; messages say what changed (a number, a title, a preamble line).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { edit, FORMAT_TEXT, realDraft, sha256 } from './support/format-fixtures.mjs';

const compare = (candidate, base = FORMAT_TEXT) => {
  const frozen = Buffer.from(base);
  return checkFrozenCandidate({ frozen, candidate: Buffer.from(candidate), reference: sha256(frozen) });
};
const STDERR = '| F-STDERR | all | section 3, in an R | none |';
const PROCS = '| F-PROCS | all | section 3, in an R | none |';
const READY = '| F-READY | daemon, web | section 2, in an R | `{T}` |';
const LAST_ROW = '| 7. Changelog | common |\n';
const lineOf = (text, content, from = 0) => text.split('\n').findIndex((line, index) => index >= from && line === content) + 1;
const joined = result => result.lines.join('\n');

function assertRefused(result, ...rules) {
  assert.equal(result.exit, 1, joined(result));
  for (const rule of rules) assert.match(joined(result), rule);
}
const assertPasses = result => assert.equal(result.exit, 0, joined(result));

test('An_F_id_with_two_rows_in_the_candidate_is_refused_naming_the_id_and_both_lines_before_or_after_the_real_row', () => {
  const changed = '| F-STDERR | all, and more | section 4, in an R | none |';
  const before = edit(FORMAT_TEXT, STDERR, `${changed}\n${STDERR}`);
  assertRefused(compare(before), /F-STDERR/, new RegExp(`line ${lineOf(before, changed)}\\b.*line ${lineOf(before, STDERR)}\\b`));
  const after = edit(FORMAT_TEXT, STDERR, `${STDERR}\n${changed}`);
  assertRefused(compare(after), /F-STDERR/, new RegExp(`line ${lineOf(after, STDERR)}\\b.*line ${lineOf(after, changed)}\\b`));
});

test('A_free_F_id_with_two_rows_in_the_candidate_is_refused_too', () => {
  const doubled = edit(FORMAT_TEXT, READY, `${READY}\n${READY}`);
  assertRefused(compare(doubled), /F-READY/, /has 2 rows/);
});

test('An_F_id_with_two_rows_in_the_frozen_file_is_refused_naming_the_id_and_both_frozen_lines', () => {
  const doubled = edit(FORMAT_TEXT, STDERR, `${STDERR}\n${STDERR}`);
  assertRefused(compare(doubled, doubled), /F-STDERR/, new RegExp(`frozen line ${lineOf(doubled, STDERR)}\\b.*frozen line ${lineOf(doubled, STDERR) + 1}\\b`));
});

test('A_protected_row_moved_out_of_the_table_into_the_prose_is_refused', () => {
  const moved = edit(edit(FORMAT_TEXT, `${STDERR}\n`, ''), 'Text after the table.', `Text after the table.\n${STDERR}`);
  assertRefused(compare(moved), /2\.1 How they are used.*class common/);
});

test('A_free_row_moved_out_of_the_table_into_the_prose_is_refused_whole_line_or_inside_a_sentence', () => {
  const alone = edit(edit(FORMAT_TEXT, `${READY}\n`, ''), 'Text after the table.', `Text after the table.\n${READY}`);
  assertRefused(compare(alone), /2\.1 How they are used.*class common/);
  const inside = edit(edit(FORMAT_TEXT, `${READY}\n`, ''), 'The sentences are said identically.', `The sentences are said identically\n${READY}\n, and so on.`);
  assertRefused(compare(inside), /2\.1 How they are used.*class common/);
});

test('A_line_that_looks_like_an_F_row_in_2_1s_prose_is_plain_text_of_its_part_even_with_its_sentence_defined_in_a_daemon_web_part', () => {
  const planted = edit(edit(FORMAT_TEXT, 'Text after the table.', 'Text after the table.\n| F-ZZZ | all | section 3, in an R | none |'), '### 2.4 Daemon and web\n\n', '### 2.4 Daemon and web\n\nF-ZZZ: a new sentence.\n\n');
  assertRefused(compare(planted), /2\.1 How they are used.*class common/, /protected bytes differ/);
});

test('Two_protected_rows_swapped_are_refused_a_free_row_moved_inside_the_block_passes', () => {
  assertRefused(compare(edit(FORMAT_TEXT, `${STDERR}\n${PROCS}`, `${PROCS}\n${STDERR}`)), /2\.1 How they are used.*class common/, /order/);
  assertPasses(compare(edit(edit(FORMAT_TEXT, `${READY}\n`, ''), STDERR, `${READY}\n${STDERR}`)));
});

test('The_two_unprotected_rows_may_be_added_anywhere_in_the_block_when_their_sentences_stand_in_daemon_web_parts', () => {
  const withRow = row => edit(edit(FORMAT_TEXT, STDERR, `${row}\n${STDERR}`), '### 2.4 Daemon and web\n\n', '### 2.4 Daemon and web\n\nF-NEW: a new sentence.\n\n');
  assertPasses(compare(withRow('| F-NEW | daemon | section 2, in an R | none |')));
});

const SUB4 = ['4.1 Lexical rules', "4.2 The file's head", '4.3 Steps', '4.4 Groups and the end', '4.5 Keywords', "4.6 The runner's bounds"];
/** Section 4 (daemon-web as a whole) made mixed, every subsection daemon-web, with this lead text between the `##` line and `### 4.1`. */
function mixed4(lead) {
  let text = edit(FORMAT_TEXT, '| 4. The scenario script | daemon-web |\n', '');
  for (const row of SUB4) text = edit(text, LAST_ROW, `${LAST_ROW}| ${row} | daemon-web |\n`);
  return edit(text, '## 4. The scenario script\n\nThe grammar.\n\n### 4.1', `## 4. The scenario script\n\n${lead}### 4.1`);
}

test('The_lead_text_of_a_daemon_web_whole_that_becomes_mixed_may_only_be_the_heading_line_and_blank_lines_or_the_frozen_lead', () => {
  assertPasses(compare(mixed4('')));
  assertPasses(compare(mixed4('\n\n')));
  assertPasses(compare(mixed4('The grammar.\n\n')));
  const planted = mixed4('Every olympiad task also obeys this new common rule.\n\n');
  assertRefused(compare(planted), /4\. The scenario script \(lead text\).*added as common/);
  assertRefused(compare(mixed4('The grammar, and a new common rule.\n\n')), /added as common/);
});

const HEADER = { version: 'Format version: 2\n', verdict: 'Gate verdict: not yet gated\n', tag: 'Freeze tag: none yet\n' };
const withRow = (label, value) => edit(FORMAT_TEXT, HEADER[label], value);

test('The_header_rows_stay_at_the_frozen_lines_a_moved_or_swapped_row_is_refused_naming_the_row_and_both_lines', () => {
  const moved = edit(edit(FORMAT_TEXT, HEADER.verdict, ''), 'The changelog only grows.\n', 'The changelog only grows.\n\nGate verdict: not yet gated\n');
  assertRefused(compare(moved), /Gate verdict/, /line 4/);
  const intoPreambleTail = edit(edit(FORMAT_TEXT, HEADER.verdict, ''), `${LAST_ROW}\n## 1.`, `${LAST_ROW}\nGate verdict: not yet gated\n\n## 1.`);
  assertRefused(compare(intoPreambleTail), /Gate verdict/, /line 4/);
  const swapped = edit(FORMAT_TEXT, `${HEADER.verdict}${HEADER.tag}`, `${HEADER.tag}${HEADER.verdict}`);
  assertRefused(compare(swapped), /Gate verdict|Freeze tag/, /line/);
});

test('A_header_value_is_matched_against_a_strict_pattern_the_draft_values_and_the_later_ones_pass', () => {
  for (const [label, value] of [['version', 'Format version: 2-olympiad-tasks-may-ignore-section-2.6\n'], ['version', 'Format version: two\n'], ['version', 'Format version: 2.\n'], ['version', 'Format version: 2.1.1\n'],
    ['verdict', 'Gate verdict: olympiad tasks may ignore the closed list of section 2.6\n'], ['verdict', 'Gate verdict: a: b\n'], ['verdict', 'Gate verdict: Proceed\n'], ['verdict', `Gate verdict: ${'word '.repeat(20).trim()}\n`], ['verdict', 'Gate verdict: \n'], ['verdict', `Gate verdict: ${'a'.repeat(90)}\n`], ['verdict', 'Gate verdict: `Proceed`\n'], ['verdict', 'Gate verdict: `proceed` · 2026-13\n'], ['verdict', 'Gate verdict: `proceed` · 2026-12-01 and more\n'], ['verdict', 'Gate verdict: `proceed` 2026-12-01\n'], ['verdict', 'Gate verdict: `two words`\n'],
    ['tag', 'Freeze tag: spec-frozen-A-2026120\n'], ['tag', 'Freeze tag: anything goes here\n'], ['tag', 'Freeze tag: none yet and more\n']]) {
    assertRefused(compare(withRow(label, value)), new RegExp(value.split(':')[0]), /pattern|value/);
  }
  for (const [label, value] of [['version', 'Format version: 2.1\n'], ['version', 'Format version: 3\n'], ['verdict', 'Gate verdict: proceed\n'], ['verdict', 'Gate verdict: not yet gated\n'], ['verdict', 'Gate verdict: passed on 2026-12-01\n'],
    ['tag', 'Freeze tag: spec-frozen-A-20261201\n'], ['tag', 'Freeze tag: spec-frozen-20261201\n'], ['tag', 'Freeze tag: none yet\n'],
    ['verdict', 'Gate verdict: `proceed`\n'], ['verdict', 'Gate verdict: `proceed` · 2026-12-01\n'], ['verdict', 'Gate verdict: `not_gated` · 2026-01-31\n']]) {
    assertPasses(compare(withRow(label, value)));
  }
});

test('An_unprotected_heading_renamed_together_with_its_class_row_passes_the_row_is_matched_by_the_heading_number', () => {
  const renamed24 = edit(edit(FORMAT_TEXT, '### 2.4 Daemon and web\n', '### 2.4 Daemon and web tasks\n'), '| 2.4 Daemon and web | daemon-web |', '| 2.4 Daemon and web tasks | daemon-web |');
  assertPasses(compare(renamed24));
  const renamed4 = edit(edit(FORMAT_TEXT, '## 4. The scenario script\n', '## 4. The scenario grammar\n'), '| 4. The scenario script | daemon-web |', '| 4. The scenario grammar | web |');
  assertPasses(compare(renamed4));
});

test('An_unprotected_heading_renamed_without_its_row_is_refused_by_the_class_rules_and_a_protected_row_must_stay_byte_equal', () => {
  const rowKept = edit(FORMAT_TEXT, '### 2.4 Daemon and web\n', '### 2.4 Daemon and web tasks\n');
  assert.equal(compare(rowKept).exit, 1);
  assert.match(joined(compare(rowKept)), /2\.4 Daemon and web/);
  const retitled = edit(edit(FORMAT_TEXT, '### 2.3 Olympiad\n', '### 2.3 Olympiad sentences\n'), '| 2.3 Olympiad | olympiad |', '| 2.3 Olympiad sentences | olympiad |');
  assertRefused(compare(retitled), /preamble.*class common.*protected bytes differ/, /2\.3 Olympiad.*class olympiad.*heading text changed/);
  assert.equal(compare(edit(FORMAT_TEXT, '| 2.3 Olympiad | olympiad |', '|  2.3 Olympiad | olympiad |')).exit, 1, 'a row whose bytes changed is refused');
});

test('A_free_or_added_row_may_not_name_all_or_olympiad_in_its_kinds_cell', () => {
  assertRefused(compare(edit(FORMAT_TEXT, READY, READY.replace('| daemon, web |', '| all |'))), /F-READY/, /all|olympiad/);
  assertRefused(compare(edit(FORMAT_TEXT, READY, READY.replace('| daemon, web |', '| daemon, web, olympiad |'))), /F-READY/, /olympiad/);
  assertRefused(compare(edit(FORMAT_TEXT, READY, READY.replace('| daemon, web |', '| Olympiad |'))), /F-READY/);
  assertPasses(compare(edit(FORMAT_TEXT, READY, READY.replace('| daemon, web |', '| daemon or web |'))));
  const added = kinds => edit(edit(FORMAT_TEXT, READY, `${READY}\n| F-NEW | ${kinds} | section 2, in an R | none |`), '### 2.4 Daemon and web\n\n', '### 2.4 Daemon and web\n\nF-NEW: a new sentence.\n\n');
  assertRefused(compare(added('all')), /F-NEW/, /all|olympiad/);
  assertRefused(compare(added('daemon and olympiad')), /F-NEW/, /olympiad/);
  assertPasses(compare(added('daemon')));
});

test('A_shorter_or_extended_candidate_preamble_names_the_first_differing_candidate_line_and_never_throws', () => {
  const shorter = edit(FORMAT_TEXT, 'This document fixes the shape of every spec. It is a synthetic stand-in.\n\n', '');
  assertRefused(compare(shorter), /preamble.*protected bytes differ/, new RegExp(`candidate line ${lineOf(shorter, '**Section classes.** Every section below has one class, or, where a section mixes kinds of task, every subsection has')}\\b`));
  const appended = edit(FORMAT_TEXT, `${LAST_ROW}\n## 1.`, `${LAST_ROW}\nAn appended preamble paragraph.\n\n## 1.`);
  assertRefused(compare(appended), /preamble.*protected bytes differ/, new RegExp(`candidate line ${lineOf(appended, 'An appended preamble paragraph.')}\\b`));
  const extended = edit(FORMAT_TEXT, `${LAST_ROW}\n## 1.`, `${LAST_ROW}\n\nX appended.\n\n## 1.`);
  assertRefused(compare(extended), /preamble.*protected bytes differ/, new RegExp(`candidate line ${lineOf(extended, 'X appended.')}\\b`));
  const truncated = edit(FORMAT_TEXT, `${LAST_ROW}\n## 1.`, `${LAST_ROW}## 1.`);
  assertRefused(compare(truncated), /preamble.*protected bytes differ/);
});

test('A_protected_subsection_moved_to_another_section_is_reported_as_a_number_change_not_a_deletion', () => {
  const text = edit(edit(edit(FORMAT_TEXT, '### 2.6 Olympiad output\n\nThe closed list.\n\n', ''), '| 2.6 Olympiad output | olympiad |', '| 3.4 Olympiad output | olympiad |'), '### 3.3 Daemon and web samples\n\nScenario pairs.\n', '### 3.3 Daemon and web samples\n\nScenario pairs.\n\n### 3.4 Olympiad output\n\nThe closed list.\n');
  const result = compare(text);
  assertRefused(result, /2\.6 Olympiad output.*class olympiad.*heading number changed \(now 3\.4\)/);
  assert.doesNotMatch(joined(result), /2\.6 Olympiad output.*deleted/);
});

test('A_protected_heading_with_the_same_number_and_another_title_is_refused_as_a_changed_heading_text', () => {
  const retitled = edit(edit(FORMAT_TEXT, '### 2.3 Olympiad\n', '### 2.3 Olympiad sentences\n'), '| 2.3 Olympiad | olympiad |', '| 2.3 Olympiad sentences | olympiad |');
  assertRefused(compare(retitled), /2\.3 Olympiad.*class olympiad.*heading text changed/);
});

test('The_real_draft_frees_exactly_the_14_daemon_and_web_rows_of_2_1_and_protects_the_17_others', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const rows = real.split('\n').filter(line => /^\| F-[A-Z0-9-]+ \|/.test(line));
  const free = [];
  const protectedIds = [];
  for (const row of rows) {
    const id = /^\| (F-[A-Z0-9-]+) \|/.exec(row)[1];
    const result = checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(edit(real, row, `${row.slice(0, -1)} x |`)), reference: draft.recorded });
    (result.exit === 0 ? free : protectedIds).push(id);
  }
  assert.deepEqual(free.sort(), ['F-BYTES-D', 'F-BYTES-W', 'F-DEADLINES', 'F-EOF', 'F-JSON', 'F-LIMITS-D', 'F-LOAD', 'F-PORT', 'F-READY', 'F-RUN-D', 'F-SAMPLES-D', 'F-STDOUT-D', 'F-STOP', 'F-TIMING']);
  assert.equal(protectedIds.length, 17, protectedIds.join(' '));
  assert.equal(free.length + protectedIds.length, 31);
});

test('An_unprotected_section_heading_of_level_two_renamed_with_its_row_passes_and_its_subsections_follow_by_number', () => {
  const renamed = edit(edit(FORMAT_TEXT, '## 6. Worked examples\n', '## 6. Examples that illustrate\n'), '| 6. Worked examples | daemon-web |', '| 6. Examples that illustrate | daemon-web |');
  assertPasses(compare(renamed));
  assert.equal(compare(edit(FORMAT_TEXT, '## 6. Worked examples\n', '## 6. Examples that illustrate\n')).exit, 1, 'without its row the rename is refused by the class rules');
  const protectedRenamed = edit(edit(FORMAT_TEXT, '## 1. The spec file\n', '## 1. The file of the spec\n'), '| 1. The spec file | common |', '| 1. The file of the spec | common |');
  assert.equal(compare(protectedRenamed).exit, 1, 'a protected section may not be renamed');
});
