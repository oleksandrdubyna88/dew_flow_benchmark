// T-L56, the frozen-against-candidate half (E1.S9): after the freeze of the first group of tasks the `common` and `olympiad` parts of FORMAT do not change.
// The protected set comes from the FROZEN file's class table only; every cut of the text is by the parts the class rule defines (lead text, subsection, whole section),
// so no byte is in two parts or in none. The cases call the exported function with an explicit reference; the CLI is in format-classes.test.mjs.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { edit, FORMAT_TEXT, realDraft, sha256 } from './support/format-fixtures.mjs';

const compare = (candidate, base = FORMAT_TEXT) => {
  const frozen = Buffer.from(base);
  return checkFrozenCandidate({ frozen, candidate: Buffer.from(candidate), reference: sha256(frozen) });
};
const LAST_ROW = '| 7. Changelog | common |\n';
const appendRow = (text, row) => edit(text, LAST_ROW, `${LAST_ROW}${row}\n`);
const appendSub = (text, row) => appendRow(edit(text, 'The closed list.\n\n## 3.', 'The closed list.\n\n### 2.7 New part\n\nNew text.\n\n## 3.'), row);
/** Rewrites `### s.k` headings and `| s.k ` table rows with k >= from to k + 1: room for a subsection inserted at number `from`. */
const shiftSubs = (text, section, from) => text.replace(new RegExp(`^(### |\\| )${section}\\.(\\d+) `, 'gm'), (all, lead, k) => (Number(k) >= from ? `${lead}${section}.${Number(k) + 1} ` : all));

function assertRefused(result, heading, cls, rule) {
  assert.equal(result.exit, 1, result.lines.join('\n'));
  const named = result.lines.filter(line => line.includes(heading) && line.includes(`class ${cls}`) && rule.test(line));
  assert.ok(named.length >= 1, `a line names ${heading}, class ${cls} and ${rule}; the lines were:\n${result.lines.join('\n')}`);
}

function assertPasses(result) {
  assert.equal(result.exit, 0, result.lines.join('\n'));
}

test('A_candidate_equal_to_the_frozen_file_passes', () => assertPasses(compare(FORMAT_TEXT)));

test('The_three_header_rows_may_change_whole_lines', () => {
  const text = edit(edit(edit(FORMAT_TEXT, 'Format version: 2\n', 'Format version: 2.1\n'), 'Gate verdict: not yet gated\n', 'Gate verdict: passed on 2026-12-01\n'), 'Freeze tag: none yet\n', 'Freeze tag: spec-frozen-20261201\n');
  assertPasses(compare(text));
});

test('A_missing_or_doubled_header_row_is_refused', () => {
  assertRefused(compare(edit(FORMAT_TEXT, 'Gate verdict: not yet gated\n', '')), 'preamble', 'common', /Gate verdict/);
  assertRefused(compare(edit(FORMAT_TEXT, 'Freeze tag: none yet\n', 'Freeze tag: none yet\nFreeze tag: again\n')), 'preamble', 'common', /Freeze tag/);
});

test('An_edit_of_the_preamble_outside_the_header_rows_and_the_class_table_is_refused', () => {
  assertRefused(compare(edit(FORMAT_TEXT, 'It is a synthetic stand-in.', 'It is a different stand-in.')), 'preamble', 'common', /protected bytes differ/);
});

test('Class_table_rows_appended_after_the_last_row_pass_and_a_row_inserted_before_it_is_refused', () => {
  assertPasses(compare(appendSub(FORMAT_TEXT, '| 2.7 New part | daemon |')));
  const middle = edit(edit(appendSub(FORMAT_TEXT, '| 2.7 New part | daemon |'), '| 2.7 New part | daemon |\n', ''), '| 3.1 Files and ids | common |\n', '| 2.7 New part | daemon |\n| 3.1 Files and ids | common |\n');
  assertRefused(compare(middle), 'preamble', 'common', /protected bytes differ/);
  assertPasses(compare(edit(FORMAT_TEXT, '| 4. The scenario script | daemon-web |', '| 4. The scenario script | web |')));
});

test('A_protected_class_row_that_is_relabelled_is_refused_naming_the_heading_and_the_frozen_class', () => {
  assertRefused(compare(edit(FORMAT_TEXT, '| 2.3 Olympiad | olympiad |', '| 2.3 Olympiad | daemon-web |')), '2.3 Olympiad', 'olympiad', /class row differs/);
  assertRefused(compare(edit(FORMAT_TEXT, '| 1. The spec file | common |', '| 1. The spec file | olympiad |')), '1. The spec file', 'common', /class row differs/);
});

