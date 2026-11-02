#!/usr/bin/env node
// The host probe of the execution layer (E1.S8b), run THROUGH the layer: the real prefix, the delegated scope, the v2 box. It records every line of the plan's probe list and the memory view
// at 256 and 512 MiB, then exits 3 naming the first failed line (or `memory-policy-undecided` / `memory-view-mismatch` / `memory-not-certifiable`).
//
//   node spikes/lang-bench/exec-probe.mjs [--out <file>] [--policy <file>] [--pinned-identity <file>] [--runner-pin <file>] [--no-specs]
//
// The record goes to inputs/exec-probe-<YYYY-MM-DD>[-<n>].json (the date, the host, the layer identity, allow-listed environment keys only); the first of a day has no suffix, a second run is `-2`, a
// third `-3`. A record is NEVER overwritten (T-L53, parent D59), and its path is RESERVED before the probe starts (an empty placeholder, created exclusively): a --out path that exists or is held by another
// probe is refused with exit 3 before anything is probed, and a probe that fails or is stopped removes its empty placeholder. The record's `layerIdentity` is COMPUTED from the tree that ran; a tree that is not
// the pinned identity (`--pinned-identity` names another pin file) is exit 3 `layer-identity-mismatch`, before probing. The record names the runner's version and manifest. Text that reaches the terminal carries
// no control character (`\n`, `\r` and `\t` are shown as escapes). Every failure after the reservation, a SIGINT and a SIGTERM release an unwritten placeholder; a signal also ends every run the probe opened
// (the scopes, the holder, the views) before it exits 130 or 143. Exit 4: the record path cannot be created.
// A probe that exited 0 also writes `probedOn` and `host` into the layer's identity file (inputs/specs.d/exec-layer.json) and re-assembles inputs/specs.json (not with --no-specs).
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { cleanupLiveRuns } from './lib/exec-layer/cleanup.mjs';
import { computeIdentity, LAYER_DIR } from './lib/exec-layer/identity.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { computePin, verifyPin } from './lib/runner-pin.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';
import { assemble, canonicalJson } from './lib/specs-record.mjs';
import { printable } from './lib/exec-probe/printable.mjs';
import { runProbe, shouldStampProbedOn, writeRecord } from './lib/exec-probe/probe.mjs';
import { RecordExists, releaseRecord, reserveNextRecord, reserveRecord } from './lib/exec-probe/record-path.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = name => (args.includes(name) ? args[args.indexOf(name) + 1] : undefined);
const say = (stream, text) => stream.write(`${printable(text)}\n`);
const stop = (code, reason) => { say(process.stderr, `exec-probe: ${reason}`); process.exit(code); };
let held = null;
const release = () => { if (held !== null) releaseRecord(held); };
const refuse = (code, reason) => { release(); stop(code, reason); };

async function ownedHelper() {
  const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
  try {
    return await import(url.href);
  } catch (error) {
    return refuse(2, `the conventions mount's owned-name helper is needed and cannot be loaded (${error.code ?? error.message}): ${fileURLToPath(url)}`);
  }
}

/** The path of the record, RESERVED before anything is probed (P1, T-L53): a path that exists or is held by another probe is exit 3. */
function reservation() {
  const inputsDir = path.join(spike, 'inputs');
  try {
    return option('--out') === undefined ? reserveNextRecord({ inputsDir, date: new Date().toISOString().slice(0, 10) }) : reserveRecord(option('--out'));
  } catch (error) {
    if (error instanceof RecordExists) return stop(3, error.message);
    return stop(error.exitCode ?? 2, error.message);
  }
}

function pinnedIdentity() {
  try {
    return JSON.parse(readFileSync(option('--pinned-identity') ?? path.join(spike, 'inputs', 'specs.d', 'exec-layer.json'), 'utf8')).execLayer.treeSha256;
  } catch {
    return null;
  }
}

