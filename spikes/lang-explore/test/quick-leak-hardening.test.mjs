// Step 2b, code round (2026-11-08): the ten accepted findings. Canonical repository paths; reads bounded by count and bytes (a fake git that answers in several batches); one refusal shape; option-like paths and `--`; every
// object accounted for; a checked base sha; a clean git environment; completeness beyond the walk; progress per phase. INVENTED needle, temporary repositories.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { leakcheckRepos } from '../lib/leak-repos.mjs';
import { gitEnv, walkOf } from '../lib/leak-git.mjs';
import { parseBatch } from '../lib/leak-git-read.mjs';
import { NEEDLE, TOKENS, commit, git, holdsNeedle, newRepo, privateSide, runCheck, withGitLog } from './quick-leak-repos-fixture.mjs';

const clean = () => newRepo({ 'README.md': 'a task repository readme\n', 'tasks/04/notes.md': 'a public note\n' });
const run = (benchRepo, values, env, io) => runCheck({ ...privateSide(), benchRepo, values, env, io });
const TASK_CLEAN = /\[task repository [^\]]*\] clean/;

// ---- 1. canonical paths ----------------------------------------------------------------------------------------------------------------------------------

test('A_raw_variable_that_is_not_a_top_folder_never_names_the_task_repository_and_a_bench_path_that_is_not_one_is_refused', async () => {
  const repo = newRepo();
  const sha = commit(repo, { 'tasks/04/spec.md': NEEDLE }, 'publish');
  const sub = path.join(repo, 'tasks');
  const viaSub = await run(newRepo(), { 'candidate-repo': repo, candidate: sha }, { LANG_V2_TASK_REPO: sub });
  assert.match(viaSub.said, /of repository /, 'a sub folder is no canonical path: it names nothing');
  const link = path.join(tmpDir('r-link'), 'task');
  symlinkSync(repo, link);
  const viaLink = await run(newRepo(), { 'candidate-repo': repo, candidate: sha }, { LANG_V2_TASK_REPO: link });
  assert.match(viaLink.said, /of task repository\]/, 'a link to the top folder is canonicalised first');
  const bench = newRepo();
  const asSub = await run(path.join(bench, 'sub'), { 'task-repo': clean() }, {});
  assert.equal(asSub.exit, 4, asSub.said);
  const benchSub = path.join(bench, 'docs');
  commit(bench, { 'docs/x.md': 'x\n' }, 'docs');
  const inside = await run(benchSub, { 'task-repo': clean() }, {});
  assert.equal(inside.exit, 4, 'a bench path that is not its top folder is refused, never compared raw');
  assert.match(inside.said, /top folder/);
});

// ---- 2. bounded reads, a fake git that answers in several batches -----------------------------------------------------------------------------------------

test('A_repository_larger_than_one_batch_is_read_in_several_batches_and_a_needle_in_a_late_batch_is_found', async () => {
  const files = Object.fromEntries(Array.from({ length: 9 }, (_, i) => [`tasks/04/f${i}.md`, `public text number ${i}\n`]));
  files['tasks/04/f8.md'] = `late file\n${NEEDLE}`;
  const repo = newRepo({ 'README.md': 'a readme\n', ...files });
  for (let i = 0; i < 4; i += 1) commit(repo, { [`later/${i}.md`]: `a later commit ${i}\n` }, `later ${i}`);
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD~4');
  const { value: r, calls } = await withGitLog(() => run(newRepo(), { 'task-repo': repo }, {}, { gitLimits: { blobs: 2, blobBytes: 1_000_000, commits: 2, readBytes: 100_000_000 } }));
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /f8\.md/);
  assert.ok(calls.filter(c => c.includes('cat-file --batch')).length >= 5, 'ten files, two to a batch');
  assert.ok(calls.filter(c => c.includes(' log ') && c.includes('-p')).length >= 2, 'four commits, two to a batch');
  assert.equal(holdsNeedle(r.said + r.progress), false);
  const ctl = newRepo({ 'README.md': 'a readme\n', ...Object.fromEntries(Array.from({ length: 9 }, (_, i) => [`tasks/04/f${i}.md`, `public text number ${i}\n`])) });
  const control = await run(newRepo(), { 'task-repo': ctl }, {}, { gitLimits: { blobs: 2, blobBytes: 1_000_000, commits: 2, readBytes: 100_000_000 } });
  assert.equal(control.exit, 0, 'a repository larger than one batch is not a refusal for its size alone');
});

