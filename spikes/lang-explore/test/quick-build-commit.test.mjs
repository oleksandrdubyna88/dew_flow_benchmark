// The atomic build commit point (section 2.7): the build happens once per session in a temporary folder, is validated, renamed into place as `tree/`, and only then recorded in `build.json` (written once, `wx`) with the hash of
// the tree it names. Each of these fails on a build done in place, or on a marker that is not checked against the tree. No box: `prepare` and `build` are fakes.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { commitBuild, committedTree, discardUncommitted, treeHash } from '../lib/build-commit.mjs';

const workOf = () => tmpDir('quick-build-commit');
const names = work => readdirSync(work).sort();
const fakePrepare = (extra = {}) => async dest => { mkdirSync(path.join(dest, 'sub'), { recursive: true }); writeFileSync(path.join(dest, 'sub', 'a.txt'), 'invented source'); return ok({ frozen: [{ path: 'build.sh', state: 'changed' }], drift: [], left: ['d.sock'], ...extra }); };
const fakeBuild = (seen, result = { ok: true, exit: 0, timedOut: false, wallMs: 77, tail: 'tail' }) => async ({ tree }) => { seen.push(tree); writeFileSync(path.join(tree, 'built.bin'), 'out'); return ok(result); };

test('A_build_happens_in_a_temporary_owned_folder_and_is_renamed_into_place_only_after_it_was_validated_and_its_completion_record_names_the_trees_hash', async () => {
  const work = workOf();
  const seen = [];
  const order = [];
  const io = { rename: (a, b) => { order.push('rename'); renameSync(a, b); }, writeNew: (file, text) => { order.push('record'); writeFileSync(file, text, { flag: 'wx' }); } };
  const r = await commitBuild({ work, prepare: fakePrepare(), build: fakeBuild(seen), io, now: () => new Date('2026-11-07T10:00:00Z') });
  assert.equal(r.ok, true, r.reason);
  assert.match(path.basename(seen[0]), /^build-[0-9a-f]+$/, 'built in score/build-<token>');
  assert.notEqual(seen[0], path.join(work, 'tree'));
  assert.deepEqual(names(work), ['build.json', 'tree']);
  assert.deepEqual(order, ['rename', 'record'], 'the record is written after the rename');
  const record = JSON.parse(readFileSync(path.join(work, 'build.json'), 'utf8'));
  assert.equal(record.treeSha256, treeHash(path.join(work, 'tree')));
  assert.deepEqual([record.ok, record.exit, record.timedOut, record.wallMs, record.tail], [true, 0, false, 77, 'tail']);
  assert.deepEqual(record.frozenChanges, [{ path: 'build.sh', state: 'changed' }]);
  assert.deepEqual(record.nonRegularLeftOut, ['d.sock']);
  assert.equal(record.committedAt, '2026-11-07T10:00:00.000Z');
});

test('A_resume_that_finds_a_built_tree_without_its_completion_record_discards_it_and_builds_again', async () => {
  const work = workOf();
  mkdirSync(path.join(work, 'tree'));
  writeFileSync(path.join(work, 'tree', 'stray.txt'), 'x');
  mkdirSync(path.join(work, 'build-abc'));
  assert.deepEqual(discardUncommitted(work), ['build-abc', 'tree']);
  assert.deepEqual(names(work), []);
  mkdirSync(path.join(work, 'tree'));
  writeFileSync(path.join(work, 'tree', 'stray.txt'), 'x');
  const calls = [];
  discardUncommitted(work);
  const r = await commitBuild({ work, prepare: fakePrepare(), build: fakeBuild(calls) });
  assert.equal(r.ok, true, r.reason);
  assert.equal(calls.length, 1);
  assert.equal(existsSync(path.join(work, 'tree', 'stray.txt')), false);
  assert.equal(existsSync(path.join(work, 'build.json')), true);
});

test('A_completion_record_whose_hash_does_not_match_the_tree_it_names_is_a_halt_and_no_pass_runs', async () => {
  const work = workOf();
  await commitBuild({ work, prepare: fakePrepare(), build: fakeBuild([]) });
  assert.equal(committedTree(work).ok, true);
  assert.deepEqual(discardUncommitted(work), [], 'a tree with its record is committed state and is kept');
  writeFileSync(path.join(work, 'tree', 'built.bin'), 'oUt');
  const r = committedTree(work);
  assert.equal(r.ok, false);
  assert.match(r.reason, /^build-record-mismatch: /);
  const recorded = JSON.parse(readFileSync(path.join(work, 'build.json'), 'utf8')).treeSha256;
  assert.ok(r.reason.includes(recorded) && r.reason.includes(treeHash(path.join(work, 'tree'))), 'both hashes are named');
});

test('A_build_that_ran_and_failed_is_committed_with_ok_false_and_a_build_that_never_began_commits_nothing', async () => {
  const failed = workOf();
  const r = await commitBuild({ work: failed, prepare: fakePrepare(), build: fakeBuild([], { ok: false, exit: 1, timedOut: false, wallMs: 5, tail: 'error CS1002' }) });
  assert.equal(r.ok, true, r.reason);
  assert.equal(JSON.parse(readFileSync(path.join(failed, 'build.json'), 'utf8')).ok, false);
  assert.match(JSON.parse(readFileSync(path.join(failed, 'build.json'), 'utf8')).tail, /CS1002/);
  const never = workOf();
  const n = await commitBuild({ work: never, prepare: fakePrepare(), build: async () => fail('the build did not begin (exit 1)', { kind: 'environment' }) });
  assert.equal(n.ok, false);
  assert.equal(n.kind, 'environment');
  assert.deepEqual(names(never), [], 'no build.json, no tree and no temporary folder is left');
  assert.equal(committedTree(never).value, null);
});

test('The_tree_hash_covers_names_and_bytes_in_name_order_and_a_renamed_file_changes_it', () => {
  const a = workOf();
  const b = workOf();
  for (const [dir, file] of [[a, 'one.txt'], [b, 'two.txt']]) writeFileSync(path.join(dir, file), 'same bytes');
  assert.notEqual(treeHash(a), treeHash(b));
  writeFileSync(path.join(a, 'zz.txt'), 'z');
  writeFileSync(path.join(a, 'aa.txt'), 'a');
  const first = treeHash(a);
  assert.equal(treeHash(a), first, 'stable');
});
