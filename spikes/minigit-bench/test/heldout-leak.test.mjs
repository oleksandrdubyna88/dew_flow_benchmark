import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { leakcheck, parseTokens, scanFiles } from '../lib/heldout-leak.mjs';
import { runBounded } from '../lib/run.mjs';
import { tmpDir } from './helpers.mjs';

const TOKENS = 'toy-token-quokka-7421\nanother-toy-token-zebra\n';

async function toyRepo() {
  const root = tmpDir('leak-repo');
  const git = async (...args) => {
    const r = await runBounded('git', args, { cwd: root, timeoutMs: 20_000 });
    assert.equal(r.ok && r.value.code === 0, true, `git ${args.join(' ')}: ${JSON.stringify(r)}`);
  };
  await git('init', '-q');
  mkdirSync(path.join(root, 'docs'));
  writeFileSync(path.join(root, 'docs', 'clean.txt'), 'nothing to see\n');
  writeFileSync(path.join(root, '.gitignore'), 'ignored.txt\n');
  await git('add', '.');
  return { root, git };
}

test('Leakcheck_finds_a_planted_token_and_passes_a_clean_tree', async () => {
  const { root } = await toyRepo();
  const clean = await leakcheck({ repoRoot: root, tokensText: TOKENS });
  assert.equal(clean.ok, true, clean.reason);
  assert.ok(clean.scanned >= 2);

  const planted = path.join(root, 'docs', 'planted.txt');
  writeFileSync(planted, 'before another-toy-token-zebra after\n'); // untracked but not ignored: a commit-to-be
  const dirty = await leakcheck({ repoRoot: root, tokensText: TOKENS });
  assert.equal(dirty.ok, false);
  assert.deepEqual(dirty.hits, [{ file: 'docs/planted.txt', tokenNumber: 2 }]);
  assert.match(dirty.reason, /docs\/planted\.txt/);
  assert.equal(dirty.reason.includes('zebra'), false, 'the token itself is never printed');
  rmSync(planted);
  assert.equal((await leakcheck({ repoRoot: root, tokensText: TOKENS })).ok, true);
});

test('Leakcheck_scans_cached_and_untracked_files_but_not_ignored_ones', async () => {
  const { root, git } = await toyRepo();
  writeFileSync(path.join(root, 'ignored.txt'), 'toy-token-quokka-7421\n');
  assert.equal((await leakcheck({ repoRoot: root, tokensText: TOKENS })).ok, true, 'git ignores it, so no commit can carry it');
  writeFileSync(path.join(root, 'docs', 'staged.bin'), Buffer.concat([Buffer.from([0, 1, 2]), Buffer.from('toy-token-quokka-7421')]));
  await git('add', 'docs/staged.bin');
  const r = await leakcheck({ repoRoot: root, tokensText: TOKENS });
  assert.equal(r.ok, false);
  assert.deepEqual(r.hits.map(h => h.file), ['docs/staged.bin']);
});

test('A_token_shorter_than_eight_characters_or_an_empty_list_is_refused', () => {
  assert.equal(parseTokens('abcdefg\n').ok, false);
  assert.match(parseTokens('abcdefgh\nshort\n').reason, /line 2/);
  assert.equal(parseTokens('\n\n').ok, false);
  assert.deepEqual(parseTokens('abcdefgh\n\nijklmnop\n'), { ok: true, value: ['abcdefgh', 'ijklmnop'] });
});

test('A_not_a_git_repository_is_a_failure_not_a_clean_pass', async () => {
  const r = await leakcheck({ repoRoot: tmpDir('not-git'), tokensText: TOKENS });
  assert.equal(r.ok, false);
  assert.match(r.reason, /git ls-files/);
});

test('scanFiles_reports_a_vanished_file_instead_of_skipping_it_silently', () => {
  const root = tmpDir('scan');
  writeFileSync(path.join(root, 'here.txt'), 'x');
  const r = scanFiles(root, ['here.txt', 'gone.txt'], ['abcdefgh']);
  assert.deepEqual(r.hits, []);
  assert.deepEqual(r.unreadable, ['gone.txt']);
});

test('A_listed_directory_such_as_a_submodule_root_is_reported_as_skipped_not_a_crash', () => {
  const root = tmpDir('scan-dir');
  mkdirSync(path.join(root, 'submodule'));
  writeFileSync(path.join(root, 'submodule', 'inner.txt'), 'abcdefgh');
  writeFileSync(path.join(root, 'here.txt'), 'x');
  const r = scanFiles(root, ['here.txt', 'submodule'], ['abcdefgh']);
  assert.deepEqual(r.hits, [], 'a gitlink is another repository: its files are not this repository\'s');
  assert.deepEqual(r.skippedDirs, ['submodule']);
});
