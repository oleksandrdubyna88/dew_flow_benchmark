// The bare runner's loop (E3b.S1): the schedule driven for real over the run-map, with a fake launch that lays the bare-smoke fixture into each session's directory
// (and variants of it) and a fake scorer. The gates, the barrier, the voids and the re-run, the stops, the spend gate, the scorer's halts and the resume are what is under test.
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { appendFileSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { firstStack, ridOf } from '../lib/bare-plan.mjs';
import { reclassifyByHand } from '../lib/bare-reclass.mjs';
import { EXIT, dirFacts, reconcile, runLoop } from '../lib/bare-series.mjs';
import { writeSessionRecord } from '../lib/bare-records-io.mjs';
import { readLedger } from '../lib/ledger.mjs';
import { layoutPaths } from '../lib/sandbox.mjs';
import { markStopped, openSeries, readRunMap, transition } from '../lib/run-map.mjs';
import { FIXTURE, SCORE, lay } from './bare-fixture.mjs';
import { SEED, context, sleep } from './bare-ctx.mjs';
import { spikeDir } from './helpers.mjs';

function harness(over = {}) {
  const base = context();
  const parsed = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(parsed.ok, true, parsed.reason);
  const events = [];
  const variants = over.variants ?? {};
  const launched = [];
  const ctx = {
    ...base, kind: 'data', pair: 1, seed: SEED, runMapFile: path.join(base.seriesDir, 'run-map.json'), ledgerFile: path.join(base.seriesDir, 'ledger.ndjson'), ceilingUsd: 500, bareBudgetUsd: 40,
    caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, table: parsed.value.table, policy: parsed.value.policy, handVerdictsFor: () => [], snapshotPackages: [], packageRules: [],
    harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', abortReason: () => 'SIGINT', abortExit: () => 130, spin: { medianMs: 20 }, ...over.ctx,
  };
  ctx.aliasBaseline = (() => { const lay0 = { files: {} }; void lay0; const rec = aliasRecord({ events: readFileSync(path.join(FIXTURE, 'stream.ndjson'), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) }); return { aliases: rec.value.aliases, internal: rec.value.internal }; })();
  const deps = {
    guard: async arg => { events.push(`guard paid=${arg.paid}`); return over.guard ? over.guard(arg) : { ok: true }; },
    retireStale: async () => { events.push('retire'); },
    preflight: async () => ({ ok: true }),
    quiet: async arg => { events.push(`quiet ${arg.phase}`); return over.quiet ? over.quiet(arg) : { ok: true, value: { quiet: true, flags: [] } }; },
    startSampler: () => ({ stop: async () => ({ samples: 3, flags: over.samplerFlags ?? [], error: null }) }),
    loadFlag: async () => false, now: Date.now,
    scoreSession: async arg => { events.push(`score ${path.basename(arg.session)}`); return over.score ? over.score(arg) : { kind: 'scored', score: SCORE }; },
    phase: {
      maskCheckMs: 1, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [path.join(ctx.home, '.claude', 'projects', 'p1')], spawn: () => ({}),
      launchSession: async (plan, d, opts) => {
        d.spawnBox(['bwrap'], {});
        const rid = plan.layout.rid;
        events.push(`launch ${plan.layout.rid}`);
        launched.push(rid);
        lay(plan.layout, variants[rid]);
        await sleep(5);
        const meta = JSON.parse(readFileSync(plan.layout.files.meta, 'utf8'));
        return { ok: true, value: { meta: opts?.signal?.aborted ? { ...meta, stopped: 'cancelled' } : meta, exit: { code: 0, signal: null } } };
      },
    },
  };
  const map = over.map ?? openSeries({ seriesId: 's-x', seedSha256: 'b'.repeat(64), harnessIdentity: ctx.harnessIdentity, preregTag: ctx.preregTag, preregCommit: 'c'.repeat(40), pins: {}, stacksSha256: 'd', pricesSha256: 'e', snapshots: {}, caps: ctx.caps, bareBudgetUsd: 40, ceilingUsd: 500 });
  return { ctx, deps, map, events, launched };
}
const rid = (stack, phase, attempt, pair = 1) => ridOf({ seed: SEED, pair, stack, phase, attempt });

test('A_pair_of_data_sessions_runs_both_phases_with_the_barrier_between_and_records_everything', async () => {
  const t = harness();
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const order = t.events.filter(e => e.startsWith('launch') || e.startsWith('score')).map(e => e.split(' ')[0]);
  assert.deepEqual(order, ['launch', 'launch', 'score', 'score', 'launch', 'launch', 'score', 'score'], 'v1 pair, both scored, THEN the v2 pair, both scored');
  assert.deepEqual(t.events.filter(e => e.startsWith('guard')), ['guard paid=true', 'guard paid=true'], 'the guard runs before every phase');
  const map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(Object.keys(map.sessions).length, 4);
  assert.ok(Object.values(map.sessions).every(s => s.status === 'scored' && s.class === 'completed'));
  for (const s of Object.values(map.sessions)) assert.ok(existsSync(path.join(t.ctx.sessionsRoot, s.rid, 'record.json')), `record of ${s.rid}`);
  const pair = JSON.parse(readFileSync(path.join(t.ctx.seriesDir, 'pairs', 'p1.json'), 'utf8'));
  assert.equal(pair.pair, 1);
  assert.equal(pair.cells.csharp.costUsd.known, true);
  assert.equal(typeof pair.barrierAtMs, 'number');
  const ledger = readLedger(t.ctx.ledgerFile);
  assert.equal(ledger.length, 4);
  assert.ok(ledger.every(l => l.kind === 'data' && l.usd > 0));
  const v2 = JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid('csharp', 'v2', 1), 'record.json'), 'utf8'));
  assert.equal(v2.ids.phase, 'v2');
  assert.deepEqual(readdirSync(path.join(t.ctx.sessionsRoot, rid('csharp', 'v2', 1), 'box', 'work')).sort(), ['SPEC-v1.txt', 'SPEC-v2.txt', 'test-v1.sh', 'test-v2.sh'], 'the v2 workspace is the v1 tree plus the v2 inputs');
});

