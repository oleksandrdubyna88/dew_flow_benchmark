#!/usr/bin/env node
// Writes inputs/prompts-bare.json: the exact bare v1 / v2 prompt bytes per stack, DERIVED from parent plan 4.3 at a commit of origin/main.
//   node spikes/minigit-bench/prereg/extract-prompts.mjs --parent-commit 85c54b7 [--out <file>]
// Exit 0 written | 4 usage | 5 the plan at that commit does not hold what this script reads.
import { createHash } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../lib/result.mjs';
import { parseParentFlags } from './cli.mjs';
import { fullSha, readAtCommit } from './parent.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const LABELS = Object.freeze({ csharp: 'C#', node: 'TypeScript' });
const sha256 = text => createHash('sha256').update(text).digest('hex');

/** The templates and stack sentences of the parent's section 4.3 -> `ok({ templates: { v1, v2 }, sentences: { csharp, node } })` | `fail`. */
export function extractPrompts(planText) {
  const v1 = /^v1: (.+)$/m.exec(planText)?.[1];
  const v2 = /^v2: (.+)$/m.exec(planText)?.[1];
  const sentences = /Stack sentences: C# — \*"(.+?)"\* TS — \*"([\s\S]+?)"\*/.exec(planText);
  if (!v1 || !v2) return fail('the parent plan holds no bare `v1:` / `v2:` prompt lines in section 4.3');
  if (!sentences) return fail('the parent plan holds no `Stack sentences:` line in section 4.3');
  const unwrap = s => s.replace(/\s*\n\s*/g, ' ');
  return ok({ templates: { v1, v2 }, sentences: { csharp: unwrap(sentences[1]), node: unwrap(sentences[2]) } });
}

/** The registered file's content from the extraction, with each filled prompt and its sha256. */
export function buildPrompts({ extracted, commit }) {
  const slots = Object.fromEntries(Object.keys(LABELS).map(s => [s, { STACK: LABELS[s], STACK_SENTENCE: extracted.sentences[s] }]));
  const fill = (stack, phase) => extracted.templates[phase].replaceAll('{STACK_SENTENCE}', slots[stack].STACK_SENTENCE).replaceAll('{STACK}', slots[stack].STACK);
  const prompts = Object.fromEntries(Object.keys(LABELS).map(s => [s, Object.fromEntries(['v1', 'v2'].map(p => { const text = fill(s, p); return [p, { text, sha256: sha256(text) }]; }))]));
  return { source: { file: 'todo/PLAN_minigit_stack_bench.md', commit, section: '4.3' }, templates: extracted.templates, slots, prompts };
}

/** The prompts of the parent plan at `commit`: `ok(extracted)` | `fail`. */
async function readPrompts({ repo, commit }) {
  const text = await readAtCommit({ repo, commit });
  if (!text.ok) return fail(`cannot read the parent plan: ${text.reason}`);
  return extractPrompts(text.value);
}

export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  const flags = parseParentFlags(argv);
  if (!flags.ok) { write(flags.reason); return 4; }
  const commit = flags.value['parent-commit'];
  const repo = path.resolve(SPIKE, '..', '..');
  const extracted = await readPrompts({ repo, commit });
  if (!extracted.ok) { write(extracted.reason); return 5; }
  const sha = await fullSha({ repo, commit });
  const out = flags.value.out ?? path.join(SPIKE, 'inputs', 'prompts-bare.json');
  writeFileSync(out, `${JSON.stringify(buildPrompts({ extracted: extracted.value, commit: sha.ok ? sha.value : commit }), null, 2)}\n`);
  write(`written ${out}`);
  return 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
