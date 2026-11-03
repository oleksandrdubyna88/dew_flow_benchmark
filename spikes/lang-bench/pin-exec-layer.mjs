#!/usr/bin/env node
// Pins the execution layer (E1.S8b): writes inputs/specs.d/exec-layer.json (the tree's sha256) and re-assembles inputs/specs.json. Run it LAST, after every edit of lib/exec-layer/.
//
//   node spikes/lang-bench/pin-exec-layer.mjs           write the identity
//   node spikes/lang-bench/pin-exec-layer.mjs --check   exit 1 when lib/exec-layer/ differs from the committed identity, change nothing
//
// Exit 0 done / equal; 1 --check found a difference; 2 the tree cannot be pinned (a link or another kind of entry). The probe's `probedOn` and `host` (E1.S8b brief B) are kept when present.
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { computeIdentity, LAYER_DIR, verifyIdentity } from './lib/exec-layer/identity.mjs';
import { assemble, canonicalJson } from './lib/specs-record.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const recordFile = path.join(spike, 'inputs', 'specs.d', 'exec-layer.json');
const checking = process.argv.includes('--check');

function committed() {
  try {
    return JSON.parse(readFileSync(recordFile, 'utf8'));
  } catch {
    return { execLayer: { files: [], treeSha256: '' } };
  }
}

const identity = computeIdentity(LAYER_DIR);
if (!identity.ok) {
  process.stderr.write(`pin-exec-layer: ${identity.reason}\n`);
  process.exit(2);
}
if (checking) {
  const verdict = verifyIdentity(LAYER_DIR, committed());
  if (!verdict.ok) process.stderr.write(`pin-exec-layer: ${verdict.reason}\n`);
  process.exit(verdict.ok ? 0 : 1);
}
const { probedOn, host } = committed().execLayer;
writeFileSync(recordFile, canonicalJson({ execLayer: { ...identity.value.execLayer, ...(probedOn === undefined ? {} : { probedOn }), ...(host === undefined ? {} : { host }) } }));
const built = assemble(path.join(spike, 'inputs'));
if (!built.ok) {
  process.stderr.write(`pin-exec-layer: ${built.reason}\n`);
  process.exit(2);
}
writeFileSync(path.join(spike, 'inputs', 'specs.json'), built.text);
process.stdout.write(`pin-exec-layer: ${identity.value.execLayer.files.length} files, tree ${identity.value.execLayer.treeSha256}\n`);
