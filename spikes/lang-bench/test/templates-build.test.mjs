// E1.S3 phase A, the builder: what a built workspace holds and how two of them differ, the two refusals (a destination inside a git worktree, a task of another group), the hashes of the runner, of FORMAT and of its
// grammar, and the agent-visible text of the templates (the push check's scans, no other stack, no comparison word). The files of the templates are in templates.test.mjs; the box tests are in workspace-exec.test.mjs.
// Most cases build with the synthetic FORMAT of the FORMAT tools' tests (no task repository needed); the cases that name the real record use `realDraft` and skip without LANG_V2_TASK_REPO.
import assert from 'node:assert/strict';
import { chmodSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fail } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { renderPackagesMd } from '../lib/packages-md.mjs';
import { findStackFolderFiles } from '../lib/push-visible.mjs';
import { scanText } from '../lib/push-scan.mjs';
import { computePin } from '../lib/runner-pin.mjs';
import { CSHARP_PINS, diffTaskPair } from '../lib/workspace-diff.mjs';
import { PINNED_TASK_SHA } from '../lib/task-repo.mjs';
import { buildWorkspace, loadInputs, runnerHash, stackFilePatterns } from '../lib/workspace-build.mjs';
import { edit, FORMAT_TEXT, realDraft, sha256 } from './support/format-fixtures.mjs';
import { loadInputsForTest } from './support/push-inputs.mjs';
import { KINDS, SPIKE, STACKS, TEMPLATES, TEMPLATE_FILES, filesUnder, formatRow, git, helloSamples, helloStatement, inputsWith, readJson, recipeRecord } from './support/workspace-fixtures.mjs';
import { owned } from './support/owned.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const SYN = formatRow();
const realInputs = () => loadInputs().value;
const read = file => readFileSync(file, 'utf8');

/** The arguments of one build with the synthetic FORMAT and the hello olympiad statement and samples (`helloFixture`); `over` replaces any of them. The destination is a fresh `w` in an owned folder. */
function args(t, over = {}) {
  const kind = over.kind ?? 'olympiad';
  return {
    stack: 'csharp', kind, task: 'hello', group: 'A', out: path.join(ownedFolder(t, 'out'), 'w'), statement: readFileSync(helloStatement(kind)), samplesDir: helloSamples(kind),
    formatBytes: SYN.bytes, inputs: inputsWith({ A: SYN.row }), helloFixture: true, ...over,
  };
}
const built = (t, over = {}) => {
  const a = args(t, over);
  const r = buildWorkspace(a);
  assert.equal(r.ok, true, r.reason);
  return a.out;
};

// ---- what a built workspace holds ------------------------------------------------------------------------------------------------------------

const D33 = /parser|adapter|grader/i;
// Three files are checked by NAME only: the lock and the packages list name packages (`@typescript-eslint/parser`), and the lint config of the contract has the option `parserOptions`.
const NAME_ONLY = Object.freeze(['package-lock.json', 'PACKAGES.md', 'eslint.config.mjs']);
/** The paths outside `runner/` and `samples/` whose name, or whose text (but for `NAME_ONLY`), names a parser, an adapter or a grader (D33). */
function d33Problems(root) {
  return filesUnder(root).filter(rel => !/^(runner|samples)\//.test(rel))
    .filter(rel => D33.test(rel) || (!NAME_ONLY.includes(rel) && D33.test(read(path.join(root, rel))))).map(rel => `${rel} names a parser, an adapter or a grader`);
}

test('Olympiad_workspaces_have_the_same_file_set_per_stack_and_no_parser_adapter_or_grader_file', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const inputs = realInputs();
  for (const stack of STACKS) {
    const [first, second] = ['01', '09'].map(task => {
      const samples = writeTree(ownedFolder(t, 'smp'), { 's01.in': `input of ${task}\n`, 's01.out': `output of ${task}\n` });
      return built(t, { stack, task, helloFixture: false, inputs, formatBytes: draft.bytes, samplesDir: samples, statement: `statement of ${task}\n` });
    });
    const [a, b] = [filesUnder(first), filesUnder(second)];
    assert.deepEqual(a, b, `${stack}: the same paths`);
    for (const rel of a.filter(rel => rel !== 'STATEMENT.md' && !rel.startsWith('samples/'))) assert.deepEqual(readFileSync(path.join(first, rel)), readFileSync(path.join(second, rel)), `${stack}/${rel}: the same bytes`);
    assert.deepEqual(d33Problems(first), []);
    const planted = path.join(ownedFolder(t, 'copy'), 'w');
    cpSync(first, planted, { recursive: true });
    mkdirSync(path.join(planted, 'src'));
    writeFileSync(path.join(planted, 'src', 'parser.ts'), 'export {};\n');
    assert.deepEqual(d33Problems(planted), ['src/parser.ts names a parser, an adapter or a grader'], 'the check can fail');
  }
});

