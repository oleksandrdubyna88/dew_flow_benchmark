// The units of the quick run (tasks 04 05 06 07 08 10): the scenario-sample reader, the held-out reader and its identity, the pins, the fixed limits, the prompts, the note's words, and the scoring with its seams
// (a build that fails, a runner that fails, a record that misses a case). No box, no layer and no money: the build and the run are injected. The real thing is proved by `rehearse`.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { casesTreeHash, heldoutMovedProblem, loadHeldoutQuick } from '../lib/heldout-quick.mjs';
import { quickPrompts } from '../lib/init-quick.mjs';
import { loadCalibration, limitsProblems } from '../lib/limits.mjs';
import { QUICK_BASE, bannerOf } from '../lib/note-tasks.mjs';
import { isQuick, kindOf, quickCalibration, quickExplore, quickPlan } from '../lib/quick.mjs';
import { parseScenarioSamples } from '../lib/samples-sd.mjs';
import { acceptedOf } from '../lib/score.mjs';
import { casesOfRecord, combinedCases, judgedRecord, scoreQuick } from '../lib/score-quick.mjs';
import { MIB, built, frozenFiles, ran, rec, score, scoringArea } from './quick-score-fixture.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { frameworkSentence, frozenOf, quickStackSentence, quickStatement } from '../lib/workspace-quick.mjs';

const EXPLORE = loadExplore().value;

const SPEC = [
  '# Task 05 — X', '', '## 5. Build', '', '## 6. Visible samples', '', 'The same two scenarios are in `samples/`.', '',
  '### s01', '', '`samples/s01.in`', '', '```text', 'scenario 2', 'start ready port within 10s', 'signal TERM', 'wait-exit within 5s', '```', '',
  '`samples/s01.out`', '', '```text', 'transcript 2', 'ready', 'exit 0', '```', '', 'Prose after a sample.', '',
  '### s02', '', '`samples/s02.in`', '', '```text', 'scenario 2', '```', '', '`samples/s02.out`', '', '```text', 'transcript 2', '```', '', '## 7. Hidden tests', '', 'text', '',
].join('\n');

test('The_scenario_samples_are_read_from_section_6_byte_for_byte_and_in_order', () => {
  const r = parseScenarioSamples(SPEC);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.map(s => s.id), ['s01', 's02']);
  assert.equal(r.value[0].input, 'scenario 2\nstart ready port within 10s\nsignal TERM\nwait-exit within 5s\n');
  assert.equal(r.value[0].output, 'transcript 2\nready\nexit 0\n');
});

test('A_section_6_the_reader_cannot_read_is_refused_with_a_sentence', () => {
  assert.match(parseScenarioSamples('# t\n\n## 6. Visible samples\n\nno samples\n').reason, /no `### sNN`/);
  assert.match(parseScenarioSamples(SPEC.replace('`samples/s01.out`', '`samples/s09.out`')).reason, /headed by/);
  assert.match(parseScenarioSamples(SPEC.replaceAll('s02', 's03')).reason, /in order/);
});

test('The_six_quick_tasks_are_pinned_with_their_kind_and_the_pins_validate', () => {
  assert.deepEqual(EXPLORE.quick.planTasks, ['04', '05', '06', '07', '08', '10']);
  const kinds = Object.fromEntries(EXPLORE.quick.planTasks.map(id => [id, EXPLORE.tasks[id].kind]));
  assert.deepEqual(kinds, { '04': 'web', '05': 'daemon', '06': 'web', '07': 'daemon', '08': 'web', 10: 'daemon' });
  for (const id of EXPLORE.quick.planTasks) {
    const task = resolveTask(EXPLORE, id).value;
    assert.equal(isQuick(task), true);
    assert.notEqual(kindOf(task), 'olympiad');
  }
  assert.equal(isQuick(resolveTask(EXPLORE, '02').value), false);
  assert.equal(kindOf(resolveTask(EXPLORE, '02').value), 'olympiad');
});

test('The_quick_limits_are_fixed_valid_and_the_same_every_time', () => {
  const task = resolveTask(EXPLORE, '07').value;
  const a = loadCalibration('/nonexistent-home', task, () => ok(EXPLORE));
  assert.equal(a.ok, true, a.reason);
  assert.deepEqual(a.value, quickCalibration(EXPLORE));
  assert.equal(a.value.limits.memoryBytes, 512 * MIB);
  assert.deepEqual(limitsProblems(a.value.limits, EXPLORE.limits), []);
  assert.deepEqual(quickPlan(EXPLORE).planTasks, EXPLORE.quick.planTasks);
});

