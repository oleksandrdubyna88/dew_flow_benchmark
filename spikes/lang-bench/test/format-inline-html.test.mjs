// Fix batch 4 (E1.S9), the fourth review's HIGH finding: raw INLINE HTML acts at a distance. `Read on <details>` in an unprotected part leaves everything after it inside a closed
// `<details>` on a rendered page (`<style>`, `<plaintext>`, `<s>`, `<a href=…>` likewise, from a paragraph or a list item). The rule, for the WHOLE FILE and outside fenced blocks:
// no `<` followed by a letter, `/`, `!` or `?` outside a code span and not written `\<` (autolinks included). Code spans follow CommonMark's rule of equal backtick runs over a
// paragraph (a span may pair across lines); a table row is scanned cell by cell, a list item or a heading on its own. The cases are synthetic; the real draft is checked when it is there.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { htmlOpenings } from '../lib/format-grammar.mjs';
import { edit, FORMAT_TEXT, realDraft, sha256 } from './support/format-fixtures.mjs';

const compare = (candidate, base = FORMAT_TEXT) => {
  const frozen = Buffer.from(base);
  return checkFrozenCandidate({ frozen, candidate: Buffer.from(candidate), reference: sha256(frozen) });
};
const joined = result => result.lines.join('\n');
const lineOf = (text, content) => text.split('\n').indexOf(content) + 1;
const READY = '| F-READY | daemon, web | section 2, in an R | `{T}` |';
const in33 = body => edit(FORMAT_TEXT, 'Scenario pairs.\n', `Scenario pairs.\n\n${body}\n`);
const before26 = body => edit(FORMAT_TEXT, 'The defaults T and G.\n\n### 2.6', `The defaults T and G.\n\n${body}\n\n### 2.6`);
const appended = body => `${FORMAT_TEXT}\n${body}\n`;

/** The refusal is exit 1, names the planted line and says it is inline HTML. */
function assertInlineRefused(text, content) {
  const result = compare(text);
  assert.equal(result.exit, 1, joined(result));
  const line = lineOf(text, content);
  assert.ok(line > 0, `the planted line ${JSON.stringify(content)} is in the text`);
  assert.ok(result.lines.some(entry => new RegExp(`line ${line}\\b`).test(entry) && /inline HTML/.test(entry)), `a line names line ${line} and inline HTML; the lines were:\n${joined(result)}`);
}
const assertPasses = result => assert.equal(result.exit, 0, joined(result));

const TAGS = [
  ['an_opening_tag', '<details>'], ['a_style_tag', '<style>'], ['a_plaintext_tag', '<plaintext>'], ['a_strike_tag', '<s>'], ['a_link_tag_with_an_attribute', '<a href="https://example.invalid/">'],
  ['a_closing_tag', '</s>'], ['a_comment_opener', '<!-- hide'], ['a_declaration', '<!DOCTYPE x>'], ['a_processing_instruction', '<?x'], ['an_autolink', '<https://example.invalid/>'],
  ['an_email_autolink', '<someone@example.invalid>'], ['a_tag_at_the_end_of_the_line', 'tail <del>'],
];

for (const [name, tag] of TAGS) {
  test(`Inline_HTML_${name}_in_a_paragraph_a_list_item_a_free_row_a_retitled_heading_or_the_appended_changelog_is_refused`, () => {
    const paragraph = `Read on ${tag}`;
    assertInlineRefused(in33(paragraph), paragraph);
    assertInlineRefused(before26(paragraph), paragraph);
    const item = `- an item ${tag}`;
    assertInlineRefused(in33(item), item);
    const row = READY.replace('section 2, in an R', `section 2, in an R ${tag}`);
    assertInlineRefused(edit(FORMAT_TEXT, READY, row), row);
    const heading = `### 2.4 Daemon and web ${tag}`;
    assertInlineRefused(edit(edit(FORMAT_TEXT, '### 2.4 Daemon and web\n', `${heading}\n`), '| 2.4 Daemon and web | daemon-web |', `| 2.4 Daemon and web ${tag} | daemon-web |`), heading);
    const entry = `- v3 · 2026-12-01 · amendment · a later line ${tag}.`;
    assertInlineRefused(appended(entry), entry);
  });
}

test('A_tag_in_a_protected_part_of_the_candidate_is_refused_too_the_rule_is_for_the_whole_file', () => {
  const text = edit(FORMAT_TEXT, 'The file name and the header table.', 'The file name and the header table. Read on <details>');
  const result = compare(text);
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /1\. The spec file \(class common\).*inline HTML/);
});

test('A_tag_in_a_table_cell_after_a_backtick_in_another_cell_is_refused_a_span_does_not_cross_cells', () => {
  const row = READY.replace('section 2, in an R', 'section 2, `in an R').replace('`{T}`', '<s> `{T}`');
  const result = compare(edit(FORMAT_TEXT, READY, row));
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /inline HTML/);
});

test('A_tag_between_two_backticks_of_different_list_items_or_headings_is_refused_a_span_does_not_cross_blocks', () => {
  const items = '- a ` <s>\n- b `';
  assertInlineRefused(in33(items), '- a ` <s>');
  const heading = 'A paragraph with a ` and\n- an item <s> then `';
  assertInlineRefused(in33(heading), '- an item <s> then `');
});

