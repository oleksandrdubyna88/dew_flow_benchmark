// tools/grammar-extract.mjs (E1.S9): FORMAT.md's sections 3 to 5, copied byte for byte under one header line, for every workspace's runner/GRAMMAR.md.
// Also the shared reading of FORMAT's bytes and headings that both FORMAT tools rely on: LF only, no byte order mark, headings outside fences, sections in order.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, linkSync, readdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../tools/grammar-extract.mjs';
import { edit, folder, FORMAT_TEXT, put, realDraft, sha256, TOOLS } from './support/format-fixtures.mjs';

const TOOL = path.join(TOOLS, 'grammar-extract.mjs');
const run = args => spawnSync(process.execPath, [TOOL, ...args], { encoding: 'utf8', timeout: 60_000 });
const lineOf = (text, needle) => text.split('\n').findIndex(line => line === needle) + 1;

/** Runs the extractor over `content` (text or bytes) in an owned folder: `{ result, out, dir }`; `out` is the output file's bytes, or null when none was written. */
function extract(t, content) {
  const dir = folder(t, 'grammar');
  const file = put(dir, 'FORMAT.md', content);
  const out = path.join(dir, 'GRAMMAR.md');
  const result = run([file, out]);
  return { result, out: existsSync(out) ? readFileSync(out) : null, dir };
}

const headerFor = (bytes, version = '2') => `# Scenario and sample grammar (from FORMAT.md v${version}, sha256 ${sha256(bytes)})\n\n`;
const middle = text => text.slice(text.indexOf('## 3. Visible'), text.indexOf('## 6. Worked'));

test('GRAMMAR_md_is_the_verbatim_sections_of_FORMAT', t => {
  const bytes = Buffer.from(FORMAT_TEXT);
  const { result, out } = extract(t, bytes);
  assert.equal(result.status, 0, result.stderr);
  assert.equal(out.toString('utf8'), headerFor(bytes) + middle(FORMAT_TEXT));
  assert.ok(middle(FORMAT_TEXT).startsWith('## 3. Visible samples\n') && middle(FORMAT_TEXT).includes('### 5.1 Entries'), 'the slice is sections 3 to 5');
});

test('The_header_carries_the_version_of_the_header_row_and_the_sha256_of_the_raw_bytes', t => {
  const text = edit(FORMAT_TEXT, 'Format version: 2\n', 'Format version: 2.1\n');
  const { out } = extract(t, text);
  assert.ok(out.toString('utf8').startsWith(headerFor(Buffer.from(text), '2.1')));
});

test('An_edit_of_section_4_changes_the_output', t => {
  const before = extract(t, FORMAT_TEXT).out.toString('utf8');
  const after = extract(t, edit(FORMAT_TEXT, 'Strings and escapes.', 'Strings, escapes and more.')).out.toString('utf8');
  assert.notEqual(after.slice(after.indexOf('\n\n')), before.slice(before.indexOf('\n\n')), 'the copied sections differ');
  assert.ok(after.includes('Strings, escapes and more.'));
});

test('An_edit_of_section_7_changes_only_the_sha_in_the_header', t => {
  const edited = edit(FORMAT_TEXT, 'the second line.', 'the second line, longer.');
  const before = extract(t, FORMAT_TEXT).out.toString('utf8');
  const after = extract(t, edited).out.toString('utf8');
  assert.notEqual(after, before);
  assert.equal(after.slice(after.indexOf('\n\n')), before.slice(before.indexOf('\n\n')), 'everything after the header line is the same bytes');
  assert.ok(after.startsWith(headerFor(Buffer.from(edited))));
});

test('The_output_is_the_same_bytes_on_every_run', t => {
  const first = extract(t, FORMAT_TEXT);
  assert.equal(first.result.status, 0, first.result.stderr);
  assert.ok(first.out !== null && first.out.length > 0, 'an output was written');
  assert.deepEqual(extract(t, FORMAT_TEXT).out, first.out);
});

test('A_hash_comment_or_a_heading_lookalike_inside_a_fence_is_never_a_section_boundary', t => {
  const planted = edit(FORMAT_TEXT, 'The steps.\n', 'The steps.\n\n```text\n# 1. a comment\n## 6. not a section\n```\n');
  const { result, out } = extract(t, planted);
  assert.equal(result.status, 0, result.stderr);
  assert.ok(out.toString('utf8').includes('## 6. not a section\n```'), 'the fence is copied whole, the cut is at the real `## 6.`');
  assert.ok(!out.toString('utf8').includes('## 6. Worked examples'));
});