test('A_batch_whose_output_is_cut_by_the_read_limit_is_split_and_read_again_never_refused', async () => {
  const repo = newRepo({ 'README.md': 'a readme\n' });
  for (let i = 0; i < 6; i += 1) commit(repo, { [`notes/${i}.md`]: `${'padding of the notes '.repeat(20)}${i}\n${i === 5 ? NEEDLE : ''}` }, `commit ${i}`);
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD~6');
  const r = await run(newRepo(), { 'task-repo': repo }, {}, { gitLimits: { blobs: 100, blobBytes: 1_000_000, commits: 100, readBytes: 2_500 } });
  assert.equal(r.exit, 5, r.said);
  assert.doesNotMatch(r.said, /REFUSED/);
  assert.match(r.said, /patches of the local branches/);
});

test('A_single_patch_larger_than_the_read_limit_is_a_refusal_naming_the_commit_not_the_repository_size', async () => {
  const repo = newRepo({ 'README.md': 'a readme\n' });
  commit(repo, { 'big.md': 'x\n'.repeat(5_000) }, 'a big commit');
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD~1');
  const r = await run(newRepo(), { 'task-repo': repo }, {}, { gitLimits: { blobs: 100, blobBytes: 1_000_000, commits: 100, readBytes: 3_000 } });
  assert.equal(r.exit, 4, r.said);
  assert.match(r.said, /REFUSED: .*larger than the read limit/);
});

// ---- 3. one refusal shape ---------------------------------------------------------------------------------------------------------------------------------

test('Every_refusal_of_leakcheckRepos_is_an_object_with_an_exit_and_a_filled_message', async () => {
  const side = privateSide();
  const asked = (values, env = {}) => leakcheckRepos({ values, io: { home: side.home, repoRoot: newRepo(), progress: () => {}, env }, explore: side.explore, say: () => {} });
  const full = newRepo({ 'README.md': 'a readme\n' });
  const repoSha = git(full, 'rev-parse', 'HEAD').trim();
  const cases = [['no task repository', await asked({})], ['a shallow task repository', await asked({ 'task-repo': await shallowClone(clean()) })],
    ['an unknown commit', await asked({ 'candidate-repo': full, candidate: 'nope' })], ['a candidate whose object is missing', await asked({ 'candidate-repo': await missingBlobRepo(), candidate: 'HEAD' })]];
  for (const [name, r] of cases) {
    assert.equal(r.exit, 4, name);
    assert.equal(typeof r.message, 'string', name);
    assert.ok(r.message.length > 0, `${name}: the message is filled`);
  }
  const clear = await asked({ 'task-repo': clean() });
  assert.deepEqual(Object.keys(clear).sort(), ['exit', 'message'], 'the same shape when nothing is refused');
  assert.ok(repoSha);
});

async function shallowClone(source) {
  commit(source, { 'second.md': 'a second commit\n' }, 'second');
  const dest = path.join(tmpDir('r-clone'), 'clone');
  const { execFileSync } = await import('node:child_process');
  execFileSync('git', ['clone', '-q', '--depth', '1', `file://${source}`, dest]);
  return dest;
}

async function missingBlobRepo() {
  const repo = newRepo({ 'README.md': 'a readme\n', 'tasks/04/notes.md': 'notes with their own text\n' });
  const oid = git(repo, 'rev-parse', 'HEAD:tasks/04/notes.md').trim();
  rmSync(path.join(repo, '.git', 'objects', oid.slice(0, 2), oid.slice(2)));
  return repo;
}

// ---- 4. option-like paths and `--` ---------------------------------------------------------------------------------------------------------------------------

test('A_task_repo_or_candidate_repo_that_looks_like_an_option_is_a_refusal_and_so_is_such_a_variable', async () => {
  for (const [name, values, env] of [['--task-repo', { 'task-repo': '--upload-pack=x' }, {}], ['--candidate-repo', { 'candidate-repo': '-x', candidate: 'HEAD' }, {}], ['the variable', {}, { LANG_V2_TASK_REPO: '-x' }]]) {
    const r = await run(newRepo(), values, env);
    assert.equal(r.exit, 4, `${name}: ${r.said}`);
    assert.match(r.said, /looks like an option/, name);
  }
});

test('Every_git_call_that_takes_a_revision_or_a_path_puts_dash_dash_before_the_positional_arguments', async () => {
  const repo = clean();
  commit(repo, { 'later.md': 'later\n' }, 'later');
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD~1');
  const sha = git(repo, 'rev-parse', 'HEAD').trim();
  const { calls } = await withGitLog(() => run(newRepo(), { 'task-repo': repo, 'candidate-repo': repo, candidate: sha }, {}));
  const mine = calls.filter(c => c.startsWith('--no-pager'));
  assert.ok(mine.length > 10, 'the check\'s own calls all carry --no-pager and quote-path off');
  const positional = mine.filter(c => /\b(ls-tree|log|diff|rev-list)\b/.test(c));
  assert.ok(positional.length >= 6, positional.join('\n'));
  for (const c of positional) assert.ok(/ -- ?$/.test(c) || / -- /.test(c) || c.includes('--stdin'), `a call without --: ${c}`);
  assert.ok(mine.filter(c => c.includes('rev-parse') && c.includes('--verify')).every(c => c.includes('--end-of-options')), 'rev-parse --verify names its revision after --end-of-options');
});

