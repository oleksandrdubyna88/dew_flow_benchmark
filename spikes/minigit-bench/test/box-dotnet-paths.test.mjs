// Every path that builds an agent box hides the other dotnet versions of a host that holds several (E2 amendment 2026-11-19, D-9; the plan gate's point 2). The argv is built over an invented
// two-SDK listing and never run: this proves each path SENDS the tmpfs. That the tmpfs hides an SDK is the effect test's (`agent-box-dotnet.test.mjs`, the real box). No folder is made here.
// The paths that start at a run context are in the files that hold the fake home: `bare-phase.test.mjs` (`v1Plan`) and `bare-wiring.test.mjs` (the real `buildRun`); the launcher's own wiring is in
// `session-launch.test.mjs`, the smoke's in `smoke-run.test.mjs`, the pilot's in `spikes/lang-explore/test/box-dotnet-pin.test.mjs`.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { bareSessionPlan } from '../lib/bare-plan.mjs';
import { scoreBoxArgv } from '../lib/bwrap.mjs';
import { scoringBoxArgv, scoringToolchains } from '../lib/score-box.mjs';
import { snapshotBoxArgv, snapshotBoxFields } from '../lib/cache-snapshot.mjs';
import { DISTRIBUTION_DOTNET, otherDotnetVersions } from '../lib/dotnet-hide.mjs';
import { probeBoxPrefix } from '../lib/preflight.mjs';
import { agentBoxPrefix, layoutPaths } from '../lib/sandbox.mjs';
import { defaultDeps } from '../lib/score-deps.mjs';
import { boxFor } from '../lib/score-steps.mjs';
import { sessionBoxArgv } from '../lib/session-launch.mjs';
import { DOTNET_PIN } from './box-helpers.mjs';
import { PIN, ROOT, SDK_11, fakeDirs, oneSdkHost, tmpfsOf, twoSdkHost } from './dotnet-fixture.mjs';

const HOME = '/home/nobody';
const layout = layoutPaths('/series/s', 'abc123');
const twoSdks = fakeDirs(twoSdkHost());
const oneSdk = fakeDirs(oneSdkHost());
const OTHER_SDK = `${ROOT}/sdk/${SDK_11.sdk}`;
const OTHER_RUNTIME = `${ROOT}/shared/Microsoft.NETCore.App/${SDK_11.runtime}`;
const base = { layout, arm: 'bare', claudeBinary: '/x/claude', nodeDir: '/n', operatorHome: HOME };
/** The pin of the invented host (`PIN`, the SDK its fixture calls the pinned one): these tests do not move when `inputs/stacks.json` does. */
const stacks = { pins: { dotnetSdk: PIN } };

/** What a path must send on the two-SDK host for a pin: the list of the one function, never a list typed here. */
const expectedFor = pin => otherDotnetVersions({ pin, ...twoSdks });
const expected = expectedFor(PIN);

/** The argv of a box on the distribution's dotnet hides the other SDK, another major's runtime, and all of the one list; never the resolver. */
function assertHidesOtherVersions(argv, path, pin = PIN) {
  const hidden = tmpfsOf(argv);
  assert.ok(hidden.includes(OTHER_SDK), `${path}: --tmpfs ${OTHER_SDK}`);
  assert.ok(hidden.includes(OTHER_RUNTIME), `${path}: --tmpfs ${OTHER_RUNTIME}`);
  assert.deepEqual(hidden.filter(p => p.startsWith(`${ROOT}/`)), [...expectedFor(pin)], `${path}: the whole list, in the function's order`);
  assert.equal(hidden.some(p => p.startsWith(`${ROOT}/host`)), false, `${path}: host/fxr is not hidden`);
  assert.equal(hidden.includes(ROOT), false, `${path}: the root itself stays: the box runs the distribution's dotnet`);
}

/** The argv of a box with a pinned SDK directory hides the whole distribution as before, and no version folder inside it. */
function assertHidesWholeDistribution(argv, path) {
  const hidden = tmpfsOf(argv);
  for (const root of DISTRIBUTION_DOTNET.filter(twoSdks.exists)) assert.ok(hidden.includes(root), `${path}: ${root} is hidden whole`);
  assert.deepEqual(hidden.filter(p => DISTRIBUTION_DOTNET.some(root => p.startsWith(`${root}/`))), [], `${path}: no version folder is listed under a root that is hidden whole`);
}

