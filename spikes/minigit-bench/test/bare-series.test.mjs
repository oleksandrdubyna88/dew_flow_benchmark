// The bare runner's loop (E3b.S1): the schedule driven for real over the run-map, with a fake launch that lays the bare-smoke fixture into each session's directory
// (and variants of it) and a fake scorer. The gates, the barrier, the voids and the re-run, the stops, the spend gate, the scorer's halts and the resume are what is under test.
import assert from 'node:assert/strict';
import { appendFileSync, copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { ridOf } from '../lib/bare-plan.mjs';
import { EXIT, dirFacts, reconcile, runLoop } from '../lib/bare-series.mjs';
import { readLedger } from '../lib/ledger.mjs';
import { openSeries, readRunMap, transition } from '../lib/run-map.mjs';
import { STAGED } from '../lib/smoke-load.mjs';
import { SEED, context, sleep } from './bare-ctx.mjs';
import { spikeDir } from './helpers.mjs';

const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
const SCORE = { build: { ok: true }, visible: [{ script: 'test-v1.sh', passed: 10, failed: 0, expectedTotal: 10, summaryCaptured: true, buildFailed: false, exitCode: 0, timedOut: false, signal: null }],
  heldout: { known: true, primary100: true, totals: { total: 29, passed: 29, failed: 0, blocked: 0, percent: 100 }, defects: 0, latency: { known: false, why: 'stub' } }, upstream: [] };

/** The fixture laid into a session's directory; `variant` 'upstream' ends the session on a 529 (a registered void). */
function lay(layout, variant) {
  for (const [rel, flat] of Object.entries(STAGED)) {
    if (!existsSync(path.join(FIXTURE, flat))) continue;
    mkdirSync(path.dirname(path.join(layout.dir, rel)), { recursive: true });
    copyFileSync(path.join(FIXTURE, flat), path.join(layout.dir, rel));
  }
  cpSync(path.join(FIXTURE, 'transcripts'), layout.transcripts, { recursive: true });
  if (variant === 'upstream') {
    const lines = readFileSync(layout.files.stream, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
    const i = lines.findLastIndex(e => e.type === 'result');
    lines[i] = { ...lines[i], is_error: true, api_error_status: 529 };
    writeFileSync(layout.files.stream, `${lines.map(l => JSON.stringify(l)).join('\n')}\n`);
    appendFileSync(layout.files.brokerLog, `${JSON.stringify({ t: '2026-10-30T10:30:00Z', seq: 99, event: 'forwarding' })}\n${JSON.stringify({ t: '2026-10-30T10:30:01Z', seq: 99, event: 'settled', status: 529, usage: null, chargedUsd: 0 })}\n`);
  }
  if (variant === 'auth') {
    appendFileSync(layout.files.brokerLog, `${JSON.stringify({ t: '2026-10-30T10:30:00Z', seq: 99, event: 'forwarding' })}\n${JSON.stringify({ t: '2026-10-30T10:30:01Z', seq: 99, event: 'settled', status: 401, usage: null, chargedUsd: 0 })}\n`);
  }
}

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
