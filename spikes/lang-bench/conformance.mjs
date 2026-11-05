#!/usr/bin/env node
// The spec conformance check (E1.S8): which parts of a task spec and its visible samples deviate from FORMAT.md.
//
//   node spikes/lang-bench/conformance.mjs --repo <task repository> --task NN-slug --format <path to FORMAT.md> [--needles <file>] [--record <format.json>]
//
// One line per check on standard output, `ok <check>` or `FAIL <check> — <what, where>`. Exit 0 every check ok; 1 a deviation; 4 a usage or format error: a missing or unresolvable
// task, spec or samples folder, a FORMAT the checker cannot read or that lacks a sentence it expects, python3 or the runner missing, the runner timing out. The spec is at
// `<repo>/todo/tasks/<task>.md`, its samples at `<repo>/tasks/<task>/samples/`. `--needles` is a file of literal needles kept outside git, one per line. `--record` names the record of the FORMATs the checker may read (default `inputs/specs.d/format.json`); the first line, `ok format sha256 … (record A, gate …)`, goes into the gate record. In group B, check an olympiad
// spec of A under A's and B's FORMAT in turn.
import { pathToFileURL } from 'node:url';
import { run } from './lib/conformance.mjs';

/** The run, with a fault of the checker itself as exit 4: a crash must never look like a deviation (exit 1) or a pass. */
async function guarded(argv) {
  try {
    return await run(argv);
  } catch (error) {
    return { exit: 4, lines: [], errors: [`conformance: the checker itself failed: ${error?.stack ?? error}`] };
  }
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const result = await guarded(process.argv.slice(2));
  process.stdout.write(result.lines.map(line => `${line}\n`).join(''));
  process.stderr.write(result.errors.map(line => `${line}\n`).join(''));
  process.exit(result.exit);
}
