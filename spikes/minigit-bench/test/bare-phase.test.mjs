// One launch phase (E3b.S1 step 5): layouts, workspaces, seeded caches, canaries planted and unplanted, the same-tick start, the spawn stamp and the skew.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { launchPhase, maskedSpawn } from '../lib/bare-phase.mjs';
import { context, items, sleep } from './bare-ctx.mjs';

test('A_phase_starts_every_session_on_its_mask_stamps_the_spawn_and_measures_the_skew', async () => {
  const ctx = { ...context(), planOver: { tickMs: 7 } };
  const seenPlans = [];
  const spawns = [];
  const fakeChild = {};
  const deps = {
    spawn: (exe, argv) => { spawns.push({ exe, argv }); return fakeChild; },
    maskCheckMs: 5,
    procApi: { pids: () => [], read: () => { throw Object.assign(new Error('x'), { code: 'ENOENT' }); } },
    listProjectDirs: () => [path.join(ctx.home, '.claude', 'projects', 'p1')],
    // the stub launch: spawns the box through the injected seam, 30 ms after the first for the second, and returns 200 ms later
    launchSession: async (plan, d) => {
      seenPlans.push(plan);
      if (plan.layout.rid !== items(ctx)[0].rid) await sleep(30);
      d.spawnBox(['bwrap', '--x'], { A: '1' });
      await sleep(200);
      return { ok: true, value: { meta: { rid: plan.layout.rid }, exit: { code: 0, signal: null } } };
    },
    now: Date.now,
  };
  const r = await launchPhase({ ctx, items: items(ctx), deps });
  assert.equal(r.ok, true, r.reason);
  const skew = r.value.startSkewMs;
  assert.ok(skew >= 25 && skew < 150, `skew ${skew} ms is the gap between the two spawns, not between the two launch calls returning`);
  assert.deepEqual(spawns.map(s => [s.exe, ...s.argv.slice(0, 6)]), [['taskset', '-c', '0-5', 'nice', '-n', '19', 'bwrap'], ['taskset', '-c', '8-13', 'nice', '-n', '19', 'bwrap']]);
  assert.equal(r.value.sessions.length, 2);
  assert.deepEqual(seenPlans.map(p => p.tickMs), [7, 7], 'the test seam (ctx.planOver) reaches every launch plan');
  for (const s of r.value.sessions) {
    assert.equal(typeof s.stampMs, 'number');
    assert.ok(existsSync(s.layout.dir));
    assert.deepEqual(readdirSync(s.layout.work).sort(), ['SPEC-v1.txt', 'test-v1.sh'], 'only the pinned v1 inputs');
    assert.equal(existsSync(path.join(s.layout.npm, 'i')), true, 'both snapshots are seeded into every session');
    assert.equal(existsSync(path.join(s.layout.nuget, 'a', 'f')), true);
    assert.equal(s.plan.cellId, `cell-${s.rid}`);
  }
});

test('Canaries_are_planted_before_the_launch_and_every_one_is_removed_after_it', async () => {
  const ctx = context();
  const projectDir = path.join(ctx.home, '.claude', 'projects', 'p1');
  let during = null;
  const deps = {
    spawn: () => ({}), maskCheckMs: 5, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [projectDir],
    launchSession: async plan => {
      during ??= { operator: readdirSync(projectDir).filter(f => f.startsWith('scratch-note-')).length, memory: readdirSync(path.join(projectDir, 'memory')).filter(f => f.startsWith('scratch-note-')).length };
      await sleep(10);
      return { ok: true, value: { meta: { rid: plan.layout.rid }, exit: { code: 0 } } };
    },
  };
  const r = await launchPhase({ ctx, items: items(ctx), deps });
  assert.equal(r.ok, true, r.reason);
  assert.equal(during.operator >= 1 && during.memory >= 1, true, `planted while the sessions ran: ${JSON.stringify(during)}`);
  assert.deepEqual(readdirSync(projectDir).filter(f => f.startsWith('scratch-note-')), [], 'every canary is gone');
  assert.deepEqual(readdirSync(path.join(projectDir, 'memory')).filter(f => f.startsWith('scratch-note-')), []);
  for (const s of r.value.sessions) assert.ok(s.canary.tokens.length >= 3, 'two operator folders and the partner config dir');
  const manifests = readdirSync(path.join(ctx.seriesDir, 'canaries'));
  assert.equal(manifests.length, 2, 'a manifest per session, kept as the record');
});

test('Canaries_are_unplanted_even_when_a_launch_throws', async () => {
  const ctx = context();
  const projectDir = path.join(ctx.home, '.claude', 'projects', 'p1');
  const deps = { spawn: () => ({}), maskCheckMs: 5, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [projectDir], launchSession: async () => { await sleep(10); throw new Error('boom'); } };
  await assert.rejects(launchPhase({ ctx, items: items(ctx), deps }), /boom/);
  assert.deepEqual(readdirSync(projectDir).filter(f => f.startsWith('scratch-note-')), []);
});

test('A_phase_that_cannot_claim_a_layout_fails_before_anything_is_started', async () => {
  const ctx = context();
  mkdirSync(path.join(ctx.sessionsRoot, items(ctx)[1].rid), { recursive: true });
  let launched = 0;
  const r = await launchPhase({ ctx, items: items(ctx), deps: { launchSession: async () => { launched += 1; }, listProjectDirs: () => [] } });
  assert.equal(r.ok, false);
  assert.match(r.reason, /already exists/);
  assert.equal(launched, 0);
  assert.equal(existsSync(path.join(ctx.sessionsRoot, items(ctx)[0].rid)), false, 'the layout claimed before the failure is released: nothing paid was started');
});

test('The_masked_spawn_stamps_once_per_call_and_prefixes_the_argv', () => {
  const stamps = [];
  const calls = [];
  const spawn = maskedSpawn({ mask: '6-7', stamp: () => stamps.push(1), d: { spawn: (exe, argv, opts) => { calls.push({ exe, argv, opts }); return 'child'; } } });
  assert.equal(spawn(['bwrap', 'a'], { E: '1' }), 'child');
  assert.deepEqual(calls[0].argv, ['-c', '6-7', 'nice', '-n', '19', 'bwrap', 'a']);
  assert.equal(calls[0].opts.detached, true);
  assert.deepEqual(calls[0].opts.env, { E: '1' });
  assert.equal(stamps.length, 1);
});

test('A_phase_whose_workspace_fails_after_the_claim_releases_its_own_layout_so_it_can_be_tried_again', async () => {
  const ctx = context();
  const wrong = { ...ctx, hashes: ctx.hashes.map(h => ({ ...h, sha256: '0'.repeat(64) })) };
  const first = await launchPhase({ ctx: wrong, items: items(ctx), deps: { launchSession: async () => { throw new Error('never launched'); }, listProjectDirs: () => [] } });
  assert.equal(first.ok, false);
  assert.match(first.reason, /do not match hashes\.txt/);
  for (const i of items(ctx)) assert.equal(existsSync(path.join(ctx.sessionsRoot, i.rid)), false, 'the layout this attempt claimed is removed: nothing paid had started');
  const again = await launchPhase({ ctx, items: items(ctx), deps: { spawn: () => ({}), maskCheckMs: 5, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [], launchSession: async plan => ({ ok: true, value: { meta: { rid: plan.layout.rid }, exit: { code: 0 } } }) } });
  assert.equal(again.ok, true, `the second attempt claims the same rids (no EEXIST): ${again.reason}`);
});
