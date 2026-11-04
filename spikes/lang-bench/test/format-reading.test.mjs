// FORMAT is read as CommonMark reads it (E1.S9, fix batch 1): a line the reader takes one way and a renderer another is a format error (exit 4) naming the line, in EITHER file
// and in both tools. The forms: a backtick fence whose info string holds a backtick, a fence indented one to three spaces outside a list item, an HTML block start, an ATX
// heading in a form the reader does not take. A fence inside a list item stays allowed (the real draft has one); the closing rule is CommonMark's.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { test } from 'node:test';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { lineNumberAt, numberOf, parseFormat } from '../lib/format-structure.mjs';
import { edit, folder, FORMAT_TEXT, put, realDraft, sha256, TOOLS } from './support/format-fixtures.mjs';

const tool = name => (...args) => spawnSync(process.execPath, [path.join(TOOLS, name), ...args], { encoding: 'utf8', timeout: 60_000 });
const classes = tool('format-classes.mjs');
const extractor = tool('grammar-extract.mjs');
const lineOf = (text, content) => text.split('\n').indexOf(content) + 1;
const AT = 'The steps.\n';
const plantAfterSteps = body => edit(FORMAT_TEXT, AT, `${AT}\n${body}\n`);

/** Every way the text is read: both tools, the text as the candidate, the text as the frozen file. Returns the four results. */
function everyReading(t, text) {
  const dir = folder(t, 'reading');
  const file = put(dir, 'FORMAT.md', text);
  return {
    self: classes('--self', file),
    extract: extractor(file, path.join(dir, 'GRAMMAR.md')),
    candidate: checkFrozenCandidate({ frozen: Buffer.from(FORMAT_TEXT), candidate: Buffer.from(text), reference: sha256(Buffer.from(FORMAT_TEXT)) }),
    frozen: checkFrozenCandidate({ frozen: Buffer.from(text), candidate: Buffer.from(FORMAT_TEXT), reference: sha256(Buffer.from(text)) }),
  };
}

function assertFormatError(t, text, content, rule) {
  const line = lineOf(text, content);
  assert.ok(line > 0, `the planted line ${JSON.stringify(content)} is in the text`);
  const read = everyReading(t, text);
  for (const [name, result] of [['--self', read.self], ['the extractor', read.extract]]) {
    assert.equal(result.status, 4, `${name}: ${result.stdout}${result.stderr}`);
    assert.match(result.stderr, new RegExp(`line ${line}\\b`), `${name} names line ${line}`);
    assert.match(result.stderr, rule, name);
  }
  for (const [name, result] of [['the candidate', read.candidate], ['the frozen file', read.frozen]]) {
    assert.equal(result.exit, 4, `${name}: ${result.lines.join('\n')}`);
    assert.match(result.lines.join('\n'), new RegExp(`line ${line}\\b`), `${name} names line ${line}`);
    assert.match(result.lines.join('\n'), rule, name);
  }
}

function assertReadable(t, text) {
  const read = everyReading(t, text);
  assert.equal(read.self.status, 0, read.self.stderr);
  assert.equal(read.extract.status, 0, read.extract.stderr);
}

