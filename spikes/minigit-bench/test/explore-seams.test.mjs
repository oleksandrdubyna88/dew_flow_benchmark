// The four optional seams v1 gained for the exploratory pilot (spikes/lang-explore; plan todo/PLAN_lang_explore_task1.md section 2.2): W1 `ctx.fill` / `ctx.planFor` of the launch phase, W2 `locale` of
// `agentEnv`, W3 `locale` of `scoreEnv` / `scoreBoxArgv`, W4 the exported `sessionMeasure`. Each is checked twice: its DEFAULT is byte-identical to what v1 did before the seam existed (a
// comparison with `fixtures/explore-seams-baseline.json`, written from the unchanged code by `explore-seams-observe.mjs`), and its GIVEN value does the new thing.
import './fake-home.mjs'; // first: the phase reads os.homedir()
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { launchPhase } from '../lib/bare-phase.mjs';
import { cellOf, sessionMeasure } from '../lib/bare-record.mjs';
import { scoreBoxArgv, scoreEnv } from '../lib/bwrap.mjs';
import { runBounded } from '../lib/run.mjs';
import { launchSession } from '../lib/session-launch.mjs';
import { AGENT_ENV_KEYS, agentEnv, dummyToken, layoutPaths, sessionLayout } from '../lib/sandbox.mjs';
import { ok } from '../lib/result.mjs';
import { context, items } from './bare-ctx.mjs';
import { sandboxGate, spikeDir, tmpDir } from './helpers.mjs';
import { RESULT, fakeDeps, goodInit, planFor, say } from './launch-fakes.mjs';
import { observeAll, observeCells } from './explore-seams-observe.mjs';

const BASELINE = JSON.parse(readFileSync(path.join(spikeDir, 'test', 'fixtures', 'explore-seams-baseline.json'), 'utf8'));
const LOCALE = 'de_DE.UTF-8';
const layout = layoutPaths('/r', 's1');
const env = (extra, locale) => agentEnv({ layout, dummyToken: dummyToken('d'), cellId: 'c', nodeDir: '/n', tsDir: '/ts', extra, ...(locale === undefined ? {} : { locale }) });

// ---- the defaults ---------------------------------------------------------------------------------------------------------------------

test('With_no_seam_value_given_v1_behaves_byte_for_byte_as_it_did_before_the_seams_existed', async () => {
  const now = JSON.parse(JSON.stringify(await observeAll()));
  for (const part of Object.keys(BASELINE)) assert.deepEqual(now[part], BASELINE[part], `${part} differs from the baseline captured before the seams`);
  assert.deepEqual(Object.keys(now), Object.keys(BASELINE));
});

test('The_baseline_fixture_still_holds_the_pieces_it_compares_so_the_comparison_is_not_empty', () => {
  assert.equal(BASELINE.agentEnv.base.LANG, 'C.UTF-8');
  assert.equal('LC_ALL' in BASELINE.agentEnv.base, false);
  assert.equal(BASELINE.scoring.argvAllPresent.includes('--setenv'), true);
  assert.deepEqual(BASELINE.phase.map(p => p.work), [['SPEC-v1.txt', 'test-v1.sh'], ['SPEC-v1.txt', 'test-v1.sh']]);
  assert.equal(Object.keys(BASELINE.cells).length, 5);
});

// ---- W1: the phase's fill and plan ------------------------------------------------------------------------------------------------------

function phaseDeps(plans) {
  return {
    spawn: () => ({}), maskCheckMs: 5, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [],
    launchSession: async plan => { plans.push(plan); return { ok: true, value: { meta: { rid: plan.layout.rid }, exit: { code: 0 } } }; },
  };
}

