// A walker launched by a wrapper (`xargs`, `parallel`, `env`, `nice`, `timeout`, `command`, `exec`, `nohup`) takes its paths from the pipe: `echo / | xargs find` walks `/`, and the walker's own segment names no path.
// The PRODUCER of the pipe is judged as a walk: high when it names a path that is or holds a protected root, or when it cannot be resolved (a file's content, a variable); no hit when every path it names is inside the tree or
// the allow list. A wrapper that launches no walker (`xargs cat`, `xargs grep -n`) is no walk.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { shellSteps } from '../lib/contamination-cwd.mjs';
import { boxReachable, boxSkeleton, layoutPaths } from '../lib/sandbox.mjs';

const home = os.homedir();
const ts = path.join(home, 'rsd', 'minigit', 'toolchains', 'typescript-7.0.2');
const layout = layoutPaths(path.join(home, 'rsd', 'minigit', 's-20261030-0c1a', 's'), 'c9b413');
const box = { layout, nodeDir: path.join(home, '.nvm', 'versions', 'node', 'v26.12.1'), toolchains: [ts], claudeBinary: path.join(home, 'rsd', 'minigit', 'toolchains', 'claude-2.1.310') };
const ctx = { rid: layout.rid, treeRoot: layout.work, home, boxHome: layout.home, harnessPaths: [layout.harness, layout.net], partnerPaths: [], arm: 'bare', ownSecrets: [], allow: boxReachable(box), skeleton: boxSkeleton(box), isLink: () => false };
const hitsOf = command => contaminationHits({ calls: [{ agent: 'main', tool: 'Bash', input: { command } }], ctx });
const high = command => hitsOf(command).filter(h => h.severity === 'high').map(h => h.rule);
const above = path.dirname(home);

test('A_pipe_into_a_wrapped_walker_is_judged_by_what_the_producer_names', () => {
  for (const command of ['echo / | xargs find', `echo ${above} | xargs find`, `printf '%s\\n' ${home} | xargs grep -r token`, `echo ${home} | xargs -I{} grep -rn token {}`, 'echo / | xargs -n1 du -a',
    'echo / | parallel find', 'echo / | xargs nice rg token', 'echo / | xargs timeout 5 tar cf x.tar', `echo ${layout.harness} | xargs tree`, 'echo / | xargs env find', 'echo / | xargs rsync -a', 'echo / | xargs fd x',
    `cd / && echo . | xargs find`, `echo ${home} | sort | xargs grep -R token`]) assert.deepEqual(high(command), ['outside-path'], command);
});

test('A_producer_that_cannot_be_resolved_is_high_for_a_wrapped_walker', () => {
  for (const command of ['cat paths.txt | xargs find', 'echo $D | xargs grep -r token', 'echo `pwd` | xargs find', 'awk "{print $1}" list | xargs du', 'cat paths.txt | sort | xargs tar cf x.tar', 'sort paths.txt | xargs find', 'ls $D | xargs find']) {
    assert.deepEqual(high(command), ['outside-path'], command);
  }
});

test('A_producer_that_names_only_paths_inside_the_tree_or_the_box_is_no_hit', () => {
  for (const command of ['find . -name "*.cs" | xargs grep -n Main', 'ls src | xargs -I{} cat src/{}', 'git ls-files | xargs wc -l', 'find . -name "*.cs" | xargs grep -rn Main', 'git ls-files | xargs grep -r Main', 'echo src | xargs find',
    'ls src | xargs -n1 du -a', 'find . -name x | sort | xargs tar cf out.tar', 'printf "%s\\n" src lib | xargs rg token', 'echo . | xargs find', `echo ${layout.work} | xargs find`, `echo ${ts} | xargs find`, 'cd src && echo . | xargs find']) {
    assert.deepEqual(hitsOf(command), [], command);
  }
});

test('A_wrapper_that_launches_no_walker_is_no_walk', () => {
  for (const command of ['echo / | xargs cat', 'echo / | xargs grep -n token', 'echo / | xargs -r ls', 'echo $D | xargs cat', 'cat paths.txt | xargs rm -f', 'echo / | nice cat', 'cat paths.txt | xargs wc -l']) {
    assert.deepEqual(high(command), [], command);
  }
});

test('A_pipe_that_is_not_the_producer_of_the_walker_is_not_judged', () => {
  assert.deepEqual(high('echo / ; xargs find'), [], 'a semicolon is no pipe');
  assert.deepEqual(high('echo / && xargs find'), [], 'neither is &&');
  assert.deepEqual(high('echo / || xargs find'), [], 'nor ||');
  assert.deepEqual(high('echo / \n xargs find'), [], 'nor a newline');
});

test('A_step_knows_whether_the_pipe_feeds_it', () => {
  const piped = command => shellSteps(command, ctx).map(s => s.piped);
  assert.deepEqual(piped('a | b | c; d && e || f |& g\nh'), [false, true, true, false, false, false, true, false]);
});
