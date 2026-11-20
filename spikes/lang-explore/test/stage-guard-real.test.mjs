// The real halves of the Stage 1 guard (F3.S1 (a), PR 4, scenario 59 and the task repository at the record): the layer's certification probe on THIS host, and the frozen record's task-repository checks over the real task repository
// (`LANG_V2_TASK_REPO`). Behind `needs`: a host without the layer's tools, or without the variable, skips with the reason; under BENCH_REQUIRE_SANDBOX=1 the same case fails. Nothing private is read: the probe runs an empty program, and the
// task-repository half reads the public record and git objects only.
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import os from 'node:os';
import { test } from 'node:test';
import { certifyMemory } from '../../lang-bench/lib/exec-layer/certify.mjs';
import { cleanup } from '../../lang-bench/lib/exec-layer/cleanup.mjs';
import { openRun } from '../../lang-bench/lib/exec-layer/run.mjs';
import { needs } from '../../lang-bench/test/support/exec-layer-host.mjs';
import { layerOptions, MIB } from '../../lang-bench/test/support/exec-tree.mjs';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { PINNED_TASK_SHA } from '../../lang-bench/lib/task-repo.mjs';
import { REPO } from '../lib/paths.mjs';
import { certifyOnHost, hostProbe, memoryCertification } from '../lib/stage-certify.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { readRecord } from '../lib/stage-record.mjs';
import { taskRepoProblems } from '../lib/stage-guard.mjs';

test('The_certification_probe_passes_on_this_host_and_reads_the_peak_of_an_empty_program', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t);
  const probed = await certifyOnHost({ options });
  assert.equal(probed.launched, true, probed.reason);
  assert.equal(probed.certified, true, probed.reason);
  assert.ok(Number.isSafeInteger(probed.peakBytes) && probed.peakBytes > 0 && probed.peakBytes < 256 * MIB, `the peak of an empty program is read (${probed.peakBytes} bytes)`);
  for (const kind of ['data', 'pilot', 'aa', 'rehearsal']) {
    const verdict = await memoryCertification({ kind, probe: () => certifyOnHost({ options: layerOptions(t) }) });
    assert.equal(verdict.ok, true, `${kind}: ${verdict.reason}`);
    assert.deepEqual(verdict.value.flags, [], `${kind}: certified, so no flag`);
  }
  const direct = await openRun(layerOptions(t));
  assert.equal(direct.ok, true, direct.reason);
  try { assert.equal((await certifyMemory(direct.value)).certified, true, 'the layer\'s own probe agrees'); } finally { await cleanup(direct.value); }
});

test('The_real_frozen_record_and_the_real_task_repository_at_W_pass_the_task_repository_half_of_the_guard', async t => {
  const taskRepo = process.env.LANG_V2_TASK_REPO;
  if (!needs(t, taskRepo !== undefined && existsSync(taskRepo) ? null : 'LANG_V2_TASK_REPO does not name a checkout of the task repository')) return;
  const { record, problems } = readRecord(REPO);
  assert.deepEqual(problems, [], 'the three files of the record are readable');
  // the operator's checkout may be behind: the guard asks for a checkout AT W, so the test makes a detached worktree at W (removed with the test; the checkout itself is untouched)
  const atW = ownedFor({ token: 'bench' }).ownedWorktree(t, taskRepo, 'realw', { ref: PINNED_TASK_SHA }).path;
  const found = await taskRepoProblems({ repo: REPO, taskRepo: atW, record });
  assert.deepEqual(found, [], `the six spec blobs at the tag, W, the checkout at W, the pinned commit and the hash list are the record's: ${found.join(' | ')}`);
});

test('The_probe_stage_run_runs_by_default_certifies_this_host_with_the_pinned_toolchains_and_the_registered_memory_value', async t => {
  if (!needs(t)) return;
  const probed = await hostProbe({ explore: loadExplore().value, home: os.homedir() });
  assert.equal(probed.launched, true, probed.reason);
  assert.equal(probed.certified, true, probed.reason);
  assert.ok(Number.isSafeInteger(probed.peakBytes) && probed.peakBytes > 0 && probed.peakBytes < 256 * MIB, `the peak of an empty program is read (${probed.peakBytes} bytes)`);
  const verdict = await memoryCertification({ kind: 'data', probe: () => hostProbe({ explore: loadExplore().value, home: os.homedir() }) });
  assert.equal(verdict.ok, true, verdict.reason);
  const unreachable = await hostProbe({ explore: loadExplore().value, home: '/nonexistent-home' });
  assert.equal(typeof unreachable.launched, 'boolean', 'a probe that cannot be set up answers a value and never throws');
});
