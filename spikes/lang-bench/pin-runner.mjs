#!/usr/bin/env node
// Pins runner/ (E1.S8a): writes inputs/specs.d/runner.json and re-assembles inputs/specs.json. Run it LAST, after every runner edit.
//
//   node spikes/lang-bench/pin-runner.mjs           write the pin
//   node spikes/lang-bench/pin-runner.mjs --check   exit 1 when runner/ differs from the committed pin, change nothing
//
// Exit 0 done / equal; 1 --check found a difference; 2 the runner folder cannot be pinned (a stray file, no version line).
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { computePin, verifyPin } from './lib/runner-pin.mjs';
import { assemble, canonicalJson } from './lib/specs-record.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const recordFile = path.join(spike, 'inputs', 'specs.d', 'runner.json');
const checking = process.argv.includes('--check');

function committedPin() {
  try {
    return JSON.parse(readFileSync(recordFile, 'utf8'));
  } catch {
    return { runner: { files: [], version: '', manifestSha256: '' } };
  }
}

const pin = computePin(path.join(spike, 'runner'));
if (!pin.ok) {
  process.stderr.write(`pin-runner: ${pin.reason}\n`);
  process.exit(2);
}
if (checking) {
  const verdict = verifyPin(path.join(spike, 'runner'), committedPin());
  if (!verdict.ok) process.stderr.write(`pin-runner: ${verdict.reason}\n`);
  process.exit(verdict.ok ? 0 : 1);
}
writeFileSync(recordFile, canonicalJson(pin.value));
const built = assemble(path.join(spike, 'inputs'));
if (!built.ok) {
  process.stderr.write(`pin-runner: ${built.reason}\n`);
  process.exit(2);
}
writeFileSync(path.join(spike, 'inputs', 'specs.json'), built.text);
process.stdout.write(`pin-runner: ${pin.value.runner.version}, ${pin.value.runner.files.length} files, manifest ${pin.value.runner.manifestSha256}\n`);