test('A_void_session_is_re_run_once_alone_and_its_cell_is_labelled_unpaired', async () => {
  const t = harness({ variants: { [rid('csharp', 'v1', 1)]: 'upstream' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.sessions[rid('csharp', 'v1', 1)].class, 'void: upstream');
  assert.equal(map.sessions[rid('csharp', 'v1', 2)].label, 'ran unpaired');
  assert.equal(map.sessions[rid('node', 'v2', 1)].label, 'ran unpaired', 'the partner\'s v2 ran alone, before the re-run');
  assert.deepEqual(t.launched.map(x => x), [t.launched[0], t.launched[1], rid('node', 'v2', 1), rid('csharp', 'v1', 2), rid('csharp', 'v2', 1)].map(x => x));
  const ledger = readLedger(t.ctx.ledgerFile);
  assert.equal(ledger.filter(l => l.kind === 'void').length, 1, 'void spend is its own ledger kind');
  const pair = JSON.parse(readFileSync(path.join(t.ctx.seriesDir, 'pairs', 'p1.json'), 'utf8'));
  assert.equal(pair.cells.csharp.unpaired, true);
  assert.equal(pair.ratios.costUsd.without.known, false, 'the ratio without unpaired cells says why it is not given');
});

test('A_stop_class_ends_the_series_and_keeps_the_session', async () => {
  const t = harness({ variants: { [rid('node', 'v1', 1)]: 'auth' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.stop);
  const map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.stopped.rule, 'stop: auth');
  assert.ok(existsSync(path.join(t.ctx.sessionsRoot, rid('node', 'v1', 1), 'record.json')), 'the stopped session is kept, unclassified as data');
  assert.equal(t.events.filter(e => e.startsWith('launch')).length, 2, 'nothing launches after a stop');
});

test('The_guard_refuses_before_anything_is_launched', async () => {
  const t = harness({ guard: async () => ({ ok: false, reason: 'the tag is not on origin' }) });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.config);
  assert.match(r.message, /tag is not on origin/);
  assert.deepEqual(t.launched, []);
});

test('A_quiet_wait_that_gives_up_halts_resumably_with_the_verdict_and_launches_nothing', async () => {
  const t = harness({ quiet: async () => ({ ok: false, reason: 'the machine was not quiet for 30 min: host-cpu', verdict: { quiet: false } }) });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.halt);
  assert.deepEqual(t.launched, []);
  const map = readRunMap(t.ctx.runMapFile).value;
  assert.ok(Object.values(map.sessions).every(s => s.status === 'planned'), 'planned, never launched: resume starts them');
});

test('The_spend_gate_stops_a_phase_that_would_pass_the_bare_budget', async () => {
  const t = harness({ ctx: { bareBudgetUsd: 10 } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.stop);
  assert.match(r.message, /the bare budget/);
  assert.deepEqual(t.launched, []);
});

test('Quiet_and_sampler_flags_reach_the_session_records', async () => {
  const t = harness({ samplerFlags: ['foreign load'], quiet: async () => ({ ok: true, value: { quiet: false, flags: ['started not quiet'] } }) });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const rec = JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid('csharp', 'v1', 1), 'record.json'), 'utf8'));
  assert.ok(rec.flags.includes('foreign load') && rec.flags.includes('started not quiet'), rec.flags.join());
});

test('A_scorer_timeout_halts_resumably_and_resume_scores_without_launching_again', async () => {
  let calls = 0;
  const t = harness({ score: async () => (calls++ === 0 ? { kind: 'scorer-timeout', reason: 'killed at the outer limit' } : { kind: 'scored', score: SCORE }) });
  const first = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(first.exit, EXIT.halt);
  assert.match(first.message, /scorer-timeout/);
  const afterHalt = readRunMap(t.ctx.runMapFile).value;
  assert.equal(afterHalt.sessions[rid('csharp', 'v1', 1)].status === 'ran' || afterHalt.sessions[rid('node', 'v1', 1)].status === 'ran', true, 'the session stays ran');
  const launchesBefore = t.launched.length;
  const second = await runLoop({ ctx: t.ctx, deps: t.deps, map: afterHalt });
  assert.equal(second.exit, EXIT.done, second.message);
  assert.equal(t.launched.length - launchesBefore, 2, 'only the v2 pair launched after the resume: v1 was not run again');
});

test('A_scorer_that_fails_is_a_runner_failure_and_never_a_void', async () => {
  const t = harness({ score: async () => ({ kind: 'failed', reason: 'scorer failed: boom' }) });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.failed);
  const map = readRunMap(t.ctx.runMapFile).value;
  assert.ok(Object.values(map.sessions).every(s => s.class === 'completed'), 'no class changed for a scorer fault');
});

test('A_cancelled_phase_records_its_sessions_as_interrupted_harness_voids_charges_them_and_exits_130', async () => {
  const ctl = new AbortController();
  const t = harness();
  const original = t.deps.phase.launchSession;
  t.deps.phase.launchSession = async (plan, d, opts) => { ctl.abort(); return original(plan, d, opts); };
  const run = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map, signal: ctl.signal });
  assert.equal(run.exit, 130);
  const map = readRunMap(t.ctx.runMapFile).value;
  const first = Object.values(map.sessions);
  assert.equal(first.length, 2);
  assert.ok(first.every(s => s.class === 'void: harness (interrupted)' && s.data === false), first.map(s => s.class).join());
  assert.equal(readLedger(t.ctx.ledgerFile).length, 2, 'what was spent before the signal is charged');
  assert.equal(t.launched.length, 2, 'nothing launches after the abort');
});

test('A_start_skew_over_five_seconds_and_scoring_under_load_are_flagged_never_classes', async () => {
  const t = harness();
  let clock = 0;
  t.deps.phase.now = () => { clock += 6000; return clock; };
  t.deps.loadFlag = async () => true;
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const rec = JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid('csharp', 'v1', 1), 'record.json'), 'utf8'));
  assert.ok(rec.flags.includes('start skew'), rec.flags.join());
  assert.ok(rec.flags.includes('scored under load'), rec.flags.join());
  assert.equal(rec.class, 'completed', 'a flag never changes the class');
});

// ---- resume -----------------------------------------------------------------------------------------------------------------------

