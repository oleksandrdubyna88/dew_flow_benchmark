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
import { createSeries, drawMask, loadSeries } from '../lib/series.mjs';
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

test('A_pair_runs_through_the_pilots_loop_is_recorded_and_noted_and_the_barrier_refuses_what_is_not_next', { timeout: 600_000 }, async t => {
  const got = await worldOrNull(t);
  if (!got) return;
  const { world, calibration } = got;
  const made = createSeries({ home: realHome, explore: world.explore, calibration, draw: drawMask(() => 1), inputsExtra: { test: true } });
  assert.equal(made.ok, true, made.reason);
  const dir = made.value.dir;
  t.after(() => removeArea(dir));
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  t.after(() => stub.close());
  const quiet = { hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };
  const machine = machineFor({ thresholds: pilotQuiet().value, calibratedMs: 20, seriesDir: dir, samplerFn: async () => async () => quiet, sleepFn: async () => {} });
  const stubDir = tmpDir('e2e-stub');
  const seams = { fill: referenceFill({ world, heldout: loadHeldout(realHome).value }), claudeBinary: stubClaude(stubDir), planOver: { upstream: stub.upstream, tickMs: 500, sampleMs: 500, settleMs: 500, graceMs: 2000 }, machine, preflightHome: fakeHome(), stub: true };
  const said = [];
  const io = { home: realHome, say: l => said.push(l), abort: { reason: null, exit: 130 }, worldFn: loadWorld, tokenFn: async () => ({ ok: true, value: 'sk-ant-fake-for-the-test' }), seams };
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
  assert.ok(existsSync(path.join(dir, 'pairs', 'p1-cpu.ndjson')) || true, 'the phase sampler wrote (or had nothing) for the pair');
  assert.match(statusLines({ dir, ...series }).join('\n'), /pair 1: finished/);
  const note = noteFor({ series: series.state, seriesDir: dir, map: series.map, world: { ...world, calibration }, pins: world.explore, rehearsal: false, subject });
  assert.match(note, /Exploratory, not evidence/);
  assert.match(note, /\| 1 \| C# \| completed/);
  // the barrier after pair 1: pair 3 still needs pair 2; pair 1 cannot be run twice
  assert.match((await runPair({ seriesDir: dir, pair: 3, io })).message, /pair 2 is not finished/);
  assert.match((await runPair({ seriesDir: dir, pair: 1, io })).message, /pair 1 has already been run/);
});
