// Fix batch 5 (E1.S9): the code-span scan is iterative (a paragraph of thousands of backtick runs must not overflow the stack), an ordered item whose number is not 1 directly after a
// paragraph line is refused (CommonMark reads it as paragraph text, so a code span can pair across it), and a table row that holds `\\|` is refused (renderers disagree on whether the
// pair of backslashes escapes the pipe). The cases are synthetic; the real draft is checked when it is there.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { htmlOpenings, partRefusals } from '../lib/format-grammar.mjs';
import { edit, FORMAT_TEXT, realDraft, sha256 } from './support/format-fixtures.mjs';

const compare = (candidate, base = FORMAT_TEXT) => {
  const frozen = Buffer.from(base);
  return checkFrozenCandidate({ frozen, candidate: Buffer.from(candidate), reference: sha256(frozen) });
};
const joined = result => result.lines.join('\n');
const in33 = body => edit(FORMAT_TEXT, 'Scenario pairs.\n', `Scenario pairs.\n\n${body}\n`);
const appended = body => `${FORMAT_TEXT}\n${body}\n`;
const READY = '| F-READY | daemon, web | section 2, in an R | `{T}` |';
const ORDERED = /ordered list item whose number is not 1/;
const BACKSLASHES = /holds `\\\\\|`/;

/** The candidate is refused (exit 1) with a line that names the planted line number and matches `form`. */
function assertRefusedAt(text, line, form) {
  const result = compare(text);
  assert.equal(result.exit, 1, joined(result));
  assert.ok(result.lines.some(entry => new RegExp(`line ${line}\\b`).test(entry) && form.test(entry)), `a line names line ${line} and ${form}; the lines were:\n${joined(result)}`);
}
const lineOf = (text, content) => text.split('\n').indexOf(content) + 1;

test('Thousands_of_backtick_runs_in_one_paragraph_do_not_overflow_the_stack', () => {
  const many = '` ' + '`` '.repeat(20000) + '<b>';
  assert.deepEqual(htmlOpenings(many), [many.indexOf('<b>')]);
  const unmatched = Array.from({ length: 20000 }, (_, index) => '`'.repeat((index % 5) + 1)).join(' ');
  assert.deepEqual(htmlOpenings(`${unmatched} <b>`), [unmatched.length + 1]);
  const oneRun = '`'.repeat(20000);
  assert.deepEqual(htmlOpenings(`${oneRun}<b>`), [20000]);
  assert.deepEqual(htmlOpenings(`${oneRun}<b>${oneRun}`), []);
  const result = compare(in33(`${many} <s>`));
  assert.equal(result.exit, 1, 'the check ends with a verdict, it does not throw');
});

test('An_ordered_item_that_is_not_number_1_right_after_a_paragraph_line_is_refused_the_span_pairs_across_it', () => {
  const body = 'x `\n2. ` <details> `';
  const found = partRefusals(body, 1);
  assert.ok(found.some(entry => entry.no === 2 && ORDERED.test(entry.form)), JSON.stringify(found));
  assertRefusedAt(in33(body), lineOf(in33(body), '2. ` <details> `'), ORDERED);
  assertRefusedAt(in33('x `\n2. ` <style> `'), lineOf(in33('x `\n2. ` <style> `'), '2. ` <style> `'), ORDERED);
  assertRefusedAt(appended(body), lineOf(appended(body), '2. ` <details> `'), ORDERED);
  assertRefusedAt(in33('A line\n0. zero'), lineOf(in33('A line\n0. zero'), '0. zero'), ORDERED);
});

test('The_same_item_is_refused_after_a_bullet_item_and_after_its_continuation_line', () => {
  for (const body of ['- a\n2. b', '- a\n  more\n3. b', '1. a\n- b\n2. c']) {
    const found = partRefusals(body, 1);
    assert.ok(found.some(entry => ORDERED.test(entry.form)), `${JSON.stringify(body)} is refused; the refusals were ${JSON.stringify(found)}`);
  }
});

test('An_ordered_item_that_starts_a_list_with_1_or_follows_a_blank_line_a_heading_a_sibling_item_or_a_fence_is_not_refused', () => {
  for (const body of ['x\n1. a', 'x\n01. a', 'x\n\n2. a', '1. a\n2. b\n3. c', '1. a\n   more\n2. b', '### 2.7 New part\n2. a', 'x\n\n```text\ncode\n```\n2. a', '| a |\n|---|\n\n2. a']) {
    assert.deepEqual(partRefusals(body, 1).filter(entry => ORDERED.test(entry.form)), [], JSON.stringify(body));
  }
  assert.equal(compare(in33('x `\n1. ` <details> `')).exit, 0, 'a 1 interrupts the paragraph, so the span stays inside the item');
});

test('A_table_row_that_holds_two_backslashes_and_a_pipe_is_refused_in_any_part_of_the_file', () => {
  const row = '| ` \\\\| ` <style> ` |';
  const table = `| a |\n|---|\n| \` |\n${row}`;
  assertRefusedAt(in33(table), lineOf(in33(table), row), BACKSLASHES);
  assertRefusedAt(appended(row), lineOf(appended(row), row), BACKSLASHES);
  const free = READY.replace('`{T}`', '`a\\\\|b`');
  assertRefusedAt(edit(FORMAT_TEXT, READY, free), lineOf(edit(FORMAT_TEXT, READY, free), free), BACKSLASHES);
  const three = '| a \\\\\\| b |';
  assertRefusedAt(in33(three), lineOf(in33(three), three), BACKSLASHES);
});

test('A_pipe_that_one_backslash_escapes_and_two_backslashes_in_prose_are_not_refused', () => {
  assert.equal(compare(in33('| a |\n|---|\n| ` \\| <id> ` |')).exit, 0, 'one backslash: the escaped pipe stays in the cell and in the span');
  assert.equal(compare(in33('Prose with \\\\| two backslashes and a pipe.')).exit, 0, 'only a table row is refused');
  assert.equal(compare(in33('| a |\n|---|\n| two \\\\ backslashes |')).exit, 0);
});

test('The_real_draft_has_no_such_line_and_the_planted_forms_are_refused_on_it', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const run = candidate => checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(candidate), reference: draft.recorded });
  assert.equal(run(real).exit, 0);
  const at = real.indexOf('### 2.6 ');
  for (const [plant, form] of [['x `\n2. ` <details> `\n\n', ORDERED], ['| a |\n|---|\n| ` \\\\| ` <style> ` |\n\n', BACKSLASHES]]) {
    const result = run(real.slice(0, at) + plant + real.slice(at));
    assert.equal(result.exit, 1, joined(result));
    assert.match(joined(result), form);
  }
});
