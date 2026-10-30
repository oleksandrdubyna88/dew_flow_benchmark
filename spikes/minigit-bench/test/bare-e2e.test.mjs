// A bare pair end to end (E3b.S1): the real runner loop, the real launcher in real bwrap boxes behind the real broker and proxy, the real scorer; only the model is a stub
// (a script standing in for `claude`) and its upstream. No paid call and no real token. The barrier, the records, the cleanup and the ledger are observed on disk.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasStore } from '../lib/alias-store.mjs';
import { ridOf } from '../lib/bare-plan.mjs';
import { EXIT, runLoop } from '../lib/bare-series.mjs';
import { cellProcesses, realProcApi } from '../lib/janitor.mjs';
import { readLedger } from '../lib/ledger.mjs';
import { openSeries, readRunMap } from '../lib/run-map.mjs';
import { scoreSession } from '../lib/score-run.mjs';
import { fakeToken, prices, sseAnswer, sseBody, chunksOf, stubUpstream } from './broker-stub.mjs';
import { SEED, context } from './bare-ctx.mjs';
import { homeDir, inputs, keyGate, pythonGate, sandboxGate, spikeDir, tmpDir } from './helpers.mjs';
import { stubClaude } from './stub-claude.mjs';
import { TS_DIR } from './box-helpers.mjs';

test('A_stub_pair_runs_end_to_end', { timeout: 1_800_000 }, async t => {
  if (!sandboxGate(t) || !keyGate(t) || !pythonGate(t)) return;
  const fetched = await inputs();
  const upstream = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  t.after(() => upstream.close());
  const base = context();
  const seriesDir = homeDir('e2e');
  const policy = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(policy.ok, true, policy.reason);
  const ctx = {
    ...base, seriesDir, sessionsRoot: path.join(seriesDir, 's'), inputsRoot: fetched.root, hashes: fetched.entries, kind: 'data', pair: 1, seed: SEED,
    runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson'), ceilingUsd: 500, bareBudgetUsd: 40, caps: { capUsd: 6, wallMs: 120_000, stallMs: 60_000 },
    claudeBinary: stubClaude(tmpDir('stub')), cliVersion: '2.1.310', nodeDir: path.dirname(path.dirname(process.execPath)), tsDir: TS_DIR, token: fakeToken(), prices: prices(), table: policy.value.table, policy: policy.value.policy,
    harnessSources: { boxInit: path.join(spikeDir, 'harness', 'box-init.mjs'), audit: path.join(spikeDir, '..', 'policy-ab-quick', 'hooks', 'audit.mjs') },
    planOver: { upstream: upstream.upstream, tickMs: 200, sampleMs: 200, settleMs: 300, graceMs: 2000 }, snapshotPackages: [], packageRules: [], handVerdictsFor: () => [], harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030',
    abortReason: () => null, abortExit: () => 130, spin: { medianMs: 20 }, aliasStore: aliasStore(path.join(seriesDir, 'aliases.json')),
  };
  const projectDir = path.join(ctx.home, '.claude', 'projects', 'p1');
  const events = [];
  const deps = {
    guard: async () => ({ ok: true }), retireStale: async () => {}, preflight: async () => ({ ok: true }), quiet: async () => ({ ok: true, value: { quiet: true, flags: [] } }),
    startSampler: () => ({ stop: async () => ({ samples: 0, flags: [], error: null }) }), loadFlag: async () => false, now: Date.now,
    scoreSession: async arg => { events.push(`score ${path.basename(arg.session)} at ${Date.now()}`); return scoreSession({ ...arg, sleep: async () => {} }); },
    phase: { maskCheckMs: 1500, listProjectDirs: () => [projectDir] },
  };
  const map = openSeries({ seriesId: 's-e2e', seedSha256: 'b'.repeat(64), harnessIdentity: ctx.harnessIdentity, preregTag: ctx.preregTag, preregCommit: 'c'.repeat(40), pins: {}, stacksSha256: 'd', pricesSha256: 'e', snapshots: {}, caps: ctx.caps, bareBudgetUsd: 40, ceilingUsd: 500 });
  const r = await runLoop({ ctx, deps, map, say: l => events.push(l) });
  const dump = () => Object.values(readRunMap(ctx.runMapFile).value.sessions).map(x => { const d = path.join(ctx.sessionsRoot, x.rid); return `${x.rid} ${x.class}\n${['broker.ndjson', 'stream.ndjson', 'stderr.txt'].map(f => `${f}: ${existsSync(path.join(d, f)) ? readFileSync(path.join(d, f), 'utf8').slice(0, 1500) : '(absent)'}`).join('\n')}`; }).join('\n');
  assert.equal(r.exit, EXIT.done, `${r.message}\n${events.join('\n')}\n${dump()}`);
  const final = readRunMap(ctx.runMapFile).value;
  const sessions = Object.values(final.sessions);
  assert.equal(sessions.length, 4);
  assert.ok(sessions.every(s => s.status === 'scored' && s.class === 'completed'), sessions.map(s => `${s.stack}:${s.phase}:${s.status}:${s.class}`).join());
  // the barrier: every v1 was scored (score.json on disk) before any v2 session started
  const scoreTime = rid => statSync(path.join(ctx.sessionsRoot, rid, 'score', 'score.json')).mtimeMs;
  const v1 = sessions.filter(s => s.phase === 'v1');
  const v2 = sessions.filter(s => s.phase === 'v2');
  const startedAt = rid => Date.parse(JSON.parse(readFileSync(path.join(ctx.sessionsRoot, rid, 'meta.json'), 'utf8')).startedAt);
  assert.ok(Math.max(...v1.map(s => scoreTime(s.rid))) <= Math.min(...v2.map(s => startedAt(s.rid))), 'both v1 scored before the first v2 started');
  for (const s of sessions) {
    const rec = JSON.parse(readFileSync(path.join(ctx.sessionsRoot, s.rid, 'record.json'), 'utf8'));
    assert.equal(rec.ids.rid, s.rid);
    assert.equal(rec.class, 'completed');
    assert.equal(rec.quality.buildOk.known, true, 'the real scorer ran and its result is in the record');
    assert.equal(rec.cost.brokerUsd.known, true);
    assert.equal(rec.process.compliance, 'n/a (bare)');
    const dir = path.join(ctx.sessionsRoot, s.rid);
    assert.deepEqual(readdirSync(path.join(dir, 'net')).filter(f => f.endsWith('.sock')), [], 'no socket is left');
    assert.equal(cellProcesses(`cell-${s.rid}`, realProcApi).pids.length, 0, 'no cell process is left');
    assert.equal(JSON.parse(readFileSync(path.join(dir, 'state.json'), 'utf8')).phase, 'closed');
  }
  assert.ok(existsSync(path.join(seriesDir, 'pairs', 'p1.json')));
  assert.deepEqual(readdirSync(projectDir).filter(f => f.startsWith('scratch-note-')), [], 'every canary was removed');
  assert.equal(readLedger(ctx.ledgerFile).length, 4);
  assert.equal(ridOf({ seed: SEED, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1 }).length, 6);
});
