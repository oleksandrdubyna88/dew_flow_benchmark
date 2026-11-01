#!/usr/bin/env node
// Per task, csharp/<t>/ and node/<t>/ differ only in each stack's pin files and in the marked sentences of STATEMENT.md.
//
//   node spikes/lang-bench/workspace-diff.mjs --root <task repository checkout>
//
// Exit 0 the pairs agree, 1 a difference (each is printed as `<path>: <what>`), 4 usage. Nothing is changed.
import { parseArgs } from 'node:util';
import { diffAllTasks } from './lib/workspace-diff.mjs';

let root;
try {
  root = parseArgs({ options: { root: { type: 'string' } }, strict: true }).values.root;
} catch (error) {
  process.stderr.write(`workspace-diff: ${error.message}\n`);
}
if (root === undefined) {
  process.stderr.write('workspace-diff: give --root <task repository checkout>\n');
  process.exitCode = 4;
} else {
  const result = diffAllTasks({ root });
  process.stdout.write(`${result.findings.map(f => `${f.path}: ${f.what}`).concat(result.ok ? ['workspace-diff: the task pairs agree'] : []).join('\n')}\n`);
  process.exitCode = result.ok ? 0 : 1;
}
