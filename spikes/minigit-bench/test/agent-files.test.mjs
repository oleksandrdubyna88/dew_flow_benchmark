import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, renameSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { copyAgentFile, agentEntries, readAgentFile, readAgentFileUnder } from '../lib/agent-files.mjs';
import { tmpDir } from './helpers.mjs';

/** A named pipe at `file` (mkfifo(1)); asserts it was made, so a host without it fails instead of passing without the pipe. */
function makeFifo(file) {
  const r = spawnSync('mkfifo', [file], { encoding: 'utf8' });
  assert.equal(r.status, 0, `mkfifo: ${r.stderr}`);
}

/** A host file the agent must never get read for it (a planted decoy, never a real secret) and a tree with a link to it. */
function planted() {
  const root = tmpDir('af');
  const host = path.join(root, 'host-file.txt');
  writeFileSync(host, 'HOST-ONLY-CONTENT\n');
  const tree = path.join(root, 'tree');
  mkdirSync(path.join(tree, 'out'), { recursive: true });
  writeFileSync(path.join(tree, 'out', 'real.txt'), 'real\n');
  return { root, host, tree };
}

test('A_symlink_a_device_a_pipe_a_directory_and_an_oversize_file_are_unreadable_and_a_missing_file_is_null', () => {
  const { host, tree } = planted();
  symlinkSync(host, path.join(tree, 'out', 'link.txt'));
  symlinkSync('/dev/zero', path.join(tree, 'out', 'zero'));
  makeFifo(path.join(tree, 'out', 'pipe'));
  writeFileSync(path.join(tree, 'out', 'big.txt'), 'x'.repeat(100));
  assert.deepEqual(readAgentFile(path.join(tree, 'out', 'link.txt')), { unreadable: 'symlink' });
  assert.deepEqual(readAgentFile(path.join(tree, 'out', 'zero')), { unreadable: 'symlink' });
  assert.deepEqual(readAgentFile(path.join(tree, 'out')), { unreadable: 'not a regular file' });
  assert.deepEqual(readAgentFile(path.join(tree, 'out', 'pipe')), { unreadable: 'not a regular file' }, 'a pipe is refused without blocking on it');
  assert.match(readAgentFile(path.join(tree, 'out', 'big.txt'), 10).unreadable, /larger than 10 bytes/);
  assert.equal(readAgentFile(path.join(tree, 'out', 'absent.txt')), null);
  assert.equal(readAgentFile(path.join(tree, 'out', 'real.txt')).toString(), 'real\n', 'control: a regular file is read');
});

test('A_link_among_the_directories_of_a_path_makes_the_file_under_it_unreadable', () => {
  const { root, host, tree } = planted();
  const outside = path.join(root, 'outside');
  mkdirSync(outside);
  writeFileSync(path.join(outside, 'audit.ndjson'), 'HOST-ONLY-CONTENT\n');
  symlinkSync(outside, path.join(tree, 'linkdir'));
  assert.match(readAgentFileUnder(tree, 'linkdir/audit.ndjson').unreadable, /link/);
  assert.equal(readAgentFileUnder(tree, 'out/real.txt').toString(), 'real\n');
  assert.equal(readAgentFileUnder(tree, 'out/nope.txt'), null);
  assert.equal(readAgentFileUnder(tree, 'nodir/nope.txt'), null);
  assert.ok(host);
});

test('Every_entry_is_listed_and_a_link_or_a_pipe_is_a_finding_not_a_skip', () => {
  const { host, tree } = planted();
  symlinkSync(host, path.join(tree, 'out', 'link.txt'));
  symlinkSync(tree, path.join(tree, 'out', 'loop'));
  makeFifo(path.join(tree, 'out', 'pipe'));
  const rows = agentEntries(tree);
  const kinds = Object.fromEntries(rows.map(r => [r.rel, r.kind]));
  assert.equal(kinds[path.join('out', 'real.txt')], 'file');
  assert.equal(kinds[path.join('out', 'link.txt')], 'symlink');
  assert.equal(kinds[path.join('out', 'pipe')], 'other');
  assert.equal(kinds[path.join('out', 'loop')], 'symlink', 'a link to a directory is listed, and not walked into');
  assert.equal(rows.filter(r => r.rel.startsWith(`out${path.sep}loop${path.sep}`)).length, 0);
});

test('Copying_an_agent_file_copies_a_regular_file_and_refuses_a_link_with_the_reason', () => {
  const { host, root, tree } = planted();
  symlinkSync(host, path.join(tree, 'out', 'link.txt'));
  const to = path.join(root, 'copy', 'real.txt');
  assert.deepEqual(copyAgentFile(tree, path.join('out', 'real.txt'), to), { ok: true, value: path.join('out', 'real.txt') });
  assert.equal(readFileSync(to, 'utf8'), 'real\n');
  const refused = copyAgentFile(tree, path.join('out', 'link.txt'), path.join(root, 'copy', 'link.txt'));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /out.link\.txt.*symlink/);
});

test('A_parent_directory_swapped_for_a_link_between_the_check_and_the_open_is_caught_by_the_path_the_descriptor_resolves_to', () => {
  const { root, tree } = planted();
  const outside = path.join(root, 'outside');
  mkdirSync(outside);
  writeFileSync(path.join(outside, 'real.txt'), 'HOST-ONLY-CONTENT\n'); // the same file name, in a place the agent must not get read for it
  // the swap the review described: the lstat walk finds `out` a real directory; then the agent (running) replaces it with a link; the open follows the link
  const swapOut = () => { renameSync(path.join(tree, 'out'), path.join(root, 'moved-out')); symlinkSync(outside, path.join(tree, 'out')); };
  const swapped = readAgentFileUnder(tree, 'out/real.txt', undefined, { afterCheck: swapOut });
  assert.equal(typeof swapped?.unreadable, 'string', `no bytes were used: ${Buffer.isBuffer(swapped) ? swapped.toString() : JSON.stringify(swapped)}`);
  assert.match(swapped.unreadable, /resolved|swapped|elsewhere/);
  // control: with no swap the same call reads the file, through the same descriptor check
  const { tree: tree2 } = planted();
  assert.equal(readAgentFileUnder(tree2, 'out/real.txt', undefined, { afterCheck: () => {} }).toString(), 'real\n');
});

test('The_expected_real_path_is_the_roots_real_path_so_a_root_that_is_itself_reached_through_a_link_is_read_normally', () => {
  const { root, tree } = planted();
  const viaLink = path.join(root, 'tree-link');
  symlinkSync(tree, viaLink);
  assert.equal(readAgentFileUnder(viaLink, 'out/real.txt').toString(), 'real\n', 'the root is trusted and resolved once');
});