const neverListed = { exists: twoSdks.exists, listDir: dir => { throw new Error(`a box with a pinned SDK directory must not list ${dir}`); } };
const pinned = { dotnetDir: '/dn', hide: DISTRIBUTION_DOTNET };

test('The_box_on_the_distributions_dotnet_hides_every_other_version_as_an_empty_tmpfs_before_any_bind', () => {
  const argv = agentBoxPrefix({ ...base, dotnetPin: PIN, ...twoSdks });
  assertHidesOtherVersions(argv, 'agentBoxPrefix');
  assert.ok(expected.length > 0 && expected.every(p => twoSdks.exists(p)), 'the fixture holds what is hidden: this is not the empty-list path');
  const firstBind = argv.findIndex((a, i) => i > 2 && ['--bind', '--ro-bind'].includes(a)); // index 1 is the read-only root
  const lastTmpfs = argv.lastIndexOf('--tmpfs');
  assert.ok(lastTmpfs < firstBind, 'every tmpfs comes before the binds, so nothing bound is covered');
});

test('On_a_host_that_holds_only_the_pin_the_box_hides_nothing_of_the_distribution', () => {
  const argv = agentBoxPrefix({ ...base, dotnetPin: PIN, ...oneSdk });
  assert.deepEqual(tmpfsOf(argv).filter(p => DISTRIBUTION_DOTNET.some(root => p === root || p.startsWith(`${root}/`))), []);
});

test('A_box_with_a_pinned_sdk_directory_hides_the_whole_distribution_as_before_and_never_lists_it', () => {
  const argv = agentBoxPrefix({ ...base, toolchains: ['/dn'], ...pinned, ...neverListed });
  assertHidesWholeDistribution(argv, 'agentBoxPrefix');
  const before = ['bwrap', '--ro-bind', '/', '/', '--dev', '/dev', '--proc', '/proc', '--tmpfs', HOME, '--tmpfs', '/usr/lib/dotnet', '--tmpfs', '/etc/dotnet'];
  assert.deepEqual(argv.slice(0, before.length), before, 'the argv of the pinned path is what it was before the amendment (the fixture has no /mnt, /run, /tmp, /var/tmp, /usr/share/dotnet)');
  assert.deepEqual(argv, agentBoxPrefix({ ...base, toolchains: ['/dn'], ...pinned, dotnetPin: PIN, ...neverListed }), 'a pin given beside the pinned directory changes nothing');
});

test('A_box_with_a_pinned_sdk_directory_hides_every_distribution_root_itself_whether_or_not_its_caller_names_them', () => {
  const roots = DISTRIBUTION_DOTNET.filter(twoSdks.exists);
  assert.deepEqual(roots, ['/usr/lib/dotnet', '/etc/dotnet'], 'the invented host holds two of the three roots: a root it lacks is not mounted over');
  const given = { ...base, toolchains: ['/dn'], dotnetDir: '/dn', ...neverListed };
  // the refuted state: the box left the roots to its caller's `hide`, so a caller that passed the directory alone got every SDK of the distribution beside the pinned one, and an empty version list
  const alone = agentBoxPrefix(given);
  assertHidesWholeDistribution(alone, 'agentBoxPrefix, dotnetDir and no hide');
  assert.deepEqual(alone, agentBoxPrefix({ ...given, hide: DISTRIBUTION_DOTNET }), 'a caller that names the roots as well gets the same argv: each root once, in the same place');
  const count = argv => root => tmpfsOf(argv).filter(p => p === root).length;
  assert.deepEqual(roots.map(count(agentBoxPrefix({ ...given, hide: DISTRIBUTION_DOTNET }))), [1, 1], 'no second tmpfs for a path the caller already hides');
  assert.deepEqual(tmpfsOf(agentBoxPrefix({ ...given, hide: ['/etc/dotnet', '/x/more'], exists: p => p === '/x/more' || twoSdks.exists(p) })), [HOME, '/etc/dotnet', '/x/more', '/usr/lib/dotnet'], 'the caller\'s own paths first and in its order, then the roots it did not name');
  assert.deepEqual(tmpfsOf(agentBoxPrefix({ ...base, dotnetPin: PIN, ...oneSdk })), [HOME], 'control: without a pinned directory the roots stay, the box runs the distribution\'s dotnet');
  // every path that can hand a pinned directory to the box, with no hide list at all
  assertHidesWholeDistribution(probeBoxPrefix({ layout, nodeDir: '/n', tsDir: '/ts', claudeBinary: '/x/claude', stacks, dotnetDir: '/dn', seams: { ...neverListed, operatorHome: HOME } }), 'preflight, dotnetDir and no hide');
  assertHidesWholeDistribution(sessionBoxArgv({ plan: { ...bareSessionPlan({ ...planGiven, dotnetPin: PIN }), toolchains: ['/ts', '/dn'], dotnetDir: '/dn' }, command: ['claude'], seams: neverListed }), 'session-launch, dotnetDir and no hide');
  assertHidesWholeDistribution(snapshotBoxArgv({ layout, nodeDir: '/n', command: ['true'], toolchains: ['/dn'], dotnetDir: '/dn', seams: { ...neverListed, operatorHome: HOME } }), 'cache-snapshot, dotnetDir and no hide');
});

