// The contamination scan's `outside-path` rule against the paths the agent's box exposes on purpose (the Node pilot 9804fc, voided as contaminated for three of them): the pinned
// toolchain the box binds read-only and the session's own box dirs are no reach; a partner's dir, the operator's other files, another series and the harness's own dirs stay one.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import { appendFileSync, mkdirSync, mkdtempSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { collectSession } from '../lib/bare-collect.mjs';
import { agentBoxPrefix, boxReachable, boxSkeleton, layoutPaths } from '../lib/sandbox.mjs';
import { loadSessionDir } from '../lib/smoke-load.mjs';
import { lay } from './bare-fixture.mjs';
import { pilotCommands } from './pilot-ts-calls.mjs';
import { spikeDir } from './helpers.mjs';

const home = os.homedir();
const toolchains = path.join(home, 'rsd', 'minigit', 'toolchains');
const ts = path.join(toolchains, 'typescript-7.0.2');
const claudeBinary = path.join(toolchains, 'claude-2.1.310');
const nodeDir = path.join(home, '.nvm', 'versions', 'node', 'v26.12.1');
const BOX = Object.freeze({ nodeDir, toolchains: [ts], claudeBinary });
const PLAN = { token: 'sk-ant-oat01-REALREALREALREALREAL0123456789', dummy: 'sk-ant-oat01-dummy-abc123def' };

/** A series under the fake home (outside-path is high only under the home, a partner's dir, or the harness's). */
function series() {
  const root = path.join(mkdtempSync(path.join(home, 'testsToDelete-series-')), 's');
  mkdirSync(root, { recursive: true });
  return root;
}
function session(root, rid) {
  const layout = layoutPaths(root, rid);
  lay(layout);
  mkdirSync(layout.work, { recursive: true });
  return layout;
}
const toolUse = (command, i) => ({ type: 'assistant', timestamp: '2026-10-30T10:00:00Z', message: { id: `msg-x${i}`, content: [{ type: 'tool_use', id: `tu-x${i}`, name: 'Bash', input: { command } }] } });
const run = (layout, commands, over = {}) => {
  commands.forEach((c, i) => appendFileSync(layout.files.stream, `${JSON.stringify(toolUse(c, i))}\n`));
  const policy = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(policy.ok, true, policy.reason);
  const rec = aliasRecord({ events: loadSessionDir(layout.dir).value.events });
  const r = collectSession({ layout, plan: PLAN, launched: null, aliasBaseline: { aliases: rec.value.aliases, internal: rec.value.internal }, ...policy.value, ...over });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};

test('The_pilot_commands_that_name_the_box_toolchain_and_its_own_config_dir_are_no_contamination', () => {
  const root = series();
  const layout = session(root, '9804fc');
  const commands = pilotCommands({ session: layout.dir, ts });
  const r = run(layout, commands, { box: BOX });
  assert.equal(r.outcome.detail.high, undefined, `no high hit; got ${JSON.stringify(r.outcome.detail.contamination)}`);
  assert.equal(r.outcome.class, 'completed');
  assert.equal(r.outcome.data, true);
});

test('The_pilot_commands_given_no_toolchain_flag_the_two_toolchain_reaches_and_only_the_sessions_own_dirs_are_exempt', () => {
  const layout = session(series(), '9804fc');
  const r = run(layout, pilotCommands({ session: layout.dir, ts }));
  assert.equal(r.outcome.class, 'void: contaminated');
  assert.deepEqual(r.outcome.detail.contamination.high.map(h => h.excerpt).sort(), [path.join(ts, 'node_modules'), path.join(ts, 'node_modules', '.bin', 'tsc')], 'the config dir of the session itself is not among them');
});

test('A_listing_of_a_directory_that_only_holds_the_boxs_binds_is_no_reach_but_a_sibling_below_it_is', () => {
  const root = series();
  const holders = [toolchains, path.join(home, 'rsd'), path.join(home, 'rsd', 'minigit'), `${home}/`, path.dirname(root), root];
  const ok = run(session(root, 'ab0000'), holders.map(p => `ls ${p}`), { box: BOX });
  assert.equal(ok.outcome.class, 'completed', JSON.stringify(ok.outcome.detail));
  const siblings = [path.join(home, 'rsd', 'minigit', 's-other-series'), path.join(home, '.config'), path.join(toolchains, 'cache-node-0123456789ab')];
  for (const [i, sibling] of siblings.entries()) assert.equal(run(session(root, `ab1${i}00`), [`ls ${sibling}`], { box: BOX }).outcome.class, 'void: contaminated', sibling);
});

