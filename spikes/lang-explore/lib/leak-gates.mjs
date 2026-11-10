// The gate records are public and hold counts and words (D25's leak rule, F2.S1 scenario 29): `spikes/lang-bench/inputs/gates/spec-NN.md` carries the verdict, the reviewer count and findings that name no case. This scan reads each record
// of the bench checkout's working tree line by line and finds a case id (`h` and three digits), a witness id or a trap name of a manifest, a line equal to a short case line, and a window of six words of the private notes. A finding names
// the file and the line number and what KIND of thing it holds, never the text. (`leakcheck`'s shingle tier already covers the notes over every file; this scan adds the ids and the case lines against the gate folder.)
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { normalise } from '../../minigit-bench/lib/heldout-leak.mjs';
import { normLine } from './leak-text.mjs';

const CASE_ID = /(?<![\w-])h\d{3}(?![\w-])/;
const wordOf = id => new RegExp(`(?<![\\w-])${id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?![\\w-])`);

/** The windows of six consecutive normalised words of a line. */
const windowsOf = line => { const words = normalise(line).split(' ').filter(Boolean); return Array.from({ length: Math.max(0, words.length - 5) }, (_, i) => words.slice(i, i + 6).join(' ')); };

/** Each thing a gate record must not hold, as `[kind, holds(line, ctx)]`, in order. */
const CHECKS = Object.freeze([
  ['a case id', line => CASE_ID.test(line)],
  ['a witness or trap id', (line, { idWords }) => idWords.some(w => w.test(line))],
  ['a case line', (line, { caseLines }) => caseLines.has(normLine(line))],
  ['a window of the private notes', (line, { windows }) => windowsOf(line).some(w => windows.has(w))],
]);

/** What a line holds that a gate record must not (the first kind), or null. */
const kindOf = (line, ctx) => CHECKS.find(([, holds]) => holds(line, ctx))?.[0] ?? null;

/**
 * The lines of the gate records of `repoRoot` that hold a case id, a witness or trap id of `ids`, a line of the cases or a six-word window of the notes (`material` is `leakMaterial`'s structured material). -> `[{ file, line, kind }]`
 * sorted by file and line: names and numbers, never text. A repository with no gate folder has nothing to scan.
 */
export function leakGates({ repoRoot, ids, material }) {
  const dir = path.join(repoRoot, 'spikes', 'lang-bench', 'inputs', 'gates');
  if (!existsSync(dir)) return [];
  const ctx = { idWords: ids.map(wordOf), caseLines: new Set(material.substrings.filter(n => n.kind === 'case').map(n => n.text)), windows: new Set(material.shingleTexts) };
  return readdirSync(dir).filter(f => /^spec-.*\.md$/.test(f)).sort().flatMap(file => readFileSync(path.join(dir, file), 'utf8').split('\n').flatMap((line, i) => {
    const kind = kindOf(line, ctx);
    return kind === null ? [] : [{ file, line: i + 1, kind }];
  }));
}
