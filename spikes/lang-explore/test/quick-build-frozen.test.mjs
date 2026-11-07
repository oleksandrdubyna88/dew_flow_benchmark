// A frozen file rewritten DURING the build (the final seam review of F1, item 1; parent D65: "frozen inputs are put back"). The clean copy puts every frozen file back before the build, but a build that is the agent's own
// (an MSBuild target in `App.csproj`) runs inside the writable build box and can rewrite `run.sh` or any frozen file there. After the build, `commitBuild` compares every frozen file with its frozen bytes, puts each changed one back and
// records it as an agent deviation with the reason `changed during the build`, in `build.json`, `score.json` and `record.json`. The passes then run the restored file. The build's output folder is not frozen and is never touched.
// Invented files only; the real box is in `quick-build-frozen-real.test.mjs`.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { commitBuild } from '../lib/build-commit.mjs';
import { DURING_BUILD } from '../lib/copy-quick.mjs';
import { exploreSection } from '../lib/records.mjs';
import { finishRollup, prepareTree } from '../lib/score-quick.mjs';
import { ran, rec, scoreWith, scoringArea } from './quick-score-fixture.mjs';

const RUN_SH = '#!/usr/bin/env bash\nexec dotnet bin/Release/net10.0/App.dll\n';
const REWRITTEN = '#!/usr/bin/env bash\necho a launcher the build wrote\n';
const frozen = () => ({
  'run.sh': { bytes: Buffer.from(RUN_SH), mode: 0o755 },
  'build.sh': { bytes: Buffer.from('#!/usr/bin/env bash\ndotnet build\n'), mode: 0o755 },
  'samples/s01.in': { bytes: Buffer.from('scenario 2\n'), mode: 0o644 },
});

/** The agent's tree: the frozen files as frozen, a program, and `over` (relative path -> text) on top. */
function agentTree(over = {}) {
  const source = path.join(tmpDir('quick-build-frozen'), 'tree');
  mkdirSync(path.join(source, 'samples'), { recursive: true });
  for (const [rel, f] of Object.entries(frozen())) writeFileSync(path.join(source, rel), f.bytes, { mode: f.mode });
  writeFileSync(path.join(source, 'Program.cs'), 'class P {}');
  for (const [rel, text] of Object.entries(over)) writeFileSync(path.join(source, rel), text);
  return source;
}

const args = source => ({ source, stack: 'csharp', files: frozen(), profile: { kind: 'daemon' }, contractIdentityFn: () => 'a'.repeat(64) });
const GOOD = Object.freeze({ ok: true, exit: 0, timedOut: false, wallMs: 5, tail: '' });

/** A build that writes its output folder and then runs `edit(tree)`, as an MSBuild target of the agent's project would. */
const buildThat = (edit, result = GOOD) => async ({ tree }) => {
  mkdirSync(path.join(tree, 'bin', 'Release'), { recursive: true });
  writeFileSync(path.join(tree, 'bin', 'Release', 'App.dll'), 'built output');
  edit(tree);
  return ok(result);
};

const commit = async ({ source, build }) => {
  const work = tmpDir('quick-build-frozen-work');
  const made = await commitBuild({ work, prepare: async dest => prepareTree(args(source), dest), build });
  return { made, work, tree: path.join(work, 'tree') };
};

test('A_frozen_file_the_build_rewrites_is_put_back_and_recorded_as_changed_during_the_build', async () => {
  const { made, work, tree } = await commit({ source: agentTree(), build: buildThat(t => writeFileSync(path.join(t, 'run.sh'), REWRITTEN)) });
  assert.equal(made.ok, true, made.reason);
  assert.equal(readFileSync(path.join(tree, 'run.sh'), 'utf8'), RUN_SH, 'the passes run the restored launcher, not the one the build wrote');
  const record = JSON.parse(readFileSync(path.join(work, 'build.json'), 'utf8'));
  assert.deepEqual(record.frozenChanges, [{ path: 'run.sh', state: 'changed', reason: DURING_BUILD }], 'one agent deviation, with the new reason');
  assert.equal(DURING_BUILD, 'changed during the build');
  assert.equal(readFileSync(path.join(tree, 'bin', 'Release', 'App.dll'), 'utf8'), 'built output', 'the output folder is not frozen and is not touched');
});

