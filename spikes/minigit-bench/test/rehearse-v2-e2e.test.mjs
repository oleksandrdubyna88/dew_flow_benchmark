// The v2 rehearsal end to end (E3b.S4, G-14): the real box, broker, proxy and scorer behind a STUB CLI and a loopback stub upstream, with a FAKE token; nothing is paid and no real token is
// read. This file keeps the real HOME on purpose (the real scorer decrypts the held-out suite under it) and so imports neither the CLI nor the wiring: it has no road to the token reader
// (fake-home.test.mjs checks both). The command's own tests, which DO go through `main`, are in rehearse-v2.test.mjs under a fake HOME.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasStore } from '../lib/alias-store.mjs';
import { EXIT } from '../lib/bare-series.mjs';
import { rehearseV2 } from '../lib/rehearsal.mjs';
import { readLedger } from '../lib/ledger.mjs';
import { layoutPaths, sessionLayout } from '../lib/sandbox.mjs';
import { openSeries, planSession, readRunMap, transition, writeRunMap } from '../lib/run-map.mjs';
import { scoreSession } from '../lib/score-run.mjs';
import { fillV1 } from '../lib/workspace.mjs';
import { prices } from './broker-stub.mjs';
import { context, write } from './bare-ctx.mjs';
import { TS_DIR } from './box-helpers.mjs';
import { homeDir, inputs, keyGate, pythonGate, sandboxGate, spikeDir } from './helpers.mjs';

test('A_v2_rehearsal_runs_end_to_end_without_a_real_token_and_G14_holds', { timeout: 900_000 }, async t => {
  if (!sandboxGate(t) || !keyGate(t) || !pythonGate(t)) return;
  const fetched = await inputs();
  const base = context();
  const seriesDir = homeDir('rehearse');
  mkdirSync(path.join(seriesDir, 'canaries'), { recursive: true });
  const policy = crosscheckPolicyFrom(spikeDir, {});
  const ctx = {
    ...base, seriesDir, sessionsRoot: path.join(seriesDir, 's'), inputsRoot: fetched.root, hashes: fetched.entries, kind: 'rehearsal', pair: 0, seed: null,
    runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson'), ceilingUsd: 500, bareBudgetUsd: 40, caps: { capUsd: 6, wallMs: 120_000, stallMs: 60_000 },
    claudeBinary: '/the/real/pinned/claude', cliVersion: '2.1.310', nodeDir: path.dirname(path.dirname(process.execPath)), tsDir: TS_DIR, token: 'REAL-TOKEN-MUST-NOT-BE-USED', prices: prices(), table: policy.value.table, policy: policy.value.policy,
    harnessSources: { boxInit: path.join(spikeDir, 'harness', 'box-init.mjs'), audit: path.join(spikeDir, '..', 'policy-ab-quick', 'hooks', 'audit.mjs') },
    snapshotPackages: [], packageRules: [], handVerdictsFor: () => [], harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', abortReason: () => null, abortExit: () => 130, spin: { medianMs: 20 },
    aliasStore: aliasStore(path.join(seriesDir, 'aliases.json')),
  };
  // the twin: a finished, scored A/A v1 session whose tree holds source and build output
  const twinLayout = sessionLayout(ctx.sessionsRoot, 'aaaaaa').value;
  fillV1({ inputsRoot: ctx.inputsRoot, hashes: ctx.hashes, work: twinLayout.work });
  for (const [p, text] of Object.entries({ 'Program.cs': 'class P {}', 'bin/Debug/x.dll': 'dll', 'obj/y.json': '{}' })) write(path.join(twinLayout.work, p), text);
  let map = openSeries({ seriesId: 's-e2e', seedSha256: 'b'.repeat(64), harnessIdentity: ctx.harnessIdentity, preregTag: ctx.preregTag, preregCommit: 'c'.repeat(40), pins: {}, stacksSha256: 'd', pricesSha256: 'e', snapshots: {}, caps: ctx.caps, bareBudgetUsd: 40, ceilingUsd: 500 });
  map = planSession(map, { rid: 'aaaaaa', pair: 0, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'aa', label: 'paired', mask: '0-5', twin: 'a' }).value;
  for (const to of ['launching', 'ran', 'scored']) map = transition(map, 'aaaaaa', to, to === 'ran' ? { class: 'completed', data: true } : {}).value;
  writeRunMap(ctx.runMapFile, map);
  const lines = [];
  const deps = {
    guard: async () => ({ ok: true }), retireStale: async () => {}, preflight: async () => ({ ok: true }), quiet: async () => { throw new Error('a rehearsal measures nothing: no quiet check'); },
    startSampler: () => ({ stop: async () => ({ samples: 0, flags: [], error: null }) }), loadFlag: async () => false, now: Date.now,
    scoreSession: arg => scoreSession({ ...arg, sleep: async () => {} }), phase: { maskCheckMs: 1500, listProjectDirs: () => { throw new Error('a rehearsal never plants a canary in the operator\'s folders'); } },
  };
  const r = await rehearseV2({ ctx, deps, map, say: l => lines.push(l) });
  assert.equal(r.exit, EXIT.done, `${r.message}\n${lines.join('\n')}`);
  assert.equal(r.verdict.holds, true, JSON.stringify(r.verdict.checks));
  const final = readRunMap(ctx.runMapFile).value;
  const rehearsal = Object.values(final.sessions).find(s => s.kind === 'rehearsal');
  assert.equal(rehearsal.status, 'scored');
  assert.equal(rehearsal.class, 'completed');
  const work = layoutPaths(ctx.sessionsRoot, rehearsal.rid).work;
  assert.ok(existsSync(path.join(work, 'Program.cs')) && !existsSync(path.join(work, 'bin')) && existsSync(path.join(work, 'SPEC-v2.txt')), 'the v2 workspace: the twin\'s source, no build output, the v2 inputs');
  const record = JSON.parse(readFileSync(path.join(ctx.sessionsRoot, rehearsal.rid, 'record.json'), 'utf8'));
  assert.equal(record.workspace.value.excludedDirs.includes('bin'), true);
  assert.equal(JSON.parse(readFileSync(path.join(seriesDir, 'rehearsal-v2.json'), 'utf8')).holds, true, 'the verdict is written for aa-verdicts');
  assert.ok(readLedger(ctx.ledgerFile).every(l => l.kind === 'rehearsal' && l.usd === 0), 'a stub run costs nothing and says so');
  assert.equal(existsSync(path.join(ctx.sessionsRoot, rehearsal.rid, 'broker.ndjson')), true);
  assert.doesNotMatch(readFileSync(path.join(ctx.sessionsRoot, rehearsal.rid, 'broker.ndjson'), 'utf8') + readFileSync(path.join(ctx.sessionsRoot, rehearsal.rid, 'argv.json'), 'utf8'), /REAL-TOKEN-MUST-NOT-BE-USED/, 'the real token never reached the broker or an argv');
});
