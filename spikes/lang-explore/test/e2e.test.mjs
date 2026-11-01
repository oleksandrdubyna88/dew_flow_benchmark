// The pilot end to end for $0 (plan T-X11 and T-X10): the rehearsal, and a real `runPair` (kind `pilot`, the barrier, the records, the pair record, the note, `prune`) over v1's stub CLI and stub upstream in the real
// box behind the real broker, with a FAKE token. The real token file is never read (no `tokenFn` here reads a file), and every series directory a test makes is removed. The tests of this file run one after
// the other: each uses the two CPU masks. A host without the held-out side, the snapshots or the task repository is `hostLacks`.
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { stubClaude } from '../../minigit-bench/lib/stub-cli.mjs';
import { chunksOf, sseAnswer, sseBody, stubUpstream } from '../../minigit-bench/lib/stub-upstream.mjs';
import { TOKEN_FILE_PARTS } from '../../minigit-bench/capture/capture-env.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { hostLacks, sandboxGate, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { statusLines } from '../lib/commands.mjs';
import { loadPrompts } from '../lib/plan.mjs';
import { readbackOf } from '../lib/readback.mjs';
import { createSeries, drawMask, loadSeries, planPair, runMapFileOf } from '../lib/series.mjs';
import { transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { sessionLayout } from '../../minigit-bench/lib/sandbox.mjs';
import { loadCalibration, runPair } from '../lib/run.mjs';
import { machineFor, pilotQuiet } from '../lib/machine.mjs';
import { missingPaths } from '../lib/records.mjs';
import { loadHeldout } from '../lib/heldout.mjs';
import { noteFor, referenceFill, rehearse } from '../lib/rehearse.mjs';
import { removeArea } from '../lib/score.mjs';
import { loadWorld } from '../lib/world.mjs';

/** The world of this host, or null (`hostLacks`) when `prepare`, `calibrate`, the held-out side or the task repository is missing. */
async function worldOrNull(t) {
  if (!sandboxGate(t)) return null;
  const calibration = loadCalibration(realHome);
  if (!calibration.ok) return hostLacks(t, calibration.reason) && null;
  const world = await loadWorld({ home: realHome, work: tmpDir('e2e-world') });
  return world.ok ? { world: world.value, calibration: calibration.value } : (hostLacks(t, world.reason) && null);
}

const subject = { branch: 'test', sha: 'a'.repeat(40), harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: 'b'.repeat(12), pilotIdentity: 'c'.repeat(40) };

test('The_rehearsal_runs_a_pair_end_to_end_for_free_and_every_criterion_holds', { timeout: 600_000 }, async t => {
  const got = await worldOrNull(t);
  if (!got) return;
  const said = [];
  const r = await rehearse({ io: { home: realHome, say: l => said.push(l), abort: { reason: null, exit: 130 }, signal: undefined, worldFn: loadWorld, subject } });
  t.after(() => r.seriesDir && removeArea(r.seriesDir));
  assert.equal(r.exit, 0, `${r.message}\n${JSON.stringify(r.checks)}\n${said.join('\n')}`);
  assert.deepEqual(r.checks.map(c => c.ok), [true, true, true, true, true]);
  assert.deepEqual(r.problems, []);
  assert.ok(readLedger(path.join(r.seriesDir, 'ledger.ndjson')).every(l => l.kind === 'rehearsal' && l.usd === 0), 'a rehearsal costs nothing and says so');
  const note = readFileSync(path.join(r.seriesDir, 'rehearsal-note.md'), 'utf8');
  assert.match(note, /Exploratory, not evidence/);
  assert.match(note, /a REHEARSAL: stub CLI, fake token, \$0/);
  const series = loadSeries(r.seriesDir).value;
  for (const s of Object.values(series.map.sessions)) {
    const dir = path.join(r.seriesDir, 's', s.rid);
    assert.equal(s.kind, 'rehearsal');
    assert.match(readFileSync(path.join(dir, 'argv.json'), 'utf8'), /LC_ALL/, 'the locale reached the box\'s environment (argv.json lists the env keys)');
    assert.doesNotMatch(readFileSync(path.join(dir, 'broker.ndjson'), 'utf8'), /claude-oauth-token/);
    assert.equal(readbackOf({ sessionDir: dir, registered: loadPrompts().prompts[s.stack] }).ok, true, 'what the session was launched with reads back as registered');
    assert.deepEqual(JSON.parse(readFileSync(path.join(dir, 'record.json'), 'utf8')).egress.value.allowed, {}, 'no host was allowed or reached');
  }
});

/** A throw-away home with a fake token file, for the preflight's existence check. */
function fakeHome() {
  const home = tmpDir('e2e-home');
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, 'sk-ant-oat01-fake\n', { mode: 0o600 });
  chmodSync(path.dirname(file), 0o700);
  return home;
}

