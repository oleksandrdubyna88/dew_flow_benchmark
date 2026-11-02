#!/usr/bin/env node
// The host probe of the execution layer (E1.S8b), run THROUGH the layer: the real prefix, the delegated scope, the v2 box. It records every line of the plan's probe list and the memory view
// at 256 and 512 MiB, then exits 3 naming the first failed line (or `memory-policy-undecided` / `memory-view-mismatch` / `memory-not-certifiable`).
//
//   node spikes/lang-bench/exec-probe.mjs [--out <file>] [--policy <file>] [--no-specs]
//
// The record goes to inputs/exec-probe-<YYYY-MM-DD>[-<n>].json (the date, the host, the layer identity, allow-listed environment keys only); the first of a day has no suffix, a second run is `-2`, a
// third `-3`. A record is NEVER overwritten (T-L53, parent D59): a --out path that exists is refused with exit 3 before anything is probed, and the file is created exclusively. Unless --no-specs, a probe on which every line
// passed also writes `probedOn` and `host` into the layer's identity file (inputs/specs.d/exec-layer.json) and re-assembles inputs/specs.json.
// Exit 0 every line passed and a decided memory policy holds; 3 a line failed, the memory policy is undecided or not met, memory.peak cannot be read, or the record's path exists; 2 usage or the host
// cannot start it.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';
import { assemble, canonicalJson } from './lib/specs-record.mjs';

import { runProbe, writeRecord } from './lib/exec-probe/probe.mjs';
import { nextRecordPath, RecordExists } from './lib/exec-probe/record-path.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = name => (args.includes(name) ? args[args.indexOf(name) + 1] : undefined);
const usage3 = reason => { process.stderr.write(`exec-probe: ${reason}\n`); process.exit(3); };
const usage = reason => { process.stderr.write(`exec-probe: ${reason}\n`); process.exit(2); };

async function ownedHelper() {
  const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
  try {
    return await import(url.href);
  } catch (error) {
    return usage(`the conventions mount's owned-name helper is needed and cannot be loaded (${error.code ?? error.message}): ${fileURLToPath(url)}`);
  }
}

function identityOf() {
  try {
    return JSON.parse(readFileSync(path.join(spike, 'inputs', 'specs.d', 'exec-layer.json'), 'utf8')).execLayer.treeSha256;
  } catch {
    return null;
  }
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

if (option('--out') !== undefined && existsSync(option('--out'))) {
  process.stderr.write(`exec-probe: ${option('--out')} already exists: a probe record is never overwritten (T-L53); name a new file or take the default, which numbers a second run of the day\n`);
  process.exit(3);
}

const stacks = loadStacksLang(JSON.parse(readFileSync(path.join(spike, 'inputs', 'stacks-lang.json'), 'utf8')));
if (!stacks.ok) usage(stacks.reason);
const dirs = storeDirs(stacks.value, path.join(os.homedir(), ...stacks.value.storeRoot));
const helper = await ownedHelper();
const owned = helper.ownedFor({ token: 'bench' });
const scope = owned.cleanupScope();
let code = 2;
try {
  const result = await runProbe({ owned, scope, dirs, policyPath: option('--policy') ?? path.join(spike, 'inputs', 'specs.d', 'memory-policy.json'), prepare: () => emptyProgramTree(owned, scope), identity: identityOf(), progress: text => process.stderr.write(`exec-probe: ${text}\n`) });
  if (!result.ok) usage(result.reason);
  const { value: record } = result;
  const out = option('--out') ?? nextRecordPath({ inputsDir: path.join(spike, 'inputs'), date: record.date });
  mkdirSync(path.dirname(out), { recursive: true });
  try {
    writeRecord(out, record);
  } catch (error) {
    if (!(error instanceof RecordExists)) throw error;
    usage3(error.message);
  }
  for (const line of record.lines) process.stdout.write(`${line.ok ? 'ok  ' : 'FAIL'} ${line.id}\n`);
  for (const problem of record.memoryView.verdict.problems) process.stdout.write(`memory: ${problem}\n`);
  process.stdout.write(`exec-probe: record written to ${out}\n`);
  if (record.exit.halt !== null) process.stderr.write(`exec-probe: ${record.exit.halt}\n`);
  if (!args.includes('--no-specs') && record.lines.every(line => line.ok)) recordProbedOn(record);
  code = record.exit.code;
} finally {
  await scope.close();
}
process.exit(code);