test('W1_a_phase_given_fill_and_planFor_calls_each_once_per_item_and_uses_what_they_return', async () => {
  const fills = [];
  const planned = [];
  const plans = [];
  const base = context();
  const ctx = {
    ...base,
    fill: ({ ctx: c, item, layout: l }) => { fills.push(item.rid); writeFileSync(path.join(l.work, 'FILLED.txt'), item.session.stack); return ok({ files: ['FILLED.txt'], flags: ['from the seam'] }); },
    planFor: ({ ctx: c, item }) => { planned.push(item.rid); return { layout: item.layout, arm: 'bare', cellId: `cell-${item.rid}`, prompt: 'from the seam', allow: [], toolchains: [], envExtra: {}, locale: LOCALE }; },
  };
  const r = await launchPhase({ ctx, items: items(ctx), deps: phaseDeps(plans) });
  assert.equal(r.ok, true, r.reason);
  const rids = items(ctx).map(i => i.rid);
  assert.deepEqual(fills, rids, 'fill is called once per item, in order');
  assert.deepEqual(planned, rids, 'planFor is called once per item, in order');
  for (const s of r.value.sessions) {
    assert.deepEqual(readdirSync(s.layout.work), ['FILLED.txt'], 'the given fill, not SPEC-v1.txt and test-v1.sh');
    assert.equal(s.plan.prompt, 'from the seam');
    assert.equal(s.plan.locale, LOCALE, 'a field the v1 plan has no name for travels through');
    assert.deepEqual(s.workspace.flags, ['from the seam']);
  }
  assert.deepEqual(plans.map(p => p.prompt), ['from the seam', 'from the seam']);
});

test('W1_a_fill_that_fails_releases_the_layouts_and_nothing_is_launched', async () => {
  const ctx = { ...context(), fill: () => ({ ok: false, reason: 'no workspace for you' }) };
  const plans = [];
  const r = await launchPhase({ ctx, items: items(ctx), deps: phaseDeps(plans) });
  assert.equal(r.ok, false);
  assert.match(r.reason, /no workspace for you/);
  assert.deepEqual(plans, []);
  assert.deepEqual(readdirSync(ctx.sessionsRoot), [], 'the layout the failed fill claimed is released');
});

// ---- W2: agentEnv's locale -----------------------------------------------------------------------------------------------------------

test('W2_a_given_locale_sets_LANG_and_LC_ALL_and_changes_nothing_else', () => {
  const plain = env({});
  const given = env({}, LOCALE);
  assert.equal(given.LANG, LOCALE);
  assert.equal(given.LC_ALL, LOCALE);
  const { LANG: _a, ...restPlain } = plain;
  const { LANG: _b, LC_ALL: _c, ...restGiven } = given;
  assert.deepEqual(restGiven, restPlain, 'every other key and value is the default');
  assert.deepEqual(Object.keys(given).sort(), [...AGENT_ENV_KEYS, 'LC_ALL'].sort());
  assert.deepEqual(Object.keys(plain).sort(), [...AGENT_ENV_KEYS].sort(), 'AGENT_ENV_KEYS is the key set of the DEFAULT environment and stays as it is; LC_ALL is the locale seam\'s own key');
});

test('W2_extra_may_not_replace_LANG_or_LC_ALL_when_a_locale_is_given_in_any_case', () => {
  for (const key of ['LC_ALL', 'lc_all', 'LANG', 'Lang']) assert.throws(() => env({ [key]: 'C' }, LOCALE), /already set/, key);
  assert.equal(env({ LC_ALL: 'C' }).LC_ALL, 'C', 'with no locale given the default is unchanged: extra may add LC_ALL as before');
});

test('W2_a_locale_that_is_not_a_locale_name_is_refused_naming_the_shape', () => {
  for (const bad of ['', 'de DE', 'de_DE.UTF-8; rm -rf /', 7, null, 'x'.repeat(80)]) assert.throws(() => env({}, bad), /locale/i, String(bad));
});

test('W2_the_locale_of_the_plan_reaches_the_environment_the_box_is_spawned_with', async () => {
  for (const [locale, expected] of [[LOCALE, { LANG: LOCALE, LC_ALL: LOCALE }], [undefined, { LANG: 'C.UTF-8', LC_ALL: undefined }]]) {
    const made = sessionLayout(tmpDir('w2'), 's1');
    assert.equal(made.ok, true, made.reason);
    const l = made.value;
    const order = [];
    const { deps, spawned } = fakeDeps(order, l, { script: (child, lay) => { say(child, goodInit(lay), RESULT); child.finish(0); } });
    const plan = planFor(l, locale === undefined ? {} : { locale });
    const launched = await launchSession(plan, deps);
    assert.equal(launched.ok, true, launched.reason);
    assert.equal(spawned[0].env.LANG, expected.LANG);
    assert.equal(spawned[0].env.LC_ALL, expected.LC_ALL);
    const argv = JSON.parse(readFileSync(l.files.argv, 'utf8'));
    assert.equal(argv.envKeys.includes('LC_ALL'), locale !== undefined, 'argv.json names the environment keys the session really had');
  }
});