/** A series with a stub CLI behind a loopback stub upstream, a scripted quiet machine and the reference trees as the agents' work: everything a `runPair` needs, free. `machine` and `worldFn` can be replaced. */
async function pilotRig(t, { world, calibration }, { machine, worldFn = loadWorld } = {}) {
  const made = createSeries({ home: realHome, explore: world.explore, calibration, draw: drawMask(() => 1), inputsExtra: { test: true, prompts: loadPrompts() } });
  assert.equal(made.ok, true, made.reason);
  const dir = made.value.dir;
  t.after(() => removeArea(dir));
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  t.after(() => stub.close());
  const stubDir = tmpDir('e2e-stub');
  const said = [];
  const rig = {
    dir, said, quiet: { hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 },
    seams: { fill: referenceFill({ world, heldout: loadHeldout(realHome).value }), claudeBinary: stubClaude(stubDir), planOver: { upstream: stub.upstream, tickMs: 500, sampleMs: 500, settleMs: 500, graceMs: 2000 }, preflightHome: fakeHome(), stub: true },
  };
  const machineOf = sample => machineFor({ thresholds: pilotQuiet().value, calibratedMs: 20, seriesDir: dir, samplerFn: async () => async () => sample, sleepFn: async () => {}, now: (() => { let n = 0; return () => (n += 7000); })() });
  rig.machineOf = machineOf;
  rig.io = (over = {}) => ({ home: realHome, say: l => said.push(l), abort: { reason: null, exit: 130 }, worldFn, tokenFn: async () => ({ ok: true, value: 'sk-ant-fake-for-the-test' }), seams: { ...rig.seams, machine: machine ?? machineOf(rig.quiet) }, ...over });
  return rig;
}

