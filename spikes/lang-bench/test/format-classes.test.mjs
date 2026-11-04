// T-L56 (E1.S9): tools/format-classes.mjs as a command. `--self <FORMAT>` checks the document's own class table; `--frozen <A> --candidate <B>` checks that the protected
// parts did not change (the cases of the comparison are in format-classes-frozen.test.mjs). Exit 0 passes, 1 refuses (one line per refusal), 4 is usage or a format error.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { test } from 'node:test';
import { edit, folder, FORMAT_TEXT, put, realDraft, recordFor, sha256, TOOLS } from './support/format-fixtures.mjs';

const TOOL = path.join(TOOLS, 'format-classes.mjs');
const run = args => spawnSync(process.execPath, [TOOL, ...args], { encoding: 'utf8', timeout: 60_000 });
const lineOf = (text, needle) => text.split('\n').findIndex(line => line === needle) + 1;

/** `--self` over `text` in an owned folder. */
function selfCheck(t, text) {
  return run(['--self', put(folder(t, 'classes'), 'FORMAT.md', text)]);
}

test('A_document_whose_class_table_is_complete_passes_self_check', t => {
  const result = selfCheck(t, FORMAT_TEXT);
  assert.equal(result.status, 0, result.stderr);
});

const SELF_REFUSALS = [
  ['a_row_that_names_no_heading', text => edit(text, '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 2.9 Missing | common |\n'), '| 2.9 Missing | common |', /2\.9 Missing.*no such heading|no such heading.*2\.9 Missing/],
  ['a_row_that_names_a_heading_only_in_part', text => edit(text, '| 1. The spec file | common |', '| 1. The spec | common |'), '| 1. The spec | common |', /1\. The spec.*no such heading/],
  ['a_row_that_names_a_heading_inside_a_fence', text => edit(text, '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 9. inside a fence, not a section | common |\n'), '| 9. inside a fence, not a section | common |', /no such heading/],
  ['a_class_that_is_not_one_of_the_five', text => edit(text, '| 1. The spec file | common |', '| 1. The spec file | commons |'), '| 1. The spec file | commons |', /class `commons`/],
  ['a_section_with_no_row', text => edit(text, '| 4. The scenario script | daemon-web |\n', ''), null, /4\. The scenario script.*no row/],
  ['a_subsection_of_a_mixed_section_with_no_row', text => edit(text, '| 2.5 The timing contract | daemon-web |\n', ''), null, /2\.5 The timing contract.*no row/],
  ['a_second_row_for_one_heading', text => edit(text, '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 1. The spec file | common |\n'), '| 1. The spec file | common |', /1\. The spec file.*more than one row/],
  ['a_section_row_beside_the_rows_of_its_subsections', text => edit(text, '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 2. The fixed sentences | common |\n'), null, /2\. The fixed sentences.*row of its own.*2\.1 How they are used/],
  ['a_section_row_beside_a_subsection_row', text => edit(text, '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 1.1 Name and title | common |\n'), null, /1\. The spec file.*row of its own.*1\.1 Name and title/],
  ['a_row_that_is_not_a_two_cell_row', text => edit(text, '| 1. The spec file | common |', '| 1. The spec file | common | extra |'), '| 1. The spec file | common | extra |', /not a `\| heading \| class \|` row|class `extra`/],
];
for (const [name, plant, rowText, rule] of SELF_REFUSALS) {
  test(`Self_check_refuses_${name}_naming_the_heading_the_line_and_the_rule`, t => {
    const text = plant(FORMAT_TEXT);
    const result = selfCheck(t, text);
    assert.equal(result.status, 1, result.stderr);
    assert.match(result.stderr, rule);
    if (rowText === null) return;
    const line = text.split('\n').findLastIndex(candidate => candidate === rowText) + 1;
    assert.ok(line > 0, 'the planted row is in the text');
    assert.match(result.stderr, new RegExp(`line ${line}\\b`), `the refusal names line ${line}`);
  });
}

test('Self_check_reads_the_table_directly_after_the_section_classes_paragraph_and_nowhere_else', t => {
  assert.equal(selfCheck(t, edit(FORMAT_TEXT, '**Section classes.**', '**Section kinds.**')).status, 4);
  assert.equal(selfCheck(t, edit(FORMAT_TEXT, '`daemon-web`.\n\n| Section | Class |', '`daemon-web`.\n\nAn interposed paragraph.\n\n| Section | Class |')).status, 4);
  assert.equal(selfCheck(t, edit(FORMAT_TEXT, '| Section | Class |\n|---|---|\n', '')).status, 4);
});

