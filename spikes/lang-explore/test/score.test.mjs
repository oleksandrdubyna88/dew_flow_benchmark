// The pure parts of the Node side of the judge: the scoring box argvs (locale, extra environment, `tsc` on PATH, the cases never bound), `withEnv`, and the mapping of the judge's JSON to v1's score shape.
import assert from 'node:assert/strict';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { hundredPercent } from '../../minigit-bench/lib/score-pipeline.mjs';
import { PLACE, acceptedOf, boxArgvs, judgeConfig, scoreShape, verdictCounts, withEnv } from '../lib/score.mjs';

const pairs = argv => Object.fromEntries(argv.flatMap((a, i) => (a === '--setenv' ? [[argv[i + 1], argv[i + 2]]] : [])));
const OPTS = { toolchainDirs: ['/n'], tsDir: '/ts', nugetCache: '/c/nuget', npmCache: '/c/npm', locale: 'de_DE.UTF-8', envExtra: { DOTNET_gcServer: '0', NODE_OPTIONS: '' }, operatorHome: '/home/op', exists: () => true };

test('withEnv_replaces_a_key_in_place_adds_a_new_one_after_the_last_and_refuses_an_argv_without_an_environment', () => {
  const argv = ['bwrap', '--clearenv', '--setenv', 'A', '1', '--setenv', 'B', '2', '--chdir', '/x', '--', ];
  const out = withEnv(argv, { B: 'two', C: '3' });
  assert.deepEqual(out, ['bwrap', '--clearenv', '--setenv', 'A', '1', '--setenv', 'B', 'two', '--setenv', 'C', '3', '--chdir', '/x', '--']);
  assert.deepEqual(argv.slice(5, 8), ['--setenv', 'B', '2'], 'the input is not changed');
  assert.deepEqual(withEnv(argv, { A: '' }).slice(2, 5), ['--setenv', 'A', ''], 'an empty value is a value (NODE_OPTIONS is set empty)');
  assert.throws(() => withEnv(['bwrap', '--'], { A: '1' }), /no --setenv/);
  assert.throws(() => withEnv(argv, {}).push('y'), TypeError, 'the result is frozen');
});

test('The_two_boxes_carry_the_locale_the_extra_keys_and_tsc_on_PATH_and_neither_binds_the_cases', () => {
  const { buildBox, runBox } = boxArgvs(OPTS);
  for (const box of [buildBox, runBox]) {
    const env = pairs(box);
    assert.equal(env.LANG, 'de_DE.UTF-8');
    assert.equal(env.LC_ALL, 'de_DE.UTF-8');
    assert.equal(env.DOTNET_gcServer, '0');
    assert.equal(env.NODE_OPTIONS, '');
    assert.ok(env.PATH.split(':').includes('/ts/node_modules/.bin') && env.PATH.split(':').includes('/n/bin'), env.PATH);
    assert.equal(env.HOME, PLACE.home);
    assert.equal(env.TMPDIR, PLACE.tmp);
    assert.ok(box.includes(PLACE.tree) && box.includes('--unshare-net'), 'the tree is bound and there is no network');
    assert.equal(box.includes('--unshare-pid') && box.includes('--die-with-parent'), true);
    assert.equal(box.at(-1), '--');
  }
  assert.ok(buildBox.includes('/c/nuget') && buildBox.includes('/c/npm'), 'the build gets the cache copies');
  assert.ok(!runBox.includes('/c/nuget') && !runBox.includes('/c/npm'), 'the run does not: nothing a run writes reaches a cache');
});

const case_ = (set, id, verdict, extra = {}) => ({ set, id, verdict, wallMs: 5, peakBytes: 1000, events: {}, exit: 0, ...extra });
const RESULT = { build: { ok: true }, limits: { timeMs: 2000 }, cases: [case_('visible', 's01', 'pass'), case_('visible', 's02', 'pass'), case_('heldout', 'h001', 'pass'), case_('heldout', 'h002', 'wrong answer'), case_('heldout', 'h003', 'time limit')] };

test('The_judges_json_becomes_v1s_score_shape_with_the_explore_section', () => {
  const s = scoreShape({ result: RESULT, traps: ['h003', 'h001'] });
  assert.deepEqual(s.build, { ok: true });
  assert.equal(hundredPercent(s.visible[0]), true, 'v1\'s own 100 % test reads the visible step');
  assert.deepEqual(s.heldout.totals, { total: 3, passed: 1, failed: 2, blocked: 0, percent: 33.3 });
  assert.equal(s.heldout.primary100, false);
  assert.equal(s.heldout.defects.count, 2);
  assert.deepEqual(s.explore.verdicts, { visible: { pass: 2 }, heldout: { pass: 1, 'wrong answer': 1, 'time limit': 1 } });
  assert.equal(s.explore.trapHits, 1, 'one of the two trap cases failed: counted, not named');
  assert.deepEqual(s.explore.passRate, { visible: 100, heldout: 33.3 });
  assert.deepEqual(s.upstream, []);
  assert.equal(acceptedOf(s), false);
});

test('A_session_is_accepted_only_with_a_clean_build_every_case_passed_and_the_memory_claim_certified', () => {
  const clean = { ...RESULT, cases: RESULT.cases.map(c => ({ ...c, verdict: 'pass' })) };
  assert.equal(acceptedOf(scoreShape({ result: clean })), true);
  assert.equal(acceptedOf(scoreShape({ result: { ...clean, cases: clean.cases.map((c, i) => (i === 3 ? { ...c, peakBytes: null } : c)) } })), false, 'a case without a certified peak is not accepted');
  const noBuild = { ...clean, build: { ok: false }, cases: clean.cases.map(c => ({ ...c, verdict: 'runtime error' })) };
  const s = scoreShape({ result: noBuild });
  assert.equal(s.heldout.primary100, false);
  assert.equal(s.heldout.totals, undefined, 'a failed build has no totals: its percent is not 0');
  assert.equal(acceptedOf(s), false);
  assert.deepEqual(verdictCounts([]), {});
});

test('The_judge_config_carries_the_pilots_locale_and_environment_the_limits_and_both_case_sets', () => {
  const area = tmpDir('cfg');
  const explore = { locale: 'de_DE.UTF-8', envExtra: { DOTNET_gcServer: '0', NODE_OPTIONS: '' } };
  const files = { 'samples/s01.in': { bytes: Buffer.from('1\n') }, 'samples/s01.out': { bytes: Buffer.from('1\n') }, 'build.sh': { bytes: Buffer.from('x') } };
  const limits = { timeMs: 2000, memoryBytes: 1, buildTimeoutMs: 3 };
  const config = judgeConfig({ explore, files, sets: [{ name: 'heldout', dir: '/h' }], limits, toolchains: { dirs: ['/n'], tsDir: '/ts' }, caches: {}, area });
  assert.deepEqual(config.sets.map(s => s.name), ['visible', 'heldout']);
  assert.deepEqual(readdirSync(config.sets[0].dir).sort(), ['s01.in', 's01.out'], 'the visible set is the frozen samples only');
  assert.equal(config.limits, limits);
  assert.deepEqual(config.placeholders, PLACE);
  for (const box of [config.buildBox, config.runBox]) assert.deepEqual([pairs(box).LANG, pairs(box).LC_ALL, pairs(box).DOTNET_gcServer], ['de_DE.UTF-8', 'de_DE.UTF-8', '0']);
  assert.ok(existsSync(path.join(area, 'cache-nuget')) && existsSync(path.join(area, 'cache-npm')), 'an absent cache is an empty one');
});