test('Resume_unplants_canaries_first_and_settles_a_cut_short_launch_by_what_its_files_say', async () => {
  const t = harness();
  const order = [];
  const projectDir = path.join(t.ctx.home, '.claude', 'projects', 'p1');
  mkdirSync(path.join(t.ctx.seriesDir, 'canaries'), { recursive: true });
  const leftover = path.join(projectDir, 'scratch-note-deadbeef.md');
  writeFileSync(leftover, 'token\n');
  writeFileSync(path.join(t.ctx.seriesDir, 'canaries', 'old.json'), JSON.stringify({ planted: [{ dir: projectDir, path: leftover, token: 'x', status: 'planted' }] }));
  t.deps.retireStale = async () => { order.push(existsSync(leftover) ? 'retire-with-canary' : 'retire-after-unplant'); };
  // a map mid-phase: v1 csharp launching with a finished launch (meta.json), v1 node launching with its dir gone
  const { planSession } = await import('../lib/run-map.mjs');
  let map = t.map;
  for (const stack of ['csharp', 'node']) map = planSession(map, { rid: rid(stack, 'v1', 1), pair: 1, stack, phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5', dummy: 'sk-ant-oat01-dummy-resume' }).value;
  for (const stack of ['csharp', 'node']) map = transition(map, rid(stack, 'v1', 1), 'launching').value;
  const { layoutPaths } = await import('../lib/sandbox.mjs');
  mkdirSync(path.join(t.ctx.sessionsRoot, rid('csharp', 'v1', 1)), { recursive: true });
  lay(layoutPaths(t.ctx.sessionsRoot, rid('csharp', 'v1', 1)), undefined);
  assert.equal(dirFacts(t.ctx, rid('csharp', 'v1', 1)).hasMeta, true);
  const r = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(order, ['retire-after-unplant'], 'every canary of the manifests is removed before retireStale runs');
  assert.equal(r.value.sessions[rid('csharp', 'v1', 1)].status, 'ran');
  assert.equal(r.value.sessions[rid('csharp', 'v1', 1)].class, 'completed', 'the finished launch is adopted, not re-run');
  assert.equal(r.value.sessions[rid('node', 'v1', 1)].status, 'planned', 'nothing was paid: planned again under the same rid');
  assert.equal(readLedger(t.ctx.ledgerFile).length, 1, 'only the adopted session is charged');
});

test('A_record_carries_the_harness_identity_that_ran_the_session_even_when_it_is_rebuilt_under_another', async () => {
  const t = harness();
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const entry = readRunMap(t.ctx.runMapFile).value.sessions[rid('csharp', 'v1', 1)];
  assert.equal(entry.run.harnessIdentity, t.ctx.harnessIdentity, 'the run-map holds the identity at the launch');
  const rebuilt = writeSessionRecord({ ctx: { ...t.ctx, harnessIdentity: 'b'.repeat(64) }, entry, score: null });
  assert.equal(rebuilt.ok, true, rebuilt.reason);
  assert.equal(rebuilt.value.run.harnessIdentity.value, t.ctx.harnessIdentity, 'a later harness (an accepted fix) does not rewrite what ran then');
});

test('Whether_the_v1_phase_started_quiet_is_kept_in_the_run_map_so_the_noisy_rule_for_v2_survives_a_resume', async () => {
  const seen = [];
  const t = harness({ quiet: async arg => { seen.push(`${arg.phase}:${arg.v1Quiet}`); return arg.phase === 'v1' ? { ok: true, value: { quiet: true, flags: [] } } : { ok: false, reason: 'the machine is loaded now' }; } });
  const first = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(first.exit, EXIT.halt, first.message);
  assert.deepEqual(seen, ['v1:null', 'v2:true']);
  assert.equal(readRunMap(t.ctx.runMapFile).value.pairs[1].v1Quiet, true, 'written with the phase, so a crash cannot lose it');
  seen.length = 0;
  const resumed = await runLoop({ ctx: t.ctx, deps: t.deps, map: readRunMap(t.ctx.runMapFile).value });
  assert.equal(resumed.exit, EXIT.halt);
  assert.deepEqual(seen, ['v2:true'], 'a fresh process reads it from the run-map: --accept-noisy is still refused for this v2');
});

// ---- an abort outside a launch (review M7) ------------------------------------------------------------------------------------------

test('An_abort_while_scoring_exits_without_launching_the_next_phase_and_uses_no_re_run', async () => {
  const ctl = new AbortController();
  const t = harness({ score: async () => { ctl.abort(); return { kind: 'scored', score: SCORE }; } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map, signal: ctl.signal });
  assert.equal(r.exit, 130, r.message);
  assert.equal(t.launched.length, 2, 'only the v1 pair launched: the v2 pair was not started under an aborted signal');
  const sessions = Object.values(readRunMap(t.ctx.runMapFile).value.sessions);
  assert.equal(sessions.length, 2, 'no session was planned or voided for the v2 phase');
  assert.ok(sessions.every(s => s.attempt === 1 && s.class === 'completed'), 'no interrupted void, so no re-run is spent by a Ctrl-C that came between phases');
});

test('An_abort_in_a_gate_stops_before_the_launch_and_leaves_the_sessions_planned', async () => {
  for (const gate of ['guard', 'quiet']) {
    const ctl = new AbortController();
    const t = harness({ [gate]: async () => { ctl.abort(); return gate === 'guard' ? { ok: true } : { ok: true, value: { quiet: true, flags: [] } }; } });
    const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map, signal: ctl.signal });
    assert.equal(r.exit, 130, `${gate}: ${r.message}`);
    assert.deepEqual(t.launched, [], `${gate}: nothing launched after the abort`);
    assert.ok(Object.values(readRunMap(t.ctx.runMapFile).value.sessions).every(s => s.status === 'planned'), `${gate}: planned again, not voided`);
  }
});

test('An_abort_before_the_loop_starts_launches_nothing', async () => {
  const ctl = new AbortController();
  ctl.abort();
  const t = harness();
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map, signal: ctl.signal });
  assert.equal(r.exit, 130);
  assert.deepEqual(t.launched, []);
  assert.deepEqual(t.events.filter(e => e.startsWith('guard')), [], 'not even the gates ran');
});

// ---- every session of a phase is settled before the loop acts on a stop (review M8, G1) ------------------------------------------------

test('A_stop_in_the_first_session_of_a_phase_does_not_leave_its_partner_uncharged_and_unrecorded', async () => {
  const first = firstStack({ seed: SEED, pair: 1, phase: 'v1' });
  const partner = first === 'csharp' ? 'node' : 'csharp';
  const t = harness({ variants: { [rid(first, 'v1', 1)]: 'auth' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.stop, r.message);
  const map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.stopped.rule, 'stop: auth');
  assert.equal(map.sessions[rid(first, 'v1', 1)].status, 'ran');
  assert.equal(map.sessions[rid(partner, 'v1', 1)].status, 'ran', 'the partner ran in the same phase: it is settled, not left launching');
  assert.equal(map.sessions[rid(partner, 'v1', 1)].class, 'completed');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid).sort(), [rid('csharp', 'v1', 1), rid('node', 'v1', 1)].sort(), 'both sessions were paid for, both are charged');
});

test('A_collection_failure_of_one_session_leaves_it_recoverable_and_does_not_block_its_partner', async () => {
  const first = firstStack({ seed: SEED, pair: 1, phase: 'v1' });
  const partner = first === 'csharp' ? 'node' : 'csharp';
  const t = harness({ variants: { [rid(first, 'v1', 1)]: 'corrupt' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.failed, r.message);
  assert.match(r.message, new RegExp(rid(first, 'v1', 1)));
  let map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.sessions[rid(partner, 'v1', 1)].status, 'ran', 'the partner was collected, charged and recorded though its partner failed');
  assert.equal(map.sessions[rid(first, 'v1', 1)].status, 'launching', 'the failed one waits for resume');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid), [rid(partner, 'v1', 1)], 'no charge for what could not be read');
  // the evidence is repaired (a reader fix, say) and the run resumes: the session is adopted and charged once, the partner is not charged again
  lay(layoutPaths(t.ctx.sessionsRoot, rid(first, 'v1', 1)), undefined);
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, true, settled.reason);
  map = settled.value;
  assert.equal(map.sessions[rid(first, 'v1', 1)].status, 'ran');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid).sort(), [rid('csharp', 'v1', 1), rid('node', 'v1', 1)].sort());
});

