// The restricted grammar (E1.S9, fix batch 2): every line of every unprotected part of a candidate is one of a few allowed forms, and no line of any part starts a link reference
// definition or an HTML block. The protected parts then render as they read, whatever a candidate does around them (a deletion can no longer turn a literal into a heading, a
// reference definition cannot act at a distance). Refusals are exit 1 naming the part, the line and the form; the frozen file that breaks the rules is exit 4.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { paragraphStartProblem, partRefusals } from '../lib/format-grammar.mjs';
import { edit, FORMAT_TEXT, realDraft, sha256 } from './support/format-fixtures.mjs';

const compare = (candidate, base = FORMAT_TEXT) => {
  const frozen = Buffer.from(base);
  return checkFrozenCandidate({ frozen, candidate: Buffer.from(candidate), reference: sha256(frozen) });
};
const GRAMMAR_SOURCE = fileURLToPath(new URL('../lib/format-grammar.mjs', import.meta.url));
const joined = result => result.lines.join('\n');
const lineOf = (text, content) => text.split('\n').indexOf(content) + 1;
const LAST_ROW = '| 7. Changelog | common |\n';
/** The text of the end of 2.5 (an unprotected part followed by the protected 2.6) with `body` planted there. */
const before26 = (body, base = FORMAT_TEXT) => edit(base, 'The defaults T and G.\n\n### 2.6', `The defaults T and G.\n\n${body}\n\n### 2.6`);
/** `body` planted in 3.3 (unprotected, the last subsection of section 3). */
const in33 = (body, base = FORMAT_TEXT) => edit(base, 'Scenario pairs.\n', `Scenario pairs.\n\n${body}\n`);

function assertRefused(text, content, form, base = FORMAT_TEXT) {
  const result = compare(text, base);
  assert.equal(result.exit, 1, joined(result));
  const line = lineOf(text, content);
  assert.ok(line > 0, `the planted line ${JSON.stringify(content)} is in the text`);
  const named = result.lines.filter(entry => new RegExp(`line ${line}\\b`).test(entry) && form.test(entry));
  assert.ok(named.length >= 1, `a line names line ${line} and ${form}; the lines were:\n${joined(result)}`);
  assert.match(named[0], /class (?:daemon|web|daemon-web)/, 'the refusal names the part and its class');
}
const assertPasses = result => assert.equal(result.exit, 0, joined(result));

