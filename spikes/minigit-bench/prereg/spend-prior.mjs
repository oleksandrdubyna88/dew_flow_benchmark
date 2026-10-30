#!/usr/bin/env node
// Writes inputs/spend-prior.json (E3b.S3): the paid instrument work before E3b, READ from its evidence files and never typed. It counts in the $500 ceiling and
// not in the $40 bare budget (`spend-gate.mjs`: kind `prior`). Each entry is `{ date, what, usd, evidence }`.
//   node spikes/minigit-bench/prereg/spend-prior.mjs [--out <file>]
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../lib/result.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const REPO = path.resolve(SPIKE, '..', '..');
const E2_PLAN = 'todo/PLAN_minigit_stack_bench_E2.md';
const FIXTURES = 'spikes/minigit-bench/test/fixtures/real';
const MODULE_DOC = 'research/module_minigit_bench.md';

const json = rel => JSON.parse(readFileSync(path.join(REPO, rel), 'utf8'));
const number = (text, re, what, file = E2_PLAN) => { const m = re.exec(text); return m ? ok(Number(m[1])) : fail(`${file} no longer says ${what}`); };

/** -> `ok([{ date, what, usd, evidence }])` | `fail` naming the evidence that could not be read. */
export function spendPrior(repo = REPO) {
  const e2 = readFileSync(path.join(repo, E2_PLAN), 'utf8');
  const design = number(e2, /cost \*\*\$([0-9.]+), 13\.7 s wall/, 'the design run cost');
  const proof = number(e2, /paid total \$([0-9.]+)\)/, 'the broker proof paid total');
  const doc = readFileSync(path.join(repo, MODULE_DOC), 'utf8');
  const first = number(doc, /FIRST smoke's \(envelope \$([0-9.]+)\)/, 'the first smoke\'s envelope', MODULE_DOC);
  const resmoke1 = number(doc, /first re-smoke[\s\S]{0,400}?envelope \*\*\$([0-9.]+)\*\*/, 'the first re-smoke\'s envelope', MODULE_DOC);
  const resmoke2 = number(doc, /second re-smoke[\s\S]{0,400}?envelope \*\*\$([0-9.]+)\*\*/, 'the second re-smoke\'s envelope', MODULE_DOC);
  const bad = [design, proof, first, resmoke1, resmoke2].find(r => !r.ok);
  if (bad) return bad;
  const capture = json(`${FIXTURES}/claude-2.1.310-capture/meta.json`);
  const smoke = json(`${FIXTURES}/claude-2.1.310-bare-smoke/meta.json`);
  return ok([
    { date: capture.date.slice(0, 10), what: 'E1.S3 capture session (one tiny session on an unrelated task)', usd: capture.costUsd, evidence: `${FIXTURES}/claude-2.1.310-capture/meta.json` },
    { date: '2026-10-29', what: 'E2.S2 design run (one bare session, real token in the box)', usd: design.value, evidence: `${E2_PLAN} (the design run paragraph)` },
    { date: '2026-10-29', what: 'E2.S2 credential-broker proof sessions', usd: proof.value, evidence: `${E2_PLAN} (the credential broker section: paid total)` },
    { date: '2026-10-30', what: 'E2.S2 bare-session smoke through the broker (the first one)', usd: first.value, evidence: `${MODULE_DOC} (the first smoke, "Replaced" note)` },
    { date: '2026-10-30', what: 'E2.S2 first re-smoke after the review fixes', usd: resmoke1.value, evidence: `${MODULE_DOC} (the first re-smoke)` },
    { date: '2026-10-30', what: 'E2.S2 second re-smoke', usd: resmoke2.value, evidence: `${MODULE_DOC} (the second re-smoke)` },
    { date: String(smoke.startedAt).slice(0, 10), what: 'E2.S2 third re-smoke, the committed fixture', usd: smoke.envelopeCostUsd, evidence: `${FIXTURES}/claude-2.1.310-bare-smoke/meta.json` },
  ]);
}

export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  let values;
  try { values = parseArgs({ args: argv, options: { out: { type: 'string' } } }).values; } catch (e) { write(`usage error: ${e.message}`); return 4; }
  const r = spendPrior();
  if (!r.ok) { write(r.reason); return 5; }
  const out = values.out ?? path.join(SPIKE, 'inputs', 'spend-prior.json');
  writeFileSync(out, `${JSON.stringify(r.value, null, 2)}\n`);
  write(`written ${out}`);
  return 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