const SAME = Object.freeze({ csharp: ['App.csproj'], node: ['package-lock.json', 'package.json'] });

/** The paths where the built workspaces of the kinds differ in bytes or presence, over every file but the three of `SAME`. */
function kindProblems(stack, dirs) {
  const names = [...new Set(dirs.flatMap(filesUnder))].sort();
  return names.filter(rel => !SAME[stack].includes(rel)).filter(rel => new Set(dirs.map(d => (existsSync(path.join(d, rel)) ? sha256(readFileSync(path.join(d, rel))) : null))).size > 1).map(rel => `${rel} differs between the kinds`);
}

test('The_kinds_of_a_stack_differ_only_in_the_listed_files', t => {
  for (const stack of STACKS) {
    const dirs = KINDS.map(kind => built(t, { stack, kind, statement: readFileSync(helloStatement('olympiad')), samplesDir: helloSamples('olympiad') }));
    assert.deepEqual(kindProblems(stack, dirs), [], `${stack}: only ${SAME[stack].join(' and ')} differ`);
    assert.equal(new Set(dirs.map(d => sha256(readFileSync(path.join(d, 'PACKAGES.md'))))).size, 1, `${stack}: PACKAGES.md is byte-equal across the kinds (D47)`);
    const planted = path.join(ownedFolder(t, 'copy'), 'w');
    cpSync(dirs[1], planted, { recursive: true });
    writeFileSync(path.join(planted, 'build.sh'), `${read(path.join(planted, 'build.sh'))}echo changed\n`);
    assert.deepEqual(kindProblems(stack, [dirs[0], planted, dirs[2]]), ['build.sh differs between the kinds'], 'the check can fail');
  }
});

test('The_two_stacks_differ_only_in_the_listed_stack_files', t => {
  const [cs, nd] = STACKS.map(stack => built(t, { stack }));
  assert.deepEqual(diffTaskPair({ csharp: cs, node: nd }).findings, []);
  const [a, b] = [filesUnder(cs), filesUnder(nd)];
  const differing = [...new Set([...a, ...b])].filter(rel => !a.includes(rel) || !b.includes(rel) || !readFileSync(path.join(cs, rel)).equals(readFileSync(path.join(nd, rel)))).sort();
  assert.deepEqual(differing, [...new Set([...TEMPLATE_FILES.csharp, ...TEMPLATE_FILES.node, 'PACKAGES.md'])].sort(), 'exactly the two stacks\' own files');
  assert.ok(CSHARP_PINS.includes('build.sh') && CSHARP_PINS.includes('PACKAGES.md'));
});

// ---- the destination --------------------------------------------------------------------------------------------------------------------------

async function repoWith(t, files) {
  const repo = writeTree(ownedFolder(t, 'tree'), files);
  await git(repo, ['init', '-q', '-b', 'main']);
  await git(repo, ['add', '-A']);
  await git(repo, ['commit', '-q', '-m', 'files']);
  return repo;
}

function refusedInside(t, worktree, outs) {
  for (const out of outs) {
    const r = buildWorkspace(args(t, { out }));
    assert.equal(r.ok, false, out);
    assert.equal(r.refusal, true);
    assert.ok(r.reason.includes(out) && r.reason.includes(worktree), `the message names ${out} and ${worktree}: ${r.reason}`);
    if (out !== worktree) assert.equal(existsSync(out), false, `nothing was written at ${out}`);
  }
}

test('The_builder_refuses_a_destination_inside_a_clean_git_worktree', async t => {
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  refusedInside(t, repo, [repo, path.join(repo, 'ws'), path.join(repo, 'deep', 'er')]);
  assert.deepEqual(readdirSync(repo).filter(n => n !== '.git'), ['a.txt']);
});

