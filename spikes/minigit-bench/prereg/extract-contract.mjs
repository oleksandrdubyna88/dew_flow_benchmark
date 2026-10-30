#!/usr/bin/env node
// Writes the VERBATIM half of inputs/analysis-contract.md (E3b.S3): parent plan quotes read from a commit of origin/main, never typed. The operationalizations
// below the marker are the pre-registration's own text (Opus); this script keeps whatever follows the marker and replaces only what is above it.
//   node spikes/minigit-bench/prereg/extract-contract.mjs --parent-commit <sha on origin/main> [--out <file>]
// Exit 0 written | 4 usage | 5 a quoted block is missing at that commit.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { blockAt } from './blocks.mjs';
import { parseParentFlags } from './cli.mjs';
import { fullSha, readAtCommit } from './parent.mjs';
import { fail, ok } from '../lib/result.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
export const MARKER = '<!-- OPERATIONALIZATIONS: everything below this line is the pre-registration\'s own text; extract-contract.mjs never rewrites it -->';
/** The quoted blocks: a name for the header comment, the section of the parent, and the words the block starts with. */
export const QUOTES = Object.freeze([
  { name: 'Unit of analysis', section: '11', start: '**Unit of analysis' },
  { name: 'Censored and mixed outcomes', section: '11', start: '**Censored and mixed outcomes' },
  { name: 'What n = 3 licenses', section: '11', start: '**What n = 3 licenses' },
  { name: 'Void list', section: '11.1', start: '- **Void' },
  { name: 'Data points, not voids', section: '11.1', start: '- **Data points' },
  { name: 'A void session is re-run once', section: '11.1', start: '- **A void session is re-run once' },
  { name: 'Acceptance (delivered is process-only)', section: '5.3', start: 'A session is **accepted** when' },
]);

/** The parent's text -> `ok([{ name, section, lines }])` | `fail` naming the first block that is not there. */
export function extractQuotes(parentText) {
  const found = QUOTES.map(q => ({ q, b: blockAt(parentText, q.start) }));
  const missing = found.find(f => f.b.missing);
  return missing ? fail(`the parent plan has no block starting '${missing.b.missing}'`) : ok(found.map(({ q, b }) => ({ name: q.name, section: q.section, lines: b.lines })));
}

/** The quote half as markdown: each block between comment markers, every line a blockquote line. */
export function renderQuotes({ quotes, commit }) {
  const blocks = quotes.map(q => [`<!-- quote: ${q.name} (parent section ${q.section}) -->`, ...q.lines.map(l => `> ${l}`.trimEnd()), '<!-- /quote -->'].join('\n'));
  return [
    '# Analysis contract, bare scope', '',
    `The blocks below are quoted word for word from \`todo/PLAN_minigit_stack_bench.md\` at \`${commit}\`, a commit of origin/main; \`prereg/extract-contract.mjs\` wrote them and a test checks them. `
    + 'Section 5.3\'s acceptance is quoted whole; its `delivered` clause is the process arm\'s and is **process-only**: a bare session is never judged on it.', '',
    blocks.join('\n\n'), '', MARKER, '',
  ].join('\n');
}

/** The text of an existing contract below the marker (what the script keeps), or ''. */
export function keptTail(existing) {
  const at = existing.indexOf(MARKER);
  return at < 0 ? '' : existing.slice(at + MARKER.length).replace(/^\n/, '');
}

/** The quotes of the parent plan at `commit`: `ok([{ name, section, lines }])`, or a `fail` that says whether the plan or a block was unreadable. */
async function readQuotes({ repo, commit }) {
  const text = await readAtCommit({ repo, commit });
  if (!text.ok) return fail(`cannot read the parent plan: ${text.reason}`);
  return extractQuotes(text.value);
}

/** Write the quote half over `out`, keeping what the file holds below the marker. */
function writeContract({ out, quotes, commit }) {
  const tail = existsSync(out) ? keptTail(readFileSync(out, 'utf8')) : '';
  writeFileSync(out, `${renderQuotes({ quotes, commit })}${tail}`);
}

export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  const flags = parseParentFlags(argv);
  if (!flags.ok) { write(flags.reason); return 4; }
  const commit = flags.value['parent-commit'];
  const repo = path.resolve(SPIKE, '..', '..');
  const quotes = await readQuotes({ repo, commit });
  if (!quotes.ok) { write(quotes.reason); return 5; }
  const sha = await fullSha({ repo, commit });
  const out = flags.value.out ?? path.join(SPIKE, 'inputs', 'analysis-contract.md');
  writeContract({ out, quotes: quotes.value, commit: sha.ok ? sha.value : commit });
  write(`written ${out}`);
  return 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
