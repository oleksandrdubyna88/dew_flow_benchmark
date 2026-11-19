// The pilot's paths to an agent box carry the pinned SDK (v1 E2 amendment 2026-11-19, D-9): the run context (`run-ctx.mjs`), the session plan (`plan.mjs`), the plan record (`readback.mjs`) and the
// preflight edge. Each is followed to the argv v1 builds from it, over an invented host that holds two SDKs: a box on the distribution's dotnet hides the other one, a quick box (the pinned SDK
// directory) hides the whole distribution as before. The argv is built, never run: the effect is v1's `agent-box-dotnet.test.mjs`. One owned folder, for the plan record; the rest makes none.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { DISTRIBUTION_DOTNET, otherDotnetVersions } from '../../minigit-bench/lib/dotnet-hide.mjs';
import { probeBoxPrefix } from '../../minigit-bench/lib/preflight.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { sessionBoxArgv } from '../../minigit-bench/lib/session-launch.mjs';
import { ROOT, SDK_11, fakeDirs, tmpfsOf, twoSdkHost } from '../../minigit-bench/test/dotnet-fixture.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { loadPrompts, planForSession } from '../lib/plan.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { planRecord } from '../lib/readback.mjs';
import { contextOf, preflightDep } from '../lib/run-ctx.mjs';

const EXPLORE = loadExplore().value;
const HOME = '/home/nobody';
const twoSdks = fakeDirs(twoSdkHost());
const neverListed = { exists: twoSdks.exists, listDir: dir => { throw new Error(`a box with a pinned SDK directory must not list ${dir}`); } };
const layout = layoutPaths('/series/s', 'abc123');
const item = (stack, over = layout) => ({ session: { stack, phase: 'v1', attempt: 1 }, rid: 'abc123', layout: over });
const snap = { dir: '/snap', parts: ['x'], record: {} };

/** The pilot's world (the distribution's dotnet) and the quick world (the pinned SDK directory), as `loadWorld` shapes them for the run context. */
const pilotWorld = { explore: EXPLORE, paths: { nodeDir: '/n', tsDir: '/ts', claudeBinary: '/x/claude' }, snapshots: { csharp: snap, node: snap } };
const quickWorld = { ...pilotWorld, explore: quickExplore(EXPLORE, HOME), paths: { ...pilotWorld.paths, dotnetDir: '/dn' }, quick: true };

/** The run context of a rehearsal over `world` (a rehearsal keeps no alias store, so nothing touches the disk). */
const contextFor = world => contextOf({
  world, calibration: {}, series: {}, seriesDir: '/series', kind: 'rehearsal', pair: 1, token: 'fake-token', claudeBinary: '/x/claude', fill: () => {}, abort: { reason: null, exit: 130 }, home: HOME,
  policy: { table: {}, policy: {} }, identity: 'id', prompts: loadPrompts(),
});

const argvOf = (plan, seams) => sessionBoxArgv({ plan, command: ['claude'], seams });

function assertHidesOtherVersions(argv, pin, where) {
  const hidden = tmpfsOf(argv);
  assert.ok(hidden.includes(`${ROOT}/sdk/${SDK_11.sdk}`), `${where}: --tmpfs ${ROOT}/sdk/${SDK_11.sdk}`);
  assert.ok(hidden.includes(`${ROOT}/shared/Microsoft.NETCore.App/${SDK_11.runtime}`), `${where}: another major's runtime is hidden`);
  assert.deepEqual(hidden.filter(p => p.startsWith(`${ROOT}/`)), [...otherDotnetVersions({ pin, ...twoSdks })], `${where}: the one list, whole`);
}

function assertHidesWholeDistribution(argv, where) {
  const hidden = tmpfsOf(argv);
  for (const root of DISTRIBUTION_DOTNET.filter(twoSdks.exists)) assert.ok(hidden.includes(root), `${where}: ${root} is hidden whole`);
  assert.deepEqual(hidden.filter(p => DISTRIBUTION_DOTNET.some(root => p.startsWith(`${root}/`))), [], `${where}: no version folder beneath a root that is hidden whole`);
}

