#!/usr/bin/env node
// Writes inputs/heldout-prereg.json (E3b.S3): the held-out suite's identity, DERIVED from files that are in clear in git — the manifest, the archive's hash file,
// hashes.txt and the ids-only calibration — never typed, never decrypted (no key is read). Counts and hashes only: no case, reference or mutant is named.
//   node spikes/minigit-bench/prereg/heldout-prereg.mjs [--out <file>]
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { parseHashes } from '../lib/inputs.mjs';
import { fail, ok } from '../lib/result.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

/** `{ spike }` -> `ok(object)` | `fail` when the three sources disagree about the archive's hash or the counts. */
export function heldoutPrereg(spike = SPIKE) {
  const read = p => readFileSync(path.join(spike, p));
  const manifest = JSON.parse(read('heldout/manifest.json'));
  const calibrationBytes = read('heldout/calibration.json');
  const calibration = JSON.parse(calibrationBytes);
  const archiveSha256 = read('heldout/suite.enc.sha256').toString('utf8').split(/\s+/)[0];
  const hashes = parseHashes(read('inputs/hashes.txt').toString('utf8'));
  if (!hashes.ok) return hashes;
  const listed = hashes.value.find(h => h.path === 'heldout/suite.enc')?.sha256;
  if (listed !== archiveSha256) return fail(`the archive hash differs: suite.enc.sha256 says ${archiveSha256}, hashes.txt says ${listed}`);
  if (sha256(read('heldout/suite.enc')) !== archiveSha256) return fail('heldout/suite.enc does not hash to suite.enc.sha256');
  const counts = ['n1', 'n2', 'm'].find(k => manifest[k] !== calibration[k]);
  if (counts) return fail(`the manifest and the calibration disagree on ${counts}: ${manifest[counts]} against ${calibration[counts]}`);
  return ok({
    archiveSha256, plaintextSha256: manifest.plaintextSha256, n1: manifest.n1, n2: manifest.n2, m: manifest.m, sealedOn: manifest.sealedOn,
    calibration: { file: 'heldout/calibration.json', sha256: sha256(calibrationBytes) },
    note: 'counts and hashes only; no case, reference implementation or mutant is named in this repository',
  });
}

export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  let values;
  try { values = parseArgs({ args: argv, options: { out: { type: 'string' } } }).values; } catch (e) { write(`usage error: ${e.message}`); return 4; }
  const r = heldoutPrereg();
  if (!r.ok) { write(r.reason); return 5; }
  const out = values.out ?? path.join(SPIKE, 'inputs', 'heldout-prereg.json');
  writeFileSync(out, `${JSON.stringify(r.value, null, 2)}\n`);
  write(`written ${out}`);
  return 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
