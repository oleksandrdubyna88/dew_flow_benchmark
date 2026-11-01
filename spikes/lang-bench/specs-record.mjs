#!/usr/bin/env node
// Assembles inputs/specs.json from the writers' files in inputs/specs.d/ (never edit specs.json by hand).
//
//   node spikes/lang-bench/specs-record.mjs           write inputs/specs.json
//   node spikes/lang-bench/specs-record.mjs --check   exit 1 when it differs from the assembly, change nothing
//
// Exit 0 done / equal; 1 --check found a difference; 2 a writer's file is unreadable, claims a taken key, or does not belong.
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { assemble, checkRecord } from './lib/specs-record.mjs';

const inputs = path.join(fileURLToPath(new URL('.', import.meta.url)), 'inputs');
const checking = process.argv.includes('--check');
const built = assemble(inputs);
if (!built.ok) {
  process.stderr.write(`specs-record: ${built.reason}\n`);
  process.exit(2);
}
if (checking) {
  const verdict = checkRecord(inputs);
  if (!verdict.ok) process.stderr.write(`specs-record: ${verdict.reason}\n`);
  process.exit(verdict.ok ? 0 : 1);
}
writeFileSync(path.join(inputs, 'specs.json'), built.text);
process.stdout.write(`specs-record: wrote inputs/specs.json from ${built.text.length} bytes of writers\n`);