test('Resume_writes_the_record_of_a_void_session_that_ended_but_whose_record_was_not_written', async () => {
  const t = harness({ variants: { [rid('csharp', 'v1', 1)]: 'upstream' } });
  const first = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map, signal: undefined });
  assert.equal(first.exit, EXIT.done, first.message);
  const record = path.join(t.ctx.sessionsRoot, rid('csharp', 'v1', 1), 'record.json');
  assert.equal(existsSync(record), true);
  rmSync(record);
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map: readRunMap(t.ctx.runMapFile).value });
  assert.equal(settled.ok, true, settled.reason);
  assert.equal(existsSync(record), true, 'a crash between the transition and the record write is repaired before the schedule advances');
  assert.equal(readLedger(t.ctx.ledgerFile).filter(l => l.rid === rid('csharp', 'v1', 1)).length, 1, 'and nothing is charged twice');
});

test('Resume_clears_a_claimed_layout_that_never_paid_so_the_same_rid_can_be_launched_again', async () => {
  const t = harness();
  const { planSession } = await import('../lib/run-map.mjs');
  let map = t.map;
  for (const stack of ['csharp', 'node']) map = planSession(map, { rid: rid(stack, 'v1', 1), pair: 1, stack, phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5', dummy: 'sk-ant-oat01-dummy-resume' }).value;
  for (const stack of ['csharp', 'node']) map = transition(map, rid(stack, 'v1', 1), 'launching').value;
  // a crash between the layout claim and the first stream byte: the directory exists, holds nothing paid and has no meta.json
  const { sessionLayout } = await import('../lib/sandbox.mjs');
  for (const stack of ['csharp', 'node']) assert.equal(sessionLayout(t.ctx.sessionsRoot, rid(stack, 'v1', 1)).ok, true);
  assert.equal(sessionLayout(t.ctx.sessionsRoot, rid('csharp', 'v1', 1)).ok, false, 'the stale claim is what a relaunch would hit (EEXIST)');
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, true, settled.reason);
  assert.ok(['csharp', 'node'].every(s => settled.value.sessions[rid(s, 'v1', 1)].status === 'planned'));
  assert.equal(readLedger(t.ctx.ledgerFile).length, 0, 'nothing was paid: nothing is charged');
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: settled.value });
  assert.equal(r.exit, EXIT.done, `the same rids launch after the reset: ${r.message}`);
});

test('Resume_keeps_a_directory_whose_broker_forwarded_a_request_and_charges_it', async () => {
  const t = harness();
  const { planSession } = await import('../lib/run-map.mjs');
  let map = planSession(t.map, { rid: rid('csharp', 'v1', 1), pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5', dummy: 'sk-ant-oat01-dummy-resume' }).value;
  map = transition(map, rid('csharp', 'v1', 1), 'launching').value;
  const { sessionLayout } = await import('../lib/sandbox.mjs');
  const made = sessionLayout(t.ctx.sessionsRoot, rid('csharp', 'v1', 1));
  writeFileSync(made.value.files.brokerLog, `${JSON.stringify({ t: '2026-10-30T10:00:00Z', seq: 1, event: 'forwarding' })}\n`);
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, true, settled.reason);
  assert.match(settled.value.sessions[rid('csharp', 'v1', 1)].class, /^void: harness/);
  assert.equal(readLedger(t.ctx.ledgerFile).length, 1, 'a forwarded request was reserved against the budget: charged');
  assert.ok(readLedger(t.ctx.ledgerFile)[0].usd > 0);
});

// ---- the registered series stops: a T1 breach and the second contaminated void (review M4) ------------------------------------------------

/** A launch that also writes the session's own planted canary token into its stream: what a session that read an operator's file leaves behind. */
function contaminating(t, rids) {
  const original = t.deps.phase.launchSession;
  t.deps.phase.launchSession = async (plan, d, opts) => {
    const r = await original(plan, d, opts);
    if (rids.includes(plan.layout.rid)) {
      const manifest = JSON.parse(readFileSync(path.join(t.ctx.seriesDir, 'canaries', `${plan.layout.rid}.json`), 'utf8'));
      const token = manifest.planted.find(p => p.status === 'planted').token;
      appendFileSync(plan.layout.files.stream, `${JSON.stringify({ type: 'user', message: { content: [{ type: 'tool_result', content: `read ${token}` }] } })}\n`);
    }
    return r;
  };
}

test('A_T1_breach_under_policy_stop_stops_the_series_and_under_investigate_does_not', async () => {
  const t = harness({ variants: { [rid('node', 'v1', 1)]: 't1' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.stop, r.message);
  assert.match(r.message, /T1 breach/);
  assert.equal(readRunMap(t.ctx.runMapFile).value.stopped.rule, 'T1 breach');
  assert.equal(t.launched.length, 2, 'nothing launches after the stop');
  const rec = JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid('node', 'v1', 1), 'record.json'), 'utf8').toString());
  assert.ok(rec.flags.includes('T1 breach'));
  const lenient = harness({ variants: { [rid('node', 'v1', 1)]: 't1' }, ctx: { policy: { ...harness().ctx.policy, t1: 'investigate' } } });
  const r2 = await runLoop({ ctx: lenient.ctx, deps: lenient.deps, map: lenient.map });
  assert.equal(r2.exit, EXIT.done, 'control: the same breach under t1 = investigate is investigated, not a stop');
});

test('The_second_contaminated_void_of_the_series_stops_it_and_the_first_does_not', async () => {
  const one = harness();
  contaminating(one, [rid('csharp', 'v1', 1)]);
  const r1 = await runLoop({ ctx: one.ctx, deps: one.deps, map: one.map });
  assert.equal(r1.exit, EXIT.done, `one contaminated void is a void, re-run once: ${r1.message}`);
  assert.equal(readRunMap(one.ctx.runMapFile).value.stopped, null);
  const two = harness();
  contaminating(two, [rid('csharp', 'v1', 1), rid('node', 'v1', 1)]);
  const r2 = await runLoop({ ctx: two.ctx, deps: two.deps, map: two.map });
  assert.equal(r2.exit, EXIT.stop, r2.message);
  assert.equal(readRunMap(two.ctx.runMapFile).value.stopped.rule, 'second void: contaminated');
  assert.equal(two.launched.length, 2, 'the re-run pair is not launched: the series is stopped');
});

test('The_runner_refuses_to_start_a_stopped_series', async () => {
  const t = harness({ variants: { [rid('node', 'v1', 1)]: 't1' } });
  await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  const again = await runLoop({ ctx: t.ctx, deps: t.deps, map: readRunMap(t.ctx.runMapFile).value });
  assert.equal(again.exit, EXIT.stop);
  assert.match(again.message, /new series/, 'the loop says a new series is needed');
  assert.equal(t.launched.length, 2, 'a stopped series launches nothing more');
});