const FENCE_TRICK = '### 2.6 Olympiad output\n\nThe closed list.\n';
const FORMAT_ERRORS = [
  ['a_backtick_fence_opener_whose_info_string_holds_a_backtick_the_reviewers_fence_trick', () => edit(FORMAT_TEXT, FENCE_TRICK, `\`\`\`\`x\`\n\n${FENCE_TRICK.replace('The closed list.', 'Rewritten.')}\n\`\`\`\`\n\n${FENCE_TRICK}`), '````x`', /backtick/],
  ['a_three_backtick_opener_with_a_backtick_in_its_info_string', () => plantAfterSteps('```a`b\ntext\n```'), '```a`b', /backtick/],
  ['a_fence_indented_one_space_outside_a_list_item', () => plantAfterSteps(' ```text\n code\n ```'), ' ```text', /indent/],
  ['a_fence_indented_three_spaces_outside_a_list_item', () => plantAfterSteps('   ```text\n   code\n   ```'), '   ```text', /indent/],
  ['a_tilde_fence_indented_two_spaces_outside_a_list_item', () => plantAfterSteps('  ~~~\n  code\n  ~~~'), '  ~~~', /indent/],
  ['a_fence_indented_one_space_under_a_list_item_that_needs_two', () => plantAfterSteps('- item\n\n ```text\n code\n ```'), ' ```text', /indent/],
  ['an_HTML_comment_the_reviewers_html_trick', () => plantAfterSteps('<!--\n````\n-->'), '<!--', /HTML/],
  ['a_div_block', () => plantAfterSteps('<div>\nwords\n</div>'), '<div>', /HTML/],
  ['a_div_block_indented_three_spaces', () => plantAfterSteps('   <div class="x">'), '   <div class="x">', /HTML/],
  ['a_div_block_that_interrupts_a_paragraph', () => plantAfterSteps('A paragraph that goes on\n<div>'), '<div>', /HTML/],
  ['a_script_block_that_interrupts_a_paragraph', () => plantAfterSteps('A paragraph that goes on\n<script>'), '<script>', /HTML/],
  ['a_script_block_of_any_case', () => plantAfterSteps('<SCRIPT>\nx\n</SCRIPT>'), '<SCRIPT>', /HTML/],
  ['a_processing_instruction', () => plantAfterSteps('<?php\nx\n?>'), '<?php', /HTML/],
  ['a_declaration', () => plantAfterSteps('<!DOCTYPE html>'), '<!DOCTYPE html>', /HTML/],
  ['cdata', () => plantAfterSteps('<![CDATA[\nx\n]]>'), '<![CDATA[', /HTML/],
  ['a_complete_tag_alone_on_a_line_after_a_blank_line', () => plantAfterSteps('<custom-tag attr="1">'), '<custom-tag attr="1">', /HTML/],
  ['a_closing_tag_alone_on_a_line_after_a_blank_line', () => plantAfterSteps('</custom-tag>'), '</custom-tag>', /HTML/],
  ['a_complete_tag_alone_on_a_line_right_after_a_heading', () => plantAfterSteps('#### A level four heading\n<custom-tag>'), '<custom-tag>', /HTML/],
  ['a_complete_tag_alone_on_a_line_right_after_a_closed_fence', () => plantAfterSteps('```text\ncode\n```\n<custom-tag>'), '<custom-tag>', /HTML/],
  ['a_fence_indented_after_a_thematic_break_that_looks_like_a_list_marker', () => plantAfterSteps('* * *\n\n  ```text\n  code\n  ```'), '  ```text', /indent/],
  ['a_fence_indented_after_a_list_that_has_ended', () => plantAfterSteps('- item\n\nA paragraph that ends the list.\n\n  ```text\n  code\n  ```'), '  ```text', /indent/],
  ['a_setext_heading_with_a_dash_underline_a_fake_section_heading', () => plantAfterSteps('2.6 Olympiad output (copy)\n------'), '------', /setext/],
  ['a_setext_heading_with_an_equals_underline', () => plantAfterSteps('A paragraph line\n======='), '=======', /setext/],
  ['a_setext_underline_indented_three_spaces', () => plantAfterSteps('A paragraph line\n   -----'), '   -----', /setext/],
  ['a_setext_underline_with_trailing_blanks', () => plantAfterSteps('A paragraph line\n--------  '), '--------  ', /setext/],
  ['a_single_dash_after_a_paragraph_line', () => plantAfterSteps('A paragraph line\n-'), '-', /setext/],
  ['a_setext_heading_over_a_paragraph_of_two_lines', () => plantAfterSteps('First line of the paragraph\nsecond line of it\n=========='), '==========', /setext/],
  ['a_dash_line_under_a_list_item', () => plantAfterSteps('- an item\n---------'), '---------', /a dash line under a list item \(a thematic break or a setext underline: refused\)/],
  ['an_ATX_heading_in_a_block_quote', () => plantAfterSteps('> ## 2.6 Olympiad output'), '> ## 2.6 Olympiad output', /block quote/],
  ['an_ATX_heading_in_a_block_quote_indented_three_spaces_without_a_space_after_the_marker', () => plantAfterSteps('   >### 2.6 Olympiad output'), '   >### 2.6 Olympiad output', /block quote/],
  ['an_ATX_heading_in_a_nested_block_quote', () => plantAfterSteps('> > ## 2.6 Olympiad output'), '> > ## 2.6 Olympiad output', /block quote/],
  ['an_ATX_heading_in_a_nested_block_quote_with_extra_spaces_between_the_markers', () => plantAfterSteps('>  > ## 2.6 Olympiad output'), '>  > ## 2.6 Olympiad output', /block quote/],
  ['a_backtick_fence_opener_in_a_block_quote', () => plantAfterSteps('> ```text'), '> ```text', /block quote/],
  ['a_tilde_fence_opener_in_a_block_quote', () => plantAfterSteps('> ~~~'), '> ~~~', /block quote/],
  ['an_ATX_heading_after_a_bullet_marker', () => plantAfterSteps('- ## 2.6 Olympiad output'), '- ## 2.6 Olympiad output', /list marker/],
  ['a_fence_opener_after_a_number_marker', () => plantAfterSteps('1. ```x'), '1. ```x', /list marker/],
  ['an_ATX_heading_after_a_star_marker_and_a_paren_number_marker', () => plantAfterSteps('* ### 2.6 Olympiad output\n\n2) ## 2.7 Another'), '* ### 2.6 Olympiad output', /list marker/],
  ['a_tilde_fence_opener_after_a_plus_marker', () => plantAfterSteps('+ ~~~'), '+ ~~~', /list marker/],
  ['an_ATX_heading_after_two_list_markers', () => plantAfterSteps('- - ## 2.6 Olympiad output'), '- - ## 2.6 Olympiad output', /list marker/],
  ['an_ATX_heading_after_a_block_quote_marker_and_a_list_marker', () => plantAfterSteps('> - ## 2.6 Olympiad output'), '> - ## 2.6 Olympiad output', /block quote|list marker/],
  ['a_tab_after_the_hashes_the_reviewers_tab_heading', () => plantAfterSteps('###\t2.6 Olympiad output: hidden'), '###\t2.6 Olympiad output: hidden', /heading/],
  ['a_tab_after_one_hash', () => plantAfterSteps('#\ttitle'), '#\ttitle', /heading/],
  ['an_ATX_heading_with_one_leading_space', () => plantAfterSteps(' ## 2.6 Olympiad output'), ' ## 2.6 Olympiad output', /heading/],
  ['an_ATX_heading_with_three_leading_spaces', () => plantAfterSteps('   ### 2.6 Olympiad output'), '   ### 2.6 Olympiad output', /heading/],
  ['hashes_with_nothing_after_them', () => plantAfterSteps('##'), '##', /heading/],
  ['three_hashes_with_nothing_after_them', () => plantAfterSteps('###'), '###', /heading/],
];
for (const [name, plant, content, rule] of FORMAT_ERRORS) {
  test(`${name}_is_a_format_error_naming_the_line_in_both_tools_and_both_files`, t => assertFormatError(t, plant(), content, rule));
}