test('The_quick_profile_pins_the_v2_toolchains_and_leaves_the_olympiad_pins_alone', () => {
  const q = quickExplore(EXPLORE, '/h');
  assert.equal(q.dotnet.sdk, '10.0.403');
  assert.equal(q.node.version, '26.13.0');
  assert.equal(q.envExtra.DOTNET_ROOT, '/h/rsd/lang-v2/toolchains/dotnet-sdk-10.0.403');
  assert.equal(EXPLORE.dotnet.sdk, '10.0.114');
  assert.equal(EXPLORE.envExtra.DOTNET_ROOT, undefined);
});

test('The_prompts_carry_the_v2_sentences_and_nothing_of_the_plan_or_the_other_stack', () => {
  const p = quickPrompts(quickExplore(EXPLORE, '/h'));
  assert.match(p.prompts.csharp.text, /^Implement the program described in STATEMENT.md in this directory, in C#\. Use C# 14 on \.NET 10 \(SDK 10\.0\.403 is installed\)\./);
  assert.match(p.prompts.node.text, /in TypeScript\. Use TypeScript 6\.0 with `strict`, run by Node 26/);
  for (const stack of ['csharp', 'node']) assert.doesNotMatch(p.prompts[stack].text, /D\d\d|plan|held-out|10\.0\.114|comparison/i);
  assert.notEqual(p.prompts.csharp.sha256, p.prompts.node.sha256);
});

test('The_statement_marks_the_stack_sentence_and_adds_the_framework_sentence_to_a_web_task_only', () => {
  const q = quickExplore(EXPLORE, '/h');
  const web = quickStatement({ spec: 'spec\n', kind: 'web', stackSentence: quickStackSentence(q, 'node'), frameworkSentence: frameworkSentence(q, 'node', { node: { packages: [{ name: 'fastify', version: '5.12.7' }] } }) });
  assert.match(web, /<!-- stack-sentence -->\nUse TypeScript 6\.0/);
  assert.match(web, /<!-- framework-sentence -->\nUse Fastify \(fastify 5\.12\.7, listed in PACKAGES\.md\) for the HTTP server\.\n$/);
  const daemon = quickStatement({ spec: 'spec\n', kind: 'daemon', stackSentence: 's' });
  assert.doesNotMatch(daemon, /framework-sentence/);
  assert.match(frameworkSentence(q, 'csharp', null), /ASP\.NET Core Minimal API \(the shared framework of SDK 10\.0\.403\)/);
});

test('The_frozen_set_has_the_pin_files_the_scripts_the_samples_and_the_runner_but_not_the_project_files', () => {
  const f = n => ({ bytes: Buffer.from(n), mode: 0o644 });
  const all = Object.fromEntries(['App.csproj', 'global.json', 'nuget.config', 'Directory.Build.props', 'build.sh', 'run.sh', 'STATEMENT.md', 'PACKAGES.md', 'samples/s01.in', 'runner/run_samples.py'].map(n => [n, f(n)]));
  assert.deepEqual(Object.keys(frozenOf(all, 'csharp')).sort(), ['Directory.Build.props', 'PACKAGES.md', 'STATEMENT.md', 'build.sh', 'global.json', 'nuget.config', 'run.sh', 'runner/run_samples.py', 'samples/s01.in']);
  const node = Object.fromEntries(['package.json', 'package-lock.json', '.npmrc', 'tsconfig.json', 'eslint.config.mjs', 'build.sh'].map(n => [n, f(n)]));
  assert.deepEqual(Object.keys(frozenOf(node, 'node')).sort(), ['.npmrc', 'build.sh', 'eslint.config.mjs', 'tsconfig.json']);
});

function casesFolder(t, over = {}) {
  const root = tmpDir('quick-cases');
  const dir = path.join(root, 'heldout-05');
  const cases = path.join(dir, 'cases');
  mkdirSync(cases, { recursive: true });
  for (const id of over.ids ?? ['h001', 'h002']) { writeFileSync(path.join(cases, `${id}.in`), `in ${id}\n`); writeFileSync(path.join(cases, `${id}.out`), `out ${id}\n`); }
  if (over.manifest !== false) writeFileSync(path.join(cases, 'cases.txt'), over.manifest ?? '# c\nh001 bytes R1\nh002 json R2 R3\n');
  if (over.traps) writeFileSync(path.join(dir, 'traps.txt'), over.traps);
  const home = path.join(root, 'home');
  mkdirSync(path.join(home, 'rsd', 'lang-v2', 'explore'), { recursive: true });
  mkdirSync(path.dirname(path.join(home, 'rsd', 'lang-v2', 'explore', 'heldout-05')), { recursive: true });
  return { home, dir, cases, task: { ...resolveTask(EXPLORE, '05').value, heldoutDirParts: [path.relative(home, dir)].flatMap(p => p.split(path.sep)) } };
}

test('The_held_out_reader_accepts_the_agreed_shape_and_records_a_tree_hash_that_moves_with_a_byte', t => {
  const f = casesFolder(t, { traps: '# t\nh002\n' });
  const r = loadHeldoutQuick(f.home, f.task);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.caseIds, ['h001', 'h002']);
  assert.deepEqual(r.value.traps, ['h002']);
  const before = r.value.treeSha256;
  assert.equal(before, casesTreeHash(f.cases));
  writeFileSync(path.join(f.cases, 'h002.out'), 'out h002 \n');
  assert.notEqual(casesTreeHash(f.cases), before);
  assert.match(heldoutMovedProblem({ inputs: { heldoutQuick: { treeSha256: before, caseCount: 2 } }, heldout: loadHeldoutQuick(f.home, f.task).value }), /changed since the series was made/);
  assert.equal(heldoutMovedProblem({ inputs: { heldoutQuick: { treeSha256: before } }, heldout: r.value }), null);
  assert.match(heldoutMovedProblem({ inputs: {}, heldout: r.value }), /no held-out hash/);
});