test('Only_a_classes_paragraph_in_the_preamble_counts_and_there_must_be_exactly_one', t => {
  const body = edit(FORMAT_TEXT, 'The file name and the header table.', 'The file name and the header table.\n\n**Section classes.** A look-alike in a section.');
  assert.equal(selfCheck(t, body).status, 0, 'a paragraph after the preamble is not the class paragraph');
  const twice = edit(FORMAT_TEXT, '| 7. Changelog | common |\n\n## 1.', '| 7. Changelog | common |\n\n**Section classes.** Again.\n\n## 1.');
  assert.equal(selfCheck(t, twice).status, 4, 'two class paragraphs in the preamble are not one table');
});

test('Self_check_reports_a_format_error_as_exit_4', t => {
  assert.equal(selfCheck(t, edit(FORMAT_TEXT, '### 2.4 Daemon and web', '### 2.5 Daemon and web')).status, 4);
  assert.equal(selfCheck(t, edit(FORMAT_TEXT, 'Samples are pairs of files.\n', 'Samples are pairs of files.\r\n')).status, 4);
});

test('Self_check_ignores_a_class_table_row_look_alike_after_the_table', t => {
  const planted = edit(FORMAT_TEXT, 'The changelog only grows.\n', 'The changelog only grows.\n\n| 9. After the table | olympiad |\n');
  assert.equal(selfCheck(t, planted).status, 0);
});

test('The_command_wants_one_mode_and_refuses_every_other_argument_shape_as_exit_4', t => {
  const file = put(folder(t, 'classes-usage'), 'FORMAT.md', FORMAT_TEXT);
  for (const args of [[], ['--self'], ['--self', file, '--frozen', file, '--candidate', file], ['--frozen', file], ['--candidate', file], ['--self', file, 'extra'], ['--bogus'], ['--frozen', file, '--candidate', file, '--reference-sha', sha256(Buffer.from(FORMAT_TEXT))]]) {
    const result = run(args);
    assert.equal(result.status, 4, `${args.join(' ')}: ${result.stderr}`);
  }
});

test('The_frozen_comparison_through_the_command_uses_the_record_and_prints_one_line_per_refusal', t => {
  const dir = folder(t, 'classes-cli');
  const frozen = put(dir, 'A.md', FORMAT_TEXT);
  const record = recordFor(dir, Buffer.from(FORMAT_TEXT));
  const same = run(['--frozen', frozen, '--candidate', frozen, '--record', record]);
  assert.equal(same.status, 0, same.stderr);
  const edited = edit(FORMAT_TEXT, 'Olympiad sentences.', 'Olympiad sentences, changed.');
  const candidate = put(dir, 'B.md', edited);
  const refused = run(['--frozen', frozen, '--candidate', candidate, '--record', record]);
  assert.equal(refused.status, 1);
  const lines = refused.stderr.trim().split('\n');
  assert.equal(lines.length, 1, 'one refusal, one line');
  assert.match(lines[0], new RegExp(`2\\.3 Olympiad.*class olympiad.*line ${lineOf(edited, '### 2.3 Olympiad')}\\b`));
});

test('A_frozen_file_that_is_not_the_one_the_record_names_is_refused_with_exit_1', t => {
  const dir = folder(t, 'classes-reference');
  const other = put(dir, 'A.md', edit(FORMAT_TEXT, 'Olympiad sentences.', 'Olympiad sentences, another.'));
  const record = recordFor(dir, Buffer.from(FORMAT_TEXT));
  const result = run(['--frozen', other, '--candidate', other, '--record', record]);
  assert.equal(result.status, 1);
  assert.match(result.stderr, /the frozen reference is not A's recorded FORMAT/);
});

test('A_record_that_is_missing_or_has_no_A_sha256_is_exit_4', t => {
  const dir = folder(t, 'classes-record');
  const frozen = put(dir, 'A.md', FORMAT_TEXT);
  assert.equal(run(['--frozen', frozen, '--candidate', frozen, '--record', path.join(dir, 'absent.json')]).status, 4);
  const empty = put(dir, 'empty.json', '{ "A": {} }');
  assert.equal(run(['--frozen', frozen, '--candidate', frozen, '--record', empty]).status, 4);
  const notHex = put(dir, 'nothex.json', '{ "A": { "sha256": "abc" } }');
  assert.equal(run(['--frozen', frozen, '--candidate', frozen, '--record', notHex]).status, 4);
  const broken = put(dir, 'broken.json', '{nope');
  assert.equal(run(['--frozen', frozen, '--candidate', frozen, '--record', broken]).status, 4);
});

test('The_real_FORMAT_draft_passes_self_check_and_the_frozen_comparison_with_itself', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const file = put(folder(t, 'classes-real'), 'FORMAT.md', draft.bytes);
  const self = run(['--self', file]);
  assert.equal(self.status, 0, self.stderr);
  const same = run(['--frozen', file, '--candidate', file, '--record', draft.record]);
  assert.equal(same.status, 0, same.stderr);
});
