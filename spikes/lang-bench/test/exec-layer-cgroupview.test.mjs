// E1.S8b brief C (OP-K, parent D15, decided 2026-11-02): each program sees its OWN cgroup, read-only — `--unshare-cgroup` (so /proc/self/cgroup shows `0::/`) and `prog/` bound read-only at
// /sys/fs/cgroup, ALWAYS: it is the layer's box, not an option. Nothing else of the host's cgroups or `/sys` is visible.
import assert from 'node:assert/strict';
import path from 'node:path';
import { test } from 'node:test';
import { boxArgv } from '../lib/exec-layer/box.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { chain, MIB, needs, start, text, withoutView } from './support/exec-chain.mjs';
import { toolchains } from './support/exec-layer-host.mjs';

const SCRIPT = `#!/bin/bash
exec > view.out
p() { printf '%s\\t%s\\n' "$1" "$2"; }
p self "$(cat /proc/self/cgroup)"
p max "$(cat /sys/fs/cgroup/memory.max 2>&1)"
p write "$( (echo 1 > /sys/fs/cgroup/memory.max) 2>&1 | head -1 )"
p dirs "$(find /sys/fs/cgroup -mindepth 1 -maxdepth 1 -type d 2>/dev/null | paste -sd,)"
p sys "$(ls -A /sys | paste -sd,)"
p sysfs "$(ls -A /sys/fs | paste -sd,)"
p host "$(ls /sys/fs/cgroup/user.slice /sys/fs/cgroup/.. 2>&1 | paste -sd,)"
`;
const factsOf = body => Object.fromEntries(body.split('\n').filter(line => line.includes('\t')).map(line => [line.slice(0, line.indexOf('\t')), line.slice(line.indexOf('\t') + 1)]));

async function seen(t, boxTweak) {
  const c = await chain(t, { files: { 'view.sh': SCRIPT }, boxTweak, memoryMax: 256 * MIB });
  await start(t, c.launch(['bash', 'view.sh']), { stdio: ['ignore', 'ignore', 'ignore'] }).exited;
  return c;
}

test('The_box_argv_always_carries_the_own_cgroup_view_and_refuses_to_be_built_without_a_decision_about_it', () => {
  const argv = boxArgv({ view: '/v', dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: '/sys/fs/cgroup/x/prog' });
  assert.ok(argv.includes('--unshare-cgroup'));
  const at = argv.findIndex((arg, i) => arg === '--ro-bind' && argv[i + 1] === '/sys/fs/cgroup/x/prog');
  assert.deepEqual(argv.slice(at, at + 3), ['--ro-bind', '/sys/fs/cgroup/x/prog', '/sys/fs/cgroup']);
  assert.throws(() => boxArgv({ view: '/v', dirs: toolchains, env: boxEnv({ dirs: toolchains }) }), /prog/);
  const bare = boxArgv({ view: '/v', dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null });
  assert.ok(!bare.includes('--unshare-cgroup'), 'null is for a box outside any scope (a build), never for a run');
});

test('Each_program_sees_only_its_own_cgroup_read_only', async t => {
  if (!needs(t)) return;
  const c = await seen(t, argv => argv);
  const facts = factsOf(text(path.join(c.view, 'view.out')));
  assert.equal(facts.self, '0::/', '/proc/self/cgroup is the root of the program\'s own cgroup namespace');
  assert.equal(Number(facts.max), 256 * MIB, '/sys/fs/cgroup/memory.max is the limit');
  assert.match(facts.write, /Read-only file system/, 'and it cannot be written');
  assert.equal(facts.dirs, '', 'no child, sibling or parent cgroup is visible');
  assert.equal(facts.sys, 'fs', 'nothing of /sys but fs');
  assert.equal(facts.sysfs, 'cgroup');
  assert.match(facts.host, /No such file/, 'the host\'s cgroup paths do not exist');
});

test('Without_the_view_there_is_no_cgroup_in_the_box_and_that_is_the_point', async t => {
  if (!needs(t)) return;
  const c = await seen(t, withoutView);
  const facts = factsOf(text(path.join(c.view, 'view.out')));
  assert.match(facts.max, /No such file/, 'no /sys/fs/cgroup: a runtime that reads its limit there finds the host\'s memory');
  assert.notEqual(facts.self, '0::/');
});
