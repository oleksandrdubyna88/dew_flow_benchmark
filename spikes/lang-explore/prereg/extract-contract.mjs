#!/usr/bin/env node
// Writes the VERBATIM half of `inputs/prereg/analysis-contract.md` (F3.S1 (b), PR 5): the parent plan's blocks that are Stage 1's analysis, read from a commit of `origin/main` and never typed, above a marker; the contract's own
// operationalisations below the marker are prose (PR 6) and are kept as they are. Over v1's `readAtCommit`, `MARKER` and `keptTail` (`spikes/minigit-bench/prereg/`).
//   node spikes/lang-explore/prereg/extract-contract.mjs --parent-commit <sha on origin/main> [--out <file>] [--check]
// Exit 0 written (or checked equal) | 1 `--check` differs | 4 usage | 5 the parent at that commit lacks a block (named).
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { MARKER, keptTail } from '../../minigit-bench/prereg/extract-contract.mjs';
import { fullSha, readAtCommit } from '../../minigit-bench/prereg/parent.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { PREREG_DIR } from '../lib/stage-lists.mjs';
import { REPO } from '../lib/paths.mjs';
import { committedText, runGenerator } from './generator.mjs';
import { cutQuote } from './quote-blocks.mjs';

export { MARKER };
export const PARENT_FILE = 'todo/PLAN_lang_comparison_v2.md';
const row = (name, id) => ({ name, section: 'the operative part of epoch 9, decisions D69 to D76 and the decisions of section 3', start: `| ${id} |`, mode: 'row' });
const threat = (id, start = `| ${id} |`) => ({ name: `Threat ${id}: the Stage 1 reading and the report's sentence`, section: 'the operative part of epoch 9, threats this change touches', start, mode: 'row' });

/** The quoted blocks, in the order the contract shows them: the claim first, then the decisions, section 9's metric and cases, the outcome classes and the threat rows whose sentences the report carries. */
export const QUOTES = Object.freeze([
  { name: 'The goal, narrowed', section: 'the operative part of epoch 9', start: '> On six tasks of two kinds', mode: 'block' },
  { name: 'The claim and its stated limit (D73)', section: 'the operative part of epoch 9', start: '**The claim and its stated limit (D73).**', mode: 'block' },
  { name: 'Outside the claim', section: 'the operative part of epoch 9', start: '**Outside the claim.**', mode: 'block' },
  row('D24', 'D24'), row('D28', 'D28'), row('D29', 'D29'), row('D71', 'D71'), row('D72', 'D72'), row('D73', 'D73'),
  { name: 'Primary metric, exactly (D24)', section: '9', start: '**Primary metric, exactly (D24):**', mode: 'paragraphs', count: 3 },
  { name: 'The CPC ratio and its undefined cases', section: '9', start: '**The CPC ratio and its undefined cases', mode: 'paragraphs', count: 2 },
  { name: 'Outcome classes', section: '9', start: '**Outcome classes:**', mode: 'block' },
  threat('T3'), threat('T5'), threat('T6, T9', '| T6, T9 |'), threat('T23'), threat('T25'), threat('T26'), threat('T27'), threat('T28'),
].map(Object.freeze));

/** The parent's text -> `ok([{ name, section, lines }])` | `fail` naming the first block that is not there. */
export function extractQuotes(parentText) {
  const found = QUOTES.map(q => ({ q, b: cutQuote(parentText, q) }));
  const missing = found.find(f => f.b.missing !== undefined);
  return missing ? fail(`the parent plan has no block starting '${missing.b.missing}' (${missing.q.name})`) : ok(found.map(({ q, b }) => ({ name: q.name, section: q.section, lines: b.lines })));
}

/** The quote half as markdown: each block between comment markers, every line a blockquote line, then the marker. */
export function renderQuotes({ quotes, commit }) {
  const blocks = quotes.map(q => [`<!-- quote: ${q.name} (parent ${q.section}) -->`, ...q.lines.map(l => `> ${l}`.trimEnd()), '<!-- /quote -->'].join('\n'));
  return [
    '# Analysis contract, Stage 1', '',
    `The blocks below are quoted word for word from \`${PARENT_FILE}\` at \`${commit}\`, a commit of origin/main; \`prereg/extract-contract.mjs\` wrote them and a test checks them. What follows the marker is this pre-registration's own text.`, '',
    blocks.join('\n\n'), '', MARKER, '',
  ].join('\n');
}

/** The contract as the generator writes it: the quotes of `commit` of the parent, then what `existing` holds below the marker. -> `ok(text)` | `fail`. */
export async function buildContract({ repo, commit, existing }) {
  const text = await readAtCommit({ repo, commit, file: PARENT_FILE });
  if (!text.ok) return fail(`cannot read the parent plan: ${text.reason}`);
  const quotes = extractQuotes(text.value);
  if (!quotes.ok) return fail(quotes.reason);
  const sha = await fullSha({ repo, commit });
  return ok(`${renderQuotes({ quotes: quotes.value, commit: sha.ok ? sha.value : commit })}${keptTail(existing ?? '')}`);
}

/** The command: -> the exit code. `repo` is the seam of a test (a temporary clone that holds the parent plan). */
export async function main(argv, say = line => process.stdout.write(`${line}\n`), repo = REPO) {
  const build = async values => {
    if (!values['parent-commit']) return { ...fail('usage error: --parent-commit <sha on origin/main> is required'), exit: 4 };
    return buildContract({ repo, commit: values['parent-commit'], existing: committedText(values.out) });
  };
  return runGenerator({ argv, extra: { 'parent-commit': { type: 'string' } }, build, defaultOut: path.join(REPO, PREREG_DIR, 'analysis-contract.md'), say });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
