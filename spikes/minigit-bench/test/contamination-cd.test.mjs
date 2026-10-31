// A `cd` reads nothing: it is no walk and no reach by itself (the Node pilot c9b413 was voided for `cd / && rm -rf /tmp/mg`, its own scratch cleanup). The danger is a LATER relative read, so every rule of the
// contamination scan resolves a relative path word, and a relative search-tool path, from the directory the `cd` segments before it moved to. A directory the scan cannot place (`cd -`, `$OLDPWD`, a variable, a
// substitution) makes the directory unknown: a relative word that is a walk's argument is then high (the conservative case), any other relative word is skipped.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { boxReachable, boxSkeleton, layoutPaths } from '../lib/sandbox.mjs';

const home = os.homedir();
const ts = path.join(home, 'rsd', 'minigit', 'toolchains', 'typescript-7.0.2');
const layout = layoutPaths(path.join(home, 'rsd', 'minigit', 's-20261030-0c1a', 's'), 'c9b413');
const box = { layout, nodeDir: path.join(home, '.nvm', 'versions', 'node', 'v26.12.1'), toolchains: [ts], claudeBinary: path.join(home, 'rsd', 'minigit', 'toolchains', 'claude-2.1.310') };
const ctx = { rid: layout.rid, treeRoot: layout.work, home, boxHome: layout.home, harnessPaths: [layout.harness, layout.net], partnerPaths: [], arm: 'bare', ownSecrets: [], allow: boxReachable(box), skeleton: boxSkeleton(box), isLink: () => false };
const hitsOf = command => contaminationHits({ calls: [{ agent: 'main', tool: 'Bash', input: { command } }], ctx });
const shown = command => hitsOf(command).map(h => `${h.rule}:${h.severity}`);
const high = command => hitsOf(command).filter(h => h.severity === 'high').map(h => h.rule);
const underRoot = p => path.relative('/', p); // an absolute path as a word relative to `/`

// call 4 of the pilot c9b413 (series s-20261030-0c1a): the series path generalised; the one high hit was the `/` of its last `cd`
const PILOT_CALL = [
  "cat >> src/node-shims.d.ts <<'EOF'", '', 'declare class TextEncoder {', '  encode(input: string): Uint8Array;', '}', 'EOF',
  `PATH=${ts}/node_modules/.bin:$PATH tsc --noEmit -p . && echo TYPECHECK OK`, 'bash test-v1.sh | tail -3',
  'mkdir -p /tmp/mg && cd /tmp/mg && rm -rf .minigit && echo x > f && $OLDPWD/minigit init && $OLDPWD/minigit add f && $OLDPWD/minigit commit -m "one two" && echo y > g && $OLDPWD/minigit add g && $OLDPWD/minigit commit -m two && $OLDPWD/minigit log && cat .minigit/commits/$(cat .minigit/HEAD); cd / && rm -rf /tmp/mg',
].join('\n');

test('The_pilots_scratch_cleanup_cd_slash_then_rm_of_its_own_dir_is_no_high_hit', () => {
  assert.deepEqual(high(PILOT_CALL), []);
  assert.deepEqual(high('cd / && rm -rf /tmp/mg'), []);
  assert.deepEqual(shown('cd / && rm -rf /tmp/mg'), ['outside-path:low'], 'the box /tmp path is low, as a plain reach for it is');
});

test('A_cd_is_no_reach_and_no_walk_by_itself', () => {
  for (const command of ['cd /tmp && ls', 'cd /', 'cd / && ls', 'cd /usr && ls', `cd ${home}`, `cd ${home} && ls`, `pushd ${home}`, `cd ${layout.dir}`, 'cd ~ && ls', 'cd', 'popd']) assert.deepEqual(shown(command), [], command);
});

test('A_relative_read_after_a_cd_is_judged_where_the_cd_moved_to', () => {
  const harnessFile = underRoot(path.join(layout.harness, 'settings.json'));
  assert.deepEqual(high(`cd / && cat ${harnessFile}`), ['outside-path'], 'the review example');
  assert.deepEqual(high(`cd ${path.dirname(home)} && cat ${path.basename(home)}/.ssh/id_rsa`), ['outside-path'], 'a dir above the home');
  assert.deepEqual(high(`cd ${home} && cat rsd/minigit/s-20261030-0c1a/s/c9b413/harness/audit.mjs`), ['outside-path'], 'the home itself');
  assert.deepEqual(high(`cd /usr/lib && cat ../../${harnessFile}`), ['outside-path'], '.. from the cd target, not from the work tree');
  assert.deepEqual(high(`cd ${layout.harness} && ls ..`), ['outside-path'], 'the parent of the harness dir');
  assert.deepEqual(high(`pushd / > /dev/null && cat ${harnessFile}`), ['outside-path'], 'pushd moves the directory as cd does');
});

