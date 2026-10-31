// `init` (E3b.S1 step 1): everything the series is pinned to, and a dry run that writes nothing.
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { applyInit, describePlan, initPlan } from '../lib/bare-init.mjs';
import { treeHash } from '../lib/cache-snapshot.mjs';
import { appendLedger, readLedger } from '../lib/ledger.mjs';
import { markStopped, planSession, readRunMap, transition, writeRunMap } from '../lib/run-map.mjs';
import { spendGate, spendTotals } from '../lib/spend-gate.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const put = (p, t) => { mkdirSync(path.dirname(p), { recursive: true }); writeFileSync(p, t); };

function setup() {
  const inputsDir = tmpDir('inputs');
  for (const f of ['stacks.json', 'prices-2026-10-29.json', 'spend-prior.json', 'quiet.json', 'package-flags.json']) cpSync(path.join(spikeDir, 'inputs', f), path.join(inputsDir, f));
  writeFileSync(path.join(inputsDir, 'seed.sha256'), `${'b'.repeat(64)}\n`);
  const toolchainsDir = tmpDir('toolchains');
  const registered = {};
  for (const [stack, part] of [['csharp', 'nuget'], ['node', 'npm']]) {
    const stage = tmpDir(`stage-${stack}`);
    put(path.join(stage, part, 'f'), stack);
    const hash = treeHash(stage);
    cpSync(stage, path.join(toolchainsDir, `cache-${stack}-${hash.slice(0, 12)}`), { recursive: true });
    registered[stack] = { treeSha256: hash, parts: { [part]: treeHash(path.join(stage, part)) }, sizeBytes: 1 };
  }
  writeFileSync(path.join(inputsDir, 'cache-snapshots.json'), JSON.stringify(registered));
  const versionsDir = tmpDir('versions');
  put(path.join(versionsDir, '2.1.310'), '#!/bin/sh\necho fake claude\n');
  const seriesRoot = tmpDir('rsd');
  return { inputsDir, toolchainsDir, versionsDir, seriesRoot, seriesId: 's-20261030-ab12', facts: { preregTag: 'prereg/minigit-bare-20261030', preregCommit: 'c'.repeat(40), harnessIdentity: 'a'.repeat(64) } };
}

test('Init_pins_the_cli_copy_and_dry_run_writes_nothing', () => {
  const s = setup();
  const planned = initPlan(s);
  assert.equal(planned.ok, true, planned.reason);
  const lines = describePlan(planned.value).join('\n');
  assert.match(lines, /pinned CLI 2\.1\.310/);
  const priorCount = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'spend-prior.json'), 'utf8')).length; // the registered list, not a number typed here
  assert.match(lines, new RegExp(`ledger seeded with ${priorCount} prior line`));
  assert.deepEqual(readdirSync(s.seriesRoot), [], 'the dry run (initPlan + describePlan) writes no file');
  assert.equal(existsSync(path.join(s.toolchainsDir, 'claude-2.1.310')), false);

  const done = applyInit({ plan: planned.value, spin: { medianMs: 20, spreadMs: 1 } });
  assert.equal(done.ok, true, done.reason);
  const copy = path.join(s.toolchainsDir, 'claude-2.1.310');
  assert.equal(statSync(copy).mode & 0o777, 0o555, 'the pinned copy is read-only and executable');
  assert.equal(readFileSync(copy, 'utf8'), readFileSync(path.join(s.versionsDir, '2.1.310'), 'utf8'));
  const map = readRunMap(path.join(s.seriesRoot, s.seriesId, 'run-map.json')).value;
  assert.deepEqual([map.seriesId, map.seedSha256, map.pins.claude, map.spin.medianMs, map.stopped], ['s-20261030-ab12', 'b'.repeat(64), '2.1.310', 20, null]);
  assert.equal(map.claude.sha256.length, 64);
  for (const [field, file] of [['quietSha256', 'quiet.json'], ['packageFlagsSha256', 'package-flags.json']]) assert.equal(map[field], createHash('sha256').update(readFileSync(path.join(s.inputsDir, file))).digest('hex'), `${field} is pinned at init: the thresholds that applied are named by the run-map`);
  assert.deepEqual(Object.keys(map.snapshots).sort(), ['csharp', 'node']);
  assert.deepEqual([map.caps.capUsd, map.bareBudgetUsd, map.ceilingUsd], [6, 40, 500]);
  const ledger = readLedger(path.join(s.seriesRoot, s.seriesId, 'ledger.ndjson'));
  assert.equal(ledger.length, priorCount);
  assert.ok(ledger.some(l => l.approx === true), 'an approximate prior figure stays marked in the ledger');
  assert.ok(ledger.every(l => l.kind === 'prior'));
  for (const d of ['s', 'pairs', 'canaries']) assert.ok(existsSync(path.join(s.seriesRoot, s.seriesId, d)));
  assert.equal(applyInit({ plan: planned.value, spin: null }).ok, false, 'a series is initialised once');
});