test('The_builder_refuses_a_destination_inside_a_dirty_git_worktree', async t => {
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  writeFileSync(path.join(repo, 'a.txt'), 'changed\n');
  writeFileSync(path.join(repo, 'untracked.txt'), 'new\n');
  refusedInside(t, repo, [path.join(repo, 'ws')]);
});

test('The_builder_refuses_a_nested_destination_that_does_not_exist_yet', async t => {
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  refusedInside(t, repo, [path.join(repo, 'a', 'b', 'c')]);
  assert.equal(existsSync(path.join(repo, 'a')), false, 'the first missing folder was not created either');
});

test('The_builder_refuses_a_linked_worktree_whose_git_is_a_file', async t => {
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  const linked = owned.ownedWorktree(t, repo, 'link');
  assert.equal(readFileSync(path.join(linked.path, '.git'), 'utf8').startsWith('gitdir:'), true, 'its .git is a file');
  refusedInside(t, linked.path, [path.join(linked.path, 'ws')]);
});

test('The_builder_refuses_a_linked_worktree_whose_admin_folder_is_gone_and_a_path_inside_a_git_folder_and_a_bare_repository', async t => {
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  const linked = owned.ownedWorktree(t, repo, 'link');
  t.after(() => rmSync(linked.path, { recursive: true, force: true }));
  rmSync(path.join(repo, '.git', 'worktrees'), { recursive: true, force: true });
  const orphan = path.join(linked.path, 'ws');
  const refusedOrphan = buildWorkspace(args(t, { out: orphan }));
  assert.equal(refusedOrphan.ok, false, 'git cannot answer for a worktree whose admin folder is gone, so the walk of .git entries does');
  assert.ok(refusedOrphan.reason.includes(linked.path), refusedOrphan.reason);
  const inGit = path.join(repo, '.git', 'ws');
  const refusedInGit = buildWorkspace(args(t, { out: inGit }));
  assert.equal(refusedInGit.ok, false);
  assert.match(refusedInGit.reason, /inside a \.git folder/);
  const bare = ownedFolder(t, 'bare');
  await git(bare, ['init', '-q', '--bare']);
  const refusedBare = buildWorkspace(args(t, { out: path.join(bare, 'ws') }));
  assert.equal(refusedBare.ok, false);
  assert.match(refusedBare.reason, /bare git repository/);
  for (const out of [orphan, inGit, path.join(bare, 'ws')]) assert.equal(existsSync(out), false, `nothing was written at ${out}`);
});

test('A_link_into_a_repository_is_a_way_in_and_is_refused', async t => {
  const bare = ownedFolder(t, 'bare');
  await git(bare, ['init', '-q', '--bare']);
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  const places = ownedFolder(t, 'jump');
  symlinkSync(path.join(bare, 'refs'), path.join(places, 'r'));
  symlinkSync(path.join(repo, '.git', 'refs'), path.join(places, 'g'));
  for (const link of ['r', 'g']) {
    const out = path.join(places, link, 'ws');
    const r = buildWorkspace(args(t, { out }));
    assert.equal(r.ok, false, `${link}: a path through a link into a repository`);
    assert.equal(r.refusal, true);
    assert.match(r.reason, /through a link/);
    assert.equal(existsSync(path.join(places, 'r', 'ws')) || existsSync(path.join(places, 'g', 'ws')), false);
  }
});

test('Three_plain_folders_or_a_text_file_called_HEAD_are_not_a_repository_and_a_real_HEAD_is', t => {
  const plainFolders = ownedFolder(t, 'plain');
  for (const name of ['HEAD', 'objects', 'refs']) mkdirSync(path.join(plainFolders, name));
  const textHead = writeTree(ownedFolder(t, 'text'), { HEAD: 'just a note\n' });
  for (const name of ['objects', 'refs']) mkdirSync(path.join(textHead, name));
  const realHead = writeTree(ownedFolder(t, 'real'), { HEAD: 'ref: refs/heads/main\n' });
  for (const name of ['objects', 'refs']) mkdirSync(path.join(realHead, name));
  assert.equal(buildWorkspace(args(t, { out: path.join(plainFolders, 'ws') })).ok, true, 'three plain folders with those names are not refused');
  assert.equal(buildWorkspace(args(t, { out: path.join(textHead, 'ws') })).ok, true, 'a text file called HEAD that is not a git HEAD is not refused');
  const refused = buildWorkspace(args(t, { out: path.join(realHead, 'ws') }));
  assert.equal(refused.ok, false, 'a HEAD file that reads as one, beside objects and refs folders, is a bare repository');
  assert.match(refused.reason, /bare git repository/);
});