// ---- 5. every object accounted for ---------------------------------------------------------------------------------------------------------------------------

const A = 'a'.repeat(40);
const B = 'b'.repeat(40);
const entry = (oid, body) => Buffer.concat([Buffer.from(`${oid} blob ${body.length}\n`), Buffer.from(body), Buffer.from('\n')]);

test('parseBatch_accepts_every_object_exactly_once_and_tells_apart_a_missing_object_a_truncation_and_an_early_end', () => {
  const good = parseBatch(Buffer.concat([entry(A, 'one'), entry(B, 'two')]), [A, B]);
  assert.equal(good.ok, true);
  assert.equal(good.value.get(B).toString(), 'two');
  const missing = parseBatch(Buffer.concat([entry(A, 'one'), Buffer.from(`${B} missing\n`)]), [A, B]);
  assert.deepEqual([missing.ok, missing.kind], [false, 'missing']);
  const cut = parseBatch(Buffer.concat([entry(A, 'one'), Buffer.from(`${B} blob 99\nshort`)]), [A, B]);
  assert.deepEqual([cut.ok, cut.kind], [false, 'truncated']);
  const early = parseBatch(entry(A, 'one'), [A, B]);
  assert.deepEqual([early.ok, early.kind], [false, 'ended-early']);
  const wrong = parseBatch(Buffer.concat([entry(B, 'two'), entry(A, 'one')]), [A, B]);
  assert.deepEqual([wrong.ok, wrong.kind], [false, 'mismatch']);
  const extra = parseBatch(Buffer.concat([entry(A, 'one'), entry(B, 'two'), entry(A, 'again')]), [A, B]);
  assert.deepEqual([extra.ok, extra.kind], [false, 'extra']);
  assert.equal(new Set([missing.reason, cut.reason, early.reason, wrong.reason, extra.reason]).size, 5, 'each has its own reason');
});

// ---- 6. the base sha ---------------------------------------------------------------------------------------------------------------------------------------

test('A_base_that_is_not_a_full_hex_object_id_never_reaches_rev_list', async () => {
  const repo = clean();
  for (const bad of ['--all', 'origin/main', 'abc', `${'a'.repeat(39)}g`, `${'a'.repeat(65)}`]) assert.equal((await walkOf(repo, bad)).ok, false, bad);
  const good = await walkOf(repo, git(repo, 'rev-parse', 'HEAD').trim());
  assert.equal(good.ok, true);
  assert.equal((await walkOf(repo, null)).ok, true, 'no base is a legal walk');
});

// ---- 7. a clean git environment ------------------------------------------------------------------------------------------------------------------------------

test('Git_runs_with_an_allowlisted_environment_and_an_injected_git_config_has_no_effect', async () => {
  const keep = { ...process.env };
  const dirtyRepo = newRepo({ 'README.md': 'a readme\n', 'notes/x.md': NEEDLE });
  const bench = newRepo();
  const side = privateSide();
  Object.assign(process.env, { GIT_CONFIG_COUNT: '1', GIT_CONFIG_KEY_0: 'core.bare', GIT_CONFIG_VALUE_0: 'true', GIT_CONFIG_PARAMETERS: "'core.bare=true'", GIT_DIR: '/nonexistent', GIT_WORK_TREE: '/nonexistent', GIT_INDEX_FILE: '/nonexistent', GIT_OBJECT_DIRECTORY: '/nonexistent', GIT_ALTERNATE_OBJECT_DIRECTORIES: '/nonexistent', GIT_COMMON_DIR: '/nonexistent', SOME_OTHER: 'x', LC_ALL: 'C', TZ: 'UTC' });
  try {
    const env = gitEnv();
    for (const name of Object.keys(env)) assert.ok(['PATH', 'HOME', 'LANG', 'LC_ALL', 'TZ', 'GIT_CONFIG_NOSYSTEM', 'GIT_CONFIG_GLOBAL', 'GIT_NO_REPLACE_OBJECTS', 'GIT_NO_LAZY_FETCH', 'GIT_TERMINAL_PROMPT', 'GIT_PAGER', 'GIT_OPTIONAL_LOCKS'].includes(name), `${name} passed through`);
    assert.equal(env.GIT_CONFIG_NOSYSTEM, '1');
    assert.equal(env.GIT_CONFIG_GLOBAL, '/dev/null');
    const dirty = await runCheck({ ...side, benchRepo: bench, values: { 'task-repo': dirtyRepo } });
    assert.equal(dirty.exit, 5, `an injected GIT_CONFIG_* or GIT_DIR changes nothing: ${dirty.said}`);
  } finally {
    for (const name of Object.keys(process.env)) if (!(name in keep)) delete process.env[name];
    Object.assign(process.env, keep);
  }
});

