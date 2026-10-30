// Collecting a session back from its directory and classifying how it ended (E3b.S1 steps 5 and 7), over the bare-smoke fixture laid out as a live session dir.
import assert from 'node:assert/strict';
import { appendFileSync, copyFileSync, cpSync, mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { collectSession } from '../lib/bare-collect.mjs';
import { layoutPaths } from '../lib/sandbox.mjs';
import { loadSessionDir } from '../lib/session-load.mjs';
import { STAGED } from '../lib/smoke-load.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
const REAL = 'sk-ant-oat01-REALREALREALREALREAL0123456789';
const DUMMY = 'sk-ant-oat01-dummy-abc123def';
const PLAN = { token: REAL, dummy: DUMMY };

/** The flat fixture copied to where a live session keeps each file. */
function sessionFromFixture() {
  const layout = layoutPaths(tmpDir('series'), 'abc123');
  for (const [rel, flat] of Object.entries(STAGED)) {
    try {
      mkdirSync(path.dirname(path.join(layout.dir, rel)), { recursive: true });
      copyFileSync(path.join(FIXTURE, flat), path.join(layout.dir, rel));
    } catch (e) { if (e.code !== 'ENOENT') throw e; }
  }
  cpSync(path.join(FIXTURE, 'transcripts'), layout.transcripts, { recursive: true });
  mkdirSync(layout.work, { recursive: true });
  return layout;
}
const ctx = () => { const r = crosscheckPolicyFrom(spikeDir, {}); assert.equal(r.ok, true, r.reason); return r.value; };
const baselineOf = layout => { const rec = aliasRecord({ events: loadSessionDir(layout).value.events }); return { aliases: rec.value.aliases, internal: rec.value.internal }; };
const collect = (layout, over = {}) => {
  const { table, policy } = ctx();
  const r = collectSession({ layout, plan: PLAN, launched: null, aliasBaseline: baselineOf(layout), table, policy, ...over });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};

test('A_finished_session_dir_is_collected_and_classified_completed_with_its_spend', () => {
  const r = collect(sessionFromFixture());
  assert.equal(r.outcome.class, 'completed');
  assert.equal(r.outcome.data, true);
  assert.ok(Math.abs(r.usd - 0.1148446) < 1e-6, `spend ${r.usd}`);
  assert.equal(r.cost.known, true);
  assert.equal(r.scan.hits, 0);
});

test('A_canary_token_in_the_evidence_is_a_contaminated_void', () => {
  const layout = sessionFromFixture();
  const canary = 'c0ffee0123456789abcdef0123456789';
  appendFileSync(layout.files.stream, `${JSON.stringify({ type: 'user', message: { content: [{ type: 'tool_result', content: `read ${canary}` }] } })}\n`);
  const r = collect(layout, { canaryTokens: [canary] });
  assert.equal(r.outcome.class, 'void: contaminated');
  assert.equal(r.outcome.data, false);
  assert.equal(collect(layout).outcome.class, 'completed', 'control: the same session without the canary registered');
});

test('The_real_token_anywhere_in_the_session_dir_stops_the_series', () => {
  const layout = sessionFromFixture();
  appendFileSync(layout.files.stderr, `${REAL}\n`);
  assert.equal(collect(layout).outcome.class, 'stop: security');
});

test('The_dummy_credential_is_not_a_token_hit', () => {
  const layout = sessionFromFixture();
  appendFileSync(layout.files.stream, `${JSON.stringify({ type: 'user', message: { content: [{ type: 'tool_result', content: `env ${DUMMY}` }] } })}\n`);
  assert.equal(collect(layout).outcome.class, 'completed');
});

test('An_alias_that_now_resolves_to_another_id_stops_the_series', () => {
  const layout = sessionFromFixture();
  const baseline = baselineOf(layout);
  const drifted = { ...baseline, aliases: Object.fromEntries(Object.keys(baseline.aliases).map(a => [a, 'claude-other-model'])) };
  assert.ok(Object.keys(drifted.aliases).length > 0, 'the fixture used at least one alias');
  assert.equal(collect(layout, { aliasBaseline: drifted }).outcome.class, 'stop: alias drift');
});

test('A_failed_launch_that_left_paid_work_is_a_disposed_harness_void', () => {
  const layout = sessionFromFixture();
  rmSync(layout.files.meta);
  rmSync(layout.files.stream);
  const r = collect(layout, { launched: { ok: false, reason: 'the launch did not complete' } });
  assert.equal(r.outcome.class, 'void: harness (disposed)');
  assert.ok(r.usd > 0, 'its settled spend is still charged');
});

test('An_alias_that_resolves_to_two_ids_inside_one_session_stops_the_series', () => {
  const layout = sessionFromFixture();
  const launch = (id, resolved) => [
    { type: 'assistant', timestamp: '2026-10-30T10:00:00Z', message: { id: `m${id}`, content: [{ type: 'tool_use', id: `t${id}`, name: 'Agent', input: { model: 'sonnet', description: 'x' } }] } },
    { type: 'user', timestamp: '2026-10-30T10:00:05Z', tool_use_result: { resolvedModel: resolved, agentId: `a${id}` }, message: { content: [{ type: 'tool_result', tool_use_id: `t${id}`, content: 'done' }] } },
  ];
  for (const ev of [...launch(1, 'claude-sonnet-5-5'), ...launch(2, 'claude-sonnet-other')]) appendFileSync(layout.files.stream, `${JSON.stringify(ev)}\n`);
  const r = collectSession({ layout, plan: PLAN, launched: null, aliasBaseline: { aliases: {}, internal: [] }, ...ctx() });
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.outcome.class, 'stop: alias drift');
  assert.match(r.value.outcome.detail.reasons.join(' '), /two ids/);
});