test('A_box_with_no_pinned_sdk_directory_and_no_pin_is_refused_and_the_refusal_names_the_pin', () => {
  assert.throws(() => agentBoxPrefix({ ...base, ...twoSdks }), /dotnetPin undefined is not an SDK version/);
  assert.throws(() => agentBoxPrefix({ ...base, ...oneSdk }), /dotnetPin undefined/, 'also on a host with one SDK: the refusal does not depend on what the host holds today');
});

test('The_preflights_probe_box_hides_the_other_versions_and_with_a_pinned_directory_the_whole_distribution', () => {
  const given = { layout, nodeDir: '/n', tsDir: '/ts', claudeBinary: '/x/claude', stacks };
  assertHidesOtherVersions(probeBoxPrefix({ ...given, seams: { ...twoSdks, operatorHome: HOME } }), 'preflight');
  assertHidesWholeDistribution(probeBoxPrefix({ ...given, ...pinned, seams: { ...neverListed, operatorHome: HOME } }), 'preflight, pinned');
});

const planGiven = { layout, stack: 'csharp', phase: 'v1', claudeBinary: '/x/claude', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/ts', token: 'fake', dummy: 'dummy', prices: {}, operatorHome: HOME, harnessSources: {} };

test('A_sessions_box_built_from_v1s_bare_plan_hides_the_other_versions', () => {
  const plan = bareSessionPlan({ ...planGiven, dotnetPin: PIN });
  assert.equal(plan.dotnetPin, PIN, 'the plan carries the pin');
  assertHidesOtherVersions(sessionBoxArgv({ plan, command: ['claude'], seams: twoSdks }), 'session-launch');
  assert.throws(() => sessionBoxArgv({ plan: bareSessionPlan(planGiven), command: ['claude'], seams: twoSdks }), /dotnetPin undefined/, 'a plan without the pin does not launch');
});

test('A_sessions_box_built_from_a_plan_with_a_pinned_sdk_directory_hides_the_whole_distribution', () => {
  const plan = { ...bareSessionPlan({ ...planGiven, dotnetPin: PIN }), toolchains: ['/ts', '/dn'], ...pinned };
  const argv = sessionBoxArgv({ plan, command: ['claude'], seams: neverListed });
  assertHidesWholeDistribution(argv, 'session-launch, pinned');
  assert.ok(argv.includes('/dn'), 'the pinned SDK directory is bound');
});

test('A_snapshot_steps_box_hides_the_other_versions_and_with_a_pinned_directory_the_whole_distribution', () => {
  const given = { layout, nodeDir: '/n', command: ['dotnet', 'restore'] };
  assertHidesOtherVersions(snapshotBoxArgv({ ...given, dotnetPin: PIN, seams: { ...twoSdks, operatorHome: HOME } }), 'cache-snapshot');
  assertHidesWholeDistribution(snapshotBoxArgv({ ...given, toolchains: ['/dn'], ...pinned, seams: { ...neverListed, operatorHome: HOME } }), 'cache-snapshot, pinned');
  assert.throws(() => snapshotBoxArgv({ ...given, seams: twoSdks }), /dotnetPin undefined/);
});

test('A_snapshot_made_with_no_pin_of_its_callers_is_given_v1s_and_one_with_a_pin_or_a_pinned_directory_keeps_its_own', () => {
  const never = () => { throw new Error('the registered pin is read only when the caller gave none'); };
  assert.deepEqual(snapshotBoxFields(), { dotnetPin: DOTNET_PIN }, 'no box options: v1\'s pin of inputs/stacks.json');
  assert.deepEqual(snapshotBoxFields({ env: { A: '1' } }), { env: { A: '1' }, dotnetPin: DOTNET_PIN });
  assert.deepEqual(snapshotBoxFields({ dotnetPin: '10.0.403' }, never), { dotnetPin: '10.0.403' }, 'the pilot\'s own pin is kept');
  assert.deepEqual(snapshotBoxFields({ toolchains: ['/dn'], ...pinned }, never), { toolchains: ['/dn'], ...pinned }, 'a pinned SDK directory needs no pin: that box hides the whole distribution');
  assertHidesOtherVersions(snapshotBoxArgv({ layout, nodeDir: '/n', command: ['true'], ...snapshotBoxFields(), seams: { ...twoSdks, operatorHome: HOME } }), 'makeSnapshot, no box options', DOTNET_PIN);
});

// ---- the scoring box: the same list, from the same function, at the same moment ----------------------------------------------------------

const score = { copyDir: '/w/copy', casesDir: '/w/cases', home: '/w/home', tmp: '/w/tmp', toolchainDirs: ['/t/node'], nugetCache: '/w/nuget', npmCache: '/w/npm', cwd: '/w/copy', operatorHome: HOME };

test('The_scoring_box_hides_every_other_version_before_any_bind_and_it_is_the_agent_boxs_list', () => {
  const argv = scoringBoxArgv({ ...score, dotnetPin: PIN, ...twoSdks });
  assertHidesOtherVersions(argv, 'scoringBoxArgv');
  const firstBind = argv.findIndex((a, i) => i > 2 && ['--bind', '--ro-bind'].includes(a)); // index 1 is the read-only root
  assert.ok(argv.lastIndexOf('--tmpfs') < firstBind, 'every tmpfs comes before the binds');
  const underRoots = a => tmpfsOf(a).filter(p => DISTRIBUTION_DOTNET.some(root => p.startsWith(`${root}/`)));
  assert.deepEqual(underRoots(argv), underRoots(agentBoxPrefix({ ...base, dotnetPin: PIN, ...twoSdks })), 'the two boxes hide the same folders: a tree is scored by the SDK it was built with');
  // the box is scoreBoxArgv's own argv with the list put in, and nothing else touched: taking the list out again gives the primitive's argv word for word
  const primitive = scoreBoxArgv({ ...score, exists: twoSdks.exists });
  assert.deepEqual(argv.filter((a, i) => !(a === '--tmpfs' && expected.includes(argv[i + 1])) && !(argv[i - 1] === '--tmpfs' && expected.includes(a))), [...primitive]);
  assert.equal(argv.indexOf(expected[0]) - 1, primitive.findIndex((a, i) => i > 2 && ['--bind', '--ro-bind'].includes(a)), 'the list sits exactly where the primitive\'s first bind was');
});

test('The_scoring_box_hides_nothing_of_dotnet_on_a_host_with_only_the_pin_and_its_argv_is_then_the_primitives', () => {
  const now = scoringBoxArgv({ ...score, dotnetPin: PIN, ...oneSdk });
  assert.deepEqual(tmpfsOf(now), [HOME], 'the fixture has no /mnt, /run, /tmp, /var/tmp: the operator\'s home, and no dotnet folder');
  assert.deepEqual(now, scoreBoxArgv({ ...score, exists: oneSdk.exists }), 'byte for byte what lib/bwrap.mjs builds: that file is pinned and is not changed');
});

test('A_scoring_box_with_a_pinned_sdk_directory_binds_it_read_only_hides_every_distribution_root_itself_and_lists_nothing', () => {
  const argv = scoringBoxArgv({ ...score, dotnetDir: '/dn', ...neverListed });
  assertHidesWholeDistribution(argv, 'scoringBoxArgv, dotnetDir');
  const at = (flag, a, b) => argv.findIndex((x, i) => x === flag && argv[i + 1] === a && argv[i + 2] === b);
  assert.ok(at('--ro-bind', '/dn', '/dn') > argv.lastIndexOf('--tmpfs'), 'the pinned directory is bound read-only, after every tmpfs');
  assert.equal(argv.filter((x, i) => x === '--ro-bind' && argv[i + 1] === '/dn').length, 1, 'once');
  const path_ = argv[argv.findIndex((x, i) => x === '--setenv' && argv[i + 1] === 'PATH') + 2];
  assert.equal(path_, '/t/node/bin:/t/node:/dn/bin:/dn:/usr/bin:/bin', 'and it is on PATH with the other toolchains');
  assert.deepEqual(scoringBoxArgv({ ...score, toolchainDirs: ['/t/node', '/dn'], dotnetDir: '/dn', ...neverListed }), argv, 'a caller that lists the directory among its toolchains too gets the same argv');
  assert.deepEqual([...scoringToolchains({ toolchainDirs: ['/t/node'], dotnetDir: '/dn' })], ['/t/node', '/dn']);
  assert.deepEqual(scoringBoxArgv({ ...score, dotnetDir: '/dn', dotnetPin: PIN, ...neverListed }), argv, 'a pin given beside the pinned directory changes nothing');
});

test('A_scoring_box_with_no_pinned_directory_and_no_pin_is_refused_and_the_refusal_names_the_pin', () => {
  assert.throws(() => scoringBoxArgv({ ...score, ...twoSdks }), /dotnetPin undefined is not an SDK version/);
  assert.throws(() => scoringBoxArgv({ ...score, ...oneSdk }), /dotnetPin undefined/, 'also on a host with one SDK');
});

test('The_place_of_the_scoring_boxs_dotnet_tmpfs_is_asserted_on_the_argv_it_is_given_never_guessed', () => {
  const given = { ...score, dotnetPin: PIN, ...twoSdks };
  const shaped = argv => () => scoringBoxArgv({ ...given, scoreBox: () => argv });
  assert.throws(shaped(['bwrap', '--dev', '/dev', '--bind', '/w', '/w', '--']), /does not open with its program and --ro-bind \/ \//);
  assert.throws(shaped(['bwrap', '--ro-bind', '/', '/', '--dev', '/dev', '--']), /binds nothing after its root/);
  assert.throws(shaped(['bwrap', '--ro-bind', '/', '/', '--bind', '/w', '/w', '--tmpfs', '/late', '--']), /holds a tmpfs \(\/late\) after its first bind \(\/w\)/);
  assert.doesNotThrow(shaped(['bwrap', '--ro-bind', '/', '/', '--tmpfs', '/early', '--bind', '/w', '/w', '--']));
});

/** A bind that is, or holds, a dotnet folder the box hides: on the distribution's dotnet (version folders) and with a pinned directory (the roots). */
const OVER = [['/usr/lib/dotnet', 'holds'], ['/usr/lib', 'holds'], ['/usr', 'holds'], [`${ROOT}/sdk/${SDK_11.sdk}`, 'is']];
const refusalOf = (bound, word, hidden) => e => e.message.startsWith(`the bind ${bound} ${word} ${hidden}, a dotnet folder this box hides`);

test('The_agents_box_refuses_a_bind_that_is_or_holds_a_dotnet_folder_it_hides_naming_both_paths', () => {
  for (const [bound, word] of OVER) {
    const hidden = expected.find(p => p === bound || p.startsWith(`${bound}/`));
    assert.throws(() => agentBoxPrefix({ ...base, toolchains: [bound], dotnetPin: PIN, ...twoSdks }), refusalOf(bound, word, hidden), `toolchains: ${bound}`);
  }
  assert.throws(() => agentBoxPrefix({ ...base, extraRw: ['/usr/lib'], dotnetPin: PIN, ...twoSdks }), refusalOf('/usr/lib', 'holds', expected[0]), 'any bind, a writable one too');
  assert.throws(() => agentBoxPrefix({ ...base, toolchains: ['/usr/lib/dotnet'], dotnetDir: '/dn', ...neverListed }), refusalOf('/usr/lib/dotnet', 'is', '/usr/lib/dotnet'), 'with a pinned directory the hidden folder is the root itself');
  assert.throws(() => agentBoxPrefix({ ...base, toolchains: ['/usr/lib'], dotnetDir: '/usr/lib/dotnet', ...neverListed }), /the bind \/usr\/lib holds \/usr\/lib\/dotnet/, 'the distribution\'s own folder cannot be "pinned" beside itself');
  for (const fine of ['/usr/lib/node', '/usr/lib/dotnet-tools', `${ROOT}/sdk/${PIN}`, `${ROOT}/sdk/${SDK_11.sdk}/inner`]) assert.doesNotThrow(() => agentBoxPrefix({ ...base, toolchains: [fine], dotnetPin: PIN, ...twoSdks }), `control: ${fine} holds no hidden folder`);
  assert.doesNotThrow(() => agentBoxPrefix({ ...base, toolchains: ['/usr/lib'], dotnetPin: PIN, ...oneSdk }), 'control: on a host with only the pin nothing is hidden, so nothing can be shown again');
});

test('The_scoring_box_refuses_a_bind_that_is_or_holds_a_dotnet_folder_it_hides_naming_both_paths', () => {
  for (const [bound, word] of OVER) {
    const hidden = expected.find(p => p === bound || p.startsWith(`${bound}/`));
    assert.throws(() => scoringBoxArgv({ ...score, toolchainDirs: [bound], dotnetPin: PIN, ...twoSdks }), refusalOf(bound, word, hidden), `toolchainDirs: ${bound}`);
  }
  assert.throws(() => scoringBoxArgv({ ...score, roFiles: ['/usr/lib'], dotnetPin: PIN, ...twoSdks }), refusalOf('/usr/lib', 'holds', expected[0]), 'any bind, a read-only file or folder too');
  assert.throws(() => scoringBoxArgv({ ...score, toolchainDirs: ['/usr/lib/dotnet'], dotnetDir: '/dn', ...neverListed }), refusalOf('/usr/lib/dotnet', 'is', '/usr/lib/dotnet'));
  for (const fine of ['/usr/lib/node', `${ROOT}/sdk/${PIN}`]) assert.doesNotThrow(() => scoringBoxArgv({ ...score, toolchainDirs: [fine], dotnetPin: PIN, ...twoSdks }), `control: ${fine}`);
  assert.doesNotThrow(() => scoringBoxArgv({ ...score, toolchainDirs: ['/usr/lib'], dotnetPin: PIN, ...oneSdk }), 'control: nothing hidden, nothing to show again');
});

test('Both_boxes_refuse_to_be_built_on_a_host_where_a_version_folder_they_must_hide_is_a_link', () => {
  const link = `${ROOT}/sdk/${SDK_11.sdk}`;
  const host = fakeDirs(twoSdkHost(), { links: [link] });
  const refusal = e => e.message.startsWith(`${link} is a symbolic link`);
  assert.throws(() => agentBoxPrefix({ ...base, dotnetPin: PIN, ...host }), refusal, 'the agent\'s box');
  assert.throws(() => scoringBoxArgv({ ...score, dotnetPin: PIN, ...host }), refusal, 'the scoring box');
  assert.doesNotThrow(() => agentBoxPrefix({ ...base, toolchains: ['/dn'], ...pinned, exists: host.exists, listDir: neverListed.listDir }), 'a box with a pinned SDK directory hides the roots whole and never looks');
});

/** The scorer's context as a step sees it: the plan's folders and the dependencies `defaultDeps` builds (the held-out slot is not asked here). */
const scorerCtx = (given, seams) => {
  const deps = defaultDeps({ inputs: {}, toolchainDirs: ['/t/node'], heldout: async () => { throw new Error('not asked'); }, ...given });
  return { plan: { casesDir: '/w/cases', homeDir: '/w/home', tmpDir: '/w/tmp', nugetCopy: '/w/nuget', npmCopy: '/w/npm' }, deps: { ...deps, box: { ...deps.box, operatorHome: HOME, ...seams } } };
};

test('Every_step_of_the_scorer_builds_its_box_with_the_pin_of_its_dependencies_the_callers_or_v1s_registered_one', () => {
  assert.deepEqual(defaultDeps({ inputs: {}, heldout: () => {} }).box, { toolchainDirs: [], dotnetPin: DOTNET_PIN }, 'no pin given (the held-out pipeline, a scorer run by hand): v1\'s, of inputs/stacks.json');
  assert.equal(defaultDeps({ inputs: {}, heldout: () => {}, dotnetPin: '10.0.403' }).box.dotnetPin, '10.0.403', 'a run\'s own pin is kept');
  for (const options of [{}, { cwd: false, cases: true, roFiles: ['/w/copy'] }]) {
    assertHidesOtherVersions(boxFor(scorerCtx({ dotnetPin: PIN }, twoSdks), '/w/copy', options), 'score-steps boxFor');
    assertHidesOtherVersions(boxFor(scorerCtx({}, twoSdks), '/w/copy', options), 'score-steps boxFor, v1\'s pin', DOTNET_PIN);
  }
  const noPin = scorerCtx({}, twoSdks);
  assert.throws(() => boxFor({ ...noPin, deps: { ...noPin.deps, box: { toolchainDirs: [], ...twoSdks } } }, '/w/copy'), /dotnetPin undefined/, 'box options made anywhere else, without the pin, build no box');
});