test('A_crash_between_the_ledger_line_and_the_run_map_transition_does_not_charge_the_session_twice', async () => {
  const t = harness();
  const { planSession } = await import('../lib/run-map.mjs');
  const r0 = rid('csharp', 'v1', 1);
  let map = planSession(t.map, { rid: r0, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5', dummy: 'sk-ant-oat01-dummy-resume' }).value;
  map = transition(map, r0, 'launching').value;
  const layout = layoutPaths(t.ctx.sessionsRoot, r0);
  mkdirSync(layout.dir, { recursive: true });
  lay(layout, undefined);
  // the process died after appending the charge and before the run-map was written: the ledger knows the session, the map says launching
  const { appendLedger } = await import('../lib/ledger.mjs');
  appendLedger(t.ctx.ledgerFile, { kind: 'data', usd: 3.1, what: 'completed', rid: r0, t: '2026-10-30T10:00:00.000Z' });
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, true, settled.reason);
  assert.equal(settled.value.sessions[r0].status, 'ran', 'the session moves on');
  assert.equal(readLedger(t.ctx.ledgerFile).filter(l => l.rid === r0).length, 1, 'and is charged once: the ledger line that was already there stands');
});

test('The_record_of_a_v2_session_lists_which_directories_its_workspace_left_out_per_stack', async () => {
  const t = harness();
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const record = (stack, phase) => JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid(stack, phase, 1), 'record.json'), 'utf8'));
  assert.deepEqual(record('csharp', 'v2').workspace.value.excludedDirs, ['bin', 'obj', 'node_modules'], 'C#: build output and dependencies');
  assert.deepEqual(record('node', 'v2').workspace.value.excludedDirs, ['node_modules'], 'TypeScript: dependencies only, a Node bin/ may hold source');
  assert.equal(record('csharp', 'v1').workspace.known, false, 'a v1 workspace is filled from the pinned inputs: nothing was left out');
  assert.match(record('csharp', 'v1').workspace.why, /pinned inputs/);
});

test('A_record_rebuilt_later_keeps_the_run_maps_class_and_reports_a_new_hand_verdict_as_a_discrepancy', async () => {
  const t = harness();
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.done, r.message);
  const entry = readRunMap(t.ctx.runMapFile).value.sessions[rid('csharp', 'v1', 1)];
  assert.equal(entry.class, 'completed');
  const later = { ...t.ctx, handVerdictsFor: id => (id === entry.rid ? [{ g: 'G-1', verdict: 'refuted', rid: id, evidence: 'x', who: 'me', date: '2026-11-03' }] : []) };
  const rebuilt = writeSessionRecord({ ctx: later, entry, score: null });
  assert.equal(rebuilt.ok, true, rebuilt.reason);
  assert.equal(rebuilt.value.class, 'completed', 'the class is the run-map\'s: a record never re-classifies a session on its own');
  assert.equal(rebuilt.value.data, true);
  assert.equal(rebuilt.value.discrepancies.length, 1);
  assert.deepEqual([rebuilt.value.discrepancies[0].runMap, rebuilt.value.discrepancies[0].recomputed], ['completed', 'void: harness']);
  assert.match(rebuilt.value.discrepancies[0].why, /hand verdict refuted G-1/);
  assert.ok(rebuilt.value.flags.includes('class discrepancy'));
  const plain = writeSessionRecord({ ctx: t.ctx, entry, score: null });
  assert.deepEqual(plain.value.discrepancies, [], 'control: nothing to report when the recomputed class is the run-map\'s');
});

test('A_T2_breach_stops_the_series_when_the_policy_makes_t2_the_stop_and_only_then', async () => {
  const armed = harness({ variants: { [rid('node', 'v1', 1)]: 't2' }, ctx: { policy: { ...harness().ctx.policy, t2: 'stop' } } });
  const r = await runLoop({ ctx: armed.ctx, deps: armed.deps, map: armed.map });
  assert.equal(r.exit, EXIT.stop, r.message);
  assert.match(r.message, /T2 breach/);
  assert.equal(readRunMap(armed.ctx.runMapFile).value.stopped.rule, 'T2 breach', 'the pre-registered fallback: when T\'s coverage is not established, T2 is the stop');
  const rec = JSON.parse(readFileSync(path.join(armed.ctx.sessionsRoot, rid('node', 'v1', 1), 'record.json'), 'utf8'));
  assert.ok(rec.flags.includes('T2 breach'), 'the cause of the stop is the flag');
  assert.equal(rec.flags.includes('otel incomplete'), false, 'one flag per cause: under t2: stop a T2 breach is `T2 breach` and not also `otel incomplete`');
  assert.equal(armed.launched.length, 2, 'nothing launches after the stop');
  const registered = harness({ variants: { [rid('node', 'v1', 1)]: 't2' } });
  const r2 = await runLoop({ ctx: registered.ctx, deps: registered.deps, map: registered.map });
  assert.equal(r2.exit, EXIT.done, 'control: under the registered policy (t2 otel-incomplete) the same breach is a flag and the cell stays');
});

test('A_series_stop_is_not_persisted_while_a_session_of_the_phase_is_unsettled_and_is_persisted_once_the_last_one_is_charged', async () => {
  const first = firstStack({ seed: SEED, pair: 1, phase: 'v1' });
  const partner = first === 'csharp' ? 'node' : 'csharp';
  const t = harness({ variants: { [rid(first, 'v1', 1)]: 'auth', [rid(partner, 'v1', 1)]: 'corrupt' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.failed, `the collection failure is returned, and it is resumable: ${r.message}`);
  assert.match(r.message, new RegExp(rid(partner, 'v1', 1)));
  assert.match(r.message, /stop: auth/, 'the pending stop is named, so the operator knows what the resume will end with');
  let map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.stopped, null, 'no stop is persisted while a session is still unsettled: a stopped series refuses resume, and that session could never be charged');
  assert.equal(map.sessions[rid(partner, 'v1', 1)].status, 'launching');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid), [rid(first, 'v1', 1)], 'the settled one is charged, the unread one is not yet');
  lay(layoutPaths(t.ctx.sessionsRoot, rid(partner, 'v1', 1)), undefined); // the evidence is repaired
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, false, 'the resume settles the last session, and only then the stop that was pending is recorded');
  assert.equal(settled.exit, EXIT.stop);
  map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.stopped.rule, 'stop: auth');
  assert.equal(map.sessions[rid(partner, 'v1', 1)].status, 'ran');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid).sort(), [rid('csharp', 'v1', 1), rid('node', 'v1', 1)].sort(), 'both sessions are charged');
  const again = await runLoop({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(again.exit, EXIT.stop);
  assert.equal(t.launched.length, 2, 'nothing launched after the stop');
});