test('A_frozen_file_the_build_deleted_or_a_sample_it_rewrote_is_put_back_too', async () => {
  const build = buildThat(t => { writeFileSync(path.join(t, 'samples', 's01.in'), 'changed sample\n'); writeFileSync(path.join(t, 'build.sh'), 'x'); });
  const { made, tree } = await commit({ source: agentTree(), build });
  assert.equal(made.ok, true, made.reason);
  assert.equal(readFileSync(path.join(tree, 'samples', 's01.in'), 'utf8'), 'scenario 2\n');
  assert.deepEqual(made.value.frozenChanges.map(c => c.path).sort(), ['build.sh', 'samples/s01.in']);
});

test('A_build_that_touches_no_frozen_file_records_nothing_new_and_the_agents_earlier_change_is_kept_beside_it', async () => {
  const clean = await commit({ source: agentTree(), build: buildThat(() => {}) });
  assert.deepEqual(clean.made.value.frozenChanges, [], 'the control: an untouched build adds no deviation');
  const both = await commit({ source: agentTree({ 'run.sh': '# the agent edited it before the build\n' }), build: buildThat(t => writeFileSync(path.join(t, 'build.sh'), 'x')) });
  assert.deepEqual(both.made.value.frozenChanges, [{ path: 'run.sh', state: 'changed' }, { path: 'build.sh', state: 'changed', reason: DURING_BUILD }], 'the change before the build is as it was; the one during it has its reason');
});

test('A_failed_build_that_rewrote_a_frozen_file_is_restored_and_recorded_as_well', async () => {
  const { made, tree } = await commit({ source: agentTree(), build: buildThat(t => writeFileSync(path.join(t, 'run.sh'), REWRITTEN), { ok: false, exit: 1, timedOut: false, wallMs: 5, tail: 'error' }) });
  assert.equal(made.ok, true, made.reason);
  assert.equal(made.value.ok, false);
  assert.equal(readFileSync(path.join(tree, 'run.sh'), 'utf8'), RUN_SH);
  assert.equal(made.value.frozenChanges[0].reason, DURING_BUILD);
});

test('The_change_during_the_build_reaches_the_score_and_the_session_record_as_a_deviation_and_never_as_a_verdict', async () => {
  const { made, work } = await commit({ source: agentTree(), build: buildThat(t => writeFileSync(path.join(t, 'run.sh'), REWRITTEN)) });
  const build = JSON.parse(readFileSync(path.join(work, 'build.json'), 'utf8'));
  assert.equal(existsSync(path.join(work, 'build.json')), true);
  const rollup = { build: { ran: true, ok: true, status: 0, wallMs: 5, timedOut: false, tail: '' }, limits: { memoryBytes: 1, buildTimeoutMs: 1, timeMs: 1 }, cases: [] };
  const finished = finishRollup({ rollup, build });
  assert.equal(finished.ok, true);
  assert.deepEqual(finished.value.explore.frozenChanges, [{ path: 'run.sh', state: 'changed', reason: DURING_BUILD }], 'score.json names it with its reason');
  assert.deepEqual(made.value.frozenChanges, build.frozenChanges);
  const section = exploreSection({ score: { ...finished.value, explore: { ...finished.value.explore, cases: [], passRate: { visible: 1, heldout: 1 } } }, audit: [] });
  assert.deepEqual(section.deviations, { known: true, value: { count: 1, paths: ['run.sh'] } }, 'record.json counts it as an agent deviation');
});

test('The_one_pass_scoring_of_a_replay_puts_back_a_file_its_build_rewrote_and_names_it_in_the_score', async t => {
  const a = scoringArea(t);
  const buildFn = async ({ tree }) => { writeFileSync(path.join(tree, 'build.sh'), '#!/bin/bash\necho rewritten\n'); return ok({ ok: true, exit: 0, wallMs: 1, timedOut: false, tail: '' }); };
  const seen = [];
  const runFn = async arg => { seen.push(readFileSync(path.join(arg.tree, 'build.sh'), 'utf8')); return ran(['s01', 'h001', 'h002'].map(id => rec(id)))(arg); };
  const made = await scoreWith(a, { buildFn, runFn });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(made.value.explore.frozenChanges, [{ path: 'build.sh', state: 'changed', reason: DURING_BUILD }]);
  assert.deepEqual(seen, ['#!/bin/bash\n'], 'the cases ran against the restored file');
});