test('Init_refuses_what_is_missing_or_does_not_match_naming_it', () => {
  const s = setup();
  assert.match(initPlan({ ...s, seriesId: 'series-1' }).reason, /s-yyyymmdd/);
  assert.match(initPlan({ ...s, versionsDir: tmpDir('empty') }).reason, /auto-update/);
  writeFileSync(path.join(s.inputsDir, 'cache-snapshots.json'), JSON.stringify({ csharp: { treeSha256: 'd'.repeat(64), parts: {} }, node: { treeSha256: 'e'.repeat(64), parts: {} } }));
  assert.match(initPlan(s).reason, /snapshot directory/);
  const t = setup();
  writeFileSync(path.join(t.toolchainsDir, readdirSync(t.toolchainsDir).find(n => n.startsWith('cache-node-')), 'npm', 'f'), 'tampered');
  assert.match(initPlan(t).reason, /no longer hashes/);
  const u = setup();
  cpSync(path.join(u.versionsDir, '2.1.310'), path.join(u.toolchainsDir, 'claude-2.1.310'));
  writeFileSync(path.join(u.toolchainsDir, 'claude-2.1.310'), 'not the pinned cli');
  const planned = initPlan(u);
  assert.match(applyInit({ plan: planned.value, spin: null }).reason, /not the pinned CLI/);
  const v = setup();
  assert.match(initPlan({ ...v, inputsDir: tmpDir('bare-inputs') }).reason, /stacks\.json does not exist/, 'an inputs dir with no stacks.json is a refusal, never a default');
});

// ---- spend across series (review N12) --------------------------------------------------------------------------------------------

/** An earlier series initialised in the same runs root, with the spend lines it earned. */
function earlierSeries(s, id, lines) {
  const planned = initPlan({ ...s, seriesId: id });
  assert.equal(planned.ok, true, planned.reason);
  assert.equal(applyInit({ plan: planned.value, spin: { medianMs: 20, spreadMs: 1 } }).ok, true);
  for (const l of lines) appendLedger(path.join(s.seriesRoot, id, 'ledger.ndjson'), l);
}