const READABLE = [
  ['a_fence_inside_a_list_item_with_heading_look_alikes_in_it', () => plantAfterSteps('- item\n\n  ```text\n  ## 9. looks like a heading\n    ### 9.1 so does this\n  ```')],
  ['a_fence_inside_a_numbered_list_item', () => plantAfterSteps('1. item\n\n   ```text\n   code\n   ```')],
  ['a_fence_inside_a_nested_list_item', () => plantAfterSteps('- item\n  - nested\n\n    ```text\n    code\n    ```')],
  ['a_tilde_fence_with_a_backtick_in_its_info_string', () => plantAfterSteps('~~~ a`b\n## 9. inside\n~~~')],
  ['a_list_marker_followed_by_five_spaces_has_its_content_one_space_after_it', () => plantAfterSteps('-      item\n\n  ```text\n  code\n  ```')],
  ['a_fence_indented_by_a_tab_is_indented_code', () => plantAfterSteps('\t```text\n\tcode')],
  ['a_dash_line_after_a_blank_line_is_a_thematic_break', () => plantAfterSteps('---')],
  ['star_and_underscore_thematic_breaks_after_a_paragraph_line', () => plantAfterSteps('A paragraph line\n***\n\nAnother paragraph line\n___')],
  ['a_dash_line_directly_after_a_thematic_break_is_a_thematic_break', () => plantAfterSteps('***\n---')],
  ['a_dash_line_after_a_closed_fence_that_interrupted_a_paragraph_is_a_thematic_break', () => plantAfterSteps('A paragraph line\n```text\ncode\n```\n---')],
  ['a_dash_line_after_indented_code_is_a_thematic_break', () => plantAfterSteps('    indented code\n---')],
  ['a_dash_line_after_a_heading_is_a_thematic_break', () => plantAfterSteps('#### A level four heading\n----')],
  ['an_underline_indented_four_spaces_continues_the_paragraph', () => plantAfterSteps('A paragraph line\n    ---')],
  ['a_dash_line_with_text_after_it_is_no_underline', () => plantAfterSteps('A paragraph line\n--- and more')],
  ['a_block_quote_without_a_heading_or_a_fence_in_it', () => plantAfterSteps('> a quoted line with ## inside and ``` inside\n> more')],
  ['a_list_item_without_a_heading_or_a_fence_after_its_marker', () => plantAfterSteps('- an item with ## inside\n\n1. a numbered item\n\n* * *')],
  ['a_fence_indented_four_spaces_is_indented_code', () => plantAfterSteps('    ```text\n    code')],
  ['hash_forms_that_are_not_headings', () => plantAfterSteps('#hashtag\n\n####### seven hashes\n\n#### four hashes with text')],
  ['an_html_tag_inside_a_line_and_a_tag_name_at_the_start_of_a_paragraph_line', () => plantAfterSteps('Some <b>bold</b> words\nand a line that goes on\n<type> then more words, as the real draft has it')],
  ['a_complete_tag_that_continues_a_paragraph_cannot_start_an_HTML_block', () => plantAfterSteps('A paragraph\n<custom-tag attr="1">')],
  ['an_html_block_start_indented_four_spaces_is_indented_code', () => plantAfterSteps('    <div>')],
];
for (const [name, plant] of READABLE) test(`${name}_is_read_without_error_in_both_tools`, t => assertReadable(t, plant()));