test('A_daemon_web_part_relabelled_as_common_or_olympiad_is_a_part_added_as_protected', () => {
  assertRefused(compare(edit(FORMAT_TEXT, '| 2.4 Daemon and web | daemon-web |', '| 2.4 Daemon and web | common |')), '2.4 Daemon and web', 'daemon-web', /added as common/);
  assertRefused(compare(edit(FORMAT_TEXT, '| 4. The scenario script | daemon-web |', '| 4. The scenario script | olympiad |')), '4. The scenario script', 'daemon-web', /added as olympiad/);
});

for (const [name, section, needle, planted, cls] of [
  ['a_whole_common_section', '1. The spec file', 'The file name and the header table.', 'The file name, the header table and a new rule.', 'common'],
  ['a_common_subsection', '2.2 Every kind', 'Standard error is not compared.', 'Standard error is compared.', 'common'],
  ['an_olympiad_subsection', '2.3 Olympiad', 'Olympiad sentences.', 'Olympiad sentences, changed.', 'olympiad'],
  ['the_lead_text_of_a_mixed_section', '2. The fixed sentences', 'The lead text of section 2.', 'The lead text of section 2, changed.', 'common'],
  ['a_common_subsection_of_another_mixed_section', '3.1 Files and ids', 'The ids.', 'The ids, changed.', 'common'],
]) {
  test(`An_edit_in_${name}_is_refused_naming_its_heading_and_class`, () => {
    assertRefused(compare(edit(FORMAT_TEXT, needle, planted)), section, cls, /protected bytes differ/);
  });
}

test('A_protected_heading_with_another_title_is_refused_naming_its_heading_and_class', () => {
  const retitled = edit(edit(FORMAT_TEXT, '### 2.3 Olympiad\n', '### 2.3 Olympiad sentences\n'), '| 2.3 Olympiad | olympiad |', '| 2.3 Olympiad sentences | olympiad |');
  assertRefused(compare(retitled), '2.3 Olympiad', 'olympiad', /protected bytes differ/);
});

test('An_edit_of_a_daemon_web_part_passes_also_next_to_a_protected_part', () => {
  assertPasses(compare(edit(FORMAT_TEXT, 'Strings and escapes.', 'Strings, escapes and more.')));
  assertPasses(compare(edit(FORMAT_TEXT, 'The defaults T and G.', 'The defaults T and G, changed.')));
  assertPasses(compare(edit(FORMAT_TEXT, 'The defaults T and G.\n\n### 2.6', 'The defaults T and G.\n\nA new paragraph at the end of 2.5.\n\n### 2.6')));
});

test('A_blank_line_added_between_two_protected_parts_is_refused_it_belongs_to_the_part_before', () => {
  assertRefused(compare(edit(FORMAT_TEXT, '`UTC`.\n```\n\n### 2.3 Olympiad', '`UTC`.\n```\n\n\n### 2.3 Olympiad')), '2.2 Every kind', 'common', /protected bytes differ/);
  assertRefused(compare(edit(FORMAT_TEXT, 'Olympiad sentences.\n\n### 2.4', 'Olympiad sentences.\n\n\n### 2.4')), '2.3 Olympiad', 'olympiad', /protected bytes differ/);
  assertRefused(compare(edit(FORMAT_TEXT, 'Samples are pairs of files.\n\n### 3.1', 'Samples are pairs of files.\n\n\n### 3.1')), '3. Visible samples', 'common', /protected bytes differ/);
  assertPasses(compare(edit(FORMAT_TEXT, 'The defaults T and G.\n\n### 2.6', 'The defaults T and G.\n\n\n### 2.6')));
});

test('The_changelog_may_only_grow_it_must_start_with_the_frozen_bytes', () => {
  assertPasses(compare(`${FORMAT_TEXT}- v2.1 · 2026-12-01 · amendment · touches all — a later line.\n`));
  assertRefused(compare(edit(FORMAT_TEXT, 'the first line.', 'the first line, reworded.')), '7. Changelog', 'common', /must start with the frozen bytes/);
  assertRefused(compare(edit(FORMAT_TEXT, '- v2 · 2026-11-04 · draft — the second line.\n', '')), '7. Changelog', 'common', /must start with the frozen bytes/);
});

test('A_protected_part_that_is_deleted_is_refused_naming_it', () => {
  const gone = edit(edit(FORMAT_TEXT, '### 2.6 Olympiad output\n\nThe closed list.\n\n', ''), '| 2.6 Olympiad output | olympiad |\n', '');
  assertRefused(compare(gone), '2.6 Olympiad output', 'olympiad', /missing/);
});

