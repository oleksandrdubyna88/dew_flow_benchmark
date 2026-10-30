// Collecting a session back from its directory and classifying how it ended (E3b.S1 steps 5 and 7), over the bare-smoke fixture laid out as a live session dir.
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { appendFileSync, copyFileSync, cpSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { collectSession } from '../lib/bare-collect.mjs';
import { layoutPaths } from '../lib/sandbox.mjs';
import { STAGED, loadSessionDir } from '../lib/smoke-load.mjs';

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
const baselineOf = layout => { const rec = aliasRecord({ events: loadSessionDir(layout.dir).value.events }); return { aliases: rec.value.aliases, internal: rec.value.internal }; };
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
  const fixtureUsd = JSON.parse(readFileSync(path.join(FIXTURE, 'meta.json'), 'utf8')).broker.spentUsd;
  assert.ok(Math.abs(r.usd - fixtureUsd) < 1e-6, `spend ${r.usd} against the fixture's broker ledger ${fixtureUsd}`);
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

test('A_live_session_dir_with_files_missing_loads_with_empty_lists_never_a_failure', () => {
  const layout = layoutPaths(tmpDir('series'), 'cut123');
  mkdirSync(layout.dir, { recursive: true });
  writeFileSync(path.join(layout.dir, 'stream.ndjson'), `${JSON.stringify({ type: 'system', subtype: 'init' })}\n`);
  const r = loadSessionDir(layout.dir);
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.events.length, 1);
  assert.deepEqual([r.value.brokerLines, r.value.audit, r.value.transcripts], [[], [], []]);
  assert.equal(r.value.meta, null, 'an absent meta.json is null, not an invented value');
});

test('A_link_in_a_session_dir_is_refused_by_the_one_loader_not_followed', () => {
  const layout = sessionFromFixture();
  rmSync(layout.files.brokerLog);
  symlinkSync('/etc/hostname', layout.files.brokerLog);
  const r = loadSessionDir(layout.dir);
  assert.equal(r.ok, false);
  assert.match(r.reason, /broker\.ndjson: symlink|not plain regular files/);
});

test('A_canary_or_a_credential_in_an_evidence_file_over_16_MiB_is_found_not_skipped', () => {
  const canary = 'c0ffee0123456789abcdef0123456789';
  const layout = sessionFromFixture();
  // 20 MiB of filler with the canary in its last bytes: the old reader skipped every evidence file over 16 MiB
  const big = path.join(layout.work, 'big.log');
  writeFileSync(big, Buffer.alloc(20 * 1024 * 1024, '.'));
  appendFileSync(big, canary);
  const r = collect(layout, { canaryTokens: [canary] });
  assert.equal(r.outcome.class, 'void: contaminated', 'the canary is in a file an agent would choose to hide it in');
  const none = collect(layout);
  assert.equal(none.outcome.class, 'completed', 'control: the same big file without the canary registered');
  const credential = sessionFromFixture();
  writeFileSync(path.join(credential.work, 'big.log'), Buffer.alloc(20 * 1024 * 1024, '.'));
  appendFileSync(path.join(credential.work, 'big.log'), `ghp_${'a1B2'.repeat(9)}`);
  assert.ok(['void: contaminated', 'stop: security'].includes(collect(credential).outcome.class), 'a GitHub token shape in a big file is a hit too');
});

test('A_session_with_no_cost_evidence_of_any_kind_is_flagged_not_silently_charged_zero', () => {
  const empty = layoutPaths(tmpDir('series'), 'abc123');
  mkdirSync(empty.dir, { recursive: true });
  const r = collect(empty, { aliasBaseline: { aliases: {}, internal: {} } });
  assert.equal(r.usd, 0);
  assert.equal(r.costCaptured, false, 'no envelope, no broker log: the 0 is a gap in the evidence, not a measurement');
  assert.ok(r.outcome.flags.includes('cost not captured'), r.outcome.flags.join());
  const logged = layoutPaths(tmpDir('series'), 'abc123');
  mkdirSync(logged.dir, { recursive: true });
  writeFileSync(logged.files.brokerLog, '');
  const known = collect(logged, { aliasBaseline: { aliases: {}, internal: {} } });
  assert.equal(known.costCaptured, true, 'an empty broker log is a ledger that saw no request: a known zero');
  assert.ok(!known.outcome.flags.includes('cost not captured'));
  assert.equal(collect(sessionFromFixture()).costCaptured, true);
});

