// A link in the agent-writable tree is classified from its target STRING only, mapped through the box's own bind table (box path -> host path),
// lexically normalised; the link is never followed. `inside` = the target lies in the session's agent-writable tree; everything else stays `high`.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { classifyLink } from '../lib/link-class.mjs';
import { agentBoxPrefix, sessionLayout, writableBinds } from '../lib/sandbox.mjs';
import { scanSessionDir } from '../lib/session-scan.mjs';
import { fakeToken } from './broker-stub.mjs';
import { boxOptions } from './box-helpers.mjs';
import { tmpDir } from './helpers.mjs';

/** A real layout under a temp root with the CLI's shape on disk: a subagent transcript in box/config and the tasks dir in box/tmp. */
function session() {
  const made = sessionLayout(tmpDir('lc'), 's1');
  assert.equal(made.ok, true, made.reason);
  const layout = made.value;
  const transcript = path.join(layout.config, 'projects', 'p', 'subagents', 'agent-1.jsonl');
  mkdirSync(path.dirname(transcript), { recursive: true });
  writeFileSync(transcript, '{}\n');
  const tasks = path.join(layout.tmp, 'claude-1000', 'work', 'sid', 'tasks');
  mkdirSync(tasks, { recursive: true });
  return { layout, transcript, tasks, binds: writableBinds(layout) };
}
/** Plant a link at `<tasks>/<name>` with `target` (the string the CLI wrote) and classify it. */
function planted(s, name, target) {
  symlinkSync(target, path.join(s.tasks, name));
  return classifyLink({ root: s.layout.dir, rel: path.relative(s.layout.dir, path.join(s.tasks, name)), binds: s.binds });
}

test('The_bind_table_is_what_the_box_argv_binds_read_write', () => {
  const { layout } = session();
  const argv = agentBoxPrefix(boxOptions(layout));
  for (const { box, host } of writableBinds(layout)) {
    const i = argv.findIndex((a, j) => a === '--bind' && argv[j + 1] === host);
    assert.ok(i >= 0, `${host} is bound`);
    assert.equal(argv[i + 2], box, `at ${box}`);
  }
  assert.ok(writableBinds(layout).some(b => b.box === '/tmp' && b.host === layout.tmp), 'the box /tmp is box/tmp');
});

test('A_link_to_a_file_in_the_agent_writable_tree_is_inside_and_reported_in_box_paths', () => {
  const s = session();
  // the CLI's own shape: tasks/<id>.output -> subagents/agent-<id>.jsonl, the target an absolute box path
  const r = planted(s, 'a1.output', s.transcript);
  assert.equal(r.severity, 'inside');
  assert.equal(r.target, s.transcript);
  assert.equal(r.link, path.posix.join('/tmp', 'claude-1000', 'work', 'sid', 'tasks', 'a1.output'), 'the link is named by the path the box sees');
  // a relative target is read from the link's own box directory; with no `..` segment the kernel and a lexical read agree
  const down = planted(s, 'a2.output', 'sub/x.txt');
  assert.equal(down.severity, 'high', 'a relative target that does not exist is dangling');
  const sibling = path.join(s.layout.config, 'projects', 'p', 'subagents');
  symlinkSync('agent-1.jsonl', path.join(sibling, 'agent-2.jsonl'));
  const within = classifyLink({ root: s.layout.dir, rel: path.relative(s.layout.dir, path.join(sibling, 'agent-2.jsonl')), binds: s.binds });
  assert.deepEqual([within.severity, within.target], ['inside', s.transcript], 'a relative link inside one bind');
});

test('Every_other_link_stays_high', () => {
  const s = session();
  const decoy = path.join(tmpDir('lc-decoy'), 'host-file.txt');
  writeFileSync(decoy, 'HOST-ONLY\n');
  const rows = {
    'a .. escape that leaves every bind': '../../../../../../../../etc/passwd',
    'a relative link out through the tmp root': '../../../../../../../tmp-sibling/x',
    'an absolute host path': decoy,
    'a host path of the session\'s own harness dir': path.join(s.layout.harness, 'box-init.mjs'),
    'the host path of box/tmp, which the box does not see': path.join(s.layout.tmp, 'claude-1000'),
    'a path no bind maps': '/proc/1/environ',
    'a dangling target inside a bind': path.join(s.layout.config, 'projects', 'p', 'subagents', 'agent-gone.jsonl'),
    'a dangling relative target': 'nothing-here.jsonl',
  };
  for (const [name, target] of Object.entries(rows)) {
    const r = planted(s, `l${Object.keys(rows).indexOf(name)}.output`, target);
    assert.equal(r.severity, 'high', `${name}: ${JSON.stringify(r)}`);
    assert.equal(typeof r.why, 'string');
  }
});