for (const [cls, passes] of [['common', false], ['olympiad', false], ['daemon', true], ['web', true], ['daemon-web', true]]) {
  test(`A_subsection_appended_at_the_end_of_a_mixed_section_classed_${cls.replace('-', '_')}_${passes ? 'passes' : 'is_refused'}`, () => {
    const result = compare(appendSub(FORMAT_TEXT, `| 2.7 New part | ${cls} |`));
    if (passes) assertPasses(result);
    else assertRefused(result, '2.7 New part', 'none', new RegExp(`added as ${cls}`));
  });
}

test('A_subsection_inserted_inside_a_protected_mixed_section_changes_the_numbers_of_protected_parts_and_is_refused', () => {
  const shifted = shiftSubs(FORMAT_TEXT, 2, 3);
  const inserted = appendRow(edit(shifted, '### 2.4 Olympiad\n', '### 2.3 Inserted part\n\nNew text.\n\n### 2.4 Olympiad\n'), '| 2.3 Inserted part | daemon |');
  assertRefused(compare(inserted), '2.3 Olympiad', 'olympiad', /heading number changed.*2\.4/);
  assertRefused(compare(inserted), '2.6 Olympiad output', 'olympiad', /missing|heading number changed/);
});

test('A_subsection_inserted_in_a_protected_whole_section_changes_its_bytes', () => {
  assertRefused(compare(edit(FORMAT_TEXT, 'One name.\n', 'One name.\n\n### 1.2 Added\n\nNew.\n')), '1. The spec file', 'common', /protected bytes differ/);
});

test('A_daemon_web_whole_section_may_become_mixed_with_parts_classed_daemon_web_or_web', () => {
  const text = edit(FORMAT_TEXT, '| 4. The scenario script | daemon-web |\n', '');
  const rows = ['4.1 Lexical rules | daemon-web', "4.2 The file's head | web", '4.3 Steps | daemon', '4.4 Groups and the end | daemon-web', '4.5 Keywords | daemon-web', "4.6 The runner's bounds | daemon-web"];
  assertPasses(compare(rows.reduce((acc, row) => appendRow(acc, `| ${row} |`), text)));
  assertRefused(compare(rows.map(row => row.replace('4.3 Steps | daemon', '4.3 Steps | common')).reduce((acc, row) => appendRow(acc, `| ${row} |`), text)), '4.3 Steps', 'none', /added as common/);
});

test('A_mixed_section_turned_into_one_daemon_web_whole_loses_the_rows_of_its_protected_subsections', () => {
  let text = FORMAT_TEXT;
  for (const row of ['2.1 How they are used | common', '2.2 Every kind | common', '2.3 Olympiad | olympiad', '2.4 Daemon and web | daemon-web', '2.5 The timing contract | daemon-web', '2.6 Olympiad output | olympiad']) text = edit(text, `| ${row} |\n`, '');
  assertRefused(compare(appendRow(text, '| 2. The fixed sentences | daemon-web |')), '2.3 Olympiad', 'olympiad', /class row missing/);
});

test('A_frozen_file_that_breaks_its_own_class_rules_is_a_format_error_not_a_reference', () => {
  const broken = appendRow(FORMAT_TEXT, '| 9. No such heading | common |');
  const result = checkFrozenCandidate({ frozen: Buffer.from(broken), candidate: Buffer.from(broken), reference: sha256(Buffer.from(broken)) });
  assert.equal(result.exit, 4);
  assert.match(result.lines.join('\n'), /9\. No such heading/);
});

test('A_candidate_whose_class_table_names_no_heading_or_misses_a_section_is_refused_by_the_class_rules', () => {
  const result = compare(appendRow(FORMAT_TEXT, '| 9. No such heading | common |'));
  assert.equal(result.exit, 1, result.lines.join('\n'));
  assert.match(result.lines.join('\n'), /9\. No such heading/);
});

test('The_fixed_sentence_table_is_compared_row_by_row_by_the_id', () => {
  const ready = '| F-READY | daemon, web | section 2, in an R | `{T}` |';
  assertPasses(compare(edit(FORMAT_TEXT, ready, '| F-READY | daemon, web | section 2, in an R | `{T}` and `{G}` |')));
  assertPasses(compare(edit(FORMAT_TEXT, `${ready}\n`, '')));
  assertRefused(compare(edit(FORMAT_TEXT, '| F-STDERR | all | section 3, in an R | none |', '| F-STDERR | all | section 4, in an R | none |')), '2.1 How they are used', 'common', /F-STDERR/);
  assertRefused(compare(edit(FORMAT_TEXT, '| F-STDERR | all | section 3, in an R | none |\n', '')), '2.1 How they are used', 'common', /F-STDERR/);
  assertRefused(compare(edit(FORMAT_TEXT, 'Text after the table.', 'Text after the table, changed.')), '2.1 How they are used', 'common', /protected bytes differ/);
});

