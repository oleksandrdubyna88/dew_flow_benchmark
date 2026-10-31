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

test('Quoted_absolute_paths_are_found_like_unquoted_ones', () => {
  const hits = outside([
    call('Bash', { command: 'cat "/home/u/.ssh/id_rsa"' }),
    call('Bash', { command: "cat '/srv/partner/box/work/a.cs'" }),
    call('Bash', { command: 'cp "/home/u/my docs/a b.txt" .' }),
  ]);
  assert.deepEqual(hits.map(h => h.resolved), ['/home/u/.ssh/id_rsa', '/srv/partner/box/work/a.cs', '/home/u/my docs/a b.txt']);
  assert.deepEqual(outside([call('Bash', { command: 'cat "/srv/me/box/work/src/a.cs"' })]), [], 'a quoted path inside the tree is fine');
  assert.deepEqual(outside([call('Bash', { command: "tr '/' '-' < f; sed 's/a/b/' f; echo \"a/b\"" })]), [], 'a lone slash and a substitution are not paths');
});

test('A_tilde_path_is_one_path_not_also_the_absolute_path_after_its_tilde', () => {
  assert.deepEqual(resolved([call('Bash', { command: 'ls ~/rsd/minigit' })]), ['/home/u/rsd/minigit'], 'no stray /rsd/minigit');
  assert.deepEqual(resolved([call('Bash', { command: 'cat ~/.npmrc; ls "$HOME/.nuget"' })]), ['/home/u/.npmrc', '/home/u/.nuget']);
});

test('A_dot_dot_inside_an_absolute_path_resolves_lexically_and_is_not_a_second_relative_path', () => {
  const command = 'ls /srv/tc/typescript-7.0.2/../claude-2.1.310';
  assert.deepEqual(resolved([call('Bash', { command })]), ['/srv/tc/claude-2.1.310'], 'one path, not also ../claude-2.1.310 against the tree');
  assert.deepEqual(outside([call('Bash', { command })], { allow: ['/srv/tc/claude-2.1.310'] }), [], 'it lands on a bound copy: no hit');
  assert.deepEqual(resolved([call('Bash', { command: 'cat ./../../x' })]), ['/srv/me/x'], 'a ./ before the dots still climbs from the tree');
});

test('A_dot_dot_after_a_link_is_not_resolved_lexically_because_the_real_parent_is_unknown', () => {
  const isLink = p => p === '/srv/tc/typescript-7.0.2';
  const command = 'ls /srv/tc/typescript-7.0.2/../claude-2.1.310';
  const hits = outside([call('Bash', { command })], { allow: ['/srv/tc/claude-2.1.310', '/srv/tc/'], isLink });
  assert.equal(hits.length, 1, 'the allow list does not cover a path whose parent is unknown');
  assert.equal(hits[0].resolved, '/srv/tc/typescript-7.0.2/../claude-2.1.310', 'reported un-normalised, under the home or not, never inside or allowed');
  assert.deepEqual(outside([call('Bash', { command: 'ls /srv/tc/typescript-7.0.2/../claude-2.1.310' })], { allow: ['/srv/tc/claude-2.1.310'], isLink: () => false }), [], 'no component is a link: lexical');
  assert.equal(outside([call('Bash', { command: 'cd /srv/me/box/work/up/../x' })], { isLink: p => p.endsWith('/up') }).length, 1, 'a link inside the tree does not make the climb out of it safe');
});

test('A_call_may_name_the_directory_its_relative_paths_are_taken_from_and_paths_it_extracted_itself', () => {
  const at = (cwd, command, paths) => outside([{ agent: 'main', tool: 'Bash', input: { command }, cwd, paths }]).map(h => h.resolved);
  assert.deepEqual(at('/srv/other', 'cat ../x', ['rel/y']), ['/srv/x', '/srv/other/rel/y'], 'a `..` chain and a given path from the call\'s own directory, not the tree');
  assert.deepEqual(at('/srv/me/box/work/src', 'cat ../a', ['b']), [], 'inside the tree from a subdirectory');
  assert.deepEqual(at(undefined, 'cat ../../other', []), ['/srv/me/other'], 'no cwd: the tree, as ever');
  assert.deepEqual(at(null, 'cat ../x /abs/y ~/z', ['rel/y', '/abs/w']), ['/abs/y', '/home/u/z', '/abs/w'], 'an unknown directory: a relative path is skipped, a rooted one is not');
});