test('The_held_out_reader_refuses_a_missing_folder_a_missing_manifest_a_stray_file_and_an_unknown_trap', t => {
  const missing = casesFolder(t);
  assert.match(loadHeldoutQuick(missing.home, { ...missing.task, heldoutDirParts: ['nowhere'] }).reason, /not there/);
  assert.match(loadHeldoutQuick(...(f => [f.home, f.task])(casesFolder(t, { manifest: false }))).reason, /cases\.txt is missing/);
  assert.match(loadHeldoutQuick(...(f => [f.home, f.task])(casesFolder(t, { manifest: 'h001 bytes R1\n' }))).reason, /does not list h002/);
  const stray = casesFolder(t);
  writeFileSync(path.join(stray.cases, 'notes.txt'), 'x');
  assert.match(loadHeldoutQuick(stray.home, stray.task).reason, /not a pair or cases\.txt: notes\.txt/);
  const trap = casesFolder(t, { traps: 'h009\n' });
  assert.match(loadHeldoutQuick(trap.home, trap.task).reason, /traps\.txt names case\(s\) that are not in cases\/: h009/);
});

// ---- the scoring with its seams ---------------------------------------------------------------------------------------------------------

test('A_tree_whose_every_visible_and_held_out_case_passes_is_accepted_and_each_verdict_is_recorded', async t => {
  const r = await score(t, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal(r.ok, true, r.reason);
  assert.equal(acceptedOf(r.value), true);
  assert.deepEqual(r.value.explore.cases.map(c => [c.id, c.set, c.verdict]), [['h001', 'heldout', 'pass'], ['h002', 'heldout', 'pass'], ['s01', 'visible', 'pass']]);
  assert.deepEqual(r.value.explore.frozenChanges, []);
});

test('A_failing_case_is_a_wrong_answer_with_its_reason_and_the_tree_is_not_accepted_and_traps_are_counted', async t => {
  const r = await score(t, { traps: ['h002'], runFn: ran([rec('s01'), rec('h001'), rec('h002', 'FAIL', 'entry 2 does not match')]) });
  assert.equal(r.ok, true, r.reason);
  assert.equal(acceptedOf(r.value), false);
  const c = r.value.explore.cases.find(x => x.id === 'h002');
  assert.deepEqual([c.verdict, c.reason], ['wrong answer', 'entry 2 does not match']);
  assert.equal(r.value.explore.trapHits, 1);
  assert.deepEqual(r.value.explore.verdicts.heldout, { pass: 1, 'wrong answer': 1 });
});

test('A_case_that_hit_the_memory_limit_is_a_memory_limit_verdict', async t => {
  const r = await score(t, { runFn: ran([rec('s01'), rec('h001', 'FAIL', 'memory limit: oom_kill 1'), rec('h002')]) });
  assert.equal(r.value.explore.cases.find(x => x.id === 'h001').verdict, 'memory limit');
});

test('A_build_that_exits_non_zero_is_a_build_failed_verdict_for_every_case_and_nothing_is_run', async t => {
  let runs = 0;
  const r = await score(t, { buildFn: built(false), runFn: async () => { runs += 1; return {}; } });
  assert.equal(r.ok, true, r.reason);
  assert.equal(runs, 0);
  assert.equal(acceptedOf(r.value), false);
  assert.deepEqual([...new Set(r.value.explore.cases.map(c => c.verdict))], ['build failed']);
  assert.equal(r.value.build.ok, false);
});

test('A_harness_failure_is_never_a_verdict_it_is_an_environment_failure_the_loop_halts_on', async t => {
  const environment = result => { assert.equal(result.ok, false); assert.equal(result.kind, 'environment', result.reason); };
  environment(await score(t, { runFn: async () => ({ exitCode: 5, stdout: '', stderr: 'running a daemon sample is part ii', record: null }) }));
  environment(await score(t, { runFn: async () => ({ exitCode: 3, stdout: '', stderr: 'launch failed', record: null }) }));
  environment(await score(t, { runFn: async () => ({ exitCode: 4, stdout: '', stderr: 'h001.in:3: unknown keyword', record: null }) }));
  environment(await score(t, { runFn: async () => ({ exitCode: 3, halt: 'memory-not-certifiable', stderr: 'x', record: null }) }));
  environment(await score(t, { runFn: async () => ({ exitCode: 1, stderr: '', record: { launch: { failed: true, stderr: 'bwrap: no' }, cases: [] } }) }));
  environment(await score(t, { runFn: ran([rec('s01'), rec('h001')]) })); // a case missing from the record
  environment(await score(t, { runFn: ran([rec('s01'), rec('h001'), rec('h002'), rec('h003')]) })); // an unexpected case
  environment(await score(t, { buildFn: async () => fail('the build box did not run', { kind: 'environment' }), runFn: ran([]) }));
});

test('The_pristine_copy_restores_a_frozen_file_the_agent_changed_and_says_so', async t => {
  const a = scoringArea(t);
  writeFileSync(path.join(a.source, 'build.sh'), '#!/bin/bash\nexit 0\n', { mode: 0o755 });
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, caseWallMs: 1, runCeilingMs: 1 };
  let seen = null;
  const r = await scoreQuick({
    stack: 'node', source: a.source, files: frozenFiles(), sets: [{ name: 'heldout', dir: a.held }], limits: { memoryBytes: 512 * MIB, buildTimeoutMs: 1, timeMs: 1 }, work: a.work, profile,
    buildFn: async ({ tree }) => { seen = readFileSync(path.join(tree, 'build.sh'), 'utf8'); return ok({ ok: true, exit: 0, wallMs: 1, timedOut: false, tail: '' }); },
    runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))),
  });
  assert.equal(seen, '#!/bin/bash\n');
  assert.deepEqual(r.value.explore.frozenChanges, [{ path: 'build.sh', state: 'changed' }]);
});

