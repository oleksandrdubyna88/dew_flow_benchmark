// The guard before a paid launch of Stage 1 (F3.S1 (a), PR 4, scenarios 42 to 50 of the stories document): the frozen record (the consumers' table, the twelve references), the task repository at the record (the spec blobs at the tag, the
// workspace commit W, the hash list, the twelve workspaces by tree hash), every identity the pre-registration freezes, memory certification before the launch and the agent box's environment. Fakes, invented task repositories
// (`quick-freeze-fixture.mjs`) and temporary folders only: no real record, held-out folder, key or series is read here.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { allowFor } from '../../lang-bench/lib/exec-layer/env.mjs';
import { dummyToken, layoutPaths, agentEnv } from '../../minigit-bench/lib/sandbox.mjs';
import { entryOfSeal } from '../lib/heldout-registry.mjs';
import { HALT } from '../../lang-bench/lib/exec-layer/certify.mjs';
import { RECORD_PATHS, stageGuard, workspacesProblems } from '../lib/stage-guard.mjs';
import { agentEnvVerdict, memoryCertification } from '../lib/stage-certify.mjs';
import { IDENTITY_NAMES, identityProblems, liveIdentities } from '../lib/stage-identity.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { REPO, underHome } from '../lib/paths.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { freezeContext } from '../lib/workspaces-build.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';
import { FORMAT, HOME, TAG, changeAndCommit, committedCheckout, folder, freezeTaskList, git, pinsFor, sha256, specOf } from './quick-freeze-fixture.mjs';

const hex = (n, c) => c.repeat(n);
const HASHES_TEXT = 'invented hash list\n';
const REF = (task, stack) => sha256(`reference ${task} ${stack}`);
const put = (file, value) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, typeof value === 'string' ? value : `${JSON.stringify(value, null, 2)}\n`); };
const leak = commit => ({ commit, exit: 0, date: '2026-11-20' });

/** The three record files, the packages file and the hash list of an invented bench repository root, consistent with the invented task repository `c` (a committed checkout: the tag at C and W on top). */
function writeRoot(root, c, over = {}) {
  const tasks = freezeTaskList();
  const specStates = {
    specs: Object.fromEntries(tasks.map(task => [task.id, { path: task.spec.path, sha256: sha256(specOf(task.id)), state: 'frozen', gate: `inputs/gates/spec-${task.id}.md`, rIds: ['R1'], commitSha: c.repo.commit, tagObjectSha: git(c.dir, 'rev-parse', TAG) }])),
    freeze: {
      tag: TAG, tagObjectSha: git(c.dir, 'rev-parse', TAG), commitSha: c.repo.commit, workspaceCommit: c.head, formatSha256: sha256(FORMAT), hashesLangSha256: sha256(HASHES_TEXT),
      leakcheck: { freezeCommit: leak(c.repo.commit), scratchWorkspace: leak(hex(40, '9')), workspaceCommit: leak(c.head) }, state: 'accepted', provisionalFrom: '2026-11-20', acceptedOn: '2026-11-21', superseded: [], workspacesCheck: { commit: c.head, exit: 0, date: '2026-11-21' },
    },
  };
  const heldoutFrozen = { tasks: Object.fromEntries(FREEZE_TASKS.map(id => [id, entryOfSeal({ archive: `heldout-${id}/suite.enc`, archiveSha256: hex(64, '1'), treeSha256: hex(64, '2'), caseCount: 19, trapCount: 7, manifestSha256: hex(64, '3'), keySha256: hex(64, '4'), sealedOn: '2026-11-12' })])) };
  const memoryLimit = { bytes: 536870912, mib: 512, decidedOn: '2026-11-07', by: 'invented', note: 'invented', registration: { date: '2026-11-18', references: FREEZE_TASKS.flatMap(task => ['csharp', 'node'].map(stack => ({ task, stack, treeSha256: REF(task, stack) }))) } };
  const packages = Object.fromEntries(FREEZE_TASKS.map(id => [id, { refsTreeSha256: { csharp: REF(id, 'csharp'), node: REF(id, 'node') } }]));
  const files = { specStates, heldoutFrozen, memoryLimit, ...over };
  for (const [name, rel] of Object.entries(RECORD_PATHS)) put(path.join(root, rel), files[name]);
  put(path.join(root, 'spikes/lang-explore/inputs/packages.json'), over.packages ?? packages);
  put(path.join(root, 'spikes/lang-bench/inputs/hashes-lang.txt'), over.hashes ?? HASHES_TEXT);
  return { specStates, heldoutFrozen, memoryLimit, packages };
}

