// The scenarios that need the step 0 capture. Each SKIPS with the reason 'awaits the E1.S3 step 0 capture' until
// test/fixtures/real/claude-2.1.310-capture/ exists, and none of them is satisfied by anything invented: they read that directory only.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { claudeArgv } from '../capture/capture-plan.mjs';
import { loadCapture } from '../capture/capture-files.mjs';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { computeVerdicts, decisionsOf, renderVerdicts, verdictBlockOf } from '../capture/verdicts.mjs';
import { viewsOf } from '../capture/verdict-rules.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { crosscheck } from '../lib/crosscheck.mjs';
import { OTEL_STAMP_MARKS } from '../lib/otel-decode.mjs';
import { startReceiver } from '../lib/otel-receiver.mjs';
import { asTheCliBills, priceFor } from '../lib/prices.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { CAPTURE_DIR, captureGate, parseLines } from './synth.mjs';

const loaded = () => {
  const capture = loadCapture(CAPTURE_DIR);
  assert.equal(capture.ok, true, capture.reason);
  const inputs = crosscheckPolicyFrom(spikeDir);
  assert.equal(inputs.ok, true, inputs.reason);
  return { capture: capture.value, ...inputs.value, views: viewsOf(capture.value, inputs.value) };
};
const captureMd = () => readFileSync(path.join(CAPTURE_DIR, 'CAPTURE.md'), 'utf8');

test('The_real_capture_decodes_and_names_every_attribute_the_plan_reads', t => {
  if (!captureGate(t)) return;
  const { capture, views } = loaded();
  assert.ok(capture.sink.length > 0);
  assert.ok(capture.sink.every(l => /^application\/json/i.test(l.contentType)), 'every sink line is JSON');
  assert.ok(views.decoded.every(x => x.d.ok), 'every logs payload decodes');
  assert.ok(views.O.requests.length >= 3, `api_request records: ${views.O.requests.length} (main turns plus two children)`);
  assert.deepEqual(views.O.requests.flatMap(r => r.missing), [], 'no named attribute is missing; per the capture\'s amendment this list is the one it recorded');
});

test('The_stamp_verdict_is_a_named_constant_and_matches_the_capture', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  assert.equal(decisionsOf(computeVerdicts(capture, { table, policy })).stampMarks, OTEL_STAMP_MARKS);
});

test('The_policy_file_carries_what_the_capture_decided', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  const d = decisionsOf(computeVerdicts(capture, { table, policy }));
  assert.equal(policy.join, d.join);
  assert.equal(policy.t3OutputSource, d.t3OutputSource, 'an undecided A-T2 (null) must be settled by hand before the series');
});

test('The_receiver_replays_the_capture_and_sums_cost_live', async t => {
  if (!captureGate(t)) return;
  const { capture, views } = loaded();
  const dir = tmpDir('replay');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  for (const line of capture.sink.filter(l => l.path === '/v1/logs')) {
    const res = await fetch(`http://127.0.0.1:${rx.port}${line.path}`, { method: 'POST', headers: { 'content-type': line.contentType }, body: line.body });
    assert.equal(res.status, 200);
  }
  assert.equal(rx.server.address().address, '127.0.0.1');
  const expected = views.O.requests.reduce((a, r) => a + (r.costUsd ?? 0), 0);
  const live = rx.liveSum();
  assert.ok(Math.abs(live.usd - expected) < 1e-9);
  assert.equal(live.requests, views.O.requests.length);
  assert.equal(parseLines(readFileSync(path.join(dir, 'otel.ndjson'), 'utf8')).length, views.records.length);
});

test('T4_holds_on_the_real_capture', t => {
  if (!captureGate(t)) return;
  const { table, policy, views } = loaded();
  const c = crosscheck({ E: views.E, O: views.O, T: views.T, table, policy, cliVersion: views.cliVersion });
  assert.equal(c.t4.status, 'pass', c.t4.detail);
  assert.equal(c.t4.gap, 0, 'token counts agree exactly');
  assert.equal(c.t3.status, 'pass', `${c.t3.detail} ${c.t3.why ?? ''}`);
  assert.equal(c.t1.status, 'pass');
});

test('The_real_capture_resolves_every_alias_it_used_to_an_exact_id', t => {
  if (!captureGate(t)) return;
  const { capture, table } = loaded();
  const rec = aliasRecord({ events: capture.events });
  assert.equal(rec.ok, true, rec.reason);
  const init = capture.events.find(e => e.type === 'system' && e.subtype === 'init');
  assert.equal(rec.value.aliases.opus, init.model);
  assert.ok(rec.value.aliases.sonnet, 'the sonnet child resolved an id');
  if (!rec.value.aliases.fable) assert.match(captureMd(), /A-M1[^\n]*fable not exercised/, 'a missing fable alias must be stated as not exercised in CAPTURE.md');
  for (const id of Object.values(rec.value.aliases)) assert.equal(priceFor(table, id).known, true, `${id} is in the price table`);
});

test('Every_assumption_has_a_verdict_and_none_is_blank_and_CAPTURE_md_is_the_computed_table', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  const rows = computeVerdicts(capture, { table, policy });
  assert.equal(rows.length, 15);
  assert.ok(rows.every(r => ['holds', 'refuted', 'not exercised'].includes(r.verdict) && r.observed.length > 0));
  assert.equal(verdictBlockOf(captureMd()), renderVerdicts(rows), 'CAPTURE.md equals renderVerdicts byte for byte');
});

test('The_capture_ran_the_argv_the_plan_prescribes_and_stayed_inside_its_budget', t => {
  if (!captureGate(t)) return;
  const { capture } = loaded();
  assert.deepEqual(capture.argv.argv, claudeArgv('/cap'));
  assert.ok(capture.meta.costUsd !== null && capture.meta.costUsd <= 1.5, `cost ${capture.meta.costUsd}`);
  assert.match(capture.meta.cliVersion, /^2\.1\.310$/);
});

test('A_O5_is_evaluated_on_the_real_capture_and_never_skipped', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  const row = computeVerdicts(capture, { table, policy }).find(r => r.id === 'A-O5');
  assert.notEqual(row.verdict, 'not exercised', row.observed);
  assert.match(row.observed, /subagent record/);
});

test('For_2_1_310_the_cli_billed_view_equals_published_on_the_real_capture', t => {
  if (!captureGate(t)) return;
  const { table, views, capture } = loaded();
  assert.equal(views.cliVersion, '2.1.310');
  assert.equal(views.cliVersion, capture.meta.cliVersion);
  const sonnet = (tb, v) => priceFor(asTheCliBills(tb, v), 'claude-sonnet-5-5').value;
  assert.deepEqual(sonnet(table, '2.1.310'), priceFor(table, 'claude-sonnet-5-5').value);
  assert.notDeepEqual(sonnet(table, '2.1.289'), priceFor(table, 'claude-sonnet-5-5').value);
  const row = computeVerdicts(capture, { table, policy: loaded().policy }).find(r => r.id === 'A-P1');
  assert.equal(row.verdict, 'holds', row.observed);
  assert.ok(views.E.perModel['claude-sonnet-5-5'].cacheRead > 0, 'the sonnet line reads from cache, so its rate matters in this capture');
});