// [name, planted body, the line that is refused, the form]
const REFUSED = [
  ['a_paragraph_line_that_starts_with_a_hash', '#### four hashes', '#### four hashes', /starts with `#`/],
  ['a_paragraph_line_that_starts_with_a_tag', '<b>bold</b> starts the line', '<b>bold</b> starts the line', /starts with `<`/],
  ['a_paragraph_line_that_starts_with_a_quote_marker', '> a quote', '> a quote', /starts with `>`/],
  ['a_paragraph_line_that_starts_with_a_bar_but_is_no_table_row', '| a bar and no end', '| a bar and no end', /starts with `\|`/],
  ['a_paragraph_line_that_starts_with_a_tilde', '~~~ a tilde fence opener', '~~~ a tilde fence opener', /starts with `~`/],
  ['a_paragraph_line_that_starts_with_an_equals_sign', '===', '===', /starts with `=`/],
  ['a_paragraph_line_that_starts_with_a_dash_and_no_space', '-x', '-x', /starts with `-`/],
  ['a_paragraph_line_that_is_a_dash_line', '--------', '--------', /starts with `-`/],
  ['a_paragraph_line_that_starts_with_a_plus', '+ item', '+ item', /starts with `\+`/],
  ['a_paragraph_line_that_starts_with_an_underscore', '___', '___', /starts with `_`/],
  ['a_paragraph_line_that_starts_with_a_star_and_a_space', '* item', '* item', /starts with `\*`/],
  ['a_paragraph_line_that_starts_with_three_stars', '***', '***', /starts with `\*`/],
  ['a_paragraph_line_that_starts_with_three_backticks_and_a_blank', '``` text', '``` text', /three backticks/],
  ['a_paragraph_line_that_starts_with_a_square_bracket_and_no_close', '[never closed', '[never closed', /starts with `\[`/],
  ['a_paragraph_line_that_is_a_reference_definition', '[target]: /changed "Changed"', '[target]: /changed "Changed"', /starts with `\[`/],
  ['a_paragraph_line_that_starts_with_a_closing_square_bracket', ']: /continues a label', ']: /continues a label', /starts with `\]`/],
  ['a_paragraph_line_that_starts_with_a_space', ' an indented line', ' an indented line', /indented line/],
  ['a_paragraph_line_that_starts_with_a_tab', '\ta tabbed line', '\ta tabbed line', /starts with a space or a tab/],
  ['a_paragraph_line_that_starts_with_an_ordered_marker_with_a_paren', '1) item', '1) item', /ordered list marker/],
  ['a_paragraph_line_that_starts_with_a_long_ordered_marker', '1234. item', '1234. item', /ordered list marker/],
  ['an_empty_list_item', '- ', '- ', /is empty/],
  ['a_reference_definition_with_an_escaped_bracket_in_its_label', '[a\\]]: https://example.invalid/', '[a\\]]: https://example.invalid/', /starts with `\[`/],
  ['a_list_item_holding_a_reference_definition_with_an_escaped_bracket', '- [b\\]]: https://example.invalid/', '- [b\\]]: https://example.invalid/', /list item whose text starts with `\[`/],
  ['a_list_item_whose_text_starts_with_a_hash', '- # text', '- # text', /list item whose text starts with `#`/],
  ['a_list_item_whose_text_starts_with_a_space_the_reviewers_N1', '1.  Note.', '1.  Note.', /list item whose text starts with a space/],
  ['a_nested_list_item', '- item\n  - nested', '  - nested', /continuation line whose text starts with `-`/],
  ['a_list_item_with_a_deeper_indent_continuation', '- item\n    deeper', '    deeper', /continuation line whose text starts with a space/],
  ['a_list_item_with_a_heading_in_it', '- item\n  ### 2.6 fake', '  ### 2.6 fake', /continuation line whose text starts with `#`/],
  ['a_list_item_with_a_fence_in_it', '- item\n  ```text', '  ```text', /continuation line whose text starts with three backticks/],
  ['an_ordered_item_after_a_paragraph_with_a_fence_in_it_the_reviewers_N2', 'See below.\n2. x\n   ```', '   ```', /continuation line whose text starts with three backticks/],
  ['an_ordered_item_whose_text_is_an_ordered_marker', '3. 4. x', '3. 4. x', /list item whose text starts with an ordered list marker/],
  ['an_indented_line_with_no_list_item_open', 'A paragraph.\n   an indented line', '   an indented line', /indented line, and no list item is open/],
  ['an_indented_line_after_a_table_row', '| a | b |\n  an indented line', '  an indented line', /indented line, and no list item is open/],
  ['a_table_row_that_holds_a_reference_definition', '| [a]: /x | b |', '| [a]: /x | b |', /reference definition/],
  ['a_fence_opener_with_an_upper_case_info_string', '```Text\ncode\n```', '```Text', /three backticks/],
  ['a_fence_opener_with_a_digit_in_the_info_string', '```text2\ncode\n```', '```text2', /three backticks/],
  ['a_fence_opener_of_four_backticks', '````\ncode\n````', '````', /three backticks/],
  ['a_tilde_fence_opener', '~~~\ncode\n~~~', '~~~', /starts with `~`/],
  ['an_indented_fence_opener', '  ```text\n  code\n  ```', '  ```text', /indented line/],
  ['a_fence_that_is_not_closed_in_its_part', '```text\ncode', '```text', /not closed in its part/],
  ['a_fence_marker_line_with_a_trailing_space_inside_a_fence', '```text\ncode\n``` \nmore\n```', '``` ', /fence marker line inside/],
  ['a_fence_marker_line_indented_three_spaces_inside_a_fence', '```text\ncode\n   ```\nmore\n```', '   ```', /fence marker line inside/],
  ['a_tilde_marker_line_inside_a_fence', '```text\ncode\n~~~\nmore\n```', '~~~', /fence marker line inside/],
  ['a_longer_backtick_line_inside_a_fence', '```text\ncode\n````\nmore\n```', '````', /fence marker line inside/],
  ['an_html_block_start_line', '<table><tr>', '<table><tr>', /starts with `<`/],
];
/** The refusals of the grammar alone (no reader before it) for a planted body, at its own line numbers: the line of `culprit` must be refused with `form`. */
function assertGrammarRefuses(body, culprit, form) {
  const at = body.split('\n').indexOf(culprit);
  assert.ok(at >= 0, `the culprit ${JSON.stringify(culprit)} is a line of the body`);
  const found = partRefusals(body, 1);
  assert.ok(found.some(entry => entry.no === at + 1 && form.test(entry.form)), `line ${at + 1} is refused with ${form}; the refusals were ${JSON.stringify(found)}`);
}