const LIVE = Object.fromEntries(IDENTITY_NAMES.map((name, i) => [name, { value: sha256(`live ${name} ${i}`) }]));
const MANIFEST = { identities: Object.fromEntries(Object.entries(LIVE).map(([name, v]) => [name, v.value])) };
const FAKES = { manifest: MANIFEST, live: async () => LIVE, harnessAccepted: [], preregGuard: async () => [] };

/** A whole world: an invented task repository with W on top, a bench root over it, and the guard's arguments. */
async function world(t, over = {}) {
  const c = await committedCheckout(t);
  const repo = folder(t, 'benchroot');
  const files = writeRoot(repo, c, over);
  return { c, repo, files, args: { repo, series: folder(t, 'series'), taskRepo: c.dir, paid: false, requireSeed: false, deps: { ...FAKES, pinnedTaskSha: c.head } } };
}
const problemsOf = async args => { const r = await stageGuard(args); return r.ok ? [] : r.problems; };
const mutate = (repo, name, fn) => { const file = path.join(repo, RECORD_PATHS[name]); const json = JSON.parse(readFileSync(file, 'utf8')); fn(json); put(file, json); };

test('The_guard_refuses_a_series_while_the_frozen_record_lacks_a_field_and_names_the_field', async t => {
  const w = await world(t);
  const control = await stageGuard(w.args);
  assert.equal(control.ok, true, control.reason);
  mutate(w.repo, 'specStates', json => { delete json.freeze.formatSha256; });
  mutate(w.repo, 'heldoutFrozen', json => { delete json.tasks['05'].manifestSha256; });
  const refused = await stageGuard(w.args);
  assert.equal(refused.ok, false, 'the guard refuses');
  assert.ok(refused.problems.some(p => /specStates: freeze\.formatSha256 is absent/.test(p)), `it names the field: ${refused.problems.join(' | ')}`);
  assert.ok(refused.problems.some(p => /heldoutFrozen: tasks\.05\.manifestSha256 is absent/.test(p)), 'every missing field is named, not the first only');
  assert.ok(refused.reason.includes('formatSha256') && refused.reason.includes('manifestSha256'), 'and the reason carries them');
  put(path.join(w.repo, RECORD_PATHS.specStates), '{ not json');
  const unreadable = await stageGuard(w.args);
  assert.ok(unreadable.problems.some(p => p.includes(RECORD_PATHS.specStates) && /cannot be read/.test(p)), `a record that cannot be read is named: ${unreadable.problems.join(' | ')}`);
});

test('The_guard_refuses_while_the_memory_value_is_not_registered', async t => {
  const w = await world(t);
  mutate(w.repo, 'memoryLimit', json => { delete json.registration; });
  const refused = await stageGuard(w.args);
  assert.equal(refused.ok, false);
  assert.ok(refused.problems.some(p => /memoryLimit: registration\.date is absent/.test(p)), `the missing registration is named: ${refused.problems.join(' | ')}`);
  assert.ok(refused.problems.some(p => /memoryLimit: registration\.references is absent/.test(p)));
});