test('A_tilde_fence_and_a_longer_fence_hold_heading_lookalikes_too', t => {
  const tilde = extract(t, edit(FORMAT_TEXT, 'The steps.\n', 'The steps.\n\n~~~\n## 8. inside tildes\n~~~\n'));
  assert.equal(tilde.result.status, 0, tilde.result.stderr);
  assert.ok(tilde.out.toString('utf8').includes('## 8. inside tildes\n~~~\n'));
  const longer = extract(t, edit(FORMAT_TEXT, 'The steps.\n', 'The steps.\n\n````\n```\n## 8. inside the longer fence\n```\n````\n'));
  assert.equal(longer.result.status, 0, longer.result.stderr);
  assert.ok(longer.out.toString('utf8').includes('## 8. inside the longer fence\n```\n````\n'));
});

test('A_duplicated_section_6_is_a_format_error_naming_the_heading_and_its_line', t => {
  const planted = edit(FORMAT_TEXT, '## 7. Changelog', '## 6. Worked again\n\nText.\n\n## 7. Changelog');
  const { result, out } = extract(t, planted);
  assert.equal(result.status, 4);
  assert.match(result.stderr, /## 6\. Worked again/);
  assert.match(result.stderr, new RegExp(`line ${lineOf(planted, '## 6. Worked again')}\\b`));
  assert.equal(out, null, 'nothing is written');
});

test('A_section_6_before_section_3_is_a_format_error', t => {
  const planted = edit(edit(FORMAT_TEXT, '## 3. Visible samples', '## 6. Early'), '## 6. Worked examples', '## 3. Visible samples');
  const { result } = extract(t, planted);
  assert.equal(result.status, 4);
  assert.match(result.stderr, /## 6\. Early/);
});

test('A_gap_in_the_subsections_is_a_format_error_naming_the_heading_and_its_line', t => {
  const planted = edit(FORMAT_TEXT, '### 2.4 Daemon and web', '### 2.5 Daemon and web');
  const { result } = extract(t, planted);
  assert.equal(result.status, 4);
  assert.match(result.stderr, /### 2\.5 Daemon and web/);
  assert.match(result.stderr, new RegExp(`line ${lineOf(planted, '### 2.5 Daemon and web')}\\b`));
});

test('A_missing_section_an_eighth_section_and_a_subsection_before_section_1_are_format_errors', t => {
  assert.equal(extract(t, edit(FORMAT_TEXT, '## 7. Changelog', '## 8. Changelog')).result.status, 4);
  assert.equal(extract(t, `${FORMAT_TEXT}\n## 8. More\n`).result.status, 4);
  assert.equal(extract(t, edit(FORMAT_TEXT, '## 1. The spec file', '### 0.1 Early\n\n## 1. The spec file')).result.status, 4);
  assert.equal(extract(t, FORMAT_TEXT.slice(0, FORMAT_TEXT.indexOf('## 7. Changelog'))).result.status, 4);
});

test('A_carriage_return_anywhere_is_a_format_error_naming_its_line', t => {
  const planted = edit(FORMAT_TEXT, 'Strings and escapes.\n', 'Strings and escapes.\r\n');
  const { result, out } = extract(t, planted);
  assert.equal(result.status, 4);
  assert.match(result.stderr, new RegExp(`line ${lineOf(planted.replaceAll('\r', ''), 'Strings and escapes.')}\\b`));
  assert.match(result.stderr, /carriage return/);
  assert.equal(out, null);
});

test('A_byte_order_mark_and_bytes_that_are_not_UTF_8_are_format_errors', t => {
  const bom = extract(t, Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from(FORMAT_TEXT)]));
  assert.equal(bom.result.status, 4);
  assert.match(bom.result.stderr, /byte order mark/);
  const bad = extract(t, Buffer.concat([Buffer.from(FORMAT_TEXT), Buffer.from([0xff, 0x0a])]));
  assert.equal(bad.result.status, 4);
  assert.match(bad.result.stderr, /UTF-8/);
});

test('The_extractor_refuses_to_overwrite_and_leaves_no_temporary_file_behind', t => {
  const dir = folder(t, 'grammar-out');
  const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
  const out = put(dir, 'GRAMMAR.md', 'keep me');
  const again = run([file, out]);
  assert.equal(again.status, 4);
  assert.equal(readFileSync(out, 'utf8'), 'keep me');
  assert.deepEqual(readdirSync(dir).sort(), ['FORMAT.md', 'GRAMMAR.md']);
  const fresh = path.join(dir, 'fresh.md');
  assert.equal(run([file, fresh]).status, 0);
  assert.deepEqual(readdirSync(dir).sort(), ['FORMAT.md', 'GRAMMAR.md', 'fresh.md'], 'only the output remains');
});

test('The_extractor_wants_two_arguments_and_a_FORMAT_that_exists', t => {
  const dir = folder(t, 'grammar-usage');
  assert.equal(run([]).status, 4);
  assert.equal(run([path.join(dir, 'FORMAT.md')]).status, 4);
  assert.equal(run([path.join(dir, 'missing.md'), path.join(dir, 'out.md')]).status, 4);
  assert.equal(existsSync(path.join(dir, 'out.md')), false);
  const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
  assert.equal(run([file, path.join(dir, 'out.md'), 'extra']).status, 4, 'a third argument is refused');
  assert.equal(run([file, path.join(dir, 'out.md'), '--bogus']).status, 4);
  assert.equal(existsSync(path.join(dir, 'out.md')), false, 'nothing is written for a refused command line');
});

test('The_real_FORMAT_draft_gives_its_sections_3_to_5_verbatim', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const { result, out } = extract(t, draft.bytes);
  assert.equal(result.status, 0, result.stderr);
  const text = draft.bytes.toString('utf8');
  assert.equal(text.split('\n## 6. ').length, 2, 'one `## 6.` heading line in the draft');
  const body = text.slice(text.indexOf('\n## 3. ') + 1, text.indexOf('\n## 6. ') + 1);
  assert.equal(out.toString('utf8'), headerFor(draft.bytes, '2') + body);
});