// Each form is refused by the grammar itself, and the candidate that plants it is refused end to end: exit 1 from the grammar, or exit 4 where the reader (which runs first) already
// cannot read the file as a renderer does (a tilde fence, an HTML block, an indented fence, a heading behind a marker, an unbalanced fence).
for (const [name, body, culprit, form] of REFUSED) {
  test(`${name}_in_an_unprotected_part_is_refused_by_the_grammar_and_the_candidate_is_not_accepted`, () => {
    assertGrammarRefuses(body, culprit, form);
    for (const text of [in33(body), before26(body)]) {
      const result = compare(text);
      assert.notEqual(result.exit, 0, `${JSON.stringify(body)} is accepted`);
      if (result.exit === 1) assert.match(joined(result), new RegExp(`class (?:daemon|web|daemon-web)\\), line ${lineOf(text, culprit)}\\b`), 'the part, its class and the line are named');
    }
  });
}

test('A_list_item_does_not_continue_across_a_heading_an_indented_line_after_a_heading_is_refused', () => {
  assertGrammarRefuses('- item\n\n### 2.7 New\n\n  text', '  text', /indented line, and no list item is open/);
  assertGrammarRefuses('- item\n| a | b |\n  text', '  text', /indented line, and no list item is open/);
  assertGrammarRefuses('- item\nA paragraph line\n  text', '  text', /indented line, and no list item is open/);
});

// The review's candidates and the consultant's two cases.
test('The_reviewers_N1_a_heading_in_a_list_item_that_renders_a_fake_protected_section_is_refused', () => {
  const body = '1.  Note.\n\n    ### 2.6 Olympiad output\n\n    Rewritten text.';
  const text = before26(body);
  const result = compare(text);
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), new RegExp(`line ${lineOf(text, '1.  Note.')}\\b.*list item whose text starts with a space`));
  assert.match(joined(result), /continuation line whose text starts with a space/);
});

test('The_reviewers_N12_a_combination_of_a_wide_list_marker_a_nested_fence_and_a_heading_is_refused', () => {
  const body = '1.  Note.\n\n    ```text\n    ### 2.6 Olympiad output\n\n- item\n  ```';
  const result = compare(before26(body));
  assert.equal(result.exit, 1, joined(result));
  assert.ok(result.lines.length >= 3, joined(result));
});

test('The_reviewers_N3_a_break_then_a_tag_then_a_long_fence_opener_is_refused_line_by_line', () => {
  const body = '***\n<x-a>\n````\n\n### 2.6 Olympiad output\n\nRewritten.';
  for (const [content, form] of [['***', /starts with `\*`/], ['<x-a>', /starts with `<`/], ['````', /three backticks/]]) assertGrammarRefuses(body, content, form);
  assert.notEqual(compare(before26(body)).exit, 0);
});

