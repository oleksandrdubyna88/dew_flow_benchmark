// The two files the harness generates from the one memory value, `STATEMENT.md` and `samples/load.json`, are judged by the same per-file rule as the template files (the merge of F1.S2 and F1.S3). A kept tree of an older
// series differs from today's bytes in them though the agent touched nothing; the series' `init` records their bytes (or their absence); for a series made before that the old bytes are made again as they were at its commit
// (no memory and heap lines, no load file); where that cannot be done the file is undecided, never the agent's and never a known 0. Invented fixtures only.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { generatedHashesNow, legacyGenerated } from '../lib/generated-drift.mjs';
import { generatedOf } from '../lib/init-quick.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { exploreSection } from '../lib/records.mjs';
import { driftDecider, templateHistory } from '../lib/template-drift.mjs';
import { packagesInputs, quickStackSentence, quickStatement, quickWorkspaceFiles } from '../lib/workspace-quick.mjs';
import { frozenFiles, ran, rec, scoreWith, scoringArea } from './quick-score-fixture.mjs';
import { EXPLORE, quickTask } from './quick-leak-fixture.mjs';

const sha = text => createHash('sha256').update(text).digest('hex');
const f = text => ({ bytes: Buffer.from(text), mode: 0o644 });
const passAll = () => ran(['s01', 'h001', 'h002'].map(id => rec(id)));
const COMMIT = 'b'.repeat(40);
const SPEC = ['# Task 05 — An invented service', '', '## 5. Build', '', '## 6. Visible samples', '', '### s01', '', '`samples/s01.in`', '', '```text', 'scenario 2', 'start ready port within 10s', '```', '', '`samples/s01.out`', '', '```text', 'transcript 2', 'ready', '```', '', '## 7. Hidden tests', '', 'text', ''].join('\n');
const EXPLORE_FILE = fileURLToPath(new URL('../inputs/explore.json', import.meta.url));
const WORLD = Object.freeze({ home: '/home/nobody', sources: { spec: SPEC }, task: { spec: { path: 'p', sha: 's' } } });
const INPUTS = Object.freeze({ subject: { sha: COMMIT }, taskPins: { spec: { path: 'p', sha: 's' } } });
const OLD_STATEMENT = quickStatement({ spec: SPEC, kind: 'daemon', stackSentence: quickStackSentence(quickExplore(JSON.parse(readFileSync(EXPLORE_FILE, 'utf8')), '/home/nobody'), 'node'), memory: null });
/** git at a commit that made workspaces before the memory value existed: it has the pins and nothing else of the memory value. */
const readAtBefore = (commit, rel) => (rel.endsWith('inputs/explore.json') ? readFileSync(EXPLORE_FILE) : null);

/** A kept tree that holds today's two generated files unless `bytes` says otherwise (`null`: the file is not there). */
function treeWith(bytes) {
  const a = scoringArea({});
  mkdirSync(path.join(a.source, 'samples'), { recursive: true });
  for (const [rel, text] of Object.entries({ 'STATEMENT.md': 'today statement, with the memory lines\n', 'samples/load.json': '{"today":true}\n', ...bytes })) {
    if (text === null) rmSync(path.join(a.source, rel), { force: true });
    else writeFileSync(path.join(a.source, rel), text);
  }
  return a;
}
const TODAY = { ...frozenFiles(), 'STATEMENT.md': f('today statement, with the memory lines\n'), 'samples/load.json': f('{"today":true}\n') };

test('A_series_that_recorded_the_bytes_of_the_generated_files_judges_them_like_a_template_file_and_a_recorded_absence_is_known', async () => {
  const history = templateHistory({ inputs: { generatedSha256: { node: { 'STATEMENT.md': sha('statement then\n'), 'samples/load.json': null } } } });
  assert.deepEqual(history({ stack: 'node', kind: 'daemon', rel: 'STATEMENT.md' }), { known: true, sha256: sha('statement then\n') });
  assert.deepEqual(history({ stack: 'node', kind: 'daemon', rel: 'samples/load.json' }), { known: true, absent: true }, 'a file the workspace did not have at init is a known absence');
  const a = treeWith({ 'STATEMENT.md': 'statement then\n', 'samples/load.json': null });
  const made = await scoreWith(a, { runFn: passAll(), files: TODAY, history });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(made.value.explore.harnessDrift.map(c => c.path).sort(), ['STATEMENT.md', 'samples/load.json'], 'the statement is the old bytes and the load file did not exist: the harness moved, not the agent');
  assert.deepEqual(made.value.explore.frozenChanges, []);
  assert.equal(exploreSection({ score: made.value, audit: [] }).deviations.value.count, 0);
});

test('A_generated_file_the_agent_changed_stays_a_deviation_though_the_harness_moved', async () => {
  const history = templateHistory({ inputs: { generatedSha256: { node: { 'STATEMENT.md': sha('statement then\n'), 'samples/load.json': sha('{"then":true}\n') } } } });
  const a = treeWith({ 'STATEMENT.md': 'the agent rewrote the statement\n', 'samples/load.json': null });
  const made = await scoreWith(a, { runFn: passAll(), files: TODAY, history });
  assert.deepEqual(made.value.explore.frozenChanges.map(c => c.path).sort(), ['STATEMENT.md', 'samples/load.json'], 'a rewrite is the agent\'s, and so is a deletion of a file the series did have');
  assert.equal('harnessDrift' in made.value.explore, false);
});