test('Init_of_a_new_series_seeds_the_spend_of_every_earlier_series_so_the_40_and_the_500_hold_across_series', () => {
  const s = setup();
  earlierSeries(s, 's-20261029-aaaa', [{ kind: 'aa', usd: 11.5, what: 'completed', rid: 'r1' }, { kind: 'aa', usd: 10, what: 'completed', rid: 'r2' }, { event: 'accept-harness', to: 'x' }]);
  earlierSeries(s, 's-20261030-bbbb', [{ kind: 'data', usd: 8, what: 'completed', rid: 'r3' }, { kind: 'void', usd: 2, what: 'void: upstream', rid: 'r4' }, { kind: 'rehearsal', usd: 0, what: 'completed', rid: 'r5' }]);
  mkdirSync(path.join(s.seriesRoot, 'seeds'), { recursive: true });
  mkdirSync(path.join(s.seriesRoot, 'toolchains'), { recursive: true }); // folders of the runs root that are not series
  const planned = initPlan({ ...s, seriesId: 's-20261031-cccc' });
  assert.equal(planned.ok, true, planned.reason);
  applyInit({ plan: planned.value, spin: { medianMs: 20, spreadMs: 1 } });
  const ledger = readLedger(path.join(s.seriesRoot, 's-20261031-cccc', 'ledger.ndjson'));
  const carried = ledger.filter(l => l.fromSeries !== undefined);
  assert.deepEqual(carried.map(l => [l.fromSeries, l.rid, l.kind, l.usd]), [['s-20261029-aaaa', 'r1', 'aa', 11.5], ['s-20261029-aaaa', 'r2', 'aa', 10], ['s-20261030-bbbb', 'r3', 'data', 8], ['s-20261030-bbbb', 'r4', 'void', 2], ['s-20261030-bbbb', 'r5', 'rehearsal', 0]], 'every earlier spend line, its kind kept, tagged with its series; no event line');
  assert.equal(ledger.filter(l => l.kind === 'prior').length, JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'spend-prior.json'), 'utf8')).length, 'the registered prior spend is seeded once, not once per series');
  const totals = spendTotals(ledger);
  assert.ok(Math.abs(totals.bareUsd - 31.5) < 1e-9, `the earlier series' bare spend counts in the $40: ${totals.bareUsd}`);
  const gate = spendGate({ ledger, liveUsd: 0, caps: [6, 6], ceilingUsd: 500, bareBudgetUsd: 40 });
  assert.equal(gate.stop, true, 'a new series cannot spend the $40 again');
  assert.deepEqual(gate.stoppedBy, ['the bare budget']);
  const first = setup();
  const fresh = initPlan({ ...first, seriesId: 's-20261031-cccc' });
  assert.equal(spendGate({ ledger: fresh.value.prior, liveUsd: 0, caps: [6, 6], ceilingUsd: 500, bareBudgetUsd: 40 }).stop, false, 'control: with no earlier series the gate lets a pair start');
  assert.match(describePlan(planned.value).join('\n'), /5 spend line\(s\) of 2 earlier series/);
});

test('Init_refuses_when_an_earlier_series_ledger_is_missing_or_unreadable_naming_it', () => {
  const s = setup();
  earlierSeries(s, 's-20261029-aaaa', [{ kind: 'aa', usd: 1, what: 'completed', rid: 'r1' }]);
  const file = path.join(s.seriesRoot, 's-20261029-aaaa', 'ledger.ndjson');
  writeFileSync(file, `${readFileSync(file, 'utf8')}{ not json\n`);
  const corrupt = initPlan({ ...s, seriesId: 's-20261031-cccc' });
  assert.equal(corrupt.ok, false);
  assert.match(corrupt.reason, /ledger of the earlier series s-20261029-aaaa is unreadable/);
  const t = setup();
  mkdirSync(path.join(t.seriesRoot, 's-20261029-dddd'), { recursive: true });
  const missing = initPlan({ ...t, seriesId: 's-20261031-cccc' });
  assert.equal(missing.ok, false);
  assert.match(missing.reason, /s-20261029-dddd.*no ledger/s);
  const u = setup();
  earlierSeries(u, 's-20261029-aaaa', [{ kind: 'mystery', usd: 1, what: 'x', rid: 'r1' }]);
  assert.match(initPlan({ ...u, seriesId: 's-20261031-cccc' }).reason, /s-20261029-aaaa.*kind 'mystery'/s, 'a line of a kind the gate does not know cannot be counted');
});