test('The_combined_case_folder_holds_the_visible_and_the_held_out_pairs_and_refuses_a_bad_id', t => {
  const a = scoringArea(t);
  const dir = path.join(a.work, 'cases');
  const r = combinedCases({ files: frozenFiles(), heldoutDir: a.held, dir });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual([r.value.visible, r.value.heldout], [['s01'], ['h001', 'h002']]);
  assert.equal(readFileSync(path.join(dir, 'cases.txt'), 'utf8').includes('h002'), true);
  writeFileSync(path.join(a.held, 'x1.in'), 'i');
  assert.equal(combinedCases({ files: frozenFiles(), heldoutDir: a.held, dir: path.join(a.work, 'cases2') }).ok, false);
});

test('judgedRecord_and_casesOfRecord_read_exactly_the_expected_cases', () => {
  assert.equal(judgedRecord({ exitCode: 0, record: { cases: [] } }).ok, true);
  assert.equal(judgedRecord({ exitCode: 2, record: { cases: [] } }).kind, 'environment');
  assert.equal(casesOfRecord({ record: { cases: [rec('a'), rec('a')] }, expected: ['a'] }).ok, false);
  assert.equal(casesOfRecord({ record: {}, expected: ['a'] }).ok, false);
});

test('The_note_of_a_quick_task_names_its_kind_and_the_quick_run_not_the_olympiad_pilot', () => {
  const banner = bannerOf(resolveTask(EXPLORE, '06').value);
  assert.match(banner, /web task 06 \(Blog API, an ungated draft spec\), the owner's quick run/);
  assert.doesNotMatch(banner, /olympiad/);
  assert.equal(QUICK_BASE.some(l => /10\.0\.403/.test(l)), true);
  assert.equal(QUICK_BASE.some(l => /10\.0\.114/.test(l)), false);
  assert.match(bannerOf(resolveTask(EXPLORE, '02').value), /olympiad task 02/);
});
