// The shape of the tree a push carries: which submodules may exist (only the conventions mount, and only from the conventions repository).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CONVENTIONS_PATH, CONVENTIONS_URL, findSubmoduleProblems } from '../lib/push-shape.mjs';

const gitlink = path => [path, { oid: '1'.repeat(40), mode: '160000' }];
const MODULES = (url, path = CONVENTIONS_PATH) => `[submodule "m"]\n\tpath = ${path}\n\turl = ${url}\n`;
const problems = (links, gitmodulesText = null) => findSubmoduleProblems({ tip: new Map(links), gitmodulesText }).map(f => [f.path, f.line, f.what]);

test('The_conventions_mount_from_the_conventions_repository_is_the_one_allowed_submodule', () => {
  assert.deepEqual(problems([gitlink(CONVENTIONS_PATH)], MODULES(CONVENTIONS_URL)), []);
});

test('Every_other_submodule_is_refused_by_path', () => {
  assert.deepEqual(problems([gitlink(CONVENTIONS_PATH), gitlink('csharp/01-x/sol')], MODULES(CONVENTIONS_URL)), [['csharp/01-x/sol', null, 'submodule outside the conventions mount']]);
});

test('A_gitmodules_url_other_than_the_conventions_repository_is_refused_by_line', () => {
  assert.deepEqual(problems([gitlink(CONVENTIONS_PATH)], MODULES('https://github.com/someone/else.git')), [['.gitmodules', 3, 'submodule URL other than the conventions repository']]);
  assert.deepEqual(problems([], MODULES(CONVENTIONS_URL, 'csharp/x')), [['.gitmodules', 2, 'submodule path other than the conventions mount']]);
  assert.deepEqual(problems([], '[submodule "a"]\n\turl=../relative.git\n'), [['.gitmodules', 2, 'submodule URL other than the conventions repository']], 'a relative URL and no spaces around = are read too');
});

test('The_gitmodules_scan_still_finds_the_allowed_entry_it_must_not_flag', () => {
  assert.equal(findSubmoduleProblems({ tip: new Map([gitlink(CONVENTIONS_PATH)]), gitmodulesText: MODULES(CONVENTIONS_URL) }).length, 0);
  assert.equal(findSubmoduleProblems({ tip: new Map([gitlink(CONVENTIONS_PATH)]), gitmodulesText: MODULES(`${CONVENTIONS_URL}x`) }).length, 1, 'the scan is alive');
});
