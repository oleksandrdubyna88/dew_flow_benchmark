// The ten-pass scoring and the per-file drift rule meet (the merge of F1.S2 and F1.S3): the build record is written once, at the commit, so the undecided files and the identity of the contract that `prepareTree` finds must be in
// it then; `finishRollup` reads them from there into `score.json`, and the record carries them on. The loop's scoring dep hands the series' `history` to the quick world's seams, so a rescore judges drift per file in the ten-pass
// path too. Invented fixtures only; no box, no layer, no process.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { passSeams } from '../lib/pass-seams.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { exploreSection } from '../lib/records.mjs';
import { scoreSessionDep } from '../lib/run-ctx.mjs';
import { finishRollup } from '../lib/score-quick.mjs';
import { LIMIT_BYTES } from './quick-passes-fixture.mjs';
import { recordFor } from './quick-passes-harness.mjs';
import { built, frozenFiles, scoringArea } from './quick-score-fixture.mjs';
import { world } from './quick-series-fixture.mjs';

const sha = text => createHash('sha256').update(text).digest('hex');
const f = text => ({ bytes: Buffer.from(text), mode: 0o644 });
const IDENTITY = 'c'.repeat(64);
const OLD_BUILD = '#!/bin/bash\n# the build of an older template\n';
/** What the history seam knows: the old bytes of `build.sh` only, so `.npmrc` cannot be judged. */
const history = ({ rel }) => (rel === 'build.sh' ? { known: true, sha256: sha(OLD_BUILD) } : { known: false, why: `no old bytes for ${rel}` });
const PROFILE = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, kind: 'daemon', caseWallMs: 660_000, runCeilingMs: 7_200_000 };

/** A kept tree whose `build.sh` is the old template's and whose `.npmrc` is bytes nobody can judge, scored by the ten passes. -> the pass driver's answer. */
async function tenPassScoring() {
  const a = scoringArea({});
  writeFileSync(path.join(a.source, 'build.sh'), OLD_BUILD, { mode: 0o755 });
  writeFileSync(path.join(a.source, '.npmrc'), 'offline=false\n');
  const seriesDir = tmpDir('quick-drift-series');
  const lock = acquireSeriesLock({ seriesDir });
  const runFn = async () => ({ exitCode: 0, stdout: '', stderr: '', record: recordFor(), memoryCertified: true });
  const limits = { memoryBytes: LIMIT_BYTES, buildTimeoutMs: 600_000, timeMs: 300_000 };
  const files = { ...frozenFiles(), '.npmrc': f('offline=true\n') };
  const seams = passSeams({
    a: { stack: 'node', source: a.source, files, sets: [{ name: 'heldout', dir: a.held }], limits, traps: [], driftPossible: false, history, contractIdentityFn: () => IDENTITY },
    profile: PROFILE, seams: { buildFn: built(), runFn, probeFn: async cpus => ok(cpus), growthFn: () => ok({}) }, seriesDir,
  });
  try { return { ...(await scorePasses({ ...seams, work: a.work, seriesDir })), work: a.work }; } finally { lock.value.release(); }
}

test('A_ten_pass_scoring_with_drift_and_an_undecided_file_writes_both_and_the_contract_identity_into_score_json_and_the_record', async () => {
  const r = await tenPassScoring();
  assert.equal(r.kind, 'scored', r.reason);
  const built = JSON.parse(readFileSync(path.join(r.work, 'build.json'), 'utf8'));
  assert.equal(built.contractIdentity, IDENTITY, 'the completion record is written once, so the identity is in it at the commit');
  assert.deepEqual(built.undecidedDrift.map(c => c.path), ['.npmrc'], 'and so is the file that could not be judged');
  assert.deepEqual(built.harnessDrift.map(c => c.path), ['build.sh']);
  const onDisk = JSON.parse(readFileSync(path.join(r.work, 'score.json'), 'utf8')).explore;
  assert.equal(onDisk.contractIdentity, IDENTITY);
  assert.deepEqual(onDisk.undecidedDrift.map(c => c.path), ['.npmrc']);
  assert.deepEqual(onDisk.harnessDrift.map(c => c.path), ['build.sh']);
  const leaf = exploreSection({ score: r.score, audit: [] });
  assert.equal(leaf.contractIdentity.value, IDENTITY, 'the session record names the contract the ten passes were made under');
  assert.equal(leaf.deviations.known, false, 'an undecided file makes the deviations unknown, never a known count');
  assert.match(leaf.deviations.why, /\.npmrc/);
  assert.deepEqual(leaf.harnessDrift.value.map(c => c.path), ['build.sh']);
});

test('The_loops_scoring_dep_hands_the_series_history_and_the_runner_drift_to_the_quick_worlds_seams', async () => {
  const w = world();
  const asked = [];
  const dep = scoreSessionDep({
    world: { passSeams: args => { asked.push(args); throw new Error('stop here: only the arguments are looked at'); } },
    calibration: { limits: {} }, heldout: { casesDir: '/x', traps: [] }, home: w.home, scoreExtra: { driftPossible: true, history },
  });
  await assert.rejects(dep({ session: w.layout.dir, source: w.layout.work, stack: 'node', nugetCache: '/n', npmCache: '/p' }), /stop here/);
  assert.equal(asked.length, 1);
  assert.equal(asked[0].driftPossible, true);
  assert.equal(asked[0].history, history, 'a rescore judges drift per file in the ten-pass path too');
});

test('A_completion_record_from_before_the_merge_has_no_undecided_files_and_no_identity_and_finishing_it_does_not_throw', () => {
  const build = { ok: true, exit: 0, timedOut: false, wallMs: 5, tail: '', frozenChanges: [], harnessDrift: [], nonRegularLeftOut: [] };
  const rollup = { build: { ran: true, ok: true, status: 0, wallMs: 5, timedOut: false, tail: '' }, limits: { memoryBytes: LIMIT_BYTES, buildTimeoutMs: 600_000, timeMs: 300_000 }, cases: [], passes: 10, memoryCertified: true };
  const finished = finishRollup({ rollup, build });
  assert.equal(finished.ok, true);
  assert.equal('undecidedDrift' in finished.value.explore, false);
  assert.equal(exploreSection({ score: finished.value, audit: [] }).contractIdentity.known, false, 'a score with no identity says so and is never given today\'s');
});