test('The_consultants_deletion_case_two_fence_lines_deleted_turn_a_literal_into_a_heading_and_are_refused', () => {
  const literal = '1.  Note.\n\n    ### 2.6 Spoof';
  const base = in33(`\`\`\`text\n${literal}\n\`\`\``);
  assertPasses(compare(base, base));
  const candidate = base.replace(`\`\`\`text\n${literal}\n\`\`\`\n`, `${literal}\n`);
  assert.notEqual(candidate, base);
  assertRefused(candidate, '1.  Note.', /list item whose text starts with a space/, base);
});

test('The_consultants_reference_definition_case_a_definition_added_in_an_unprotected_part_is_refused', () => {
  const base = edit(FORMAT_TEXT, 'The file name and the header table.', 'The file name and the header table. Protected [target].');
  assertPasses(compare(base, base));
  const candidate = in33('[target]: /changed "Changed"', base);
  assertRefused(candidate, '[target]: /changed "Changed"', /starts with `\[`/, base);
});

test('A_whole_file_rule_refuses_a_reference_definition_or_an_html_block_start_in_any_part_and_a_frozen_file_that_breaks_it_is_exit_4', () => {
  for (const [body, content, form] of [['[target]: /x', '[target]: /x', /reference definition/], ['  [target]: /x', '  [target]: /x', /reference definition/], ['    <x-a>', '    <x-a>', /first non-space character is `<`/]]) {
    const frozen = edit(FORMAT_TEXT, 'The file name and the header table.', `The file name and the header table.\n\n${body}`);
    const result = compare(frozen, frozen);
    assert.equal(result.exit, 4, joined(result));
    assert.match(joined(result), /the frozen file breaks the restricted grammar/);
    assert.match(joined(result), new RegExp(`line ${lineOf(frozen, content)}\\b[^\\n]*${form.source}`));
  }
});

test('The_whole_file_rules_apply_to_the_candidates_protected_parts_too_and_leave_fences_alone', () => {
  const protectedPlant = edit(FORMAT_TEXT, 'The file name and the header table.', 'The file name and the header table.\n\n[target]: /x');
  const result = compare(protectedPlant);
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /1\. The spec file \(class common\).*reference definition/);
  assertPasses(compare(in33('```text\n[target]: /x\n<div>\n  <x-a>\n# not a heading\n- not a list\n```')));
});

// Headings.
const appendSub = (title, cls = 'daemon-web') => edit(edit(FORMAT_TEXT, 'The closed list.\n\n## 3.', `The closed list.\n\n### 2.7 ${title}\n\nNew text.\n\n## 3.`), LAST_ROW, `${LAST_ROW}| 2.7 ${title} | ${cls} |\n`);

test('An_added_heading_with_the_title_of_a_protected_heading_is_refused_no_look_alike_section', () => {
  for (const title of ['Olympiad samples', 'Files and ids', 'Olympiad output', 'Every kind']) {
    const result = compare(appendSub(title));
    assert.equal(result.exit, 1, `${title}: ${joined(result)}`);
    assert.match(joined(result), /added heading with the title of a protected heading/, title);
  }
  assertPasses(compare(appendSub('New part')));
});

test('A_heading_with_a_closing_sequence_of_hashes_is_refused_the_reviewers_N5', () => {
  const result = compare(appendSub('New part ###'));
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /closing sequence/);
});

// A legitimate amendment passes.
test('A_changed_sentence_in_2_4_and_an_added_daemon_web_subsection_with_an_example_a_list_and_a_table_pass', () => {
  assertPasses(compare(edit(FORMAT_TEXT, 'The slot `{T}` is whole seconds.', 'The slot `{T}` is whole seconds, as before.')));
  const body = [
    'The new part has a paragraph, a **bold** start and `code` starts.', '', '**Bold** starts a line, so does `code` and ``double``. A line may end with 4.6).', '4.6). is text too.', '',
    '- a first item with a **bold** word,', '  continued exactly two spaces in;', '- a second item', '', '10. a numbered item', '    continued four spaces in', '',
    '| a | b |', '|---|---|', '| 1 | 2 |', '', '```text', '# a comment that is no heading', '<tag> is free text', '[a]: is free text', '    indented text', '```', '', 'The end.',
  ].join('\n');
  const text = edit(edit(FORMAT_TEXT, 'The closed list.\n\n## 3.', `The closed list.\n\n### 2.7 New part\n\n${body}\n\n## 3.`), LAST_ROW, `${LAST_ROW}| 2.7 New part | daemon-web |\n`);
  assertPasses(compare(text));
});