// Removing the temporary file: only "it was never made" (ENOENT) is silent; any other error is on stderr, and the exit code stays the write's.
const REAL_OPS = { writeFileSync, linkSync, unlinkSync };
const failing = code => () => {
  throw Object.assign(new Error(`${code}: the stand-in refuses`), { code });
};
const capture = () => ({ text: '', write(chunk) { this.text += chunk; } });

test('A_temporary_file_that_cannot_be_removed_is_reported_on_stderr_and_the_exit_code_stays_the_writes', t => {
  const dir = folder(t, 'grammar-unlink');
  const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
  const stderr = capture();
  assert.equal(main([file, path.join(dir, 'GRAMMAR.md')], stderr, { ...REAL_OPS, unlinkSync: failing('EPERM') }), 0, 'the write succeeded');
  assert.match(stderr.text, /EPERM/);
  assert.match(stderr.text, /temporary/);
  assert.equal(existsSync(path.join(dir, 'GRAMMAR.md')), true);
});

test('A_failed_write_and_a_temporary_file_that_cannot_be_removed_are_both_reported_and_the_exit_code_is_the_writes', t => {
  const dir = folder(t, 'grammar-unlink-both');
  const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
  const stderr = capture();
  assert.equal(main([file, path.join(dir, 'GRAMMAR.md')], stderr, { ...REAL_OPS, linkSync: failing('EACCES'), unlinkSync: failing('EBUSY') }), 4);
  assert.match(stderr.text, /EACCES/);
  assert.match(stderr.text, /EBUSY/);
});

test('A_temporary_file_that_was_never_made_is_not_reported', t => {
  const dir = folder(t, 'grammar-enoent');
  const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
  const stderr = capture();
  assert.equal(main([file, path.join(dir, 'GRAMMAR.md')], stderr, { ...REAL_OPS, writeFileSync: failing('ENOENT'), unlinkSync: failing('ENOENT') }), 4);
  assert.doesNotMatch(stderr.text, /could not be removed/);
  assert.match(stderr.text, /cannot be written/);
});

test('The_record_names_the_sha256_of_the_extractors_own_output_for_the_recorded_FORMAT', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const { result, out } = extract(t, draft.bytes);
  assert.equal(result.status, 0, result.stderr);
  const recorded = JSON.parse(readFileSync(draft.record, 'utf8')).A;
  assert.match(recorded.grammarSha256, /^[0-9a-f]{64}$/);
  assert.equal(sha256(out), recorded.grammarSha256, 'inputs/specs.d/format.json grammarSha256 is the sha256 of GRAMMAR.md as the extractor writes it');
});

// Fix batch 3: a wrong argument count is exit 4, and the temporary file is removed on every error path (a link() that fails with EPERM or EXDEV included).
test('A_wrong_argument_count_is_exit_4_with_a_usage_line_and_writes_nothing', t => {
  const dir = folder(t, 'grammar-argc');
  const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
  const stderr = capture();
  for (const argv of [[], [file], [file, path.join(dir, 'a.md'), path.join(dir, 'b.md')]]) assert.equal(main(argv, stderr, REAL_OPS), 4);
  assert.match(stderr.text, /usage/);
  assert.deepEqual(readdirSync(dir), ['FORMAT.md']);
});

test('The_temporary_file_is_removed_when_link_fails_with_EPERM_or_EXDEV_and_the_exit_code_is_the_writes', t => {
  for (const code of ['EPERM', 'EXDEV']) {
    const dir = folder(t, `grammar-link-${code}`);
    const file = put(dir, 'FORMAT.md', FORMAT_TEXT);
    const stderr = capture();
    assert.equal(main([file, path.join(dir, 'GRAMMAR.md')], stderr, { ...REAL_OPS, linkSync: failing(code) }), 4, code);
    assert.match(stderr.text, new RegExp(code));
    assert.deepEqual(readdirSync(dir), ['FORMAT.md'], `no temporary file stays after ${code}`);
  }
});