test('A_reach_for_a_partner_the_operators_config_another_series_or_the_harness_stays_high_beside_the_box_paths', () => {
  const root = series();
  const layout = session(root, '9804fc');
  const partner = session(root, 'aaaaaa');
  const other = session(series(), 'bbbbbb');
  const reaches = {
    'a partner dir': `ls ${partner.work}`,
    'the operators config': `cat ${path.join(home, '.config', 'minigit-bench', 'token')}`,
    'another series': `ls ${other.work}`,
    'a toolchain the box does not bind': `ls ${path.join(toolchains, 'typescript-6.0.0', 'node_modules')}`,
    'the harness dir of its own session': `cat ${path.join(layout.harness, 'settings.json')}`,
    'the net dir of its own session': `ls ${layout.net}`,
  };
  for (const [what, command] of Object.entries(reaches)) {
    const one = run(session(root, `c${Object.keys(reaches).indexOf(what)}00000`), [command], { box: BOX, partnerDirs: [partner.dir] });
    assert.equal(one.outcome.class, 'void: contaminated', what);
    assert.equal(one.outcome.detail.high, 1, what);
  }
});

test('The_high_hits_themselves_are_kept_in_the_outcome_detail_with_rule_source_where_and_a_masked_excerpt', () => {
  const root = series();
  const partner = session(root, 'aaaaaa');
  const layout = session(root, '9804fc');
  const token = `ghp_${'a1B2'.repeat(9)}`;
  const r = run(layout, [`ls ${partner.work} && git fetch origin run/zz9999/main ${token}`], { box: BOX, partnerDirs: [partner.dir] });
  const kept = r.outcome.detail.contamination.high;
  assert.deepEqual(kept.map(h => [h.rule, h.source, h.where]).sort(), [['foreign-run-ref', 'call', 'main:Bash'], ['outside-path', 'call', 'main:Bash']]);
  assert.equal(r.outcome.detail.high, kept.length, 'the count is the list');
  assert.ok(kept.every(h => typeof h.excerpt === 'string' && h.excerpt.length > 0 && h.excerpt.length <= 120));
  assert.ok(!JSON.stringify(kept).includes(token), 'a credential in an excerpt is masked');
});

test('The_paths_the_scan_allows_are_the_binds_the_box_argv_really_builds_less_the_harness_and_net_dirs', () => {
  const layout = layoutPaths('/r', 'a');
  const opts = { layout, nodeDir: '/n/node', toolchains: ['/t/ts', '/t/dotnet'], claudeBinary: '/t/claude', extraRo: ['/x/ro'], extraRw: ['/x/rw'] };
  const argv = agentBoxPrefix({ ...opts, arm: 'bare', operatorHome: '/home/op', exists: () => false });
  const hostsOf = flag => argv.flatMap((a, i) => (a === flag ? [argv[i + 1]] : []));
  const binds = [...hostsOf('--ro-bind'), ...hostsOf('--bind')].filter(p => p !== '/'); // the root bind is the whole host: not a path the box adds
  assert.deepEqual([...boxReachable(opts)].sort(), binds.filter(p => ![layout.harness, layout.net].includes(p)).sort(), 'one list, derived: a bind added to the box is allowed here, or the test names it');
  assert.ok(!boxReachable(opts).includes(layout.harness) && !boxReachable(opts).includes(layout.net), 'the harness and net dirs stay protected');
  assert.ok(boxSkeleton(opts).includes('/r/a/box') && boxSkeleton(opts).includes('/r/a') && boxSkeleton(opts).includes('/t'), 'the parents of the binds only hold them');
  assert.ok(!boxSkeleton(opts).includes('/'), 'the root is the host root, not a skeleton');
});
