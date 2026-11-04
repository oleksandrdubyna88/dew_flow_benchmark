// Fix batch 4 (E1.S9): a tab after an ordered list marker, look-alike titles taken from every heading of a protected part and checked in the appended changelog text, and the
// changelog (section 7) as one whole `common` row, so that nothing can class it otherwise and escape the append-only rule.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { paragraphStartProblem, partRefusals } from '../lib/format-grammar.mjs';
import { edit, folder, FORMAT_TEXT, put, realDraft, sha256, TOOLS } from './support/format-fixtures.mjs';
import { spawnSync } from 'node:child_process';

const compare = (candidate, base = FORMAT_TEXT) => {
  const frozen = Buffer.from(base);
  return checkFrozenCandidate({ frozen, candidate: Buffer.from(candidate), reference: sha256(frozen) });
};
const joined = result => result.lines.join('\n');
const lineOf = (text, content) => text.split('\n').indexOf(content) + 1;
const LAST_ROW = '| 7. Changelog | common |\n';
const in33 = body => edit(FORMAT_TEXT, 'Scenario pairs.\n', `Scenario pairs.\n\n${body}\n`);
const before26 = body => edit(FORMAT_TEXT, 'The defaults T and G.\n\n### 2.6', `The defaults T and G.\n\n${body}\n\n### 2.6`);

// A tab after a list marker.
const TABBED = [
  ['an_ordered_marker_with_a_dot_and_a_tab', '1.\t[within <d>]: https://example.invalid/', /ordered list marker/],
  ['an_ordered_marker_with_a_paren_and_a_tab', '1)\t[within <d>]: https://example.invalid/', /ordered list marker/],
  ['a_dash_item_holding_an_ordered_marker_and_a_tab', '- 1.\t[within <d>]: https://example.invalid/', /list item whose text starts with an ordered list marker/],
  ['an_ordered_marker_and_a_tab_before_a_footnote', '1.\t[^x]: a footnote', /ordered list marker/],
  ['a_dash_and_a_tab', '-\t[x]: https://example.invalid/', /tab after a list marker/],
  ['a_dash_a_space_and_a_tab', '- \t[x]: https://example.invalid/', /list item whose text starts with a space or a tab/],
  ['a_nested_ordered_marker_and_a_tab', '- item\n  1.\t[x]: https://example.invalid/', /continuation line whose text starts with an ordered list marker/],
];
for (const [name, body, form] of TABBED) {
  test(`${name}_is_refused_by_the_grammar_and_by_the_candidate_check`, () => {
    const culprit = body.split('\n').at(-1);
    const found = partRefusals(body, 1);
    const at = body.split('\n').length;
    assert.ok(found.some(entry => entry.no === at && form.test(entry.form)), `line ${at} is refused with ${form}; the refusals were ${JSON.stringify(found)}`);
    for (const text of [in33(body), before26(body)]) {
      const result = compare(text);
      assert.equal(result.exit, 1, joined(result));
      assert.match(joined(result), new RegExp(`line ${lineOf(text, culprit)}\\b[^\\n]*${form.source}`));
    }
  });
}

test('An_ordered_marker_followed_by_a_space_an_end_of_line_or_a_tab_is_a_marker_and_a_number_in_text_is_not', () => {
  for (const marked of ['1. x', '1) x', '1.\tx', '12)\tx', '1.', '123456789.']) assert.notEqual(paragraphStartProblem(marked), null, marked);
  for (const text of ['1.5 is a number', '2026-12-01', '4.6 says so', 'a 1. b', '1.x']) assert.equal(paragraphStartProblem(text), null, text);
});

// Look-alike titles.
const appendSub = (title, cls = 'daemon-web') => edit(edit(FORMAT_TEXT, 'The closed list.\n\n## 3.', `The closed list.\n\n### 2.7 ${title}\n\nNew text.\n\n## 3.`), LAST_ROW, `${LAST_ROW}| 2.7 ${title} | ${cls} |\n`);

test('The_title_of_a_subsection_inside_a_protected_whole_section_is_a_title_no_added_heading_may_carry', () => {
  for (const title of ['Name and title', 'name  and Title']) {
    const result = compare(appendSub(title));
    assert.equal(result.exit, 1, `${title}: ${joined(result)}`);
    assert.match(joined(result), /added heading with the title of a protected heading/, title);
  }
  assert.equal(compare(appendSub('New part')).exit, 0);
});