test('A_pair_runs_through_the_pilots_loop_is_recorded_and_noted_and_the_barrier_refuses_what_is_not_next', { timeout: 600_000 }, async t => {
  const got = await worldOrNull(t);
  if (!got) return;
  const { world, calibration } = got;
  const rig = await pilotRig(t, got);
  const { dir, said } = rig;
  const io = rig.io();
  // the barrier: pair 2 and 3 are refused before pair 1 exists, and nothing is launched
  const early = await runPair({ seriesDir: dir, pair: 2, io });
  assert.equal(early.exit, 4);
  assert.match(early.message, /pair 1 is not finished \(csharp: none, node: none\)/);
  assert.deepEqual(Object.keys(loadSeries(dir).value.map.sessions), []);
  const r = await runPair({ seriesDir: dir, pair: 1, io });
  assert.equal(r.exit, 0, `${r.message}\n${said.join('\n')}`);
  assert.deepEqual(r.problems, []);
  const series = loadSeries(dir).value;
  const sessions = Object.values(series.map.sessions);
  assert.deepEqual(sessions.map(s => [s.kind, s.pair, s.status, s.class, s.label]).sort(), [['pilot', 1, 'scored', 'completed', 'paired'], ['pilot', 1, 'scored', 'completed', 'paired']]);
  assert.deepEqual(sessions.map(s => s.mask).sort(), ['0-5', '8-13']);
  assert.ok(sessions.find(s => s.stack === 'csharp').mask === '8-13', 'the draw (C# high) decided pair 1');
  assert.ok(readLedger(path.join(dir, 'ledger.ndjson')).every(l => l.kind === 'pilot'), 'the charge is a `pilot` line');
  for (const s of sessions) {
    const record = JSON.parse(readFileSync(path.join(dir, 's', s.rid, 'record.json'), 'utf8'));
    assert.deepEqual(missingPaths(record, true), [], `${s.stack}: every v1 metric and every explore field is in the record`);
    assert.equal(record.explore.accepted.value, true);
    assert.equal(existsSync(layoutPaths(path.join(dir, 's'), s.rid).files.stream), true);
  }
  const pair = JSON.parse(readFileSync(path.join(dir, 'pairs', 'p1.json'), 'utf8'));
  assert.equal(pair.cells.csharp.missing, false);
  assert.match(statusLines({ dir, ...series }).join('\n'), /pair 1: finished/);
  const note = noteFor({ series: series.state, seriesDir: dir, map: series.map, world: { ...world, calibration }, pins: world.explore, rehearsal: false, subject });
  assert.match(note, /Exploratory, not evidence/);
  assert.match(note, /\| 1 \| C# \| completed/);
  // the barrier after pair 1: pair 3 still needs pair 2; pair 1 cannot be run twice
  assert.match((await runPair({ seriesDir: dir, pair: 3, io })).message, /pair 2 is not finished/);
  assert.match((await runPair({ seriesDir: dir, pair: 1, io })).message, /pair 1 has already been run/);
});

const LOUD = { hostCpus: 6, harnessCpus: 0.05, foreignCpus: 5.9, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };
const statusesOf = dir => Object.values(loadSeries(dir).value.map.sessions).map(s => `${s.status}${s.class ? `:${s.class}` : ''}`).sort();

test('F2_a_pair_halted_by_the_quiet_check_and_then_by_a_judge_failure_is_resumed_without_a_new_paid_session', { timeout: 600_000 }, async t => {
  const got = await worldOrNull(t);
  if (!got) return;
  const rig = await pilotRig(t, got);
  const ledger = () => readLedger(path.join(rig.dir, 'ledger.ndjson'));
  // 1. the machine is never quiet: the run halts (exit 3), the sessions stay planned, nothing is spent
  const halted = await runPair({ seriesDir: rig.dir, pair: 1, io: rig.io({ seams: { ...rig.seams, machine: rig.machineOf(LOUD) } }) });
  assert.equal(halted.exit, 3, halted.message);
  assert.deepEqual(statusesOf(rig.dir), ['planned', 'planned']);
  assert.equal(ledger().length, 0);
  const rids = Object.keys(loadSeries(rig.dir).value.map.sessions).sort();
  // 2. the same command resumes; the judge fails after both sessions ran: exit 5, both ran with data, both charged
  const failing = async a => { const w = await loadWorld(a); return w.ok ? { ...w, value: { ...w.value, scoreFn: async () => ({ ok: false, kind: 'harness', reason: 'harness failure: a process remains (scripted)' }) } } : w; };
  const failed = await runPair({ seriesDir: rig.dir, pair: 1, io: rig.io({ worldFn: failing }) });
  assert.equal(failed.exit, 5, failed.message);
  assert.match(failed.message, /scoring .*failed/);
  assert.deepEqual(statusesOf(rig.dir), ['ran:completed', 'ran:completed']);
  assert.equal(ledger().length, 2, 'both sessions are charged once');
  const spent = ledger().map(l => l.usd);
  // 3. a rerun scores what ran: no new session, no new charge
  const resumed = await runPair({ seriesDir: rig.dir, pair: 1, io: rig.io() });
  assert.equal(resumed.exit, 0, resumed.message);
  assert.deepEqual(statusesOf(rig.dir), ['scored:completed', 'scored:completed']);
  assert.deepEqual(Object.keys(loadSeries(rig.dir).value.map.sessions).sort(), rids, 'the same two sessions: nothing was planned again');
  assert.deepEqual(ledger().map(l => l.usd), spent, 'and nothing was charged again');
  assert.ok(rig.said.some(l => /resuming/.test(l)));
  assert.deepEqual(resumed.problems, []);
});

test('F2_a_session_left_launching_by_a_hard_crash_is_settled_and_charged_once_from_the_broker_ledger', { timeout: 600_000 }, async t => {
  const got = await worldOrNull(t);
  if (!got) return;
  const rig = await pilotRig(t, got);
  const series = loadSeries(rig.dir).value;
  const planned = planPair({ map: series.map, pair: 1, pairs: 3, draw: series.state.draw });
  let map = planned.value.map;
  for (const e of planned.value.planned) {
    map = transition(map, e.rid, 'launching', { startedAt: new Date().toISOString() }).value;
    const layout = sessionLayout(path.join(rig.dir, 's'), e.rid).value;
    writeFileSync(path.join(layout.dir, 'broker.ndjson'), [{ event: 'forwarding', seq: 1 }, { event: 'settled', seq: 1, chargedUsd: 0.0123 }].map(l => `${JSON.stringify(l)}\n`).join(''));
  }
  writeRunMap(runMapFileOf(rig.dir), map);
  const r = await runPair({ seriesDir: rig.dir, pair: 1, io: rig.io() });
  assert.equal(r.exit, 0, `${r.message}\n${rig.said.join('\n')}`);
  const sessions = Object.values(loadSeries(rig.dir).value.map.sessions);
  assert.ok(sessions.every(s => s.status === 'ran' && /^void: harness/.test(s.class)), JSON.stringify(sessions.map(s => [s.status, s.class])));
  const lines = readLedger(path.join(rig.dir, 'ledger.ndjson'));
  assert.deepEqual(lines.map(l => l.usd), [0.0123, 0.0123], 'charged from the broker ledger, once per session');
  const again = await runPair({ seriesDir: rig.dir, pair: 1, io: rig.io() });
  assert.match(again.message, /already been run/, 'two voids are not resumed: they are re-run alone or the pair stays');
  assert.equal(readLedger(path.join(rig.dir, 'ledger.ndjson')).length, 2, 'and not charged twice');
});
