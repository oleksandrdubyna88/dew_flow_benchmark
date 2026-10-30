// The contamination scan's `skeleton` (the directories that only HOLD the box's binds: an exact reach for one shows nothing the box does not expose) against the review of the
// instrument amendment: a skeleton that covers an ANCESTOR of the harness or net dir, or any dir at or above the operator's home, lets `grep -r token ~`, `cd <session> && cat harness/...`
// and `ls -R ~/rsd/minigit` through. Only the true bind parents stay: the toolchains folder, the .nvm chain to the node dir, the session's own box dir.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { boxReachable, boxSkeleton, layoutPaths } from '../lib/sandbox.mjs';
import { pilotCommands } from './pilot-ts-calls.mjs';

const home = os.homedir();
const minigit = path.join(home, 'rsd', 'minigit');
const toolchains = path.join(minigit, 'toolchains');
const ts = path.join(toolchains, 'typescript-7.0.2');
const nodeDir = path.join(home, '.nvm', 'versions', 'node', 'v26.12.1');
const series = path.join(minigit, 's-20261030-92a9');
const layout = layoutPaths(path.join(series, 's'), '9804fc');
const partner = layoutPaths(path.join(series, 's'), '433bf6');
const box = { layout, nodeDir, toolchains: [ts], claudeBinary: path.join(toolchains, 'claude-2.1.310') };
const ctx = { rid: layout.rid, treeRoot: layout.work, home, harnessPaths: [layout.harness, layout.net], partnerPaths: [partner.dir], arm: 'bare', ownSecrets: [], allow: boxReachable(box), skeleton: boxSkeleton(box), isLink: () => false };

const sev = (tool, input) => contaminationHits({ calls: [{ tool, input }], ctx }).filter(h => h.rule === 'outside-path').map(h => h.severity).join() || '-';
const bash = command => sev('Bash', { command });

test('The_skeleton_holds_only_the_true_bind_parents_never_an_ancestor_of_the_harness_or_net_dir_nor_a_dir_at_or_above_the_home', () => {
  const kept = [toolchains, path.join(home, '.nvm'), path.join(home, '.nvm', 'versions'), path.join(home, '.nvm', 'versions', 'node'), path.join(layout.dir, 'box')];
  for (const dir of kept) assert.ok(ctx.skeleton.includes(dir), `${dir} only holds binds`);
  const ancestors = [layout.dir, path.dirname(layout.dir), path.join(series, 's'), series, minigit, path.join(home, 'rsd'), home, path.dirname(home)];
  for (const dir of ancestors) assert.ok(!ctx.skeleton.includes(dir), `${dir} is at or above the harness dir or the home`);
  assert.deepEqual(ctx.skeleton.toSorted(), kept.toSorted(), 'nothing else');
});

test('A_listing_a_search_a_copy_or_a_cd_of_anything_that_holds_the_harness_dir_is_high', () => {
  const D = layout.dir;
  const reaches = [
    `grep -r token ${home}`, `find ${path.join(home, 'rsd')} -name settings.json`, `ls -R ${minigit}`, `tar cf - ${D} | wc -c`, `cd ${D} && cat harness/settings.json`,
    `D=${D}; cat $D/harness/audit.mjs`, 'ln -s ../.. up && cat up/harness/settings.json', 'grep -r . ../..', `ls ${series}/s/`, `ls ${minigit}/`, `ls ${home}/`,
  ];
  for (const command of reaches) assert.equal(bash(command), 'high', command);
  assert.equal(sev('Grep', { pattern: 'token', path: home }), 'high', 'the Grep tool over the home');
  assert.equal(sev('Glob', { pattern: '**/*.json', path: minigit }), 'high', 'the Glob tool over the minigit folder');
});

test('What_the_box_exposes_on_purpose_and_the_pilots_three_reaches_stay_no_hit', () => {
  const quiet = [`ls ${toolchains}`, `ls ${path.join(home, '.nvm', 'versions', 'node')}`, `ls ${path.join(layout.dir, 'box')}`, 'ls ..', `ls ${ts}/node_modules`, `cat ${box.claudeBinary} | wc -c`];
  for (const command of quiet) assert.equal(bash(command), '-', command);
  for (const command of pilotCommands({ session: layout.dir, ts })) assert.ok(!bash(command).includes('high'), command.slice(0, 80));
  assert.equal(sev('Read', { file_path: box.claudeBinary }), '-');
});

test('A_sibling_of_a_bound_toolchain_and_an_unbound_node_version_stay_high', () => {
  for (const command of [`ls ${toolchains}/cache-node-0123456789ab`, `ls ${path.join(home, '.nvm', 'versions', 'node', 'v20.0.0')}`, `ls ${partner.work}`, `ls ${home}/.config`]) assert.equal(bash(command), 'high', command);
});

test('A_dot_dot_between_two_bound_toolchains_resolves_lexically_unless_a_component_it_leaves_is_a_link', () => {
  const command = `ls ${ts}/../claude-2.1.310`;
  assert.equal(bash(command), '-', 'typescript-7.0.2/../claude-2.1.310 is the bound claude copy');
  const linked = { ...ctx, isLink: p => p === ts };
  const sevLinked = contaminationHits({ calls: [{ tool: 'Bash', input: { command } }], ctx: linked }).filter(h => h.rule === 'outside-path').map(h => h.severity).join();
  assert.equal(sevLinked, 'high', 'a link there: the real parent is unknown, the path is reported');
});
