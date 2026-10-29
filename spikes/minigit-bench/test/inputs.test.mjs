import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { PINNED_INPUTS_SHA, fetchInputs } from '../lib/fetch-inputs.mjs';
import { expectedTotal, parseHashes, readFileOrNull, verifyFiles } from '../lib/inputs.mjs';
import { inputs, spikeDir, tmpDir } from './helpers.mjs';

const hashesText = readFileSync(path.join(spikeDir, 'inputs', 'hashes.txt'), 'utf8');

test('Every_pinned_input_matches_hashes_txt', async () => {
  const { root, entries } = await inputs();
  assert.equal(entries.length, 6);
  const clean = verifyFiles(root, entries, readFileOrNull);
  assert.deepEqual(clean.filter(r => !r.ok), []);

  const copy = tmpDir('hashes');
  cpSync(root, copy, { recursive: true });
  const victim = path.join(copy, 'upstream', 'SPEC-v1.txt');
  const bytes = readFileSync(victim);
  bytes[0] ^= 1;
  writeFileSync(victim, bytes);
  const edited = verifyFiles(copy, entries, readFileOrNull);
  assert.deepEqual(edited.filter(r => !r.ok).map(r => r.path), ['upstream/SPEC-v1.txt']);
});

test('The_committed_hashes_file_parses_and_lists_the_six_files', () => {
  const parsed = parseHashes(hashesText);
  assert.equal(parsed.ok, true);
  assert.deepEqual(parsed.value.map(e => e.path).sort(), [
    'test-v1.sh', 'test-v2.sh', 'upstream/SPEC-v1.txt', 'upstream/SPEC-v2.txt', 'upstream/test-v1.sh', 'upstream/test-v2.sh']);
});

test('A_hashes_file_with_a_malformed_or_duplicate_line_is_refused_naming_it', () => {
  const good = `${'a'.repeat(64)}  one.txt`;
  const bad = parseHashes(`zz  a\n${good}\n`);
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /line 1/);
  const second = parseHashes(`${good}\nnot a hash line\n`);
  assert.match(second.reason, /line 2/);
  const dup = parseHashes(`${good}\n${'b'.repeat(64)}  one.txt\n`);
  assert.equal(dup.ok, false);
  assert.match(dup.reason, /duplicate/);
  assert.match(dup.reason, /one\.txt/);
  assert.equal(parseHashes(`${good}\n\n`).ok, true);
});

test('A_missing_file_is_actual_null_not_a_mismatch_with_the_empty_string', () => {
  const [row] = verifyFiles('/nowhere', [{ sha256: 'a'.repeat(64), path: 'gone.txt' }], () => null);
  assert.deepEqual(row, { path: 'gone.txt', expected: 'a'.repeat(64), actual: null, ok: false });
});

test('Expected_totals_are_derived_from_the_scripts_and_match_upstream_results', async () => {
  const { root } = await inputs();
  const read = p => readFileSync(path.join(root, p), 'utf8');
  assert.equal(expectedTotal(read('upstream/test-v1.sh')), 11);
  assert.equal(expectedTotal(read('upstream/test-v2.sh')), 30);
  // the harness copies keep every header and carry the same number as a hand-written literal
  assert.equal(expectedTotal(read('test-v1.sh')), 11);
  assert.equal(expectedTotal(read('test-v2.sh')), 30);
  assert.equal(Number(/^EXPECTED_TOTAL=(\d+)$/m.exec(read('test-v1.sh'))[1]), expectedTotal(read('upstream/test-v1.sh')));
  assert.equal(Number(/^EXPECTED_TOTAL=(\d+)$/m.exec(read('test-v2.sh'))[1]), expectedTotal(read('upstream/test-v2.sh')));
  // the second source: the upstream results recorded in the fork's own history (read, never copied here)
  const rows = JSON.parse(execFileSync('git', ['-C', root, 'show', '7f6ffdc:results/results.json'], { encoding: 'utf8', maxBuffer: 64 << 20 }));
  const seen = key => [...new Set(rows.map(r => r[key]).filter(n => n > 0))];
  assert.deepEqual(seen('v1_total_count'), [11]);
  assert.deepEqual(seen('v2_total_count'), [30]);
});

const walk = dir => readdirSync(dir).flatMap(name => {
  const p = path.join(dir, name);
  return statSync(p).isDirectory() ? walk(p) : [p];
});

test('No_upstream_line_is_anywhere_in_the_spike', async () => {
  const { root } = await inputs();
  const lines = new Set();
  for (const f of ['upstream/SPEC-v1.txt', 'upstream/SPEC-v2.txt']) {
    readFileSync(path.join(root, f), 'utf8').split('\n').map(l => l.trim()).filter(l => l.length >= 20).slice(0, 5).forEach(l => lines.add(l));
  }
  for (const f of ['upstream/test-v1.sh', 'upstream/test-v2.sh']) {
    readFileSync(path.join(root, f), 'utf8').split('\n').filter(l => /^# Test \d+:/.test(l)).forEach(l => lines.add(l));
  }
  assert.ok(lines.size >= 40, `the needle list is alive (${lines.size})`);
  const hits = walk(spikeDir).filter(file => {
    const text = readFileSync(file, 'utf8');
    return [...lines].some(l => text.includes(l));
  });
  assert.deepEqual(hits, []);
});

test('A_cached_tree_that_no_longer_matches_is_a_failure_naming_the_file_never_a_refetch', async () => {
  const { root, sha } = await inputs();
  const cacheDir = tmpDir('cache');
  cpSync(root, path.join(cacheDir, sha), { recursive: true });
  writeFileSync(path.join(cacheDir, sha, 'test-v1.sh'), 'tampered\n');
  // the repo url is unreachable on purpose: a re-fetch would fail with a different message
  const r = await fetchInputs({ sha, cacheDir, repoUrl: path.join(cacheDir, 'no-such-repo') });
  assert.equal(r.ok, false);
  assert.match(r.reason, /do not match hashes\.txt/);
  assert.deepEqual(r.mismatches, ['test-v1.sh']);
});

test('A_sha_that_is_not_on_the_inputs_branch_is_refused_and_leaves_no_cache_entry', async () => {
  const cacheDir = tmpDir('cache');
  const sha = 'f'.repeat(40);
  const repo = tmpDir('tiny-fork');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@example.invalid', ...args], { stdio: 'pipe' });
  git('init', '--quiet', '--initial-branch=inputs');
  git('commit', '--quiet', '--allow-empty', '-m', 'empty');
  const r = await fetchInputs({ sha, cacheDir, repoUrl: repo });
  assert.equal(r.ok, false);
  assert.match(r.reason, /not on the inputs branch/);
  assert.equal(existsSync(path.join(cacheDir, sha)), false);
  assert.deepEqual(readdirSync(cacheDir), []);
  assert.equal((await fetchInputs({ sha: 'nope' })).ok, false);
});

test('UPSTREAM_md_names_the_pinned_sha_the_fork_and_the_upstream_commits', () => {
  const note = readFileSync(path.join(spikeDir, 'inputs', 'UPSTREAM.md'), 'utf8');
  assert.ok(note.includes(PINNED_INPUTS_SHA), 'the pinned sha is written in UPSTREAM.md');
  for (const needle of ['oleksandrdubyna88/minigit-bench', 'mame/ai-coding-lang-bench', '7f6ffdc', '83644e0', 'D18', 'GitHub']) assert.ok(note.includes(needle), needle);
});