test('A_file_that_did_not_exist_when_the_series_began_and_is_missing_now_is_the_harnesss_change_and_one_the_agent_made_is_not', async () => {
  const history = () => ({ known: true, absent: true });
  const missing = await scoreWith(treeWith({ 'samples/load.json': null }), { runFn: passAll(), files: TODAY, history });
  assert.deepEqual(missing.value.explore.harnessDrift, [{ path: 'samples/load.json', state: 'absent' }]);
  const made = await scoreWith(treeWith({ 'samples/load.json': '{"the agent":"made it"}\n' }), { runFn: passAll(), files: TODAY, history });
  assert.deepEqual(made.value.explore.frozenChanges.map(c => c.path), ['samples/load.json'], 'a file the agent made where the series had none is the agent\'s');
  const decide = driftDecider({ stack: 'node', kind: 'daemon', treeSha: {}, history });
  assert.equal(decide({ path: 'build.sh', state: 'absent' }), 'harness', 'the rule is the same for every file the harness can move');
});

test('For_a_series_made_before_the_memory_value_the_statement_is_made_again_without_the_memory_lines_and_the_load_file_is_known_absent', async () => {
  const history = templateHistory({ inputs: INPUTS, readAt: readAtBefore, legacy: legacyGenerated({ world: WORLD, inputs: INPUTS }) });
  assert.deepEqual(history({ stack: 'node', kind: 'daemon', rel: 'samples/load.json' }), { known: true, absent: true });
  assert.deepEqual(history({ stack: 'node', kind: 'daemon', rel: 'STATEMENT.md' }), { known: true, sha256: sha(OLD_STATEMENT) });
  const a = treeWith({ 'STATEMENT.md': OLD_STATEMENT, 'samples/load.json': null });
  const made = await scoreWith(a, { runFn: passAll(), files: TODAY, history });
  assert.deepEqual(made.value.explore.harnessDrift.map(c => c.path).sort(), ['STATEMENT.md', 'samples/load.json']);
  assert.deepEqual(made.value.explore.frozenChanges, []);
});

test('Where_the_generated_bytes_cannot_be_made_again_the_file_is_undecided_and_the_deviations_are_unknown', async () => {
  const cannot = [
    ['a spec that is not the series\'s', { world: { ...WORLD, task: { spec: { path: 'p', sha: 'other' } } }, inputs: INPUTS, readAt: readAtBefore }],
    ['a commit that already had the memory value', { world: WORLD, inputs: INPUTS, readAt: (commit, rel) => (rel.endsWith('memory-limit.json') ? Buffer.from('{}') : readAtBefore(commit, rel)) }],
    ['a commit whose pins cannot be read', { world: WORLD, inputs: INPUTS, readAt: () => null }],
  ];
  for (const [what, { world, inputs, readAt }] of cannot) {
    const history = templateHistory({ inputs, readAt: (c, r) => (r.endsWith('memory-limit.json') && readAt(c, r) === null ? null : readAt(c, r)), legacy: legacyGenerated({ world, inputs }) });
    const a = treeWith({ 'STATEMENT.md': 'some bytes\n' });
    const made = await scoreWith(a, { runFn: passAll(), files: TODAY, history });
    assert.deepEqual(made.value.explore.undecidedDrift.map(c => c.path), ['STATEMENT.md'], what);
    assert.deepEqual(made.value.explore.frozenChanges, [], `${what}: the undecided file is charged to nobody`);
    const dev = exploreSection({ score: made.value, audit: [] }).deviations;
    assert.equal(dev.known, false, `${what}: unknown, never a known 0`);
  }
});

test('A_series_without_the_legacy_renderer_cannot_judge_a_generated_file_it_recorded_nothing_for', () => {
  const history = templateHistory({ inputs: INPUTS, readAt: readAtBefore });
  assert.equal(history({ stack: 'node', kind: 'daemon', rel: 'STATEMENT.md' }).known, false);
});

test('init_records_the_bytes_of_the_statement_and_the_load_file_of_each_stack_from_the_workspace_it_would_make', () => {
  const explore = quickExplore(EXPLORE, '/home/nobody');
  const task = quickTask('05');
  const sources = { spec: SPEC, format: readFileSync(fileURLToPath(new URL('../../lang-bench/test/fixtures/conformance/FORMAT.md', import.meta.url)), 'utf8') };
  const inputs = packagesInputs().value;
  const made = generatedHashesNow({ explore, task, sources, inputs });
  assert.equal(made.ok, true, made.reason);
  for (const stack of ['csharp', 'node']) {
    const files = quickWorkspaceFiles({ explore, task, stack, sources, inputs }).value.files;
    assert.deepEqual(made.value[stack], { 'STATEMENT.md': sha(files['STATEMENT.md'].bytes), 'samples/load.json': sha(files['samples/load.json'].bytes) });
  }
  assert.notEqual(made.value.csharp['samples/load.json'], made.value.node['samples/load.json'], 'the load file carries the stack\'s own heap setting');
});

test('init_asks_the_task_repository_for_the_spec_and_refuses_when_it_is_not_there_so_no_series_is_made_without_the_bytes', async () => {
  const sources = { spec: SPEC, format: readFileSync(fileURLToPath(new URL('../../lang-bench/test/fixtures/conformance/FORMAT.md', import.meta.url)), 'utf8') };
  const made = await generatedOf({ explore: EXPLORE, task: quickTask('05'), home: '/home/nobody', sourcesFn: async () => ok(sources) });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(Object.keys(made.value).sort(), ['csharp', 'node']);
  assert.match(made.value.node['STATEMENT.md'], /^[0-9a-f]{64}$/);
  const none = await generatedOf({ explore: EXPLORE, task: quickTask('05'), home: '/home/nobody', sourcesFn: async () => fail('the task repository was not found at its pins') });
  assert.equal(none.ok, false);
  assert.match(none.reason, /task repository/);
});
