// The contamination scan's `outside-path` rule against the paths the agent's box exposes on purpose (the Node pilot 9804fc, voided as contaminated for three of them): the pinned
// toolchain the box binds read-only and the session's own box dirs are no reach; a partner's dir, the operator's other files, another series and the harness's own dirs stay one.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import { appendFileSync, mkdirSync, mkdtempSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
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
const NO_TOOLCHAIN = Object.freeze({ nodeDir, toolchains: [], claudeBinary });
const PLAN = { token: 'sk-ant-oat01-REALREALREALREALREAL0123456789', dummy: 'sk-ant-oat01-dummy-abc123def' };

/** A series under the fake home, where the real ones live: `<home>/rsd/minigit/<series>/s` (outside-path is high only under the home, a partner's dir, or the harness's). */
function series() {
  mkdirSync(path.join(home, 'rsd', 'minigit'), { recursive: true });
  const root = path.join(mkdtempSync(path.join(home, 'rsd', 'minigit', 'testsToDelete-series-')), 's');
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
/** The session's own `argv.json` as the launcher writes it: the bwrap argv of the box it ran in (the allow-list is read from it, never from the plan or the context). */
const writeArgv = (layout, box) => writeFileSync(layout.files.argv, `${JSON.stringify({ bwrap: agentBoxPrefix({ layout, arm: 'bare', operatorHome: home, exists: () => false, ...box }), claude: [], envKeys: [] })}\n`);
const run = (layout, commands, { box = NO_TOOLCHAIN, argv = true, ...over } = {}) => {
  if (argv) writeArgv(layout, { ...box, layout });
  else writeFileSync(layout.files.argv, 'null\n');
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

test('A_listing_of_a_directory_that_only_holds_the_boxs_binds_is_no_reach_but_a_sibling_or_an_ancestor_of_the_harness_is', () => {
  const root = series();
  const own = session(root, 'ab0000');
  const holders = [toolchains, path.join(home, '.nvm', 'versions', 'node'), path.join(own.dir, 'box')];
  const ok = run(own, holders.map(p => `ls ${p}`), { box: BOX });
  assert.equal(ok.outcome.class, 'completed', JSON.stringify(ok.outcome.detail));
  const siblings = [path.join(home, 'rsd', 'minigit', 's-other-series'), path.join(home, '.config'), path.join(toolchains, 'cache-node-0123456789ab')];
  const ancestors = [path.join(home, 'rsd'), path.join(home, 'rsd', 'minigit'), `${home}/`, path.dirname(root), root];
  for (const [i, dir] of [...siblings, ...ancestors].entries()) assert.equal(run(session(root, `ab1${i}00`), [`ls ${dir}`], { box: BOX }).outcome.class, 'void: contaminated', dir);
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
  const layout = layoutPaths('/home/op/rsd/s', 'a');
  const opts = { layout, nodeDir: '/home/op/.nvm/n', toolchains: ['/home/op/t/ts', '/home/op/t/dotnet'], claudeBinary: '/home/op/t/claude', extraRo: ['/x/ro'], extraRw: ['/x/rw'], operatorHome: '/home/op' };
  const argv = agentBoxPrefix({ ...opts, arm: 'bare', exists: () => false });
  const hostsOf = flag => argv.flatMap((a, i) => (a === flag ? [argv[i + 1]] : []));
  const binds = [...hostsOf('--ro-bind'), ...hostsOf('--bind')].filter(p => p !== '/'); // the root bind is the whole host: not a path the box adds
  assert.deepEqual([...boxReachable(opts)].sort(), binds.filter(p => ![layout.harness, layout.net].includes(p)).sort(), 'one list, derived: a bind added to the box is allowed here, or the test names it');
  assert.ok(!boxReachable(opts).includes(layout.harness) && !boxReachable(opts).includes(layout.net), 'the harness and net dirs stay protected');
  const skeleton = boxSkeleton(opts);
  assert.deepEqual(skeleton.toSorted(), ['/home/op/.nvm', '/home/op/rsd/s/a/box', '/home/op/t'], 'the parents of the binds only hold them: not the session dir (the harness is below), not the home or above');
});

test('A_tilde_in_a_command_is_the_boxs_own_home_in_a_collected_session_and_an_explicit_operator_path_is_a_reach', () => {
  const root = series();
  const own = run(session(root, 'ad0000'), ['ls ~/.nuget/packages/', 'ls $HOME/.dotnet', 'cat ~/.npmrc', 'ls ~/.claude'], { box: BOX });
  assert.equal(own.outcome.class, 'completed', JSON.stringify(own.outcome.detail));
  assert.equal(run(session(root, 'ad1000'), [`cat ${path.join(home, '.ssh', 'id_rsa')}`], { box: BOX }).outcome.class, 'void: contaminated');
});

test('The_allow_list_is_the_binds_of_the_sessions_own_argv_json_never_the_plan_or_the_contexts_toolchain', () => {
  const root = series();
  const ts8 = path.join(toolchains, 'typescript-8.0.0');
  const bound = { nodeDir, toolchains: [ts8], claudeBinary };
  assert.equal(run(session(root, 'ae0000'), [`ls ${ts8}/node_modules`], { box: bound }).outcome.class, 'completed', 'ts-8.0.0 is what the session bound');
  assert.equal(run(session(root, 'ae1000'), [`ls ${ts}/node_modules`], { box: bound }).outcome.class, 'void: contaminated', 'ts-7.0.2 is not bound by this session, whatever the context pins');
  assert.equal(run(session(root, 'ae2000'), [`ls ${ts8}/node_modules`], { argv: false }).outcome.class, 'void: contaminated', 'a session with no readable argv.json allows nothing');
});

test('No_caller_of_collectSession_names_a_box_of_its_own', () => {
  for (const file of ['bare-settle.mjs', 'bare-records-io.mjs']) {
    const text = readFileSync(path.join(spikeDir, 'lib', file), 'utf8');
    const call = text.slice(text.indexOf('collectSession({'), text.indexOf('});', text.indexOf('collectSession({')));
    assert.ok(!/\bbox:/.test(call), `${file}: the box is read from the session's argv.json`);
  }
});

test('A_link_to_the_harness_made_in_a_call_or_held_by_the_work_tree_is_a_reach_in_a_collected_session', () => {
  const root = series();
  const made = run(session(root, 'af0000'), ['ln -s ../.. up && grep -r token up', 'rm up']);
  assert.equal(made.outcome.class, 'void: contaminated');
  assert.ok(['link-out', 'link-walk'].every(rule => made.outcome.detail.contamination.high.some(h => h.rule === rule)), JSON.stringify(made.outcome.detail.contamination.high));
  const held = session(root, 'af1000');
  symlinkSync('../..', path.join(held.work, 'up')); // a link the work tree holds at collection, made by no call of the stream
  const walked = run(held, ['grep -r token up']);
  assert.ok(walked.outcome.detail.contamination.high.some(h => h.rule === 'link-walk'), JSON.stringify(walked.outcome.detail));
  const inside = session(root, 'af2000');
  symlinkSync('src', path.join(inside.work, 'main'));
  assert.ok(!run(inside, ['grep -r token main', 'ln -s src/minigit.ts main.ts']).outcome.detail.contamination?.high?.some(h => h.rule.startsWith('link-')), 'a link inside the tree');
});