test('The_target_of_a_link_is_never_read_or_followed_to_classify_it', () => {
  const s = session();
  const decoy = path.join(tmpDir('lc-d2'), 'secret.txt');
  writeFileSync(decoy, `HOST ${fakeToken()}\n`);
  const r = planted(s, 'x.output', decoy);
  assert.equal(JSON.stringify(r).includes('HOST'), false);
  assert.equal(r.severity, 'high');
});

test('The_scan_lists_inside_links_apart_and_only_a_high_link_is_a_finding', () => {
  const s = session();
  const real = fakeToken();
  const decoy = path.join(tmpDir('lc-d3'), 'host.txt');
  writeFileSync(decoy, `x ${real}`);
  symlinkSync(s.transcript, path.join(s.tasks, 'in.output'));
  const alone = scanSessionDir(s.layout.dir, { real, binds: s.binds });
  assert.deepEqual(alone.links, [], 'an inside link is not a finding');
  assert.equal(alone.insideLinks.length, 1);
  assert.deepEqual(alone.insideLinks[0], { link: '/tmp/claude-1000/work/sid/tasks/in.output', target: s.transcript });
  symlinkSync(decoy, path.join(s.tasks, 'out.output'));
  assert.equal(spawnSync('mkfifo', [path.join(s.layout.tmp, 'pipe')]).status, 0);
  const both = scanSessionDir(s.layout.dir, { real, binds: s.binds });
  assert.deepEqual(both.links.map(l => [l.rel.split(path.sep).at(-1), l.severity]).sort(), [['out.output', 'high'], ['pipe', 'high']], 'a host link and a pipe stay high');
  assert.deepEqual(both.hits, [], 'nothing was read through a link');
  assert.equal(both.insideLinks.length, 1);
  assert.equal(scanSessionDir(s.layout.dir, { real }).links.length >= 3, true, 'without a bind table every link is high (the old rule)');
});

/** Plant a link at `<dir>/<name>` (a host path of the agent-writable tree) and classify it. */
function plantedIn(s, dir, name, target) {
  mkdirSync(dir, { recursive: true });
  symlinkSync(target, path.join(dir, name));
  return classifyLink({ root: s.layout.dir, rel: path.relative(s.layout.dir, path.join(dir, name)), binds: s.binds });
}

test('A_dotdot_after_a_link_is_never_inside_because_the_kernel_resolves_it_after_following_the_link', () => {
  const s = session();
  // the reviewer's probe: `sub` is the box /tmp (the HOST /tmp for a host reader); `sub/../etc/passwd` is work/etc/passwd lexically and
  // /etc/passwd for the kernel, which resolves `..` after following `sub`
  symlinkSync('/tmp', path.join(s.layout.work, 'sub'));
  mkdirSync(path.join(s.layout.work, 'etc'));
  writeFileSync(path.join(s.layout.work, 'etc', 'passwd'), 'decoy');
  const r = plantedIn(s, s.layout.work, 'evil', 'sub/../etc/passwd');
  assert.equal(r.severity, 'high', JSON.stringify(r));
  assert.match(r.why, /\.\./);
  // with no link in the way the kernel and a lexical read agree, so a relative `..` that stays in the bind is inside (see the npm .bin test below)
  const plain = plantedIn(s, s.layout.work, 'plain', 'etc/../etc/passwd');
  assert.equal(plain.severity, 'inside', JSON.stringify(plain));
  // a `..` that leaves the bind it started in is high, even toward another bind of the same session
  const out = plantedIn(s, s.layout.out, 'up', '../work/etc/passwd');
  assert.equal(out.severity, 'high', 'a relative `..` target that leaves its bind is high');
  assert.match(out.why, /bind/);
  // and the walk does not stop at the first `..`: `d` is a link, so `d/../f` is whatever the kernel finds above the link's TARGET
  symlinkSync(tmpDir('lc-d5'), path.join(s.layout.work, 'd'));
  writeFileSync(path.join(s.layout.work, 'f'), 'x');
  const afterLink = plantedIn(s, s.layout.work, 'via-d', 'd/../f');
  assert.equal(afterLink.severity, 'high', JSON.stringify(afterLink));
  assert.match(afterLink.why, /link/);
  // an absolute target with a `..` stays high: only a relative one is resolved from a directory the checks cover
  const abs = plantedIn(s, s.layout.out, 'abs-dotdot', `${path.join(s.layout.work, 'etc')}/../etc/passwd`); // built as a string: path.join would normalise the `..` away
  assert.equal(abs.severity, 'high', JSON.stringify(abs));
});