test('A_T2_breach_marks_the_session_otel_incomplete_as_a_flag', () => {
  const layout = sessionFromFixture();
  const { table, policy } = ctx();
  const dropped = path.join(layout.otel, 'otel-raw.ndjson');
  const lines = readFileSync(dropped, 'utf8').split('\n').filter(Boolean);
  writeFileSync(dropped, `${lines.slice(0, Math.ceil(lines.length / 2)).join('\n')}\n`); // half of the OTel records never arrived
  const r = collectSession({ layout, plan: PLAN, launched: null, aliasBaseline: baselineOf(layout), table, policy });
  assert.equal(r.ok, true, r.reason);
  assert.notEqual(r.value.views.c.t2.status, 'pass', 'half of the OTel records are gone: T2 does not pass');
  assert.ok(r.value.outcome.flags.includes('otel incomplete'));
  assert.equal(r.value.outcome.class, 'completed', 'a flag never changes the class');
  assert.ok(!collect(sessionFromFixture()).outcome.flags.includes('otel incomplete'), 'control: the whole record set has no such flag');
});

test('Every_evidence_file_is_read_once_for_the_session_scan_the_canary_and_the_real_token_whole_or_in_windows', () => {
  const canary = 'c0ffee0123456789abcdef0123456789';
  for (const [mode, scanLimits] of [['whole', undefined], ['windows', { maxScanBytes: 512, windowBytes: 4096, overlapBytes: 1024, maxStreamBytes: 1024 * 1024 * 1024 }]]) {
    const layout = sessionFromFixture();
    const reads = [];
    const r = collect(layout, { canaryTokens: [canary], scanLimits, scanHooks: { beforeRead: rel => reads.push(rel) } });
    const counts = Object.entries(Object.groupBy(reads, rel => rel)).map(([rel, list]) => [rel, list.length]);
    assert.ok(counts.length > 10, `${mode}: ${counts.length} files were read`);
    assert.deepEqual(counts.filter(([, n]) => n !== 1), [], `${mode}: a file read more than once is a second pass over the same bytes`);
    assert.equal(r.scan.scanned, counts.length, `${mode}: the pass covered every file the session scan counts`);
    assert.ok(counts.some(([rel]) => rel === 'stream.ndjson') && counts.some(([rel]) => rel.startsWith('transcripts')), `${mode}: the evidence files are among them`);
  }
});

test('A_canary_and_a_credential_shape_in_evidence_files_are_still_found_by_the_one_pass_whole_or_in_windows', () => {
  const canary = 'c0ffee0123456789abcdef0123456789';
  const windows = { maxScanBytes: 512, windowBytes: 4096, overlapBytes: 1024, maxStreamBytes: 1024 * 1024 * 1024 };
  for (const scanLimits of [undefined, windows]) {
    const withCanary = sessionFromFixture();
    appendFileSync(withCanary.files.stream, `${JSON.stringify({ type: 'user', message: { content: [{ type: 'tool_result', content: `read ${canary}` }] } })}\n`);
    assert.equal(collect(withCanary, { canaryTokens: [canary], scanLimits }).outcome.class, 'void: contaminated', `a canary in the stream (${scanLimits ? 'windows' : 'whole'})`);
    const shape = sessionFromFixture();
    writeFileSync(path.join(shape.work, 'note.txt'), `ghp_${'a1B2'.repeat(9)}\n`);
    assert.notEqual(collect(shape, { scanLimits }).outcome.class, 'completed', `a credential shape in box/work is found (${scanLimits ? 'windows' : 'whole'})`);
    assert.equal(collect(sessionFromFixture(), { canaryTokens: [canary], scanLimits }).outcome.class, 'completed', 'control: no canary, no shape');
  }
});