/** The layer identity COMPUTED from the tree that runs (Q6), exit 3 when it is not the pinned one; and the runner's version and manifest, exit 3 when the runner is not its pin. */
function identities() {
  const live = computeIdentity(LAYER_DIR);
  if (!live.ok) return refuse(2, live.reason);
  const treeSha256 = live.value.execLayer.treeSha256;
  if (treeSha256 !== pinnedIdentity()) return refuse(3, `layer-identity-mismatch: the layer's tree ${treeSha256} is not the pinned identity: pin it (pin-exec-layer.mjs) before a probe, so the record names a pinned layer`);
  const runnerDir = path.join(spike, 'runner');
  const pin = computePin(runnerDir);
  const committed = JSON.parse(readFileSync(option('--runner-pin') ?? path.join(spike, 'inputs', 'specs.d', 'runner.json'), 'utf8'));
  const verdict = pin.ok ? verifyPin(runnerDir, committed) : pin;
  if (!verdict.ok) return refuse(3, `runner-pin-mismatch: ${verdict.reason}`);
  return { identity: treeSha256, runner: { version: pin.value.runner.version, manifestSha256: pin.value.runner.manifestSha256 } };
}

function emptyProgramTree(owned, scope) {
  const tree = owned.ownedDir(scope, 'emptytree');
  const samplesDir = owned.ownedDir(scope, 'emptysamples');
  writeFileSync(path.join(tree, 'run.sh'), '#!/bin/bash\nexec cat\n', { mode: 0o755 });
  writeFileSync(path.join(samplesDir, 'p1.in'), 'hello\n');
  writeFileSync(path.join(samplesDir, 'p1.out'), 'hello\n');
  return { tree, samplesDir };
}

function recordProbedOn(record) {
  const file = path.join(spike, 'inputs', 'specs.d', 'exec-layer.json');
  const current = JSON.parse(readFileSync(file, 'utf8'));
  writeFileSync(file, canonicalJson({ execLayer: { ...current.execLayer, probedOn: record.probedOn, host: record.host } }));
  const built = assemble(path.join(spike, 'inputs'));
  if (built.ok) writeFileSync(path.join(spike, 'inputs', 'specs.json'), built.text);
}

function report(record, out) {
  for (const line of record.lines) say(process.stdout, `${line.ok ? 'ok  ' : 'FAIL'} ${line.id}`);
  for (const problem of record.memoryView.verdict.problems) say(process.stdout, `memory: ${problem}`);
  say(process.stdout, `exec-probe: record written to ${out}`);
  if (record.exit.halt !== null) say(process.stderr, `exec-probe: ${record.exit.halt}`);
}

let scope = null;
const bounded = (promise, ms) => Promise.race([promise, new Promise(resolve => setTimeout(resolve, ms).unref())]);

/** The cleanup of everything the probe started (V5): the placeholder, every open run (scopes, holder, views), the owned folders — each bounded, so a stuck step cannot hold the exit forever. */
async function cleanEverything() {
  release();
  await bounded(cleanupLiveRuns(), 20_000);
  if (scope !== null) await bounded(scope.close(), 5_000);
}

let stopping = false;
for (const [signal, code] of [['SIGINT', 130], ['SIGTERM', 143]]) {
  process.on(signal, () => {
    if (stopping) return;
    stopping = true;
    cleanEverything().finally(() => process.exit(code));
  });
}
process.on('exit', release); // a path that ends the process any other way still releases an unwritten placeholder

held = reservation();
let code = 2;
try {
  const { identity, runner } = identities();
  const stacks = loadStacksLang(JSON.parse(readFileSync(path.join(spike, 'inputs', 'stacks-lang.json'), 'utf8')));
  if (!stacks.ok) refuse(2, stacks.reason);
  const dirs = storeDirs(stacks.value, path.join(os.homedir(), ...stacks.value.storeRoot));
  const helper = await ownedHelper();
  const owned = helper.ownedFor({ token: 'bench' });
  scope = owned.cleanupScope();
  const result = await runProbe({ owned, scope, dirs, policyPath: option('--policy') ?? path.join(spike, 'inputs', 'specs.d', 'memory-policy.json'), prepare: () => emptyProgramTree(owned, scope), identity, runner, progress: text => say(process.stderr, `exec-probe: ${text}`) });
  if (!result.ok) refuse(2, result.reason);
  const { value: record } = result;
  const out = held.file;
  writeRecord(out, record, held);
  held = null; // written: the file is the record now, never released
  report(record, out);
  if (!args.includes('--no-specs') && shouldStampProbedOn(record)) recordProbedOn(record);
  code = record.exit.code;
} catch (error) {
  say(process.stderr, `exec-probe: failed: ${error.message}`);
  code = 2;
} finally {
  await cleanEverything();
}
process.exit(code);