test('A_reference_identity_that_is_not_one_of_the_twelve_registered_ones_is_named', async t => {
  const w = await world(t);
  assert.equal((await stageGuard(w.args)).ok, true, 'the control: the twelve references are the packages file\'s');
  put(path.join(w.repo, 'spikes/lang-explore/inputs/packages.json'), { ...w.files.packages, '05': { refsTreeSha256: { csharp: REF('05', 'csharp'), node: hex(64, 'f') } } });
  const moved = await problemsOf(w.args);
  assert.ok(moved.some(p => /task 05 node/.test(p) && p.includes(REF('05', 'node').slice(0, 12)) && p.includes(hex(12, 'f'))), `the reference that is not the registered one is named with both short hashes: ${moved.join(' | ')}`);
  assert.equal(moved.filter(p => /reference/.test(p)).length, 1, 'only that one');
  put(path.join(w.repo, 'spikes/lang-explore/inputs/packages.json'), w.files.packages);
  mutate(w.repo, 'memoryLimit', json => { json.registration.references[3] = { task: '99', stack: 'node', treeSha256: hex(64, 'a') }; });
  const foreign = await problemsOf(w.args);
  assert.ok(foreign.some(p => /task 99 node/.test(p) && /not one of the twelve/.test(p)), `a reference of another task is named: ${foreign.join(' | ')}`);
});

test('A_spec_blob_that_differs_from_the_tags_refuses_the_launch_and_a_difference_outside_the_spec_does_not', async t => {
  const w = await world(t);
  assert.equal((await stageGuard(w.args)).ok, true);
  mutate(w.repo, 'specStates', json => { json.specs['06'].sha256 = hex(64, 'b'); });
  const wrong = await problemsOf(w.args);
  const task06 = freezeTaskList().find(task => task.id === '06');
  assert.ok(wrong.some(p => p.includes('task 06') && p.includes(task06.spec.path) && p.includes(hex(64, 'b'))), `the spec whose pinned hash is not the tag's blob is named with its path and both hashes: ${wrong.join(' | ')}`);
  assert.equal(wrong.filter(p => /task \d\d/.test(p)).length, 1, 'only that spec');
  mutate(w.repo, 'specStates', json => { json.specs['06'].sha256 = sha256(specOf('06')); });
  const moved = changeAndCommit(w.c.dir, 'node/04-rule-api/STATEMENT.md', bytes => Buffer.concat([bytes, Buffer.from('x')]));
  mutate(w.repo, 'specStates', json => { json.freeze.workspaceCommit = moved; json.freeze.workspacesCheck.commit = moved; json.freeze.leakcheck.workspaceCommit.commit = moved; });
  assert.equal((await stageGuard({ ...w.args, deps: { ...w.args.deps, pinnedTaskSha: moved } })).ok, true, 'a difference in a workspace file (not a spec) is the permitted change: the guard goes on');
  const specChange = changeAndCommit(w.c.dir, task06.spec.path, bytes => Buffer.concat([bytes, Buffer.from('a changed spec')]));
  mutate(w.repo, 'specStates', json => { json.freeze.workspaceCommit = specChange; json.freeze.workspacesCheck.commit = specChange; json.freeze.leakcheck.workspaceCommit.commit = specChange; });
  const changed = await problemsOf({ ...w.args, deps: { ...w.args.deps, pinnedTaskSha: specChange } });
  assert.ok(changed.some(p => p.includes(task06.spec.path)), `a spec changed after the tag is refused by the workspace commit rule: ${changed.join(' | ')}`);
});

test('A_workspace_commit_that_is_not_the_records_refuses_the_launch', async t => {
  const w = await world(t);
  const other = changeAndCommit(w.c.dir, 'node/04-rule-api/STATEMENT.md', bytes => Buffer.concat([bytes, Buffer.from('y')]));
  const behind = await problemsOf({ ...w.args, deps: { ...w.args.deps, pinnedTaskSha: w.c.head } });
  assert.ok(behind.some(p => p.includes(other.slice(0, 12)) && p.includes(w.c.head.slice(0, 12)) && /workspace commit/.test(p)), `the checkout is at another commit than the record's W: ${behind.join(' | ')}`);
  const pinned = await problemsOf({ ...w.args, deps: { ...w.args.deps, pinnedTaskSha: hex(40, '7') } });
  assert.ok(pinned.some(p => /PINNED_TASK_SHA/.test(p) && p.includes(hex(12, '7')) && p.includes(other.slice(0, 12)) === false), `a pinned commit that is not the record's W is named: ${pinned.join(' | ')}`);
  const root = w.repo;
  put(path.join(root, 'spikes/lang-bench/inputs/hashes-lang.txt'), 'another hash list\n');
  const hashes = await problemsOf({ ...w.args, deps: { ...w.args.deps, pinnedTaskSha: other } });
  assert.ok(hashes.some(p => /hashes-lang\.txt/.test(p) && p.includes(sha256(HASHES_TEXT).slice(0, 12))), `the hash list that is not the record's is named: ${hashes.join(' | ')}`);
  const none = await problemsOf({ ...w.args, taskRepo: undefined, deps: { ...w.args.deps, env: {} } });
  assert.ok(none.some(p => /LANG_V2_TASK_REPO/.test(p) && /--task-repo/.test(p)), `no task repository is named, with both ways to give one: ${none.join(' | ')}`);
  const viaEnv = await stageGuard({ ...w.args, taskRepo: undefined, deps: { ...w.args.deps, pinnedTaskSha: other, env: { LANG_V2_TASK_REPO: w.c.dir } } });
  assert.equal(viaEnv.problems?.some(p => /LANG_V2_TASK_REPO/.test(p)) ?? false, false, 'the variable names the checkout when no flag is given');
});