test('A_destination_the_filesystem_cannot_judge_is_a_refusal_with_a_reason_and_no_trace', t => {
  const root = ownedFolder(t, 'jud');
  const closed = path.join(root, 'closed');
  mkdirSync(closed);
  symlinkSync(path.join(root, 'loop-b'), path.join(root, 'loop-a'));
  symlinkSync(path.join(root, 'loop-a'), path.join(root, 'loop-b'));
  const cases = [['permission denied', path.join(closed, 'ws', 'w'), /EACCES/], ['a link loop', path.join(root, 'loop-a', 'ws'), /ELOOP/], ['a name that is too long', path.join(root, 'n'.repeat(300), 'w'), /ENAMETOOLONG/]];
  chmodSync(closed, 0o000);
  try {
    for (const [what, out, code] of cases) {
      const r = buildWorkspace(args(t, { out }));
      assert.equal(r.ok, false, what);
      assert.equal(r.refusal, true, `${what}: a refusal, not a throw`);
      assert.match(r.reason, /cannot be judged/, what);
      assert.match(r.reason, code, what);
    }
  } finally {
    chmodSync(closed, 0o755);
  }
});

test('A_failed_write_leaves_no_destination_and_no_staging_folder_and_a_retry_is_not_refused', t => {
  const samples = writeTree(ownedFolder(t, 'smp'), { 's01.in': 'a\n', 's01.out': 'b\n' });
  chmodSync(path.join(samples, 's01.out'), 0o000);
  t.after(() => { if (existsSync(path.join(samples, 's01.out'))) chmodSync(path.join(samples, 's01.out'), 0o644); });
  const a = args(t, { samplesDir: samples });
  const parent = path.dirname(a.out);
  const failed = buildWorkspace(a);
  assert.equal(failed.ok, false, 'a file that cannot be read stops the write');
  assert.match(failed.reason, /staging folder of this call was removed/);
  assert.deepEqual(readdirSync(parent), [], 'neither the destination nor a staging folder is left');
  chmodSync(path.join(samples, 's01.out'), 0o644);
  const retried = buildWorkspace(a);
  assert.equal(retried.ok, true, retried.reason);
  assert.deepEqual(readdirSync(parent), ['w'], 'the retry is not refused and leaves only the workspace');
});

test('A_git_that_cannot_answer_is_a_refusal_and_not_a_pass', t => {
  const a = args(t);
  const r = buildWorkspace({ ...a, worktreeOf: () => fail('git is not here') });
  assert.equal(r.ok, false);
  assert.match(r.reason, /git is not here/);
  assert.equal(existsSync(a.out), false);
});

test('A_destination_outside_every_worktree_is_built_and_an_existing_full_one_is_refused', t => {
  const a = args(t);
  const first = buildWorkspace(a);
  assert.equal(first.ok, true, first.reason);
  assert.deepEqual(first.value.files, filesUnder(a.out), 'the files returned are the files written');
  const again = buildWorkspace(a);
  assert.equal(again.ok, false);
  assert.match(again.reason, /exists and is not empty/);
  const empty = path.join(ownedFolder(t, 'empty'));
  assert.equal(buildWorkspace({ ...a, out: empty }).ok, true, 'an existing empty folder is a fine destination');
  const target = ownedFolder(t, 'target');
  const link = path.join(ownedFolder(t, 'lnk'), 'ws');
  symlinkSync(target, link);
  const viaLink = buildWorkspace({ ...a, out: link });
  assert.equal(viaLink.ok, false);
  assert.match(viaLink.reason, /is a link/);
  assert.deepEqual(readdirSync(target), []);
});

// ---- the runner and FORMAT --------------------------------------------------------------------------------------------------------------------

