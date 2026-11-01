// The workspace of a session (plan D-X6, T-X1): exactly spec 01's R13 files per stack; STATEMENT.md the spec plus the stack sentence; the samples the spec's table; GRAMMAR.md FORMAT v2's sections 3 to 5; the
// judge byte-identical to the harness's. The spec and FORMAT texts come from the local task repository at their pinned shas; a host without it is `hostLacks`.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { hostLacks, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { JUDGE_SOURCE, candidateRepos, fillWorkspace, fillerFor, frozenChanges, grammarOf, loadSources, samplesOf, stackSentence, statementOf, workspaceFiles } from '../lib/workspace.mjs';
import { pristineCopy } from '../lib/score.mjs';
import { REPO } from '../lib/paths.mjs';

const EXPLORE = loadExplore().value;
const TASK = resolveTask(EXPLORE, '01').value;
const TYPES_NODE = JSON.parse(readFileSync(path.join(path.dirname(JUDGE_SOURCE), '..', 'inputs', 'cache-snapshots.json'), 'utf8')).node.resolved.TYPES_NODE;
const ALWAYS = ['PACKAGES.md', 'STATEMENT.md', 'build.sh', 'run.sh', 'runner/GRAMMAR.md', 'runner/run_samples.py', 'samples/s01.in', 'samples/s01.out', 'samples/s02.in', 'samples/s02.out', 'samples/s03.in', 'samples/s03.out'];
const PINS = { csharp: ['global.json', 'nuget.config', 'src/App.csproj'], node: ['.npmrc', 'package.json', 'tsconfig.json'] };

let sourcesOnce;
/** The spec and FORMAT texts at their pins, or null when this host lacks the task repository. */
async function sources(t) {
  sourcesOnce ??= loadSources({ task: TASK, candidates: candidateRepos({ repoName: EXPLORE.specs.repoName, repoRoot: REPO, commonRoot: path.resolve(REPO, '..', '..', 'dew_flow_benchmark') }) });
  const r = await sourcesOnce;
  return r.ok ? r.value : (hostLacks(t, r.reason) && null);
}

test('A_workspace_holds_exactly_the_frozen_files_of_R13_for_its_stack', async t => {
  const s = await sources(t);
  if (!s) return;
  for (const stack of ['csharp', 'node']) {
    const files = workspaceFiles({ explore: EXPLORE, stack, sources: s, typesNode: TYPES_NODE });
    assert.deepEqual(Object.keys(files).sort(), [...ALWAYS, ...PINS[stack]].sort(), stack);
  }
});

test('STATEMENT_is_the_specs_bytes_plus_the_stack_sentence_and_the_two_stacks_differ_in_nothing_else', async t => {
  const s = await sources(t);
  if (!s) return;
  const text = stack => workspaceFiles({ explore: EXPLORE, stack, sources: s, typesNode: TYPES_NODE })['STATEMENT.md'].bytes.toString('utf8');
  for (const stack of ['csharp', 'node']) {
    assert.ok(text(stack).startsWith(s.spec), 'the spec comes first, byte for byte');
    assert.equal(text(stack), `${s.spec}\n${stackSentence(EXPLORE, stack)}\n`);
  }
  assert.equal(text('csharp').replace(stackSentence(EXPLORE, 'csharp'), '@'), text('node').replace(stackSentence(EXPLORE, 'node'), '@'));
  assert.match(stackSentence(EXPLORE, 'csharp'), /SDK 10\.0\.114/);
  assert.match(stackSentence(EXPLORE, 'node'), /TypeScript 6\.0\.3/);
});