// ---- W3: the scoring box's locale -----------------------------------------------------------------------------------------------------

const SCORE_OPTS = { copyDir: '/w/copy', casesDir: '/w/cases', home: '/w/hm', tmp: '/w/tmp', toolchainDirs: ['/t/a'], nugetCache: '/w/nuget', npmCache: '/w/npm', cwd: '/w/copy', operatorHome: '/opt/operator-home', exists: () => true };

/** The `--setenv` pairs of a bwrap argv as an object. */
const setenvOf = argv => Object.fromEntries(argv.flatMap((a, i) => (a === '--setenv' ? [[argv[i + 1], argv[i + 2]]] : [])));

test('W3_scoreEnv_and_scoreBoxArgv_given_a_locale_set_LANG_and_LC_ALL_and_change_nothing_else', () => {
  const plain = scoreEnv(SCORE_OPTS);
  const given = scoreEnv({ ...SCORE_OPTS, locale: LOCALE });
  assert.equal(given.LANG, LOCALE);
  assert.equal(given.LC_ALL, LOCALE);
  assert.equal('LC_ALL' in plain, false);
  const { LANG: _a, ...restPlain } = plain;
  const { LANG: _b, LC_ALL: _c, ...restGiven } = given;
  assert.deepEqual(restGiven, restPlain);
  const argvPlain = scoreBoxArgv(SCORE_OPTS);
  const argvGiven = scoreBoxArgv({ ...SCORE_OPTS, locale: LOCALE });
  assert.deepEqual(setenvOf(argvGiven), { ...setenvOf(argvPlain), LANG: LOCALE, LC_ALL: LOCALE }, 'the box carries the locale');
  assert.equal(argvGiven.length, argvPlain.length + 3, 'one more --setenv triple (LC_ALL) and nothing else');
});

test('W3_a_locale_that_is_not_a_locale_name_is_refused_by_the_scoring_box_too', () => {
  for (const bad of ['', 'de DE', 7, 'a;b']) assert.throws(() => scoreEnv({ ...SCORE_OPTS, locale: bad }), /locale/i, String(bad));
});

test('W3_a_scoring_box_built_with_a_locale_runs_a_process_that_sees_it', async t => {
  if (!sandboxGate(t)) return;
  const base = tmpDir('w3');
  const dirs = Object.fromEntries(['copy', 'cases', 'home', 'tmp'].map(n => [n, path.join(base, n)]));
  for (const d of Object.values(dirs)) mkdirSync(d, { recursive: true });
  const argv = scoreBoxArgv({ copyDir: dirs.copy, casesDir: dirs.cases, home: dirs.home, tmp: dirs.tmp, toolchainDirs: [], locale: LOCALE });
  const r = await runBounded(argv[0], [...argv.slice(1), '/usr/bin/env'], { timeoutMs: 20_000 });
  assert.equal(r.ok, true, r.reason);
  const lines = r.value.stdout.split('\n');
  assert.ok(lines.includes(`LC_ALL=${LOCALE}`), r.value.stdout + r.value.stderr);
  assert.ok(lines.includes(`LANG=${LOCALE}`));
});

// ---- W4: the cell's measure -------------------------------------------------------------------------------------------------------

test('W4_sessionMeasure_is_exported_and_is_what_cellOf_sums', () => {
  const cells = observeCells();
  const record = { ids: { label: 'paired' }, class: 'completed', cost: { headlineUsd: { known: true, value: { usd: 0.25 } }, stopRuleUsd: { known: true, value: 0.25 } }, time: { wallMs: { known: true, value: 900 } } };
  assert.deepEqual(sessionMeasure(record, 'costUsd'), { value: 0.25, censored: false });
  assert.deepEqual(sessionMeasure(record, 'wallMs'), { value: 900, censored: false });
  assert.deepEqual(sessionMeasure({ ...record, class: 'killed: cap', cost: { ...record.cost, stopRuleUsd: { known: true, value: 6 } } }, 'costUsd'), { value: 6, censored: true }, 'a killed session\'s figure is its stop-rule figure, a lower bound');
  assert.equal(sessionMeasure({ ...record, time: {} }, 'wallMs'), null, 'a measure that is not known is null, never zero');
  const twice = cellOf(record, record);
  assert.equal(twice.wallMs.value, 1800, 'cellOf still sums two sessions through the same function');
  assert.equal(cells.completed.costUsd.known, true);
});