test('The_real_draft_passes_the_restricted_grammar_as_the_frozen_file_and_as_the_candidate_and_a_legitimate_amendment_passes', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const run = candidate => checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(candidate), reference: draft.recorded });
  assertPasses(run(real));
  assertPasses(run(edit(real, 'A spec states its own T in F-READY', 'A spec states its own T in F-READY, changed,')));
  const planted = real.replace('\n### 3.3 ', '\n[target]: /changed "Changed"\n\n### 3.3 ');
  assert.notEqual(planted, real);
  const result = run(planted);
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /\[target\]|starts with `\[`/);
});

// The kinds cell of a changed or added free row.
const KINDS_ROW = '| F-READY | daemon, web | section 2, in an R | `{T}` |';
test('The_kinds_cell_of_a_free_row_holds_only_the_allowed_words_in_plain_ASCII', () => {
  for (const [kinds, odd] of [['&#97;ll', /character/], ['аll', /character/], ['every kind', /word `every`/], ['daemon & web', /character/], ['daemon, web, others', /word `others`/], ['daemon (web)', /character/], ['daemon-web', /character/]]) {
    const result = compare(edit(FORMAT_TEXT, KINDS_ROW, KINDS_ROW.replace('| daemon, web |', `| ${kinds} |`)));
    assert.equal(result.exit, 1, `${kinds}: ${joined(result)}`);
    assert.match(joined(result), /F-READY/);
    assert.match(joined(result), odd, kinds);
  }
  for (const kinds of ['daemon or web', 'web, daemon and web', 'Daemon, Web', 'daemon']) assertPasses(compare(edit(FORMAT_TEXT, KINDS_ROW, KINDS_ROW.replace('| daemon, web |', `| ${kinds} |`))));
});

test('The_extra_words_come_from_the_frozen_free_rows_not_from_a_list_in_the_tool', () => {
  const port = '| F-PORT | daemon that listen on a port | section 2, in an R | none |';
  const base = edit(edit(FORMAT_TEXT, KINDS_ROW, `${KINDS_ROW}\n${port}`), '### 2.4 Daemon and web\n\n', '### 2.4 Daemon and web\n\nF-PORT. The port.\n\n');
  assertPasses(compare(base, base));
  assertPasses(compare(edit(base, KINDS_ROW, KINDS_ROW.replace('| daemon, web |', '| web that listen on a port |')), base));
  const without = compare(edit(FORMAT_TEXT, KINDS_ROW, KINDS_ROW.replace('| daemon, web |', '| web that listen on a port |')));
  assert.equal(without.exit, 1, 'the words are not in the frozen free rows of the plain fixture');
  assert.match(joined(without), /word `that`/);
});

test('The_real_draft_kinds_words_are_read_from_its_free_rows', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const run = candidate => checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(candidate), reference: draft.recorded });
  assertPasses(run(edit(real, '| F-READY | daemon, web |', '| F-READY | web that listen on a port, whose spec defines an end of the test |')));
  const refused = run(edit(real, '| F-READY | daemon, web |', '| F-READY | web and every kind |'));
  assert.equal(refused.exit, 1, joined(refused));
  assert.match(joined(refused), /word `every`/);
});

// Fix batch 3. Section 7's appended bytes are checked by the grammar, and must start after a blank line.
const CHANGELOG_LINE = '- v2.1 · 2026-12-01 · amendment · touches all — a later line.';
const appended = tail => `${FORMAT_TEXT}${tail}`;
function assertAppendRefused(tail, content, form) {
  const text = appended(tail);
  const result = compare(text);
  assert.equal(result.exit, 1, `${JSON.stringify(tail)}: ${joined(result)}`);
  assert.match(joined(result), /7\. Changelog \(class common\)/);
  assert.match(joined(result), new RegExp(`line ${lineOf(text, content)}\\b[^\\n]*${form.source}`), joined(result));
}