test('The_samples_are_the_specs_section_6_table_as_raw_bytes_with_no_header', async t => {
  const s = await sources(t);
  if (!s) return;
  const files = workspaceFiles({ explore: EXPLORE, stack: 'node', sources: s, typesNode: TYPES_NODE });
  assert.deepEqual(samplesOf(s.spec).map(x => x.id), ['s01', 's02', 's03']);
  const expected = { s01: ['1\n', '1\n'], s02: ['16\n', '16 8 4 2 1\n'], s03: ['7\n', '7 22 11 34 17 52 26 13 40 20 10 5 16 8 4 2 1\n'] };
  for (const [id, [input, output]] of Object.entries(expected)) {
    assert.equal(files[`samples/${id}.in`].bytes.toString('utf8'), input, `${id}.in`);
    assert.equal(files[`samples/${id}.out`].bytes.toString('utf8'), output, `${id}.out`);
  }
  assert.equal(files['samples/s03.out'].bytes.length, 45, 'FORMAT v2 section 6.1: the third sample is 45 bytes');
});

test('GRAMMAR_is_FORMAT_sections_3_to_5_byte_for_byte', async t => {
  const s = await sources(t);
  if (!s) return;
  const lines = s.format.split('\n');
  const from = lines.indexOf('## 3. Visible samples');
  const to = lines.indexOf('## 6. Worked examples');
  assert.ok(from > 0 && to > from, 'the headings are found by a second method (line scan)');
  const expected = `${lines.slice(from, to).join('\n').replace(/\n+$/, '')}\n`;
  const got = workspaceFiles({ explore: EXPLORE, stack: 'csharp', sources: s, typesNode: TYPES_NODE })['runner/GRAMMAR.md'].bytes.toString('utf8');
  assert.equal(got, expected);
  assert.equal(got, grammarOf(s.format));
  assert.ok(got.startsWith('## 3. Visible samples\n') && got.endsWith('is kept with the test\'s record.\n'));
  assert.ok(!got.includes('## 6.') && got.includes('### 5.6 The verdict and the mismatch report'));
});

test('The_judge_in_every_workspace_is_the_harnesss_file_byte_for_byte', async t => {
  const s = await sources(t);
  if (!s) return;
  for (const stack of ['csharp', 'node']) {
    assert.ok(workspaceFiles({ explore: EXPLORE, stack, sources: s, typesNode: TYPES_NODE })['runner/run_samples.py'].bytes.equals(readFileSync(JUDGE_SOURCE)));
  }
});

test('The_templates_carry_the_pins_of_explore_json_and_no_placeholder_is_left', async t => {
  const s = await sources(t);
  if (!s) return;
  const cs = workspaceFiles({ explore: EXPLORE, stack: 'csharp', sources: s, typesNode: TYPES_NODE });
  const nd = workspaceFiles({ explore: EXPLORE, stack: 'node', sources: s, typesNode: TYPES_NODE });
  const text = (files, rel) => files[rel].bytes.toString('utf8');
  assert.deepEqual(JSON.parse(text(cs, 'global.json')), { sdk: { version: EXPLORE.dotnet.sdk, rollForward: 'disable' } });
  const pkg = JSON.parse(text(nd, 'package.json'));
  assert.equal(pkg.engines.node, EXPLORE.node.version);
  assert.deepEqual(pkg.devDependencies, { '@types/node': TYPES_NODE });
  assert.match(text(nd, 'PACKAGES.md'), new RegExp(`TypeScript ${EXPLORE.typescript.version.replaceAll('.', '\\.')}.*${TYPES_NODE.replaceAll('.', '\\.')}`, 's'));
  assert.equal(text(nd, '.npmrc'), 'offline=true\n');
  for (const f of [...Object.values(cs), ...Object.values(nd)]) assert.ok(!f.bytes.toString('utf8').includes('{TYPES_NODE}'));
  assert.match(text(cs, 'build.sh'), /-m:4/, 'a build bounds its worker pool (csharp.dotnet-build)');
  assert.match(text(cs, 'run.sh'), /\nexec dotnet out\/App\.dll\n$/);
  assert.match(text(nd, 'run.sh'), /\nexec node src\/main\.ts\n$/);
  assert.match(text(nd, 'build.sh'), /exec tsc --noEmit\n$/);
  assert.match(text(nd, 'tsconfig.json'), /"strict": true/);
});

