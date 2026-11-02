// The visible samples of a spec (plan research/PLAN_lang_explore_olympiad.md D-O4, T-O3): `samples/` is generated from the spec's section 6 table, byte for byte (`⏎` is a line feed), for tasks 02, 03 and 09 as for 01, and a
// table the generator cannot parse is refused with a sentence, never guessed at. The three specs are read at their pins from the task repository (a host without it is `hostLacks`); the refusals run over written tables.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { hostLacks, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { REPO } from '../lib/paths.mjs';
import { parseSamples } from '../lib/samples.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { candidateRepos, loadSources, samplesOf, workspaceFiles } from '../lib/workspace.mjs';

const EXPLORE = loadExplore().value;
const TYPES_NODE = 'types-node-placeholder';

/** The spec and FORMAT of a task at their pins, or null when this host lacks the task repository. */
async function sourcesOf(t, id) {
  const task = resolveTask(EXPLORE, id).value;
  const r = await loadSources({ task, candidates: candidateRepos({ repoName: EXPLORE.specs.repoName, repoRoot: REPO, commonRoot: path.resolve(REPO, '..', '..', 'dew_flow_benchmark') }) });
  return r.ok ? { task, ...r.value } : (hostLacks(t, r.reason) && null);
}

/** What each spec's section 6 table shows, written here from the specs' own text (the bytes, `⏎` as a line feed). */
const EXPECTED = {
  '01': [['1\n', '1\n'], ['16\n', '16 8 4 2 1\n'], ['7\n', '7 22 11 34 17 52 26 13 40 20 10 5 16 8 4 2 1\n']],
  '02': [['1 1\n.\n', '1\n'], ['5 5\n..#..\n.#.#.\n#####\n.#...\n.#.#.\n', '5\n'], ['2 3\n###\n###\n', '0\n']],
  '03': [['1 + 2 * 3\n', '7\n'], ['(8 - 3) * (2 + 2) / 3\n', '6\n'], ['-7 / 2\n', '-3\n']],
  '09': [['6 6 4\n0 1\n1 2\n2 3\n3 4\n4 5\n1 4\n0 5 0 5\n5 0 2 3\n2 5 2 3\n3 0 1 1\n', '1\n1\n0\n1\n'], ['5 4 4\n0 2\n2 4\n4 1\n1 3\n4 1 2 2\n4 1 1 2\n3 0 0 4\n0 3 2 3\n', '0\n1\n1\n0\n'], ['1 0 1\n0 0 0 0\n', '1\n']],
};

for (const id of Object.keys(EXPECTED)) {
  test(`The_samples_of_task_${id}_are_its_section_6_table_byte_for_byte_in_the_workspace_files`, async t => {
    const s = await sourcesOf(t, id);
    if (!s) return;
    const files = workspaceFiles({ explore: EXPLORE, stack: 'node', sources: s, typesNode: TYPES_NODE });
    assert.deepEqual(samplesOf(s.spec).map(x => x.id), ['s01', 's02', 's03']);
    EXPECTED[id].forEach(([input, output], i) => {
      const n = `s0${i + 1}`;
      assert.equal(files[`samples/${n}.in`].bytes.toString('utf8'), input, `${id} ${n}.in`);
      assert.equal(files[`samples/${n}.out`].bytes.toString('utf8'), output, `${id} ${n}.out`);
    });
    assert.deepEqual(Object.keys(files).filter(f => f.startsWith('samples/')).sort(), ['s01', 's02', 's03'].flatMap(n => [`samples/${n}.in`, `samples/${n}.out`]));
  });
}

const HEAD = '| Id | Standard input | Expected standard output |\n|---|---|---|\n';
const spec = rows => `# Task 99\n\n## 5. Build\n\ntext\n\n## 6. Visible samples\n\nlead sentence\n\n${HEAD}${rows}\nprose after\n\n## 7. Hidden tests\n`;
const parse = rows => parseSamples(spec(rows));

test('The_two_backtick_form_the_escaped_pipe_and_the_line_feed_mark_are_read_as_the_format_says', () => {
  const r = parse('| `s01` | `1 2⏎` | `3⏎` |\n| `s02` | `` a`b `` | `x\\|y⏎` |\n| `s03` | `` ` ` `` | `\\|` |\n');
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value, [{ id: 's01', input: '1 2\n', output: '3\n' }, { id: 's02', input: 'a`b', output: 'x|y\n' }, { id: 's03', input: '` `', output: '|' }]);
});