test('The_workspace_runner_hash_equals_the_record', t => {
  const out = built(t);
  const pin = readJson('inputs/specs.json').runner;
  const hash = runnerHash(path.join(out, 'runner'));
  assert.equal(hash.ok, true, hash.reason);
  assert.equal(hash.value.runner.manifestSha256, pin.manifestSha256);
  assert.equal(hash.value.runner.version, pin.version);
  assert.equal(computePin(path.join(out, 'runner')).ok, false, 'the unfiltered pin refuses GRAMMAR.md');
  const copy = path.join(ownedFolder(t, 'copy'), 'runner');
  cpSync(path.join(out, 'runner'), copy, { recursive: true });
  const file = path.join(copy, 'run_samples.py');
  writeFileSync(file, `${read(file)}# one more line\n`);
  assert.notEqual(runnerHash(copy).value.runner.manifestSha256, pin.manifestSha256);
});

test('The_builder_refuses_a_local_runner_that_is_not_the_pin', t => {
  const a = args(t);
  const inputs = { ...a.inputs, specs: { ...a.inputs.specs, runner: { ...a.inputs.specs.runner, manifestSha256: '0'.repeat(64) } } };
  const r = buildWorkspace({ ...a, inputs });
  assert.equal(r.ok, false);
  assert.equal(r.refusal, true);
  assert.match(r.reason, /pin/);
  assert.equal(existsSync(a.out), false);
});

test('The_workspace_grammar_hash_equals_the_format_record', t => {
  const draft = realDraft(t);
  if (draft === null) return;
  const out = built(t, { formatBytes: draft.bytes, inputs: realInputs(), task: '01', helloFixture: false });
  assert.equal(sha256(readFileSync(path.join(out, 'runner', 'GRAMMAR.md'))), readJson('inputs/specs.d/format.json').A.grammarSha256);
});

test('A_format_that_is_not_the_recorded_one_is_refused_naming_both_hashes', t => {
  const a = args(t);
  const changed = Buffer.from(FORMAT_TEXT.replace('Format version: 2', 'Format version: 3'), 'utf8');
  const r = buildWorkspace({ ...a, formatBytes: changed });
  assert.equal(r.ok, false);
  assert.ok(r.reason.includes(sha256(changed)) && r.reason.includes(SYN.row.sha256), r.reason);
  assert.equal(existsSync(a.out), false);
  const grammar = buildWorkspace({ ...a, inputs: inputsWith({ A: { ...SYN.row, grammarSha256: '1'.repeat(64) } }) });
  assert.match(grammar.reason, /grammar/);
  assert.ok(grammar.reason.includes('1'.repeat(64)), 'the recorded grammar hash is named');
});

test('The_packages_list_is_the_renderers_output', t => {
  const inputs = realInputs();
  for (const stack of STACKS) {
    const out = built(t, { stack });
    const rendered = renderPackagesMd({ stack, record: inputs.record, recipe: inputs.recipe });
    assert.equal(rendered.ok, true, rendered.reason);
    assert.equal(read(path.join(out, 'PACKAGES.md')), rendered.value, stack);
  }
  assert.ok(Object.keys(recipeRecord().stacks).length === 2);
});

// ---- the group rule (T-L57) ---------------------------------------------------------------------------------------------------------------------

const SECOND_FORMAT = edit(FORMAT_TEXT, 'Strings and escapes.', 'Strings and escapes, as the second format states them.');
const TWO_GROUPS = () => {
  const [a, b] = [formatRow(FORMAT_TEXT, ['01']), formatRow(SECOND_FORMAT, ['04'])];
  return { a, b, inputs: inputsWith({ A: a.row, B: b.row }) };
};