test('A_sources_failure_names_the_pin_that_does_not_hold', async () => {
  const repo = tmpDir('taskrepo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  mkdirSync(path.join(repo, 'todo', 'tasks'), { recursive: true });
  const SPEC = 'spec\n\n## 6. Visible samples\n\n| Id | Standard input | Expected standard output |\n|---|---|---|\n| `s01` | `1⏎` | `1⏎` |\n';
  writeFileSync(path.join(repo, 'todo', 'tasks', 'spec.md'), SPEC);
  writeFileSync(path.join(repo, 'todo', 'tasks', 'FORMAT.md'), 'format\n');
  git('add', '.');
  git('commit', '-q', '-m', 'x');
  const sha = git('rev-parse', 'HEAD').trim();
  const pin = (file, text) => ({ sha, path: `todo/tasks/${file}`, sha256: require_sha(text) });
  const task = { id: '77', spec: pin('spec.md', SPEC), format: pin('FORMAT.md', 'format\n') };
  const good = await loadSources({ task, candidates: [repo] });
  assert.equal(good.ok, true, good.reason);
  assert.deepEqual([good.value.spec, good.value.format], [SPEC, 'format\n']);
  const wrong = await loadSources({ task: { ...task, format: { ...task.format, sha256: '0'.repeat(64) } }, candidates: [repo] });
  assert.equal(wrong.ok, false);
  assert.match(wrong.reason, /FORMAT\.md at .* hashes to/);
  const wrongSpec = await loadSources({ task: { ...task, spec: { ...task.spec, sha256: '1'.repeat(64) } }, candidates: [repo] });
  assert.match(wrongSpec.reason, /the spec of task 77 at .* hashes to/, 'the spec pin of a task is checked like spec01\'s was');
  const none = await loadSources({ task, candidates: [path.join(repo, 'nowhere')] });
  assert.equal(none.ok, false);
  assert.match(none.reason, /not found at its pins/);
});

function require_sha(text) {
  return execFileSync('sha256sum', [], { input: text, encoding: 'utf8' }).split(' ')[0];
}

test('A_frozen_file_the_agent_changed_is_reported_and_put_back_in_the_pristine_copy', async t => {
  const s = await sources(t);
  if (!s) return;
  const files = workspaceFiles({ explore: EXPLORE, stack: 'csharp', sources: s, typesNode: TYPES_NODE });
  const work = tmpDir('ws');
  assert.equal(fillerFor({ explore: EXPLORE, sources: s })({ item: { session: { stack: 'csharp' } }, layout: { work } }).ok, true);
  assert.deepEqual(frozenChanges({ files, work }), []);
  writeFileSync(path.join(work, 'build.sh'), 'echo cheat\n');
  writeFileSync(path.join(work, 'src', 'Program.cs'), 'class P {}\n');
  const copy = path.join(tmpDir('copy'), 'tree');
  const changed = pristineCopy({ source: work, dest: copy, stack: 'csharp', files });
  assert.deepEqual(changed, [{ path: 'build.sh', state: 'changed' }]);
  assert.equal(readFileSync(path.join(copy, 'build.sh'), 'utf8'), files['build.sh'].bytes.toString('utf8'), 'the frozen file is restored in the scoring copy');
  assert.equal(readFileSync(path.join(copy, 'src', 'Program.cs'), 'utf8'), 'class P {}\n', 'the agent\'s own source is kept');
  assert.equal(existsSync(path.join(copy, 'bin')), false);
  assert.equal(fillWorkspace({ files: { 'a/b.txt': { bytes: Buffer.from('x'), mode: 0o644 } }, work: tmpDir('ws2') }).ok, true);
  assert.equal(fillerFor({ explore: EXPLORE, sources: s })({ item: { session: { stack: 'cobol' } }, layout: { work } }).ok, false, 'an unknown stack names the legal ones');
});
