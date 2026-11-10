#!/usr/bin/env node
// Assembles inputs/specs.json from the writers' files in inputs/specs.d/ (never edit specs.json by hand).
//
//   node spikes/lang-bench/specs-record.mjs           write inputs/specs.json
//   node spikes/lang-bench/specs-record.mjs --check   exit 1 when it differs from the assembly, change nothing
//
// Exit 0 done / equal; 1 --check found a difference; 2 a writer's file is unreadable, claims a taken key, or does not belong.
import { runSpecsRecord } from './lib/specs-record-cli.mjs';

const result = await runSpecsRecord(process.argv.slice(2), { env: process.env });
process.stdout.write(result.stdout);
process.stderr.write(result.stderr);
process.exit(result.exit);
