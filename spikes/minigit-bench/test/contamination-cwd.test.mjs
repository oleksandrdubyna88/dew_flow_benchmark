// The ordered pass over a shell command that gives every simple command the directory it runs in (lib/contamination-cwd.mjs): the one tracker the path rules and the link rules read.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { absOf, resolvable, shellSteps } from '../lib/contamination-cwd.mjs';

const ctx = Object.freeze({ treeRoot: '/h/work', home: '/home/u', boxHome: '/h/home' });
const dirs = command => shellSteps(command, ctx).map(s => [s.words[0], s.cwd]);

test('Each_simple_command_has_the_directory_the_cds_before_it_left', () => {
  assert.deepEqual(dirs('ls; cd src && cat a; cd /usr/lib && ls ../x; cd .. ; ls'), [['ls', '/h/work'], ['cd', '/h/work'], ['cat', '/h/work/src'], ['cd', '/h/work/src'], ['ls', '/usr/lib'], ['cd', '/usr/lib'], ['ls', '/usr']]);
  assert.deepEqual(dirs('cd /tmp\nls'), [['cd', '/h/work'], ['ls', '/tmp']], 'a newline ends a command');
});

test('A_cd_step_says_where_it_moves_to_and_no_other_step_does', () => {
  const [ls, cd, home, bare, flag] = shellSteps('ls; cd /x; cd ~/y; cd; cd -P /z', ctx);
  assert.equal(ls.cd, undefined);
  assert.deepEqual([cd.cd, home.cd, bare.cd, flag.cd], [{ to: '/x' }, { to: '/h/home/y' }, { to: '/h/home' }, { to: '/z' }], '~ and a bare cd are the BOX home');
});

test('A_directory_the_scan_cannot_place_is_unknown_until_a_cd_to_a_known_one', () => {
  for (const unknown of ['cd -', 'cd $OLDPWD', 'cd "$d"', 'cd $(mktemp -d)', 'cd src/*', 'cd ~root', 'popd', 'pushd', 'pushd +1']) {
    assert.deepEqual(dirs(`${unknown} && ls && cd /usr && ls && cd lib && ls`).map(x => x[1]), [ctx.treeRoot, null, null, '/usr', '/usr', '/usr/lib'], unknown);
  }
  assert.deepEqual(dirs('cd - && cd /x && ls').map(x => x[1]), [ctx.treeRoot, null, '/x']);
  assert.deepEqual(dirs('cd $d && ls ../x && cd rel && ls').map(x => x[1]), [ctx.treeRoot, null, null, null], 'a relative cd from an unknown directory stays unknown');
});

test('pushd_moves_like_cd_and_the_cd_options_are_no_directory', () => {
  assert.deepEqual(dirs('pushd /a > /dev/null && ls; cd -L b && ls; cd -- /c && ls').map(x => x[1]), [ctx.treeRoot, '/a', '/a', '/a/b', '/a/b', '/c']);
});

test('A_group_keeps_its_cd_to_itself_and_a_command_substitution_is_no_group', () => {
  assert.deepEqual(dirs('(cd /a && ls); ls').map(x => x[1]), [ctx.treeRoot, '/a', ctx.treeRoot], 'the directory before the group is back at its )');
  assert.deepEqual(dirs('(cd /a; (cd b; ls); ls); ls').map(x => x[1]), [ctx.treeRoot, '/a', '/a/b', '/a', ctx.treeRoot]);
  assert.deepEqual(dirs('cd /a && echo $(date) && ls').map(x => x[1]), [ctx.treeRoot, '/a', '/a'], 'the ) of $(date) closes no group');
});

test('A_word_is_placed_from_the_directory_or_by_its_own_root_and_unplaced_when_relative_in_an_unknown_directory', () => {
  assert.equal(absOf('x/../y', '/a', ctx), '/a/y');
  assert.equal(absOf('/z', '/a', ctx), '/z');
  assert.equal(absOf('~/q', '/a', ctx), '/h/home/q');
  assert.equal(absOf('${HOME}/q', null, ctx), '/h/home/q', 'a rooted word needs no directory');
  assert.equal(absOf('/z', null, ctx), '/z');
  assert.equal(absOf('y', null, ctx), null);
  assert.deepEqual(['$d', '*.ts', '$(x)', '~root', 'a b'].map(w => resolvable(w)), [false, false, false, false, true]);
  assert.deepEqual(['$HOME/x', '${HOME}', '~/x'].map(w => resolvable(w)), [true, true, true]);
});

test('A_command_of_many_segments_is_stepped_in_linear_time', () => {
  const command = Array.from({ length: 30000 }, (_, i) => `echo ${i}`).join(' && ');
  const started = performance.now();
  const steps = shellSteps(command, ctx);
  const ms = performance.now() - started;
  assert.equal(steps.length, 30000);
  assert.ok(ms < 500, `${ms.toFixed(0)} ms: copying the steps again at every segment is quadratic`);
});