test('A_new_row_is_free_only_when_its_definition_stands_in_a_daemon_web_part', () => {
  const row = '| F-TIMING | daemon, web | section 2, in an R | `{D}` |\n';
  const withRow = edit(FORMAT_TEXT, '\nText after the table.', `${row}\nText after the table.`);
  assertRefused(compare(withRow), '2.1 How they are used', 'common', /F-TIMING/);
  const defined = edit(withRow, 'The defaults T and G.', 'F-TIMING. The slot `{D}` is a duration.\n\n```text\nThe default is changed by {D}.\n```');
  assertPasses(compare(defined));
  const introduced = edit(withRow, 'The defaults T and G.', 'The defaults T and G.\n\n- A task that needs another value adds F-TIMING in the same requirement:\n\n  ```text\n  The default is changed by {D}.\n  ```');
  assertPasses(compare(introduced));
  const inProtected = edit(withRow, 'Standard error is not compared.', 'F-TIMING. Standard error is not compared.');
  assertRefused(compare(inProtected), '2.1 How they are used', 'common', /F-TIMING/);
});

test('A_sentence_with_a_label_is_defined_where_its_label_stands_not_where_another_sentence_names_it', () => {
  const named = edit(FORMAT_TEXT, 'F-ENV, the base environment:', 'F-ENV, the base environment (it follows F-READY in the same requirement):');
  const row = '| F-READY | daemon, web | section 2, in an R | `{T}` |';
  assertPasses(compare(edit(named, row, `${row} changed`), named));
  assertRefused(compare(edit(named, '| F-STDERR | all | section 3, in an R | none |', '| F-STDERR | all | section 3, in an R | none | changed |'), named), '2.1 How they are used', 'common', /F-STDERR/);
});

test('A_frozen_file_that_is_not_the_recorded_reference_is_refused_before_anything_is_compared', () => {
  const result = checkFrozenCandidate({ frozen: Buffer.from(FORMAT_TEXT), candidate: Buffer.from(FORMAT_TEXT), reference: sha256('another file') });
  assert.equal(result.exit, 1);
  assert.match(result.lines.join('\n'), /the frozen reference is not A's recorded FORMAT/);
});

test('A_format_error_in_either_file_is_exit_4', () => {
  const crlf = edit(FORMAT_TEXT, 'Samples are pairs of files.\n', 'Samples are pairs of files.\r\n');
  assert.equal(compare(crlf).exit, 4);
  assert.equal(compare(edit(FORMAT_TEXT, '### 2.4 Daemon and web', '### 2.5 Daemon and web')).exit, 4);
  assert.equal(compare(FORMAT_TEXT, crlf).exit, 4, 'the frozen file is read the same way');
  assert.equal(checkFrozenCandidate({ frozen: Buffer.from(crlf), candidate: Buffer.from(FORMAT_TEXT), reference: sha256(Buffer.from(crlf)) }).exit, 4);
});

test('The_real_draft_against_itself_passes_and_the_class_rule_reads_it_as_the_synthetic_one_is_read', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const run = candidate => checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(candidate), reference: draft.recorded });
  assertPasses(run(real));
  assertPasses(run(real.replace('Gate verdict: not yet gated', 'Gate verdict: passed')));
  assertPasses(run(`${real}- v2.1 · 2026-12-01 · amendment · touches all — a later line.\n`));
  assertPasses(run(edit(real, 'One test lasts at most 600 seconds', 'One test lasts at most 700 seconds')));
  assertRefused(run(edit(real, 'Samples are small and clean examples of the', 'Samples are small and dirty examples of the')), '3.1 Files and ids', 'common', /protected bytes differ/);
  assertRefused(run(edit(real, '| F-STDERR | all | section 3, in an R | none |', '| F-STDERR | all | section 4, in an R | none |')), '2.1 How they are used', 'common', /F-STDERR/);
  assertPasses(run(edit(real, '| F-READY | daemon, web |', '| F-READY | daemon, web, others |')));
  assertPasses(run(edit(real, '| F-PORT | daemon and web that listen on a port |', '| F-PORT | daemon that listens on a port |')));
  assertPasses(run(edit(real, '| F-RUN-D | daemon, web |', '| F-RUN-D | daemon, web, others |')));
  assertPasses(run(edit(real, '| F-TIMING | daemon, web whose', '| F-TIMING | daemon, web with a T or G that is not the series default, whose')));
  assertRefused(run(edit(real, '| F-RUN-O | olympiad |', '| F-RUN-O | olympiad, others |')), '2.1 How they are used', 'common', /F-RUN-O/);
  assertRefused(run(edit(real, '| F-ERRORS | all, instead', '| F-ERRORS | all, rather than')), '2.1 How they are used', 'common', /F-ERRORS/);
});