test('A_file_name_with_a_non_ascii_character_is_named_as_it_is_with_quotepath_off', async () => {
  const repo = newRepo({ 'README.md': 'a readme\n', 'notes/café.md': NEEDLE });
  const r = await run(newRepo(), { 'task-repo': repo }, {});
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /notes\/café\.md/);
});

// ---- 8. completeness beyond the walk -------------------------------------------------------------------------------------------------------------------------

test('A_missing_object_in_the_tree_at_HEAD_is_exit_4_even_when_the_base_excludes_the_whole_history', async () => {
  const repo = await missingBlobRepo();
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD');
  const r = await run(newRepo(), { 'task-repo': repo }, {});
  assert.equal(r.exit, 4, r.said);
  assert.match(r.said, /missing/);
  assert.doesNotMatch(r.said, TASK_CLEAN);
  const control = newRepo({ 'README.md': 'a readme\n', 'tasks/04/notes.md': 'notes with their own text\n' });
  assert.equal((await run(newRepo(), { 'task-repo': control }, {})).exit, 0, 'the same repository with every object is scanned');
});

test('A_missing_object_in_a_candidate_tree_is_exit_4_whatever_its_parents_hold', async () => {
  const repo = await missingBlobRepo();
  const r = await run(newRepo(), { 'candidate-repo': repo, candidate: 'HEAD' }, {});
  assert.equal(r.exit, 4, r.said);
  assert.match(r.said, /missing/);
  const sub = newRepo({ 'README.md': 'a readme\n', 'tasks/04/deep/notes.md': 'deep notes with their own text\n' });
  const tree = git(sub, 'rev-parse', 'HEAD:tasks/04/deep').trim();
  rmSync(path.join(sub, '.git', 'objects', tree.slice(0, 2), tree.slice(2)));
  const t = await run(newRepo(), { 'candidate-repo': sub, candidate: 'HEAD' }, {});
  assert.equal(t.exit, 4, 'a missing tree object is a refusal too');
});

// ---- 9. the bench-only entry point -----------------------------------------------------------------------------------------------------------------------------

test('The_bench_half_is_no_export_of_the_library_and_the_only_command_is_leakcheckRepos', async () => {
  const leak = await import('../lib/leak.mjs');
  assert.equal('leakcheckCommand' in leak, false);
  assert.equal('scanBenchHalf' in leak, false, 'the test seam holds it, the library does not export it');
  const seam = await import('./bench-half-seam.mjs');
  assert.equal(typeof seam.scanBenchHalf, 'function');
  assert.equal(typeof (await import('../lib/leak-repos.mjs')).leakcheckRepos, 'function');
});

// ---- 10. progress ------------------------------------------------------------------------------------------------------------------------------------------------

test('Progress_is_one_line_at_the_start_of_each_phase_and_per_batch_counts_only', async () => {
  const repo = newRepo({ 'README.md': 'a readme\n', 'a.md': 'a\n', 'b.md': 'b\n', 'c.md': 'c\n' });
  commit(repo, { 'd.md': 'd\n' }, 'd');
  commit(repo, { 'e.md': 'e\n' }, 'e');
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD~2');
  const sha = git(repo, 'rev-parse', 'HEAD').trim();
  const r = await run(newRepo(), { 'task-repo': repo, 'candidate-repo': repo, candidate: sha }, {}, { gitLimits: { blobs: 2, blobBytes: 1_000_000, commits: 1, readBytes: 100_000_000 } });
  assert.equal(r.exit, 0, r.said);
  const lines = r.progress.split('\n');
  const has = re => lines.some(l => re.test(l));
  assert.ok(has(/^bench repository: /), 'the bench half');
  assert.ok(has(/^task repository: reading the tree at HEAD \(\d+ files in \d+ batch/), 'the task tree');
  assert.ok(has(/^task repository: reading the history \(\d+ commits in \d+ batch/), 'the task history');
  assert.ok(has(/^task repository: reading the working tree/), 'the working tree');
  assert.ok(has(/^candidate [0-9a-f]{12}: /), 'the candidate');
  assert.ok(lines.filter(l => /^task repository: tree batch \d+ of \d+/.test(l)).length >= 3, 'one line per tree batch');
  assert.ok(lines.filter(l => /^task repository: history batch \d+ of \d+/.test(l)).length >= 2, 'one line per history batch');
  assert.equal(holdsNeedle(r.progress), false);
  assert.ok(TOKENS.length > 0);
});