test('Appended_changelog_text_must_start_after_a_blank_line_and_obey_the_grammar_the_reviewers_R1_and_R3', () => {
  assertPasses(compare(FORMAT_TEXT));
  assertPasses(compare(appended(`\n${CHANGELOG_LINE}\n`)));
  assertPasses(compare(appended(`\n${CHANGELOG_LINE}\n  continued two spaces in\n- a second line\n`)));
  assertPasses(compare(appended('\n')), 'a blank line alone');
  assertAppendRefused(`  The olympiad rules of 2.6 no longer hold.\n`, '  The olympiad rules of 2.6 no longer hold.', /must start after a blank line/);
  assertAppendRefused(`${CHANGELOG_LINE}\n`, CHANGELOG_LINE, /must start after a blank line/);
  assertAppendRefused(`\n    ### 2.6 Olympiad output\n\n    Rewritten text.\n`, '    ### 2.6 Olympiad output', /indented line, and no list item is open/);
  assertAppendRefused(`\n===\n`, '===', /starts with `=`/);
  assertAppendRefused(`\n[a]: /changed\n`, '[a]: /changed', /starts with `\[`/);
  assertAppendRefused(`\n\`\`\`unclosed\ncode\n`, '```unclosed', /not closed in its part/);
  assert.equal(compare(appended('\n<div>\n')).exit, 4, 'an HTML block start is a format error of the reader');
});

test('Appended_text_after_a_changelog_that_does_not_end_with_a_line_feed_is_refused', () => {
  const base = FORMAT_TEXT.replace(/\n$/, '');
  const result = compare(`${base}\n\n${CHANGELOG_LINE}\n`, base);
  assert.equal(result.exit, 1, joined(result));
  assert.match(joined(result), /must start after a blank line|must start with the frozen bytes/);
});

// Fix batch 3. Reference definitions at any indent, with an escaped bracket, a footnote form.
test('A_reference_definition_at_any_indent_with_an_escaped_bracket_or_as_a_footnote_is_a_whole_file_refusal', () => {
  for (const [body, content] of [['- an item\n\n    [within <d>]: https://example.invalid/', '    [within <d>]: https://example.invalid/'], ['[a\\]]: https://example.invalid/', '[a\\]]: https://example.invalid/'],
    ['[^x]: a footnote', '[^x]: a footnote'], ['\t[tabbed]: /x', '\t[tabbed]: /x'], ['        [deep]: /x', '        [deep]: /x']]) {
    const frozen = edit(FORMAT_TEXT, 'The file name and the header table.', `The file name and the header table.\n\n${body}`);
    const result = compare(frozen, frozen);
    assert.equal(result.exit, 4, `${body}: ${joined(result)}`);
    assert.match(joined(result), new RegExp(`line ${lineOf(frozen, content)}\\b[^\\n]*reference definition`));
  }
  assert.equal(paragraphStartProblem('[a\\]]: x') === null, false);
  assert.equal(paragraphStartProblem('[a] and [b]') === null, true, 'brackets in text are fine');
  assertPasses(compare(in33('```text\n    [within <d>]: https://example.invalid/\n```')));
});

// Fix batch 3. The arrays of refusals are built without push.
test('The_grammar_module_builds_its_refusal_arrays_without_push', () => {
  assert.doesNotMatch(readFileSync(GRAMMAR_SOURCE, 'utf8'), /\.push\(/);
});

test('The_real_draft_still_passes_with_the_whole_file_rule_at_any_indent', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const result = checkFrozenCandidate({ frozen: draft.bytes, candidate: draft.bytes, reference: draft.recorded });
  assertPasses(result);
  const real = draft.bytes.toString('utf8');
  const appendedReal = checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(`${real}\n${CHANGELOG_LINE}\n`), reference: draft.recorded });
  assertPasses(appendedReal);
  const bad = checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(`${real}\n    [within <d>]: https://example.invalid/\n`), reference: draft.recorded });
  assert.equal(bad.exit, 1, joined(bad));
});