test('A_series_stop_of_a_check_survives_a_failed_collection_of_the_partner_the_same_way', async () => {
  const first = firstStack({ seed: SEED, pair: 1, phase: 'v1' });
  const partner = first === 'csharp' ? 'node' : 'csharp';
  const t = harness({ variants: { [rid(first, 'v1', 1)]: 't1', [rid(partner, 'v1', 1)]: 'corrupt' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.failed, r.message);
  assert.equal(readRunMap(t.ctx.runMapFile).value.stopped, null);
  lay(layoutPaths(t.ctx.sessionsRoot, rid(partner, 'v1', 1)), undefined);
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map: readRunMap(t.ctx.runMapFile).value });
  assert.equal(settled.exit, EXIT.stop, 'the T1 breach of the settled session is remembered by the run-map, not recomputed from nothing');
  assert.equal(readRunMap(t.ctx.runMapFile).value.stopped.rule, 'T1 breach');
});

test('A_rate_limit_void_is_a_resumable_halt_after_the_phase_is_settled_and_the_re_run_is_not_launched_in_the_same_window', async () => {
  const t = harness({ variants: { [rid('csharp', 'v1', 1)]: 'ratelimit' } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.halt, `contract section 9: void: rate limit is a resumable halt (exit 3): ${r.message}`);
  assert.match(r.message, /void: rate limit/);
  assert.match(r.message, new RegExp(rid('csharp', 'v1', 1)));
  assert.match(r.message, /[Rr]esume/);
  let map = readRunMap(t.ctx.runMapFile).value;
  assert.equal(map.stopped, null, 'a halt is not a series stop');
  assert.equal(map.sessions[rid('csharp', 'v1', 1)].class, 'void: rate limit');
  assert.equal(map.sessions[rid('node', 'v1', 1)].status, 'ran', 'the phase is settled: the partner is classified and charged too');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid).sort(), [rid('csharp', 'v1', 1), rid('node', 'v1', 1)].sort());
  assert.equal(t.launched.length, 2, 'nothing, the re-run included, is launched in the window that just ran out');
  // the operator resumes after the window: the schedule goes on as for any void (the partner's v2 alone, the re-run, its v2)
  const again = await runLoop({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(again.exit, EXIT.done, again.message);
  assert.deepEqual(t.launched.slice(2), [rid('node', 'v2', 1), rid('csharp', 'v1', 2), rid('csharp', 'v2', 1)]);
});

test('A_T3_breach_under_the_t3_stop_policy_stops_the_series_with_the_rule_T3_breach', async () => {
  const t = harness({ variants: { [rid('node', 'v1', 1)]: 't3' }, ctx: { policy: { ...harness().ctx.policy, t3: 'stop', t2: 'stop' } } });
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.exit, EXIT.stop, r.message);
  assert.equal(readRunMap(t.ctx.runMapFile).value.stopped.rule, 'T3 breach');
  assert.equal(t.launched.length, 2, 'nothing launches after the stop');
  const lenient = harness({ variants: { [rid('node', 'v1', 1)]: 't3' } });
  assert.equal((await runLoop({ ctx: lenient.ctx, deps: lenient.deps, map: lenient.map })).exit, EXIT.done, 'control: under the registered policy (t3 investigate) the same breach does not stop');
});

// ---- a dated `refuted` hand verdict reclassifies a session that already ended (review N4) -----------------------------------------------

const refutedBy = (...rids) => id => (rids.includes(id) ? [{ g: 'G-13', verdict: 'refuted', rid: id, evidence: 'scratch/read-back.md', who: 'operator', date: '2026-11-03' }] : []);

test('A_dated_refuted_line_reclassifies_an_ended_session_on_the_next_run_records_it_and_lets_the_schedule_plan_the_one_re_run', async () => {
  const t = harness();
  const first = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(first.exit, EXIT.done, first.message);
  assert.equal(t.launched.length, 4);
  const victim = rid('csharp', 'v1', 1);
  // the operator reads the pair by hand, refutes G-13 for the C# v1 session, and resumes: the line is read at the start of the run
  const ctx = { ...t.ctx, handVerdictsFor: refutedBy(victim) };
  const lines = [];
  const again = await runLoop({ ctx, deps: t.deps, map: readRunMap(t.ctx.runMapFile).value, say: l => lines.push(l) });
  assert.equal(again.exit, EXIT.done, again.message);
  const map = readRunMap(t.ctx.runMapFile).value;
  const entry = map.sessions[victim];
  assert.deepEqual([entry.class, entry.data, entry.status], ['void: harness', false, 'scored'], 'a refuted line reclassifies to void: harness and to nothing else; the session was scored, and stays so');
  assert.deepEqual([entry.reclassified.from.class, entry.reclassified.from.data, entry.reclassified.g, entry.reclassified.date], ['completed', true, 'G-13', '2026-11-03']);
  assert.ok(lines.some(l => l.includes(victim) && /reclassified/.test(l)), 'the run says so');
  assert.deepEqual(t.launched.slice(4), [rid('csharp', 'v1', 2)], 'the schedule plans the one re-run: C# v1 a2, alone (its partner was not voided)');
  assert.equal(map.sessions[rid('csharp', 'v1', 2)].label, 'ran unpaired');
  const event = readLedger(t.ctx.ledgerFile).filter(l => l.event === 'reclassify');
  assert.equal(event.length, 1, 'one ledger event line');
  assert.deepEqual([event[0].rid, event[0].from, event[0].to, event[0].g], [victim, 'completed', 'void: harness', 'G-13']);
  const spend = readLedger(t.ctx.ledgerFile).filter(l => l.event === undefined);
  assert.equal(spend.filter(l => l.rid === victim).length, 1, 'the session is still charged once');
  const rec = JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, victim, 'record.json'), 'utf8'));
  assert.deepEqual([rec.class, rec.data], ['void: harness', false]);
  assert.equal(rec.reclassified.from.class, 'completed', 'the record shows the reclassification');
  assert.ok(rec.flags.includes('reclassified by hand'));
  assert.deepEqual(rec.discrepancies, []);
  // run again: nothing is reclassified twice and nothing is launched twice
  const launchedBefore = t.launched.length;
  const third = await runLoop({ ctx, deps: t.deps, map, say: () => {} });
  assert.equal(third.exit, EXIT.done);
  assert.equal(t.launched.length, launchedBefore, 'the re-run was planned once');
  assert.equal(readLedger(t.ctx.ledgerFile).filter(l => l.event === 'reclassify').length, 1);
});