test('A_code_span_hides_a_tag_only_by_the_rule_of_equal_backtick_runs', () => {
  assertPasses(compare(in33('Write `<id>` in a code span.')));
  assertPasses(compare(in33('Write ``<id> and `<b>` `` in a longer span.')));
  assertPasses(compare(in33('Write `<id>` and then `<size>` again.')));
  assertInlineRefused(in33('Write `<id> with a span that is not closed.'), 'Write `<id> with a span that is not closed.');
  assertInlineRefused(in33('Write `<id>`` with runs that differ.'), 'Write `<id>`` with runs that differ.');
  assertInlineRefused(in33('Write ``<id>` with runs that differ.'), 'Write ``<id>` with runs that differ.');
  assertInlineRefused(in33('Write `a` and then <id> outside.'), 'Write `a` and then <id> outside.');
});

test('A_code_span_pairs_across_the_lines_of_one_paragraph_and_a_list_item_continuation', () => {
  assertPasses(compare(in33('A span that `starts here\nand <id> ends` there.')));
  assertPasses(compare(in33('- an item with a `span\n  over <id> two` lines')));
  assertInlineRefused(in33('A span that `starts here\nand ends` there <s>.'), 'and ends` there <s>.');
  assertInlineRefused(in33('A span `opens here\n\nand <s> closes` after a blank line.'), 'and <s> closes` after a blank line.');
});

test('A_span_does_not_pair_across_a_fenced_block_or_from_a_heading_line_to_the_next_line', () => {
  assertInlineRefused(in33('A span `opens here <s>\n```text\ncode\n```\nand closes` after a fence.'), 'A span `opens here <s>');
  const heading = '### 2.7 New ` <s>';
  const text = edit(edit(FORMAT_TEXT, 'The closed list.\n\n## 3.', `The closed list.\n\n${heading}\ntext after the heading \`\n\n## 3.`), '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 2.7 New ` <s> | daemon-web |\n');
  assertInlineRefused(text, heading);
  const afterRow = in33('| a | b |\nA row, then `text <s>\nand closes` here.');
  assertInlineRefused(afterRow, 'A row, then `text <s>');
  const sandwich = '### 2.7 New <s> `';
  const between = edit(edit(FORMAT_TEXT, 'The closed list.\n\n## 3.', `The closed list.\n\nA line before \`\n${sandwich}\ntext after the heading\n\n## 3.`), '| 7. Changelog | common |\n', '| 7. Changelog | common |\n| 2.7 New <s> ` | daemon-web |\n');
  assertInlineRefused(between, sandwich);
  const beforeRow = in33('A line, then `text <s>\n| a ` | b |');
  assertInlineRefused(beforeRow, 'A line, then `text <s>');
  assertInlineRefused(in33('| a ` | b <s> |\n| c ` | d |'), '| a ` | b <s> |');
  assertInlineRefused(in33('| a ` | b ` <s> ` c |'), '| a ` | b ` <s> ` c |');
  assertInlineRefused(in33('| a | b |\n|---|---|\n| a ` | b ` <s> ` c |'), '| a ` | b ` <s> ` c |');
});

test('A_backslash_escapes_the_angle_bracket_and_an_escaped_backslash_does_not', () => {
  assertPasses(compare(in33('Write \\<details> in prose.')));
  assertInlineRefused(in33('Write \\\\<details> in prose.'), 'Write \\\\<details> in prose.');
  assertInlineRefused(in33('Write \\`<id>\\` with escaped backticks: no span.'), 'Write \\`<id>\\` with escaped backticks: no span.');
});

test('A_less_than_sign_that_starts_no_tag_is_text', () => {
  assertPasses(compare(in33('Five is < six, 4 <5 and a <3 heart; "<" alone; x< y.')));
});

test('The_opening_scan_is_exact_on_a_few_plain_strings', () => {
  assert.deepEqual(htmlOpenings('a <b> c'), [2]);
  assert.deepEqual(htmlOpenings('`<b>`'), []);
  assert.deepEqual(htmlOpenings('``a`<b>``'), []);
  assert.deepEqual(htmlOpenings('`<b>``'), [1]);
  assert.deepEqual(htmlOpenings('\\<b> \\\\<c>'), [7]);
  assert.deepEqual(htmlOpenings('</x> <!-- <?p <3'), [0, 5, 10]);
});

test('A_pipe_that_a_backslash_escapes_keeps_a_span_in_one_cell_and_an_even_run_of_backslashes_cuts_it', () => {
  const row = READY.replace('section 2, in an R', 'section 2, `a\\|<id>` in an R');
  assertPasses(compare(edit(FORMAT_TEXT, READY, row)));
  const cut = READY.replace('section 2, in an R', 'section 2, `a\\\\|<id>` in an R');
  assertInlineRefused(edit(FORMAT_TEXT, READY, cut), cut);
});

test('Fenced_blocks_are_left_alone_by_the_inline_rule', () => {
  assertPasses(compare(in33('```text\nRead on <details> and <style>\n```')));
});

test('The_real_draft_passes_the_inline_rule_as_frozen_and_candidate_and_a_planted_tag_in_a_paragraph_a_list_item_and_the_changelog_is_refused', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const run = candidate => checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(candidate), reference: draft.recorded });
  assertPasses(run(real));
  const at = real.indexOf('### 2.6 ');
  for (const [name, plant] of [['paragraph', 'Read on <details>\n\n'], ['list item', '- an item <s>\n\n']]) {
    const result = run(real.slice(0, at) + plant + real.slice(at));
    assert.equal(result.exit, 1, `${name}: ${joined(result)}`);
    assert.match(joined(result), /inline HTML/, name);
  }
  const result = run(`${real}\n- v3 · 2026-12-01 · amendment · a later line <del>.\n`);
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /inline HTML/);
});
