#!/usr/bin/env node
// Writes inputs/prediction.md (E3b.S3): the bare parts of parent section 11's prediction, quoted from a commit of origin/main with the process clauses elided and
// MARKED as elided, then the bare-first weakenings extracted from the E3b plan. Nothing is typed.
//   node spikes/minigit-bench/prereg/extract-prediction.mjs --parent-commit 85c54b7 [--plan <E3b plan>] [--out <file>]
// Exit 0 written | 4 usage | 5 a block is missing.
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { blockAt } from './blocks.mjs';
import { parseParentFlags } from './cli.mjs';
import { fullSha, readAtCommit } from './parent.mjs';
import { fail, ok } from '../lib/result.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const PLAN = 'todo/PLAN_minigit_stack_bench_E3_bare.md';
export const ELIDED = '[…process clause — registered with the process scope…]';
export const ELIDED_TWO = '[…two process clauses — registered with the process scope…]';
const PREAMBLE = /^\*\(The ranges below[\s\S]*?\)\*$/m;
const BARE_ITEMS = Object.freeze([{ n: 1 }, { n: 2 }, { n: 5, drop: /^no process cell/ }, { n: 7, drop: /^(process scoring|any process session)/, mark: ELIDED_TWO }]);

const itemBlock = (text, n) => {
  const b = blockAt(text, `${n}. `);
  return b.missing ? null : b.lines.join('\n');
};

/** The `;`-separated parts of an item with its process clauses (`drop`) replaced, once, by `mark`. */
function keptParts(parts, drop, mark) {
  const kept = [];
  let marked = false;
  for (const p of parts) {
    if (!drop.test(p)) kept.push(p);
    else if (!marked) { kept.push(mark); marked = true; }
  }
  return kept;
}

/** One item with its process clauses (`;`-separated) replaced by the elision marker, in place. */
function bareItem(block, { drop, mark = ELIDED }) {
  if (!drop) return block;
  const head = /^(\d+\. [A-Za-z ]+: )/.exec(block)?.[1] ?? '';
  const parts = block.slice(head.length).split(/;\s+/).map(p => p.replace(/\s+/g, ' '));
  const text = `${head.replace(/\s+/g, ' ')}${keptParts(parts, drop, mark).join('; ')}`;
  return text.endsWith(']') ? `${text}.` : text;
}

/** The parent's text -> `ok({ preamble, items: [string] })` | `fail`. The items are in the parent's own words, line wraps kept for items quoted whole. */
export function extractPrediction(parentText) {
  const section = parentText.slice(parentText.indexOf('**Prediction (to be committed verbatim'));
  const preamble = PREAMBLE.exec(section)?.[0];
  if (!preamble) return fail('the parent plan holds no prediction preamble');
  const items = BARE_ITEMS.map(spec => ({ spec, block: itemBlock(section, spec.n) }));
  const missing = items.find(i => i.block === null);
  return missing ? fail(`the parent's prediction has no item ${missing.spec.n}`) : ok({ preamble, items: items.map(i => bareItem(i.block, i.spec)) });
}

const isContinuation = (items, l) => items.length > 0 && /^\s+\S/.test(l);
const endsList = (items, l) => items.length >= 5 && l.trim() === '';

/** One line of the list: a new item, or the continuation of the last one. */
function append(items, l) {
  if (/^\d+\. /.test(l)) items.push(l);
  else if (isContinuation(items, l)) items[items.length - 1] += `\n${l}`;
}

/** The numbered items of the list, each with its wrapped lines, up to the blank line after the fifth. */
function weakeningItems(lines) {
  const items = [];
  for (const l of lines) {
    if (endsList(items, l)) break;
    append(items, l);
  }
  return items;
}

/** The five weakenings under `**What the bare-first deviation weakens**` in the E3b plan, as written there. */
export function extractWeakenings(planText) {
  const start = planText.indexOf('**What the bare-first deviation weakens**');
  if (start < 0) return fail('the E3b plan has no "What the bare-first deviation weakens" list');
  const items = weakeningItems(planText.slice(start).split('\n').slice(1));
  return items.length === 5 ? ok(items) : fail(`expected five weakenings in the E3b plan, found ${items.length}`);
}

export function renderPrediction({ prediction, weakenings, commit }) {
  return [
    '# Prediction, bare part', '',
    `Quoted from \`todo/PLAN_minigit_stack_bench.md\` section 11 at \`${commit}\`, a commit of origin/main (the full prediction has been public since \`01b7459\`); `
    + `\`prereg/extract-prediction.mjs\` wrote it. Process clauses are elided and marked; items 3, 4 and 6 are process items and stand in the parent at the named commits.`, '',
    ...prediction.preamble.split('\n').map(l => `> ${l}`), '', ...prediction.items.flatMap(i => [i, '']),
    '## What the bare-first deviation weakens', '', 'Extracted from the E3b plan (`todo/PLAN_minigit_stack_bench_E3_bare.md`):', '', ...weakenings.flatMap(w => [w, '']),
    'The C#/TS comparison **inside** the bare arm keeps its concurrent pairs and loses nothing.', '',
  ].join('\n');
}

/** The prediction of the parent plan at `commit` and the weakenings of the E3b plan: `ok({ prediction, weakenings })` | `fail`. */
async function readParts({ repo, commit, plan }) {
  const text = await readAtCommit({ repo, commit });
  if (!text.ok) return fail(`cannot read the parent plan: ${text.reason}`);
  const prediction = extractPrediction(text.value);
  const weakenings = extractWeakenings(readFileSync(plan ?? path.join(repo, PLAN), 'utf8'));
  const bad = [prediction, weakenings].find(r => !r.ok);
  return bad ?? ok({ prediction: prediction.value, weakenings: weakenings.value });
}

export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  const flags = parseParentFlags(argv, { plan: { type: 'string' } });
  if (!flags.ok) { write(flags.reason); return 4; }
  const commit = flags.value['parent-commit'];
  const repo = path.resolve(SPIKE, '..', '..');
  const parts = await readParts({ repo, commit, plan: flags.value.plan });
  if (!parts.ok) { write(parts.reason); return 5; }
  const sha = await fullSha({ repo, commit });
  const out = flags.value.out ?? path.join(SPIKE, 'inputs', 'prediction.md');
  writeFileSync(out, renderPrediction({ ...parts.value, commit: sha.ok ? sha.value : commit }));
  write(`written ${out}`);
  return 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