test('The_workspaces_a_session_is_given_equal_the_published_ones_by_tree_hash_at_stage_init_and_one_changed_byte_is_named', async t => {
  const c = await committedCheckout(t);
  const ctx = freezeContext({ home: HOME, exploreFn: () => ok(pinsFor()) }).value;
  assert.deepEqual(await workspacesProblems({ taskRepo: c.dir, ctx, workspaceCommit: c.head }), [], 'the control: the twelve published workspaces are what the spec and FORMAT of HEAD give');
  const planted = changeAndCommit(c.dir, 'node/06-blog-api/STATEMENT.md', bytes => { const b = Buffer.from(bytes); b[b.length - 3] ^= 1; return b; });
  const found = await workspacesProblems({ taskRepo: c.dir, ctx, workspaceCommit: planted });
  assert.ok(found.some(p => p.includes('node/06-blog-api') && /tree hash differs/.test(p)), `the workspace whose tree hash differs is named: ${found.join(' | ')}`);
  assert.ok(found.some(p => p.includes('node/06-blog-api/STATEMENT.md') && /differs at byte/.test(p)), 'and the file and the byte');
  const atOther = await workspacesProblems({ taskRepo: c.dir, ctx, workspaceCommit: c.head });
  assert.ok(atOther.some(p => /not the record's workspace commit/.test(p) && p.includes(planted.slice(0, 12)) && p.includes(c.head.slice(0, 12))), `HEAD that is not W is named: ${atOther.join(' | ')}`);
});

// ---- 48: every identity ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The identities the pre-registration freezes, written out here so that the list cannot shrink in silence (the brief's list, F3.S1 (a) and section 9). */
const EXPECTED_NAMES = [
  'harness', 'runnerManifest', 'execLayerTree', 'memoryPolicy', 'pidsMax', 'cliSha256', 'toolchains', 'snapshot.csharp', 'snapshot.node', 'formatSha256',
  ...FREEZE_TASKS.map(id => `spec.${id}`), ...FREEZE_TASKS.map(id => `heldout.${id}`),
  'memoryValue', 'contract', 'templates', 'prompts', 'statement', 'prices', 'policy', 'caps', 'locale', 'seriesDefaults', ...FREEZE_TASKS.map(id => `trapAid.${id}`), 'pilotRules', 'adjudicationRules', 'host',
];
const live = over => ({ ...LIVE, ...over });

test('Each_live_identity_that_differs_from_the_manifest_is_named_with_both_short_hashes', () => {
  assert.deepEqual([...IDENTITY_NAMES], EXPECTED_NAMES, 'the list of identities is the written-out one: removing one from the guard fails here');
  assert.deepEqual(identityProblems(MANIFEST, LIVE), [], 'the control: a manifest equal to the live identities has no problem');
  for (const name of EXPECTED_NAMES) {
    const changed = sha256(`another ${name}`);
    const problems = identityProblems(MANIFEST, live({ [name]: { value: changed } }));
    assert.equal(problems.length, 1, `${name}: exactly one problem, got ${problems.join(' | ')}`);
    assert.ok(problems[0].startsWith(`${name} `) && problems[0].includes(MANIFEST.identities[name].slice(0, 12)) && problems[0].includes(changed.slice(0, 12)), `${name}: named with both short hashes: ${problems[0]}`);
  }
  const { host: _h, ...withoutHost } = MANIFEST.identities;
  assert.ok(identityProblems({ identities: withoutHost }, LIVE).some(p => p.startsWith('host ') && /manifest/.test(p)), 'an identity the manifest lacks is named');
  assert.ok(identityProblems(MANIFEST, live({ host: { unknown: 'no kernel read' } })).some(p => p.startsWith('host ') && p.includes('no kernel read')), 'an identity that cannot be read is named with the reason');
  assert.ok(identityProblems({ identities: { ...MANIFEST.identities, 'a.new.one': sha256('x') } }, LIVE).some(p => p.startsWith('a.new.one ') && /not in the guard's list/.test(p)), 'an identity of the manifest that the guard does not know is named: the manifest cannot grow what the guard never reads');
  assert.ok(identityProblems(null, LIVE).length === 1 && /manifest/.test(identityProblems(null, LIVE)[0]), 'no manifest is one sentence');
});

test('Only_the_harness_identity_is_accepted_by_a_dated_line_and_never_another_identity', () => {
  const moved = sha256('the harness moved');
  const changed = live({ harness: { value: moved } });
  assert.equal(identityProblems(MANIFEST, changed).length, 1, 'without a line the harness identity differs');
  assert.deepEqual(identityProblems(MANIFEST, changed, { harnessAccepted: [moved.slice(0, 12)] }), [], 'a dated instrument line naming the live harness identity accepts it');
  assert.equal(identityProblems(MANIFEST, changed, { harnessAccepted: [hex(12, 'e')] }).length, 1, 'a line naming another identity accepts nothing');
  const other = live({ runnerManifest: { value: moved } });
  assert.equal(identityProblems(MANIFEST, other, { harnessAccepted: [moved.slice(0, 12)] }).length, 1, 'no other identity is ever accepted, whatever the line names');
});

// ---- 49, 50: memory certification and the agent box's environment ---------------------------------------------------------------------------------------------------------------------------------------------------------------

const UNREADABLE = { launched: true, certified: false, reason: 'prog/memory.peak cannot be read after the exit of an empty program' };

test('A_host_that_cannot_certify_memory_refuses_every_paid_launch_before_anything_is_planned_and_a_rehearsal_goes_on_with_its_flag', async () => {
  for (const kind of ['data', 'pilot', 'aa']) {
    let probes = 0;
    const refused = await memoryCertification({ kind, probe: async () => { probes += 1; return UNREADABLE; } });
    assert.equal(refused.ok, false, `${kind}: refused`);
    assert.equal(refused.halt, HALT, `${kind}: the named halt of the layer, ${HALT}`);
    assert.equal(refused.exit, 3, `${kind}: exit 3, resumable on a certifying host`);
    assert.ok(refused.reason.includes(UNREADABLE.reason), `${kind}: the probe's own reason is in the sentence`);
    assert.equal(probes, 1, `${kind}: the probe ran once`);
  }
  const rehearsal = await memoryCertification({ kind: 'rehearsal', probe: async () => UNREADABLE });
  assert.equal(rehearsal.ok, true, 'a rehearsal goes on');
  assert.deepEqual(rehearsal.value.flags, ['memory bound not certified'], 'and carries the flag');
  const good = await memoryCertification({ kind: 'data', probe: async () => ({ launched: true, certified: true, peakBytes: 2_500_000 }) });
  assert.equal(good.ok, true, 'a host that certifies goes on');
  assert.deepEqual(good.value, { flags: [], peakBytes: 2_500_000 });
  const noLaunch = { launched: false, certified: false, reason: 'the empty program did not run through the prefix: exit 1' };
  const stopped = await memoryCertification({ kind: 'rehearsal', probe: async () => noLaunch });
  assert.equal(stopped.ok, false, 'a layer that cannot launch the empty program stops even a rehearsal');
  assert.equal(stopped.exit, 3);
  const unknownKind = await memoryCertification({ kind: 'quick', probe: async () => UNREADABLE });
  assert.equal(unknownKind.ok, false, 'an unknown kind is refused, naming the legal ones');
  assert.match(unknownKind.reason, /data, pilot, aa, rehearsal/);
});

const agentLayout = layoutPaths('/tmp/stage-guard-fixture', 'abc123');
const agentBox = extra => agentEnv({ layout: agentLayout, dummyToken: dummyToken('x'), cellId: 'cell', nodeDir: '/n', locale: 'de_DE.UTF-8', extra: { DOTNET_gcServer: '0', NODE_OPTIONS: '', ...extra } });

test('A_heap_key_in_the_agent_boxs_environment_refuses_the_launch', () => {
  const control = agentEnvVerdict({ env: agentBox({}) });
  assert.equal(control.ok, true, control.reason);
  assert.deepEqual(control.value.keys.agent, Object.keys(agentBox({})).sort(), 'the record lists the keys of the agent box');
  assert.deepEqual(control.value.keys.scoring, { csharp: allowFor('csharp'), node: allowFor('node') }, 'and of the scoring boxes, as the layer\'s allow-list names them');
  const cases = [
    ['a .NET heap key', { DOTNET_GCHeapHardLimit: '0x1c000000' }, /DOTNET_GCHeapHardLimit/],
    ['the same key in another case', { dotnet_gcheaphardlimit: '1c000000' }, /dotnet_gcheaphardlimit/],
    ['a Node old-space flag', { NODE_OPTIONS: '--max-old-space-size=460' }, /NODE_OPTIONS/],
  ];
  for (const [what, extra, named] of cases) {
    const refused = agentEnvVerdict({ env: { ...agentBox({}), ...extra } });
    assert.equal(refused.ok, false, `${what}: refused`);
    assert.match(refused.problems.join(' | '), named, `${what}: the key is named`);
  }
  const clash = agentEnvVerdict({ env: { ...agentBox({}), LANG: 'C.UTF-8' } });
  assert.equal(clash.ok, false, 'a base key with another value than the registered one is a clash');
  assert.ok(clash.problems.some(p => p.includes('LANG') && p.includes('C.UTF-8') && p.includes('de_DE.UTF-8')), `the clash names the key and both values: ${clash.problems.join(' | ')}`);
  const { LC_ALL: _l, ...noCulture } = agentBox({});
  assert.ok(agentEnvVerdict({ env: noCulture }).problems.some(p => p.includes('LC_ALL') && /absent/.test(p)), 'a base key that is absent is named');
  assert.ok(agentEnvVerdict({ env: { ...agentBox({}), DOTNET_gcServer: '1' } }).problems.some(p => p.includes('DOTNET_gcServer')), 'the server GC switch is a base key too');
});

// ---- the readers: each identity from the place that owns it -----------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_live_identities_are_read_from_the_places_that_own_them_and_one_that_cannot_be_read_is_named_alone', async t => {
  const repo = folder(t, 'readroot');
  const home = folder(t, 'readhome');
  const explore = loadExplore().value;
  const cliBytes = 'an invented CLI copy';
  put(underHome(explore.cli.pathParts, home), cliBytes);
  const task04 = resolveTask(explore, '04').value;
  const notes = 'invented notes';
  put(taskPaths(task04, home).notesFile, notes);
  const archive = 'invented archive bytes';
  put(path.join(taskPaths(task04, home).heldoutDir, 'suite.enc'), archive);
  const entry = entryOfSeal({ archive: 'heldout-04/suite.enc', archiveSha256: hex(64, '1'), treeSha256: hex(64, '2'), caseCount: 19, trapCount: 7, manifestSha256: hex(64, '3'), keySha256: hex(64, '4'), sealedOn: '2026-11-12' });
  put(path.join(repo, RECORD_PATHS.heldoutFrozen), { tasks: { '04': entry } });
  const prereg = 'spikes/lang-explore/inputs/prereg';
  put(path.join(repo, prereg, 'prices-2026-11-19.json'), 'prices');
  put(path.join(repo, prereg, 'crosscheck-policy.json'), { t1: 'stop', t2: 'otel-incomplete', t3: 'stop', t5: 'investigate' });
  const read = async over => liveIdentities({ repo, home, explore, record: {}, ...over });
  const now = await read({});
  assert.equal(now['cliSha256'].value, sha256(cliBytes), 'the CLI copy by its sha256');
  assert.equal(now['trapAid.04'].value, sha256(notes), 'the private trap aid by a hash only');
  assert.equal(now['heldout.04'].value, sha256(`archive:${sha256(archive)}|tree:${hex(64, '2')}|manifest:${hex(64, '3')}|cases:19|traps:7`), 'the archive on disk with the registry\'s tree, trap table and counts');
  assert.equal(now.prices.value, sha256('prices'));
  assert.equal(now.caps.value, `capUsd:${explore.caps.capUsd}`, 'the cap in dollars; the wall and the stall are amendable and no identity');
  assert.equal(now.pidsMax.value, '512', 'pids.max from the layer\'s scope-entry.sh');
  assert.equal(now.harness.unknown.includes('PR 5'), true, 'the harness identity has no default reader: PR 5 hands it in');
  assert.match(now['trapAid.05'].unknown, /ENOENT|no such file/i, 'an identity that cannot be read says why, for itself alone');
  assert.equal(now.formatSha256.unknown.includes('no task repository'), true);
  const looser = await read({});
  assert.equal(looser.policy.value, now.policy.value);
  put(path.join(repo, prereg, 'crosscheck-policy.json'), { t1: 'stop', t2: 'otel-incomplete', t3: 'investigate', t5: 'stop' });
  assert.equal((await read({})).policy.value, now.policy.value, 'the policy\'s free fields (t3, t5) are no part of its identity');
  put(path.join(repo, prereg, 'crosscheck-policy.json'), { t1: 'investigate', t2: 'otel-incomplete', t3: 'stop', t5: 'investigate' });
  assert.notEqual((await read({})).policy.value, now.policy.value, 'any other field is');
  put(path.join(repo, prereg, 'prices-2026-11-20.json'), 'more');
  assert.match((await read({})).prices.unknown, /2 price files/, 'two price files are no registered price');
  const withHarness = await read({ readers: { harness: () => 'abc' } });
  assert.equal(withHarness.harness.value, 'abc', 'PR 5\'s reader replaces the default');
  const real = await liveIdentities({ repo: REPO, home, explore, readers: {} });
  for (const name of ['runnerManifest', 'execLayerTree', 'memoryPolicy', 'pidsMax', 'toolchains', 'memoryValue', 'contract', 'templates', 'locale', 'seriesDefaults', 'host']) assert.equal(typeof real[name].value, 'string', `${name} reads from this repository: ${real[name].unknown}`);
});

test('The_guard_names_an_identity_that_differs_from_the_manifest_and_asks_the_paid_checks_only_for_a_paid_launch', async t => {
  const w = await world(t);
  const moved = { ...LIVE, contract: { value: sha256('the contract moved') } };
  const refused = await stageGuard({ ...w.args, deps: { ...w.args.deps, live: async () => moved } });
  assert.equal(refused.ok, false, 'a live identity that differs from the manifest refuses the guard');
  assert.ok(refused.problems.some(p => p.startsWith('contract differs')), refused.problems.join(' | '));
  const free = await stageGuard({ ...w.args, paid: false });
  assert.equal(free.ok, true, 'a free launch asks none of the paid checks');
  const unwired = await stageGuard({ ...w.args, paid: true, deps: { ...w.args.deps, preregGuard: undefined } });
  assert.equal(unwired.ok, false, 'a paid launch with no pre-registration guard handed in is refused');
  assert.ok(unwired.problems.some(p => /PR 5/.test(p)));
  const asked = [];
  const paid = await stageGuard({ ...w.args, paid: true, requireSeed: true, deps: { ...w.args.deps, preregGuard: async a => { asked.push(a.requireSeed); return ['the tag is not on the remote']; } } });
  assert.deepEqual(asked, [true], 'PR 5\'s guard is asked with the seed requirement');
  assert.ok(paid.problems.includes('the tag is not on the remote'), 'and its sentences are the guard\'s');
});