test('A_closing_fence_indented_up_to_three_spaces_closes_the_fence_as_CommonMark_closes_it', () => {
  const text = edit(FORMAT_TEXT, '## 7. Changelog', '```text\nbody\n   ```\n\n### 6.3 After the fence\n\nText.\n\n## 7. Changelog');
  const parsed = parseFormat(Buffer.from(text));
  assert.equal(parsed.ok, true, parsed.reason);
  assert.ok(parsed.format.headings.some(heading => heading.level === 3 && numberOf(heading) === '6.3'), 'the heading after the fence is read');
});

const headingsAfter = fence => {
  const parsed = parseFormat(Buffer.from(edit(FORMAT_TEXT, '## 7. Changelog', `${fence}\n\n### 6.3 After the fence\n\nText.\n\n## 7. Changelog`)));
  return parsed.ok ? parsed.format.headings.map(numberOf) : null;
};

test('A_fence_in_a_list_item_ends_where_the_list_item_does', () => {
  assert.ok(headingsAfter('- item\n\n  ```text\n  code\n').includes('6.3'), 'a heading at column 0 is a heading, the unclosed fence ended with its list item');
});

test('A_closing_line_with_other_text_or_four_spaces_of_indent_does_not_close_a_fence', () => {
  assert.equal(headingsAfter('```text\nbody\n``` not a closer'), null, 'the fence is never closed, so section 7 is inside it');
  assert.equal(headingsAfter('```text\nbody\n    ```'), null, 'four spaces of indent is code inside the fence, not a closer');
});

test('A_shorter_marker_or_another_character_does_not_close_a_fence', () => {
  const text = edit(FORMAT_TEXT, '## 7. Changelog', '````text\nbody\n```\n~~~~\n\n### 6.3 Still inside\n\n## 7. Changelog');
  assert.equal(parseFormat(Buffer.from(text)).ok, false, 'the fence is never closed, so section 7 is inside it');
});

test('The_line_number_of_an_offset_before_the_first_line_is_1_not_an_exception', () => {
  const { format } = parseFormat(Buffer.from(FORMAT_TEXT));
  assert.equal(lineNumberAt(format, -1), 1);
  assert.equal(lineNumberAt(format, 0), 1);
  assert.equal(lineNumberAt(format, format.text.indexOf('## 2.')), format.text.slice(0, format.text.indexOf('## 2.')).split('\n').length);
});

test('The_real_draft_is_still_read_without_error_with_its_one_list_indented_fence', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const read = everyReading(t, draft.bytes.toString('utf8'));
  assert.equal(read.self.status, 0, read.self.stderr);
  assert.equal(read.extract.status, 0, read.extract.stderr);
});