test('A_refuted_line_never_reclassifies_a_stop_or_a_contaminated_void_and_never_an_unlisted_rid', async () => {
  const t = harness({ variants: { [rid('node', 'v1', 1)]: 'auth' } });
  await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  const before = readRunMap(t.ctx.runMapFile).value;
  const reclassified = reclassifyByHand({ map: before, handVerdictsFor: refutedBy(rid('node', 'v1', 1), 'no-such-rid') });
  assert.equal(reclassified.changes.length, 0, 'rows 1 to 4 of the outcome table come before the hand verdict');
  assert.equal(reclassified.map.sessions[rid('node', 'v1', 1)].class, 'stop: auth');
  const holds = reclassifyByHand({ map: before, handVerdictsFor: id => [{ g: 'G-13', verdict: 'holds', rid: id, evidence: 'x', who: 'me', date: '2026-11-03' }] });
  assert.equal(holds.changes.length, 0, 'only refuted reclassifies');
});

test('A_session_record_names_the_hashes_of_the_quiet_and_package_flag_files_that_applied', async () => {
  const inputHashes = { quietSha256: 'a1'.repeat(32), packageFlagsSha256: 'b2'.repeat(32) };
  const t = harness({ ctx: { inputHashes } });
  await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  const rec = JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid('csharp', 'v1', 1), 'record.json'), 'utf8'));
  assert.deepEqual(rec.run.inputs, { known: true, value: inputHashes });
  const bare = harness();
  await runLoop({ ctx: bare.ctx, deps: bare.deps, map: bare.map });
  const none = JSON.parse(readFileSync(path.join(bare.ctx.sessionsRoot, rid('csharp', 'v1', 1), 'record.json'), 'utf8'));
  assert.equal(none.run.inputs.known, false, 'a run that recorded none says so with a reason, never a blank');
  assert.ok(none.run.inputs.why);
});

test('Settle_only_settles_and_charges_a_session_left_launching_in_a_stopped_series_and_launches_nothing', async () => {
  const first = firstStack({ seed: SEED, pair: 1, phase: 'v1' });
  const partner = first === 'csharp' ? 'node' : 'csharp';
  const t = harness({ variants: { [rid(first, 'v1', 1)]: 'auth', [rid(partner, 'v1', 1)]: 'corrupt' } });
  await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
  // a map from before a stop waited for its last session: the stop is on it and a session is still launching
  const stopped = markStopped(readRunMap(t.ctx.runMapFile).value, { rule: 'stop: auth', reason: '{}', at: 't' });
  assert.equal(stopped.sessions[rid(partner, 'v1', 1)].status, 'launching');
  lay(layoutPaths(t.ctx.sessionsRoot, rid(partner, 'v1', 1)), undefined); // its evidence is readable now
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map: stopped, settleOnly: true });
  assert.equal(settled.ok, true, settled.reason);
  assert.equal(settled.value.sessions[rid(partner, 'v1', 1)].status, 'ran', 'the session is settled');
  assert.equal(settled.value.stopped.rule, 'stop: auth', 'and the series stays stopped');
  assert.deepEqual(readLedger(t.ctx.ledgerFile).map(l => l.rid).sort(), [rid('csharp', 'v1', 1), rid('node', 'v1', 1)].sort(), 'both sessions are charged: the $40 and the $500 see all of it');
  assert.equal(t.launched.length, 2, 'nothing was launched');
});

test('Resume_never_removes_or_settles_a_session_directory_whose_state_file_names_an_owner_that_is_alive', async () => {
  const t = harness();
  const { planSession } = await import('../lib/run-map.mjs');
  const { sessionLayout } = await import('../lib/sandbox.mjs');
  const { processStartTime } = await import('../lib/session-state.mjs');
  const id = rid('csharp', 'v1', 1);
  let map = planSession(t.map, { rid: id, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5', dummy: 'sk-ant-oat01-dummy-resume' }).value;
  map = transition(map, id, 'launching').value;
  const layout = sessionLayout(t.ctx.sessionsRoot, id).value; // claimed, nothing paid, no meta.json: what a reset would remove
  const state = owner => writeFileSync(layout.files.state, `${JSON.stringify({ phase: 'running', hostPid: owner.pid, hostStartTime: owner.startTime, cell: `cell-${id}`, startedAt: 't', heartbeatAt: 't' })}\n`);
  state({ pid: process.pid, startTime: processStartTime(process.pid) }); // another runner of this series, alive
  const refused = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(refused.ok, false, 'a live owner: resume would race it');
  assert.equal(refused.exit, EXIT.halt);
  assert.match(refused.reason, new RegExp(`${id}.*still running|${id}.*alive`));
  assert.equal(existsSync(layout.dir), true, 'the directory was not removed');
  assert.equal(readRunMap(t.ctx.runMapFile).value?.sessions?.[id]?.status ?? 'launching', 'launching', 'and the session was not touched');
  state({ pid: 2 ** 22 - 1, startTime: '123' }); // the owner is gone
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, true, settled.reason);
  assert.equal(settled.value.sessions[id].status, 'planned', 'control: with a dead owner the unpaid claim is cleared as before');
  assert.equal(existsSync(layout.dir), false);
});

test('Resume_looks_for_a_live_session_owner_before_it_unplants_a_canary_or_retires_anything_and_refuses_without_touching_the_directory', async () => {
  const t = harness();
  const { planSession } = await import('../lib/run-map.mjs');
  const { sessionLayout } = await import('../lib/sandbox.mjs');
  const { processStartTime } = await import('../lib/session-state.mjs');
  const projectDir = path.join(t.ctx.home, '.claude', 'projects', 'p1');
  mkdirSync(path.join(t.ctx.seriesDir, 'canaries'), { recursive: true });
  const leftover = path.join(projectDir, 'scratch-note-cafe0001.md');
  writeFileSync(leftover, 'token\n');
  writeFileSync(path.join(t.ctx.seriesDir, 'canaries', 'live.json'), JSON.stringify({ planted: [{ dir: projectDir, path: leftover, token: 'x', status: 'planted' }] }));
  const calls = [];
  t.deps.retireStale = async () => { calls.push('retireStale'); };
  const done = rid('csharp', 'v1', 1);
  const running = rid('node', 'v1', 1);
  let map = t.map;
  for (const [id, stack] of [[done, 'csharp'], [running, 'node']]) map = planSession(map, { rid: id, pair: 1, stack, phase: 'v1', attempt: 1, kind: 'data', label: 'paired', mask: '0-5', dummy: 'sk-ant-oat01-dummy-resume' }).value;
  map = transition(map, done, 'launching').value;
  map = transition(map, done, 'ran', { class: 'completed', data: true, usd: 1 }).value; // a session of the map that is NOT launching: it still has a live owner (another runner)
  const live = sessionLayout(t.ctx.sessionsRoot, done).value;
  const stateText = `${JSON.stringify({ phase: 'running', hostPid: process.pid, hostStartTime: processStartTime(process.pid), cell: `cell-${done}`, startedAt: 't', heartbeatAt: 't' })}\n`;
  writeFileSync(live.files.state, stateText);
  const refused = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(refused.ok, false, 'a live owner anywhere in the series: resume would race it');
  assert.equal(refused.exit, EXIT.halt);
  assert.match(refused.reason, new RegExp(done));
  assert.deepEqual(calls, [], 'retireStale did not run: it would end the cell of a live session');
  assert.equal(existsSync(leftover), true, 'no canary was unplanted: a live session may be using the planted files');
  assert.equal(readFileSync(live.files.state, 'utf8'), stateText, 'the live session directory was not touched');
});

test('A_session_directory_with_a_state_file_nobody_can_read_is_no_live_owner_and_does_not_stop_the_resume', async () => {
  const t = harness();
  mkdirSync(path.join(t.ctx.sessionsRoot, 'strayrid'), { recursive: true });
  writeFileSync(path.join(t.ctx.sessionsRoot, 'strayrid', 'state.json'), '{ not json');
  const r = await reconcile({ ctx: t.ctx, deps: t.deps, map: t.map });
  assert.equal(r.ok, true, r.reason);
});

/** A pair's v1 phase cut short by a crash: the first session ended (`ran`, with what the settle recorded), its partner was left `launching` with no directory (nothing paid). */
async function endedAndUnreadPartner(t, { kind = 'data', stopRules } = {}) {
  const { planSession, writeRunMap } = await import('../lib/run-map.mjs');
  const pair = kind === 'data' ? 1 : 0;
  const ids = kind === 'data' ? [rid('csharp', 'v1', 1), rid('node', 'v1', 1)] : ['aatwin1', 'aatwin2'];
  const stacks = kind === 'data' ? ['csharp', 'node'] : ['csharp', 'csharp'];
  let map = t.map;
  ids.forEach((id, i) => { map = planSession(map, { rid: id, pair, stack: stacks[i], phase: 'v1', attempt: 1, kind, label: 'paired', mask: ['0-5', '8-13'][i], dummy: 'sk-ant-oat01-dummy-resume' }).value; });
  for (const id of ids) map = transition(map, id, 'launching').value;
  map = transition(map, ids[0], 'ran', { class: 'completed', data: true, usd: 1, run: { mask: '0-5', flags: [] }, dummy: 'sk-ant-oat01-dummy-resume', canaryTokens: [], ...(stopRules ? { stopRules } : {}) }).value;
  mkdirSync(path.join(t.ctx.sessionsRoot, ids[0]), { recursive: true });
  const layout = layoutPaths(t.ctx.sessionsRoot, ids[0]);
  lay(layout, undefined);
  mkdirSync(layout.work, { recursive: true });
  writeFileSync(path.join(layout.work, 'Program.cs'), 'class P {}\n'); // the v1 tree the v2 phase starts from
  writeRunMap(t.ctx.runMapFile, map);
  return { map, ids };
}

test('A_series_stop_an_ended_session_carries_is_recorded_when_resume_resets_its_unread_partner_and_nothing_is_launched', async () => {
  for (const kind of ['data', 'aa']) {
    const t = harness();
    t.ctx.kind = kind;
    t.ctx.pair = kind === 'data' ? 1 : 0;
    const { map, ids } = await endedAndUnreadPartner(t, { kind, stopRules: [{ rule: 'T5 unchecked', detail: 'no broker ledger' }] });
    const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
    assert.equal(settled.ok, true, `${kind}: ${settled.reason}`);
    assert.equal(settled.value.sessions[ids[1]].status, 'planned', `${kind}: the unread partner paid nothing and is planned again`);
    assert.equal(settled.value.stopped?.rule, 'T5 unchecked', `${kind}: the waiting stop is recorded at the end of the resume, because no session is left unsettled`);
    const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: settled.value });
    assert.equal(r.exit, EXIT.stop, `${kind}: ${r.message}`);
    assert.deepEqual(t.launched, [], `${kind}: the stop comes before any launch`);
    assert.equal(readRunMap(t.ctx.runMapFile).value.stopped?.rule, 'T5 unchecked', `${kind}: and it is on disk`);
  }
});