test('The_build_of_B_refuses_to_write_into_A_tasks', t => {
  const { a, inputs } = TWO_GROUPS();
  const over = { inputs, formatBytes: a.bytes, helloFixture: false };
  const r = buildWorkspace(args(t, { ...over, group: 'B', task: '01' }));
  assert.equal(r.ok, false);
  assert.equal(r.refusal, true);
  assert.match(r.reason, /the build of B refuses to write into A's tasks: the task 01 belongs to group A/);
  const out = path.join(ownedFolder(t, 'out'), 'never');
  assert.equal(buildWorkspace(args(t, { ...over, group: 'B', task: '01', out })).ok, false);
  assert.equal(existsSync(out), false, 'the refusal came before any folder existed');
});

test('A_task_of_B_is_refused_under_A_and_a_task_in_no_row_is_refused', t => {
  const { a, inputs } = TWO_GROUPS();
  const over = { inputs, formatBytes: a.bytes, helloFixture: false };
  assert.match(buildWorkspace(args(t, { ...over, group: 'A', task: '04' })).reason, /refuses to write into B's tasks/);
  assert.match(buildWorkspace(args(t, { ...over, group: 'A', task: 'zz' })).reason, /in no group's row/);
  assert.match(buildWorkspace(args(t, { ...over, group: 'C', task: '01' })).reason, /group "C" is not one of A, B/);
  assert.equal(buildWorkspace(args(t, { ...over, group: 'A', task: '01' })).ok, true, 'the same call with the right group is built');
});

test('A_task_takes_the_grammar_of_the_group_its_row_names', t => {
  const { a, b, inputs } = TWO_GROUPS();
  const grammarOf = out => sha256(readFileSync(path.join(out, 'runner', 'GRAMMAR.md')));
  const forA = built(t, { inputs, formatBytes: a.bytes, group: 'A', task: '01', helloFixture: false });
  const forB = built(t, { inputs, formatBytes: b.bytes, group: 'B', task: '04', helloFixture: false });
  assert.equal(grammarOf(forA), a.row.grammarSha256);
  assert.equal(grammarOf(forB), b.row.grammarSha256);
  assert.notEqual(grammarOf(forA), grammarOf(forB));
  const wrong = buildWorkspace(args(t, { inputs, formatBytes: b.bytes, group: 'A', task: '01', helloFixture: false }));
  assert.equal(wrong.ok, false, 'the other group\'s FORMAT is refused');
  assert.match(wrong.reason, /FORMAT\.md has sha256/);
});

// ---- the agent-visible text ----------------------------------------------------------------------------------------------------------------------

const AID = 'The invoice arrives on Tuesday and the ledger shows nothing unusual. A violet kettle sits beside the window while the clerks argue about parking.';
const stackFiles = () => readJson('templates/stack-files.json');

/** `[{ path: '<stack>/01-x/<rel>', text }]` of every template file of a stack (all kinds, the lock left out), its derived PACKAGES.md and the grammar when asked. */
function visibleTexts(t, stack, { grammar = false } = {}) {
  const out = built(t, { stack });
  const files = KINDS.flatMap(kind => TEMPLATE_FILES[stack].filter(name => name !== 'package-lock.json').map(name => ({ rel: name, file: path.join(TEMPLATES, stack, kind, name) })));
  const derived = ['PACKAGES.md', ...(grammar ? ['runner/GRAMMAR.md'] : [])].map(rel => ({ rel, file: path.join(out, rel) }));
  return [...files, ...derived].map(({ rel, file }) => ({ path: `${stack}/01-x/${rel}`, text: read(file) }));
}

test('The_templates_pass_the_scans_of_the_push_check', t => {
  const inputs = loadInputsForTest({ aidText: AID });
  for (const stack of STACKS) {
    for (const { path: file, text } of [...visibleTexts(t, stack, { grammar: true }), ...(stack === 'node' ? KINDS.map(kind => ({ path: `node/01-x/package-lock.json`, text: read(path.join(TEMPLATES, 'node', kind, 'package-lock.json')) })) : [])]) {
      const found = scanText(file, text, inputs);
      for (const check of ['leakcheck', 'secrets', 'run-base-text', 'family-names']) assert.deepEqual(found[check], [], `${file}: ${check}`);
    }
  }
  const [cs, nd] = STACKS.map(stack => built(t, { stack }));
  const entries = new Map(STACKS.flatMap((stack, i) => filesUnder([cs, nd][i]).map(rel => [`${stack}/01-x/${rel}`, { oid: 'x' }])));
  assert.deepEqual(findStackFolderFiles(entries, { templateFiles: stackFiles() }), [], 'a whole built pair is inside the template set');
  const runnerHits = readdirSync(path.join(cs, 'runner')).filter(n => n.endsWith('.py')).flatMap(n => scanText(`csharp/01-x/runner/${n}`, read(path.join(cs, 'runner', n)), inputs)['run-base-text']);
  t.diagnostic(`the pinned runner is not in this scan; the run-base-text rule finds ${runnerHits.length} plan-id hits in runner/*.py (the plan ids are the runner's own comments)`);
  const base = read(path.join(TEMPLATES, 'csharp', 'olympiad', 'build.sh'));
  const plantedLine = base.split('\n').length;
  const planted = scanText('csharp/01-x/build.sh', `${base}# E1.S3 of dew_flow_benchmark\n`, inputs);
  assert.deepEqual(planted['run-base-text'].map(f => [f.line, f.what]), [[plantedLine, 'plan story id']], 'the plan id is found, on its line');
  assert.equal(planted['family-names'].length, 1, 'the repository name is found');
  assert.equal(planted['family-names'][0].line, plantedLine);
});

const OTHER_STACK = Object.freeze({ csharp: /\b(node|npm|npx|typescript|tsc|eslint|fastify|pino)\b/i, node: /\b(dotnet|c#|csharp|nuget|msbuild|asp\.net)\b|\.net\b/i });
const COMPARISON = /\b(comparison|compared|benchmark|bench|experiment|study|other stack|lang-v2|group [abc])\b/i;

test('The_templates_name_no_other_stack_and_no_comparison', t => {
  for (const stack of STACKS) {
    for (const { path: file, text } of visibleTexts(t, stack)) {
      assert.doesNotMatch(text, OTHER_STACK[stack], `${file}: names the other stack`);
      assert.doesNotMatch(text, COMPARISON, `${file}: names the comparison`);
    }
    const planted = `${read(path.join(TEMPLATES, stack, 'olympiad', 'build.sh'))}# ${stack === 'csharp' ? 'npm' : 'nuget'} and a comparison\n`;
    assert.match(planted, OTHER_STACK[stack], 'the check can fail');
    assert.match(planted, COMPARISON, 'the check can fail');
  }
});

test('A_cs_or_ts_file_in_a_task_folder_stays_refused', () => {
  const entries = new Map([['csharp/01-x/Program.cs', { oid: 'x' }], ['node/01-x/src/main.ts', { oid: 'x' }], ['csharp/01-x/build.sh', { oid: 'x' }]]);
  const found = findStackFolderFiles(entries, { templateFiles: stackFiles() });
  assert.deepEqual(found.map(f => f.path), ['csharp/01-x/Program.cs', 'node/01-x/src/main.ts']);
});

test('The_stack_files_list_is_what_the_function_derives', () => {
  const derived = `${JSON.stringify(stackFilePatterns({ specs: readJson('inputs/specs.json') }), null, 2)}\n`;
  assert.equal(read(path.join(TEMPLATES, 'stack-files.json')), derived);
  assert.deepEqual(stackFiles(), [...stackFiles()].sort(), 'sorted');
});

// ---- the command -----------------------------------------------------------------------------------------------------------------------------

/** The command itself, through the bounded runner, with a `HOME` of the test's own (no test reads or writes the real ~/.cache/lang-v2): `{ code, stdout, stderr }`. */
async function command(home, flags) {
  const r = await runBounded(process.execPath, [path.join(SPIKE, 'build-workspaces.mjs'), ...flags], { timeoutMs: 120_000, env: { PATH: '/usr/bin:/bin', HOME: home }, maxBytes: 1024 * 1024 });
  assert.equal(r.ok, true, r.reason);
  return r.value;
}

test('The_command_exits_2_for_usage_3_for_each_refusal_and_0_for_the_list', async t => {
  const home = ownedFolder(t, 'home');
  const run = flags => command(home, flags);
  const out = path.join(ownedFolder(t, 'out'), 'w');
  const statement = helloStatement('olympiad');
  const build = extra => ['--stack', 'csharp', '--kind', 'olympiad', '--task', 'hello', '--group', 'A', '--out', out, '--statement', statement, '--samples', helloSamples('olympiad'), ...extra];
  assert.equal((await run([])).code, 2, 'no flags');
  assert.equal((await run(['--no-such-flag'])).code, 2, 'an unknown flag');
  assert.equal((await run(['--stack', 'csharp'])).code, 2, 'a build with flags missing');
  assert.equal((await run(build(['--make-locks']))).code, 2, 'two modes at once');
  assert.equal((await run(['--stack', 'rust', ...build([]).slice(2)])).code, 2, 'a stack that is not csharp or node');
  assert.equal((await run(build(['--hello']))).code, 2, 'no task repository named');
  const noRow = await run(build(['--task-repo', SPIKE]));
  assert.equal(noRow.code, 3, 'a task in no row of FORMAT\'s record');
  assert.match(noRow.stderr, /in no group's row/);
  const repo = await repoWith(t, { 'a.txt': 'x\n' });
  assert.equal((await run(['--stack', 'csharp', '--kind', 'olympiad', '--task', 'hello', '--group', 'A', '--out', path.join(repo, 'ws'), '--statement', statement, '--samples', helloSamples('olympiad'), '--hello', '--task-repo', repo])).code, 3, 'a destination inside a worktree');
  const cached = path.join(home, '.cache', 'lang-v2', PINNED_TASK_SHA);
  writeTree(cached, { 'csharp/01-x/stray.txt': 'a cached tree that is not the listed one\n' });
  const mismatch = await run(build(['--hello', '--task-repo', repo]));
  assert.equal(mismatch.code, 3, 'a cached tree off the hash list is a refusal');
  assert.match(mismatch.stderr, /not on the hash list|missing/);
  assert.equal(existsSync(out), false, 'no refusal wrote anything');
  const list = await run(['--print-stack-files']);
  assert.equal(list.code, 0);
  assert.equal(list.stdout, read(path.join(TEMPLATES, 'stack-files.json')));
});

test('An_unreadable_statement_or_samples_folder_is_one_usage_line_and_not_a_stack_trace', async t => {
  const home = ownedFolder(t, 'home');
  const out = path.join(ownedFolder(t, 'out'), 'w');
  const files = writeTree(ownedFolder(t, 'inp'), { 'statement.md': 'x\n', 'samples/s01.in': 'a\n' });
  const flags = (statement, samples) => ['--stack', 'csharp', '--kind', 'olympiad', '--task', 'hello', '--group', 'A', '--out', out, '--statement', statement, '--samples', samples, '--task-repo', files, '--hello'];
  chmodSync(path.join(files, 'statement.md'), 0o000);
  chmodSync(path.join(files, 'samples'), 0o000);
  try {
    const noStatement = await command(home, flags(path.join(files, 'statement.md'), helloSamples('olympiad')));
    assert.equal(noStatement.code, 2);
    assert.match(noStatement.stderr, /--statement .* is not a readable file/);
    const noSamples = await command(home, flags(helloStatement('olympiad'), path.join(files, 'samples')));
    assert.equal(noSamples.code, 2);
    assert.match(noSamples.stderr, /--samples .* is not a readable folder/);
    for (const result of [noStatement, noSamples]) assert.doesNotMatch(result.stderr, /\n\s+at /, 'a line, not a stack trace');
  } finally {
    chmodSync(path.join(files, 'samples'), 0o755);
    chmodSync(path.join(files, 'statement.md'), 0o644);
  }
  assert.equal(existsSync(out), false);
});

test('A_git_failure_in_the_fetch_exits_1_and_a_missing_statement_or_samples_exits_2_without_a_stack_trace', async t => {
  const home = ownedFolder(t, 'home');
  const out = path.join(ownedFolder(t, 'out'), 'w');
  const plain = ownedFolder(t, 'plain');
  const build = (over = {}) => {
    const flags = { '--stack': 'csharp', '--kind': 'olympiad', '--task': 'hello', '--group': 'A', '--out': out, '--statement': helloStatement('olympiad'), '--samples': helloSamples('olympiad'), '--task-repo': plain, ...over };
    return [...Object.entries(flags).flat(), '--hello'];
  };
  const noStatement = await command(home, build({ '--statement': path.join(plain, 'none.md') }));
  assert.equal(noStatement.code, 2);
  assert.match(noStatement.stderr, /--statement .* is not a readable file/);
  const noSamples = await command(home, build({ '--samples': path.join(plain, 'none') }));
  assert.equal(noSamples.code, 2);
  assert.match(noSamples.stderr, /--samples .* is not a readable folder/);
  for (const result of [noStatement, noSamples]) assert.doesNotMatch(result.stderr, /\n\s+at /, 'a line, not a stack trace');
  const notARepo = await command(home, build());
  assert.equal(notARepo.code, 1, 'git cannot clone a folder that is not a repository: a failure, not a refusal');
  assert.equal(existsSync(out), false);
  assert.deepEqual(readdirSync(path.join(home, '.cache', 'lang-v2')).filter(n => n.startsWith('.fetch')), [], 'no scratch folder');
});