test('The_run_context_carries_the_pinned_sdk_of_its_world_and_the_box_of_a_pilot_session_hides_every_other_version', () => {
  const ctx = contextFor(pilotWorld);
  assert.equal(ctx.dotnetPin, EXPLORE.dotnet.sdk, 'the pilot runs the distribution\'s dotnet, pinned in explore.json');
  assert.match(ctx.dotnetPin, /^\d+\.\d+\.\d+$/);
  assert.equal(ctx.dotnetDir, undefined, 'no pinned SDK directory in the pilot\'s world');
  for (const stack of ['csharp', 'node']) assertHidesOtherVersions(argvOf(planForSession({ ctx, item: item(stack) }), twoSdks), ctx.dotnetPin, `run-ctx, ${stack}`);
});

test('The_run_context_of_a_quick_world_keeps_the_pinned_sdk_directory_and_its_box_hides_the_whole_distribution_as_before', () => {
  const ctx = contextFor(quickWorld);
  assert.deepEqual([ctx.dotnetDir, ctx.hide, ctx.dotnetPin], ['/dn', DISTRIBUTION_DOTNET, EXPLORE.quick.dotnet.sdk]);
  const plan = planForSession({ ctx, item: item('csharp') });
  assert.deepEqual([plan.dotnetDir, plan.hide], ['/dn', DISTRIBUTION_DOTNET]);
  assertHidesWholeDistribution(argvOf(plan, neverListed), 'run-ctx, quick');
});

test('The_session_plan_takes_the_pin_from_its_context_and_a_context_without_one_cannot_launch', () => {
  const ctx = { ...contextFor(pilotWorld) };
  const plan = planForSession({ ctx, item: item('node') });
  assert.equal(plan.dotnetPin, ctx.dotnetPin);
  assertHidesOtherVersions(argvOf(plan, twoSdks), ctx.dotnetPin, 'plan');
  const { dotnetPin: _dropped, ...without } = ctx;
  assert.throws(() => argvOf(planForSession({ ctx: without, item: item('node') }), twoSdks), /dotnetPin undefined is not an SDK version/, 'no pin, no box: never a box that shows every SDK of the host');
});

test('The_plan_the_loop_launches_is_the_recorded_one_and_plan_json_names_the_pinned_sdk', t => {
  const dir = ownedFor({ token: 'bench' }).ownedDir(t, 'pin');
  const ctx = contextFor(pilotWorld);
  const plan = ctx.planFor({ ctx, item: item('csharp', { ...layoutPaths(dir, 'abc123'), dir }) }); // `planFor` is `recordPlan(planForSession(...))`: it writes plan.json and returns the plan the launcher takes
  const recorded = JSON.parse(readFileSync(path.join(dir, 'plan.json'), 'utf8'));
  assert.equal(recorded.dotnetPin, EXPLORE.dotnet.sdk, 'a reader of plan.json sees which SDK the box was told to show');
  assert.equal(planRecord(plan).dotnetPin, plan.dotnetPin);
  assert.equal(JSON.stringify(recorded).includes('fake-token'), false, 'still no token in the record');
  assertHidesOtherVersions(argvOf(plan, twoSdks), ctx.dotnetPin, 'readback');
});

test('The_preflight_edge_hands_v1_the_pilots_pin_so_its_probe_box_hides_every_other_version', async () => {
  for (const [world, check] of [[pilotWorld, argv => assertHidesOtherVersions(argv, EXPLORE.dotnet.sdk, 'preflight edge')], [quickWorld, argv => assertHidesWholeDistribution(argv, 'preflight edge, quick')]]) {
    const ctx = contextFor(world);
    const calls = [];
    const r = await preflightDep({ ctx, world, preflightHome: HOME, baseFn: async a => { calls.push(a); return { ok: true, value: {} }; } })();
    assert.equal(r.ok, true, r.reason);
    assert.equal(calls[0].stacks.pins.dotnetSdk, ctx.dotnetPin, 'the preflight\'s rule and the session\'s box are given the same pin');
    const { home: _home, install: _install, ...box } = calls[0];
    check(probeBoxPrefix({ layout, ...box, seams: { ...(world.quick ? neverListed : twoSdks), operatorHome: HOME } }));
  }
});
