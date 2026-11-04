// E1.S8 batch 3, the structure items C5 to C10, C12 and C15 to C20: each case plants one change in a copy of a conforming synthetic spec (as conformance-checks.test.mjs does) and shows that the
// unchanged spec is ok for the check and that the change gives a FAIL line naming it. The threat model of the batch is an honest author who makes mistakes in the ordinary forms of Markdown.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { check, failsOf, fixtureRepo, isOk, phrase } from './support/conformance-fixtures.mjs';

// [name, kind, edits, check, message]
const CASES = [
  // C5: section 8
  ['an_empty_section_8', 'olympiad', { spec: [['None yet.', '']] }, 'sections', /section 8 is empty/],
  ['a_fenced_block_in_section_8', 'olympiad', { spec: [['None yet.', 'None yet.\n\n```text\nx\n```']] }, 'sections', /section 8 holds a fenced code block/],
  ['a_table_in_section_8', 'olympiad', { spec: [['None yet.', 'None yet.\n\n| a | b |\n|---|---|\n| 1 | 2 |']] }, 'sections', /section 8 holds a table/],
  // C6: a table is found by its delimiter row, whatever stands around it
  ['a_table_right_under_a_heading', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n| a | b |\n|---|---|\n| 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_after_a_text_line', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\nSome text.\n| a | b |\n|---|---|\n| 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_followed_by_text', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n| a | b |\n|---|---|\n| 1 | 2 |\nTrailing text.\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['an_indented_table', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n  | a | b |\n  |---|---|\n  | 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_without_outer_pipes', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\na | b\n--|--\n1 | 2\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  ['a_table_in_a_block_quote', 'olympiad', { spec: [['## 3. Output\n', '## 3. Output\n\n> | a | b |\n> |---|---|\n> | 1 | 2 |\n']] }, 'r-ids', /a table outside any requirement paragraph/],
  // C7: requirement ids in any form
  ['an_id_in_a_list_item', 'olympiad', { spec: [['**R6.** The output', '- **R6.** The output']] }, 'r-ids', /looks like a requirement id/],
  ['an_indented_id', 'olympiad', { spec: [['**R6.** The output', '  **R6.** The output']] }, 'r-ids', /looks like a requirement id/],
  ['an_id_in_the_middle_of_a_line', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 and **R3.** the rest']] }, 'r-ids', /looks like a requirement id/],
  ['a_plain_id_at_the_start_of_a_line', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1\nR3. Another sentence']] }, 'r-ids', /looks like a requirement id/],
  ['an_id_glued_to_the_paragraph_above', 'olympiad', { spec: [['\n\n**R6.** The output', '\n**R6.** The output']] }, 'r-ids', /does not open a paragraph/],
  ['a_reference_to_a_requirement_that_does_not_exist', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (see R99).']] }, 'r-ids', /names R99, and the spec has requirements R1 to R14 only/],
  ['a_reference_to_R0', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (see R0).']] }, 'r-ids', /names R0/],
  ['a_reference_with_a_leading_zero', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (see R02).']] }, 'r-ids', /names R02/],
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

test('The_phrase_helper_is_available_to_the_batch_cases', () => assert.equal(typeof phrase, 'function'));

test('A_table_without_outer_pipes_inside_a_requirement_is_no_finding', async t => {
  const spec = [['| n | output |\n|---|---|\n| 1 | `1` |\n| 2 | `2 1` |', 'n | output\n--|--\n1 | `1`\n2 | `2 1`']];
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', { spec })), 'r-ids'));
});

test('Plain_references_in_prose_and_the_F_REQ_paragraph_are_no_finding', async t => {
  const spec = [['**R2.** For n = 1', '**R2.** For n = 1, as R1 says; see R3–R5 and R14. A word like CR3 or R2D2 is no reference.']];
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', { spec })), 'r-ids'));
});