test('A_loop_that_starts_with_a_waiting_stop_and_nothing_unsettled_records_it_before_the_first_launch', async () => {
  const t = harness();
  const { map, ids } = await endedAndUnreadPartner(t, { stopRules: [{ rule: 'T1 breach', detail: 'x' }] });
  const planned = transition(map, ids[1], 'planned').value; // what a resume that did not record the stop would hand the loop
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: planned });
  assert.equal(r.exit, EXIT.stop, r.message);
  assert.match(r.message, /T1 breach/);
  assert.deepEqual(t.launched, []);
  assert.equal(readRunMap(t.ctx.runMapFile).value.stopped?.rule, 'T1 breach');
});

test('A_pair_whose_first_session_ended_and_whose_partner_was_reset_launches_only_the_partner_and_does_not_crash_on_ran_to_launching', async () => {
  const t = harness();
  const { map, ids } = await endedAndUnreadPartner(t);
  const settled = await reconcile({ ctx: t.ctx, deps: t.deps, map });
  assert.equal(settled.ok, true, settled.reason);
  const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: settled.value });
  assert.equal(r.exit, EXIT.done, r.message);
  assert.equal(t.launched.includes(ids[0]), false, 'the session that ended is not launched again');
  assert.equal(t.launched[0], ids[1], 'only its partner is launched in the v1 phase');
});

// ---- the fixed lists and the rehearsal (review N15) ---------------------------------------------------------------------------------------

test('A_fixed_list_run_launches_only_the_sessions_of_its_own_kind', async () => {
  const t = harness();
  const { planSession } = await import('../lib/run-map.mjs');
  const entry = (id, kind, stack) => ({ rid: id, pair: 0, stack, phase: 'v1', attempt: 1, kind, label: 'paired', mask: '0-5' });
  let map = t.map;
  for (const e of [entry('aaaaaa', 'aa', 'csharp'), entry('bbbbbb', 'pilot', 'node'), entry('cccccc', 'rehearsal', 'csharp')]) map = planSession(map, e).value;
  const ctx = { ...t.ctx, kind: 'aa', pair: 0 };
  const r = await runLoop({ ctx, deps: t.deps, map });
  assert.equal(r.exit, EXIT.done, r.message);
  assert.deepEqual(t.launched, ['aaaaaa'], 'the A/A launches its own twin and not the pilot or the rehearsal planned in the same run-map');
  const after = readRunMap(t.ctx.runMapFile).value;
  assert.deepEqual(['aaaaaa', 'bbbbbb', 'cccccc'].map(id => after.sessions[id].status), ['scored', 'planned', 'planned']);
  const pilot = await runLoop({ ctx: { ...t.ctx, kind: 'pilot', pair: 0 }, deps: t.deps, map: after });
  assert.equal(pilot.exit, EXIT.done, pilot.message);
  assert.deepEqual(t.launched, ['aaaaaa', 'bbbbbb'], 'and the pilot launches the pilot, scoring only its own: the A/A twin was scored already');
  assert.equal(readRunMap(t.ctx.runMapFile).value.sessions.cccccc.status, 'planned', 'the rehearsal is still waiting for its own command');
});