test('A_table_the_generator_cannot_parse_is_refused_with_a_sentence_for_each_kind_of_fault', () => {
  const faults = [
    ['no table at all', () => parseSamples('# Task\n\n## 6. Visible samples\n\nno table here\n'), /no samples table/],
    ['a header with other words', () => parseSamples(spec('| `s01` | `1⏎` | `1⏎` |\n').replace('Standard input', 'Input')), /no samples table/],
    ['the table twice', () => parseSamples(`${spec('| `s01` | `1⏎` | `1⏎` |\n')}\n${HEAD}| \`s02\` | \`1⏎\` | \`1⏎\` |\n`), /more than one/],
    ['no row', () => parse(''), /no sample row/],
    ['a row that is not three code spans', () => parse('| `s01` | 1 | `1⏎` |\n'), /row 1 .*cannot be read/],
    ['an unclosed code span', () => parse('| `s01` | `1⏎ | `1⏎` |\n'), /row 1 .*cannot be read/],
    ['the size form that holds no bytes', () => parse('| `s01` | 300 bytes, see samples/s01.in | `1⏎` |\n'), /row 1 .*cannot be read/],
    ['a byte the table cannot show', () => parse('| `s01` | `é⏎` | `1⏎` |\n'), /row 1 .*not printable ASCII/],
    ['a tab', () => parse('| `s01` | `a\tb` | `1⏎` |\n'), /row 1 .*not printable ASCII/],
    ['ids out of order', () => parse('| `s01` | `1⏎` | `1⏎` |\n| `s03` | `1⏎` | `1⏎` |\n'), /ids must be s01, s02/],
    ['an id used twice', () => parse('| `s01` | `1⏎` | `1⏎` |\n| `s01` | `2⏎` | `2⏎` |\n'), /ids must be s01, s02/],
    ['an empty code span', () => parse('| `s01` | `` | `1⏎` |\n'), /row 1 .*cannot be read/],
  ];
  for (const [name, run, why] of faults) {
    const r = run();
    assert.equal(r.ok, false, `${name} must be refused`);
    assert.match(r.reason, why, name);
  }
});

test('A_table_outside_section_6_is_not_a_samples_table', () => {
  const r = parseSamples(`# Task\n\n## 3. Output\n\n${HEAD}| \`s01\` | \`1⏎\` | \`1⏎\` |\n\n## 6. Visible samples\n\nnone\n`);
  assert.equal(r.ok, false);
  assert.match(r.reason, /no samples table/);
});

/** A task repository of one commit holding `spec` and a FORMAT text, and the pins of a task over them. */
function toyRepo(specText) {
  const repo = tmpDir('o-samples-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  mkdirSync(path.join(repo, 'todo', 'tasks'), { recursive: true });
  writeFileSync(path.join(repo, 'todo', 'tasks', 'spec.md'), specText);
  writeFileSync(path.join(repo, 'todo', 'tasks', 'FORMAT.md'), 'format\n');
  git('add', '.');
  git('commit', '-q', '-m', 'x');
  const sha = git('rev-parse', 'HEAD').trim();
  const pin = (file, text) => ({ sha, path: `todo/tasks/${file}`, sha256: createHash('sha256').update(text).digest('hex') });
  return { repo, task: { id: '77', spec: pin('spec.md', specText), format: { ...pin('FORMAT.md', 'format\n'), label: 'v2.1' } } };
}

test('A_spec_with_a_bad_samples_table_is_refused_when_its_sources_load_so_no_workspace_is_filled_from_it', async () => {
  const bad = toyRepo(spec(''));
  const refused = await loadSources({ task: bad.task, candidates: [bad.repo] });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /no sample row/);
  const good = toyRepo(spec('| `s01` | `1⏎` | `2⏎` |\n'));
  const loaded = await loadSources({ task: good.task, candidates: [good.repo] });
  assert.equal(loaded.ok, true, loaded.reason);
  assert.deepEqual(samplesOf(loaded.value.spec), [{ id: 's01', input: '1\n', output: '2\n' }]);
});

// ---- the header is a whole line and the separator row follows it (coai finding 8) ---------------------------------------------------------------

test('The_header_must_be_a_whole_line_of_its_own_and_the_separator_row_must_follow_it', () => {
  const row = '| `s01` | `1⏎` | `2⏎` |\n';
  const tableAfter = prefix => parseSamples(`# T\n\n## 6. Visible samples\n\n${prefix}${HEAD.split('\n')[0]}\n${HEAD.split('\n')[1]}\n${row}`);
  assert.equal(tableAfter('').ok, true);
  for (const [name, text] of [
    ['an indented header', `# T\n\n## 6. Visible samples\n\n  ${HEAD}${row}`],
    ['the header inside a sentence', `# T\n\n## 6. Visible samples\n\nsee ${HEAD}${row}`],
    ['the header with text after it', `# T\n\n## 6. Visible samples\n\n${HEAD.split('\n')[0]} (draft)\n${HEAD.split('\n')[1]}\n${row}`],
    ['no separator row', `# T\n\n## 6. Visible samples\n\n${HEAD.split('\n')[0]}\n${row}`],
    ['a separator of another shape', `# T\n\n## 6. Visible samples\n\n${HEAD.split('\n')[0]}\n| --- | --- | --- |\n${row}`],
    ['a blank line between header and separator', `# T\n\n## 6. Visible samples\n\n${HEAD.split('\n')[0]}\n\n${HEAD.split('\n')[1]}\n${row}`],
  ]) {
    const r = parseSamples(text);
    assert.equal(r.ok, false, name);
    assert.match(r.reason, /no samples table/, name);
  }
});