test('A_heading_appended_to_the_changelog_with_the_title_of_a_protected_heading_is_refused', () => {
  for (const title of ['Olympiad output', 'Name and title', 'The spec file']) {
    const text = `${FORMAT_TEXT}\n### 7.1 ${title}\n\nAny other normalisation a spec names is allowed.\n`;
    const result = compare(text);
    assert.equal(result.exit, 1, `${title}: ${joined(result)}`);
    assert.match(joined(result), new RegExp(`7\\. Changelog \\(class common\\), line ${lineOf(text, `### 7.1 ${title}`)}\\b[^\\n]*added heading with the title of a protected heading`));
  }
  assert.equal(compare(`${FORMAT_TEXT}\n### 7.1 A new entry\n\nA later line.\n`).exit, 0);
});

// The changelog is one whole common row.
const CLI = path.join(TOOLS, 'format-classes.mjs');
const cli = args => spawnSync(process.execPath, [CLI, ...args], { encoding: 'utf8', timeout: 60_000 });

test('The_changelog_row_must_be_one_whole_common_row_in_the_class_table_for_self_and_for_the_candidate', t => {
  const dir = folder(t, 'classes-changelog');
  for (const [row, rule] of [['| 7. Changelog | daemon-web |', /changelog.*one whole `common` row/], ['| 7. Changelog | olympiad |', /changelog.*one whole `common` row/]]) {
    const text = edit(FORMAT_TEXT, '| 7. Changelog | common |', row);
    const self = cli(['--self', put(dir, 'F.md', text)]);
    assert.equal(self.status, 1, self.stderr);
    assert.match(self.stderr, rule);
    const result = compare(text);
    assert.equal(result.exit, 1, joined(result));
    assert.match(joined(result), rule);
  }
  const missing = edit(FORMAT_TEXT, LAST_ROW, '');
  const absent = cli(['--self', put(dir, 'M.md', missing)]);
  assert.equal(absent.status, 1, absent.stderr);
  assert.match(absent.stderr, /changelog.*one whole `common` row/);
  const subs = edit(edit(FORMAT_TEXT, '- v2 · 2026-11-04 · draft — the second line.', '- v2 · 2026-11-04 · draft — the second line.\n\n### 7.1 An entry\n\nText.'), LAST_ROW, '| 7.1 An entry | daemon-web |\n');
  const mixed = cli(['--self', put(dir, 'S.md', subs)]);
  assert.equal(mixed.status, 1, mixed.stderr);
  assert.match(mixed.stderr, /changelog.*one whole `common` row/);
  const both = edit(subs, '| 7.1 An entry | daemon-web |\n', `${LAST_ROW}| 7.1 An entry | daemon-web |\n`);
  const twoRows = cli(['--self', put(dir, 'B.md', both)]);
  assert.equal(twoRows.status, 1, twoRows.stderr);
  assert.match(twoRows.stderr, /changelog.*one whole `common` row/);
});

test('A_candidate_that_classes_the_changelog_otherwise_is_refused_and_a_frozen_file_that_does_is_exit_4', () => {
  const classed = edit(FORMAT_TEXT, '| 7. Changelog | common |', '| 7. Changelog | web |');
  const asCandidate = compare(classed);
  assert.equal(asCandidate.exit, 1, joined(asCandidate));
  assert.match(joined(asCandidate), /one whole `common` row/);
  const asFrozen = compare(classed, classed);
  assert.equal(asFrozen.exit, 4, joined(asFrozen));
  assert.match(joined(asFrozen), /one whole `common` row/);
});

test('The_compare_module_has_no_stray_half_comment', () => {
  const source = readFileSync(fileURLToPath(new URL('../lib/format-compare.mjs', import.meta.url)), 'utf8');
  assert.doesNotMatch(source, /The number the title of\s+return/);
  for (const block of source.split('/**').slice(1)) assert.doesNotMatch(block.split('*/')[0], /\n\}\n|\n(?:const|function|export) /, 'a doc comment holds no code');
});

test('The_real_draft_keeps_passing_with_its_changelog_row_whole_and_common', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const real = draft.bytes.toString('utf8');
  const file = put(folder(t, 'classes-batch4-real'), 'FORMAT.md', real);
  assert.equal(cli(['--self', file]).status, 0);
  const changed = real.replace('| 7. Changelog | common |', '| 7. Changelog | web |');
  assert.notEqual(changed, real);
  const result = checkFrozenCandidate({ frozen: draft.bytes, candidate: Buffer.from(changed), reference: draft.recorded });
  assert.equal(result.exit, 1, joined(result));
});