test('The_npm_bin_shape_is_inside_a_relative_dotdot_target_within_one_bind_when_no_directory_on_the_way_is_a_link', () => {
  const s = session();
  // what `npm install typescript` writes: node_modules/.bin/tsc -> ../typescript/bin/tsc
  const pkg = path.join(s.layout.work, 'node_modules', 'typescript', 'bin');
  mkdirSync(pkg, { recursive: true });
  writeFileSync(path.join(pkg, 'tsc'), '#!/usr/bin/env node\n');
  const r = plantedIn(s, path.join(s.layout.work, 'node_modules', '.bin'), 'tsc', '../typescript/bin/tsc');
  assert.equal(r.severity, 'inside', JSON.stringify(r));
  assert.equal(r.target, path.join(s.layout.work, 'node_modules', 'typescript', 'bin', 'tsc'), 'reported as the box path the kernel reaches');
  // the same shape one directory deeper (scoped packages: .bin/x -> ../@scope/pkg/bin/x, and a `..` pair)
  const deep = path.join(s.layout.work, 'node_modules', '@s', 'p', 'bin');
  mkdirSync(deep, { recursive: true });
  writeFileSync(path.join(deep, 'x'), 'x');
  assert.equal(plantedIn(s, path.join(s.layout.work, 'node_modules', '.bin'), 'x', '../@s/p/bin/x').severity, 'inside');
  assert.equal(plantedIn(s, deep, 'y', '../../../typescript/bin/tsc').severity, 'inside', 'three `..` from a deeper directory');
  // the same shape with the directory a link instead of a real directory is high
  const elsewhere = tmpDir('lc-d6');
  mkdirSync(path.join(elsewhere, 'bin'));
  writeFileSync(path.join(elsewhere, 'bin', 'tsc'), 'x');
  symlinkSync(elsewhere, path.join(s.layout.work, 'node_modules', 'linked'));
  assert.equal(plantedIn(s, path.join(s.layout.work, 'node_modules', '.bin'), 'bad', '../linked/bin/tsc').severity, 'high');
  // and a target that is not there is dangling
  assert.equal(plantedIn(s, path.join(s.layout.work, 'node_modules', '.bin'), 'gone', '../nothing/bin/x').severity, 'high');
});

test('A_relative_target_inside_the_box_tmp_is_inside_because_the_link_and_its_target_move_together_but_an_absolute_tmp_path_is_not', () => {
  const s = session();
  const dir = path.join(s.layout.tmp, 'claude-1000', 'work', 'sid');
  mkdirSync(path.join(dir, 'a'), { recursive: true });
  mkdirSync(path.join(dir, 'b'), { recursive: true });
  writeFileSync(path.join(dir, 'b', 'f'), 'x');
  const rel = plantedIn(s, path.join(dir, 'a'), 'l', '../b/f');
  assert.equal(rel.severity, 'inside', JSON.stringify(rel));
  assert.equal(rel.target, '/tmp/claude-1000/work/sid/b/f', 'reported in box paths');
  assert.equal(plantedIn(s, path.join(dir, 'a'), 'm', 'x-gone').severity, 'high');
  assert.equal(plantedIn(s, path.join(dir, 'a'), 'abs', '/tmp/claude-1000/work/sid/b/f').severity, 'high', 'the same file by its absolute box path is the HOST path for a host reader');
  // out of the tmp bind by `..` is high
  assert.equal(plantedIn(s, path.join(dir, 'a'), 'up', '../../../../../../work/x').severity, 'high');
});

test('A_target_under_the_box_tmp_is_never_inside_because_the_host_reads_an_absolute_tmp_as_its_own', () => {
  const s = session();
  mkdirSync(path.join(s.layout.tmp, 'claude-1000'), { recursive: true });
  const r = plantedIn(s, s.layout.out, 'scratch', '/tmp/claude-1000');
  assert.equal(r.severity, 'high', JSON.stringify(r));
  assert.match(r.why, /tmp|differently|host/);
  // an absolute /tmp target from a link inside box/tmp itself is the same case
  const inTmp = plantedIn(s, path.join(s.layout.tmp, 'claude-1000'), 'self', '/tmp/claude-1000');
  assert.equal(inTmp.severity, 'high');
});

test('A_target_below_a_link_is_never_inside_because_a_directory_link_in_the_path_redirects_it', () => {
  const s = session();
  const decoyDir = tmpDir('lc-d4');
  writeFileSync(path.join(decoyDir, 'secret.txt'), 'HOST-ONLY\n');
  symlinkSync(decoyDir, path.join(s.layout.work, 'door')); // the door is itself a (high) link out
  const through = plantedIn(s, s.layout.out, 'via-door', path.join(s.layout.work, 'door', 'secret.txt'));
  assert.equal(through.severity, 'high', JSON.stringify(through));
  assert.match(through.why, /link/);
  // the same target string with the door a real directory is inside
  mkdirSync(path.join(s.layout.work, 'real'));
  writeFileSync(path.join(s.layout.work, 'real', 'f.txt'), 'x');
  const direct = plantedIn(s, s.layout.out, 'direct', path.join(s.layout.work, 'real', 'f.txt'));
  assert.equal(direct.severity, 'inside', JSON.stringify(direct));
});