test('A_walk_from_a_cd_target_that_holds_a_protected_root_or_is_one_is_high', () => {
  assert.deepEqual(high(`cd ${home} && grep -r token .`), ['outside-path']);
  assert.deepEqual(high('cd / && grep -r token .'), ['outside-path'], 'the root holds the home');
  assert.deepEqual(high(`cd ${path.dirname(home)} && find . -name settings.json`), ['outside-path'], 'a dir above the home');
  assert.deepEqual(high(`cd ${layout.harness} && tar cf - .`), ['outside-path']);
  assert.deepEqual(high('cd /usr && grep -r token .'), [], 'a system dir holds no protected root');
  assert.deepEqual(shown('cd /tmp && ls'), []);
});

test('A_relative_path_after_a_cd_that_stays_in_the_tree_or_the_box_is_no_reach', () => {
  for (const command of ['cd src && cat a.ts', 'cd src && grep -r token .', 'cd src && cat ../lib/b.ts', 'cd . && find . -name x', 'cd && grep -r token .', 'cd ~ && find . -name x', `cd ${layout.work} && grep -r token .`, 'cd /tmp/mg && cat f']) {
    assert.deepEqual(high(command), [], command);
  }
  assert.deepEqual(shown('cd src && grep -r token .'), [], 'inside the tree: no hit at all');
  assert.deepEqual(shown('cd && grep -r token .'), [], 'a cd with no word goes to the box HOME');
  assert.deepEqual(high('cd src && cat ../../../../../../../../../etc/hostname'), [], 'a system path is low');
  assert.deepEqual(high(`cd src && cat ${'../'.repeat(12)}${underRoot(path.join(layout.harness, 'x'))}`), ['outside-path'], '.. out of the tree from a subdirectory');
});

test('A_cd_whose_target_the_scan_cannot_place_leaves_the_directory_unknown_and_only_a_walk_is_high', () => {
  for (const command of ['cd $OLDPWD && grep -r . .', 'cd - && grep -r token .', 'cd "$d" && find . -name x', 'cd $(mktemp -d) && grep -rn token src', 'popd && grep -r token .', 'pushd && find . -name x', 'cd ~root && grep -r x .']) {
    assert.deepEqual(high(command), ['outside-path'], command);
  }
  for (const command of ['cd $OLDPWD && cat f', 'cd - && ls', 'cd "$d" && echo x > f && $d/minigit add f', 'd=$(mktemp -d) && cd $d && cat .minigit/x; rm -rf $d', 'cd $(mktemp -d) && ls -la']) {
    assert.deepEqual(high(command), [], command);
  }
  assert.deepEqual(high('cd - && cat /usr/lib/x'), [], 'an absolute word needs no directory');
  assert.deepEqual(high(`cd - && cat ${home}/.ssh/id_rsa`), ['outside-path'], 'an absolute word is judged as ever');
  assert.deepEqual(high('cd /tmp && cd $OLDPWD && grep -r . .'), ['outside-path'], '$OLDPWD is unknown, whatever the cd before it');
  assert.deepEqual(high('cd $OLDPWD && cd /usr && grep -r token .'), [], 'a known cd after an unknown one makes the directory known again');
});

test('A_subshell_keeps_its_cd_to_itself', () => {
  assert.deepEqual(high('(cd / && grep -r token .)'), ['outside-path']);
  assert.deepEqual(high(`(cd / && cat ${underRoot(path.join(layout.harness, 'x'))})`), ['outside-path']);
  assert.deepEqual(high('(cd / && ls); grep -r token .'), [], 'after the subshell the directory is the work tree again');
  assert.deepEqual(high('(cd /usr && ls) && cd src && grep -r token .'), []);
  assert.deepEqual(high(`(cd ${home}); grep -r token src`), [], 'the cd of a closed subshell does not move the next walk');
});

test('The_search_tools_and_the_file_tools_name_their_own_paths_and_are_not_moved_by_a_cd', () => {
  const tool = (name, input) => contaminationHits({ calls: [{ agent: 'main', tool: name, input }], ctx }).filter(h => h.severity === 'high').map(h => h.rule);
  assert.deepEqual(tool('Grep', { pattern: 'token', path: home }), ['outside-path']);
  assert.deepEqual(tool('Grep', { pattern: 'token' }), []);
  assert.deepEqual(tool('Glob', { pattern: '**/*.json', path: 'src' }), []);
});

test('The_link_rules_resolve_their_relative_words_from_the_cd_target_too', () => {
  const links = command => hitsOf(command).filter(h => h.rule.startsWith('link-')).map(h => `${h.rule}:${h.severity}`);
  assert.deepEqual(links('cd src && ln -s .. up'), [], 'src/up -> the tree root: inside');
  assert.deepEqual(links('cd src && ln -s ../../.. up'), ['link-out:high'], 'src/up -> the session dir, which holds the harness');
  assert.deepEqual(links(`cd / && ln -s ${underRoot(home)} h && find h`), ['link-out:high', 'link-walk:high'], 'a relative target is judged from the link\'s directory');
  assert.deepEqual(links(`ln -s ${home} x && cd x && ls`), ['link-out:high', 'link-walk:high'], 'a cd through a link that leaves the tree is a link-walk');
  assert.deepEqual(links('cd $OLDPWD && ln -s ../.. up'), [], 'a link whose own directory is unknown cannot be placed: skipped');
});