test('A_line_an_earlier_series_carried_is_counted_once_and_kept_when_its_origin_series_is_gone', () => {
  const s = setup();
  earlierSeries(s, 's-20261029-aaaa', [{ kind: 'aa', usd: 3, what: 'completed', rid: 'r1' }]);
  earlierSeries(s, 's-20261030-bbbb', [{ kind: 'data', usd: 2, what: 'completed', rid: 'r2' }]); // its ledger already holds aaaa's r1 as a copy
  const both = initPlan({ ...s, seriesId: 's-20261031-cccc' }).value.prior.filter(l => l.kind !== 'prior');
  assert.deepEqual(both.map(l => [l.fromSeries, l.rid, l.usd]), [['s-20261029-aaaa', 'r1', 3], ['s-20261030-bbbb', 'r2', 2]], 'r1 once: from its own series, not again as bbbb\'s copy');
  rmSync(path.join(s.seriesRoot, 's-20261029-aaaa'), { recursive: true, force: true });
  const gone = initPlan({ ...s, seriesId: 's-20261031-cccc' }).value.prior.filter(l => l.kind !== 'prior');
  assert.deepEqual(gone.map(l => [l.fromSeries, l.rid, l.usd]), [['s-20261029-aaaa', 'r1', 3], ['s-20261030-bbbb', 'r2', 2]], 'the copy keeps the spend when the origin directory was removed');
});

// ---- an earlier series that is not settled blocks a new one (review MINOR-5) -------------------------------------------------------------

/** Change the run-map of an earlier series with `edit(map) -> map`. */
function editMap(s, id, edit) {
  const file = path.join(s.seriesRoot, id, 'run-map.json');
  writeRunMap(file, edit(readRunMap(file).value));
}
const sessionOf = (map, rid, status, patch = {}) => {
  let m = planSession(map, { rid, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5' }).value;
  if (status !== 'planned') m = transition(m, rid, 'launching').value;
  return status === 'ran' ? transition(m, rid, 'ran', { class: 'completed', data: true, usd: 1, ...patch }).value : m;
};

test('Init_refuses_while_an_earlier_series_holds_a_launching_session_or_a_waiting_stop_naming_the_resume_command', () => {
  const s = setup();
  earlierSeries(s, 's-20261029-aaaa', [{ kind: 'aa', usd: 1, what: 'completed', rid: 'r1' }]);
  const dir = path.join(s.seriesRoot, 's-20261029-aaaa');
  const fresh = () => initPlan({ ...s, seriesId: 's-20261031-cccc' });
  assert.equal(fresh().ok, true, 'control: an earlier series with nothing open lets a new one start');
  editMap(s, 's-20261029-aaaa', m => sessionOf(m, 'aaaaaa', 'launching'));
  const launching = fresh();
  assert.equal(launching.ok, false);
  assert.match(launching.reason, /s-20261029-aaaa/);
  assert.match(launching.reason, /aaaaaa/);
  assert.ok(launching.reason.includes(`resume --series ${dir} --settle-only`), `the sentence names the command to settle it: ${launching.reason}`);
  editMap(s, 's-20261029-aaaa', m => sessionOf(markStopped({ ...m, sessions: {} }, { rule: 'stop: auth', reason: '{}', at: 't' }), 'bbbbbb', 'launching'));
  assert.equal(fresh().ok, false, 'a stopped series that still holds a launching session is not settled either: its spend is not in its ledger');
  editMap(s, 's-20261029-aaaa', m => ({ ...sessionOf({ ...m, sessions: {}, stopped: null }, 'cccccc', 'ran', { stopRules: [{ rule: 'T5 unchecked', detail: 'no broker ledger' }] }) }));
  const waiting = fresh();
  assert.equal(waiting.ok, false, 'a stop an ended session carries and nobody recorded is open work');
  assert.match(waiting.reason, /T5 unchecked/);
  assert.ok(waiting.reason.includes(`resume --series ${dir}`) && !waiting.reason.includes('--settle-only'), 'a plain resume records the stop and launches nothing');
  editMap(s, 's-20261029-aaaa', m => markStopped(m, { rule: 'T5 unchecked', reason: '{}', at: 't' }));
  assert.equal(fresh().ok, true, 'the stop is recorded and nothing is launching: the earlier series is settled and a new series may start');
});
