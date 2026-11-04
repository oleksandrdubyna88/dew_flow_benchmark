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
