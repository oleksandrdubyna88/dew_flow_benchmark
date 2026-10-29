import assert from 'node:assert/strict';
import test from 'node:test';
import { findOutsideAccesses } from '../lib/leak.mjs';

const ctx = { treeRoot: '/srv/me/box/work', home: '/home/u', flavour: 'posix' };
const call = (tool, input, agent = 'main') => ({ agent, tool, input });
const outside = (calls, extra = {}) => findOutsideAccesses(calls, { ...ctx, ...extra });
const resolved = calls => outside(calls).map(h => h.resolved);

test('POSIX_paths_outside_the_tree_are_found', () => {
  const hits = outside([
    call('Bash', { command: 'cat /home/u/.config/x' }),
    call('Bash', { command: 'ls ~/' }),
    call('Bash', { command: 'cd ../../other' }),
    call('Read', { file_path: '/srv/partner/box/work/a.cs' }),
  ]);
  assert.deepEqual(hits.map(h => h.resolved), ['/home/u/.config/x', '/home/u', '/srv/me/other', '/srv/partner/box/work/a.cs']);
  assert.deepEqual(hits.map(h => h.tool), ['Bash', 'Bash', 'Bash', 'Read']);
  assert.equal(hits[0].agent, 'main');
});

test('Allowed_prefixes_are_passed_by_the_caller_and_never_a_default', () => {
  const calls = [call('Bash', { command: 'node /usr/bin/x < /dev/null' }), call('Bash', { command: 'ls /usr' })];
  assert.equal(outside(calls).length, 3, 'with no allow list every reach is reported');
  assert.deepEqual(outside(calls, { allow: ['/usr/', '/dev/null'] }), []);
  assert.equal(outside([call('Bash', { command: 'cat /usr2/x /usrx' })], { allow: ['/usr/'] }).length, 2, 'a prefix ends at a path boundary');
});

test('The_home_forms_and_the_tree_itself', () => {
  assert.deepEqual(resolved([call('Bash', { command: 'cat $HOME/.claude/x; ls ${HOME}/y' })]), ['/home/u/.claude/x', '/home/u/y']);
  assert.deepEqual(outside([call('Bash', { command: 'cat /srv/me/box/work/src/a.cs && cd src && ls ./b' })]), []);
  assert.deepEqual(outside([call('Edit', { file_path: 'src/a.cs' }), call('Glob', { pattern: '*.cs', path: 'src' })]), []);
  assert.equal(outside([call('Glob', { pattern: '*.cs', path: '/srv/other' })]).length, 1);
});

test('Urls_regexes_and_word_internal_slashes_are_not_paths', () => {
  const quiet = ['curl https://example.com/a/b', "sed 's/a/b/' f", 'git fetch origin run/x1/main', 'echo a/b/c', "tr '/' '-' < f"];
  for (const command of quiet) assert.deepEqual(outside([call('Bash', { command })], { allow: [] }).map(h => h.path), [], command);
});

test('The_windows_flavour_is_unchanged_and_the_default', () => {
  const win = { treeRoot: 'D:/rsd/_q1/c1/tree', home: 'C:\\Users\\me' };
  const bash = command => [call('Bash', { command })];
  assert.equal(findOutsideAccesses(bash('cat /c/Users/me/.claude/x'), win).length, 1);
  assert.equal(findOutsideAccesses(bash('cat /c/Users/me/.claude/x'), { ...win, flavour: 'win32' }).length, 1);
  assert.equal(findOutsideAccesses(bash('ls D:\\rsd\\_q1\\c1\\tree\\src'), win).length, 0);
});
