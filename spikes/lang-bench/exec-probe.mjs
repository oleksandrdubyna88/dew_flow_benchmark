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
import { readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { cleanupLiveRuns, describeLiveRuns } from './lib/exec-layer/cleanup.mjs';
import { computeIdentity, LAYER_DIR } from './lib/exec-layer/identity.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { computePin, verifyPin } from './lib/runner-pin.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';
import { printable } from './lib/exec-probe/printable.mjs';
import { ownedDirRunning } from './lib/exec-layer/stopping.mjs';
import { endOfProbe } from './lib/exec-probe/command-end.mjs';
import { emptyProgramTree } from './lib/exec-probe/empty-tree.mjs';
import { runProbe } from './lib/exec-probe/probe.mjs';
import { retireStartupStrays } from './lib/exec-probe/strays.mjs';
import { makeReservation, RecordExists, reserveNextRecord, reserveRecord } from './lib/exec-probe/record-path.mjs';
import { endArguments, installStopHandlers, newState } from './lib/exec-probe/wiring.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = name => (args.includes(name) ? args[args.indexOf(name) + 1] : undefined);
const say = (stream, text) => stream.write(`${printable(text)}\n`);
const stop = (code, reason) => { say(process.stderr, `exec-probe: ${reason}`); process.exit(code); };
let reservationHolder = makeReservation(null);
const release = () => reservationHolder.release();
/** A refusal of the probe with its exit code. Inside the probe's `try` it is thrown and turned into the exit code by the `catch`, so the `finally` runs (L4: it closes the owned folders); `refuse` never calls `process.exit`. */
class Refusal extends Error {
  constructor(code, reason) {
    super(reason);
    this.code = code;
  }
}
const refuse = (code, reason) => { throw new Refusal(code, reason); };

async function ownedHelper() {
  const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
  try {
    return await import(url.href);
  } catch (error) {
    return refuse(2, `the conventions mount's owned-name helper is needed and cannot be loaded (${error.code ?? error.message}): ${fileURLToPath(url)}`);
  }
}

/** The strays of dead probes in the folders this probe will use (L3): `inputs/` and the folder of an `--out` file; the warning for a retire file that says committing. */
function sweepStrays() {
  const inputsDir = path.join(spike, 'inputs');
  const report = retireStartupStrays({ dirs: [inputsDir, ...(option('--out') === undefined ? [] : [path.dirname(path.resolve(option('--out')))])] });
  if (report.removed.length > 0) say(process.stderr, `exec-probe: retired ${report.removed.length} stray file(s) of dead probes`);
  if (report.committing.length > 0) say(process.stderr, `exec-probe: a dead probe's lock said committing: the pins MAY be stamped without a record; check \`git diff -- spikes/lang-bench/inputs/specs.json spikes/lang-bench/inputs/specs.d/exec-layer.json\`; if the pins name a probe with no record, run the probe again (it re-stamps them) or restore them with \`git checkout -- <pins>\``);
  report.errors.forEach(text => say(process.stderr, `exec-probe: stray sweep: ${text}`));
}

/** The path of the record, RESERVED before anything is probed (P1, T-L53): a path that exists or is held by another probe is exit 3. */
function reservation() {
  sweepStrays();
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

function report(record, out) {
  for (const line of record.lines) say(process.stdout, `${line.ok ? 'ok  ' : 'FAIL'} ${line.id}`);
  for (const problem of record.memoryView.verdict.problems) say(process.stdout, `memory: ${problem}`);
  say(process.stdout, `exec-probe: record written to ${out}`);
  if (record.exit.halt !== null) say(process.stderr, `exec-probe: ${record.exit.halt}`);
}

let scope = null;

/** The cleanup of everything the probe started (V5, B1): every tracked child, every open run (scopes, holder, views); the caller bounds it. */
const cleanLive = () => cleanupLiveRuns();

let stopping = false;
const state = newState(); // `state.written`: the record's exit code once it is in place (a stop after that exits with it)
installStopHandlers({ state, say: text => say(process.stderr, text), release, describeLive: describeLiveRuns, cleanLive, closeScope: async () => scope?.close(), onStopping: () => { stopping = true; } });
process.on('exit', release); // a path that ends the process any other way still releases an unwritten placeholder (once: the reservation forgets it)

reservationHolder = makeReservation(reservation());
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
  if (stopping) await new Promise(() => {}); // a stopped probe writes nothing: the signal's own cleanup ends the process with 130 or 143
  if (!result.ok) refuse(2, result.reason);
  const { value: record } = result;
  const stageDir = ownedDirRunning(owned, scope, 'stamp');
  code = endOfProbe(endArguments({ record, reservation: reservationHolder, noSpecs: args.includes('--no-specs'), inputsDir: path.join(spike, 'inputs'), stageDir, say: text => say(process.stderr, text), report, state }));
} catch (error) {
  if (stopping) await new Promise(() => {}); // a refusal caused by the stop (no scope is started while stopping) is not this probe's failure: the signal's cleanup ends the process with 130 or 143
  if (error instanceof Refusal) {
    say(process.stderr, `exec-probe: ${error.message}`);
    code = error.code;
  } else {
    say(process.stderr, `exec-probe: failed: ${error.message}`);
    code = 2;
  }
} finally {
  release();
  if (scope !== null) await scope.close();
}
process.exit(code);
