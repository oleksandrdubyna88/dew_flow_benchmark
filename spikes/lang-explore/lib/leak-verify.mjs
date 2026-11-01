// The verify step of the leak scan: a candidate from a hashed tier (exact line, window, chunk) is only a key that hit. The case files are read again, the key is looked up where it came from and the TEXT is compared, so a hash
// collision is never a finding and a finding knows its task, case file and line range. The second pass costs nothing on a clean run (no candidate, no read). Findings carry no text.
import { caseFilesOf, caseLinesOf } from './leak-case-files.mjs';
import { LINE_MIN } from './leak-lines.mjs';
import { LineRing, takeLine } from './leak-windows.mjs';
import { CHUNK, LONG_LINE_MIN, chunkOffsets } from './leak-chunks.mjs';
import { normLine, polyHash } from './leak-text.mjs';

/** The candidates grouped by key: `Map(key -> [candidate])`. */
function byKey(pending) {
  const map = new Map();
  for (const p of pending) map.set(p.key, [...(map.get(p.key) ?? []), p]);
  return map;
}

/** The finding of a verified candidate: where it was seen and which case line it equals. */
const confirmed = (p, { task, name, from, to }) => ({ tier: p.tier, file: p.file, line: p.line, task, caseFile: `cases/${name}`, fromLine: from, toLine: to, raws: p.raws });

/** The finding of a verified window: the three case lines it equals. */
const confirmedWindow = (p, { task, name, w }) => ({ tier: 'window', file: p.file, line: p.line, task, caseFile: `cases/${name}`, fromLine: w.from, toLine: w.to, raws: p.raws });

/** Tier 3's candidates against the case files' windows: the same key AND the same three normalised lines. */
export function verifyWindows(pending, sources, hash) {
  const wanted = byKey(pending.filter(p => p.tier === 'window'));
  const found = [];
  if (wanted.size === 0) return found;
  for (const { id, casesDir } of sources) for (const name of caseFilesOf(casesDir)) windowsOfFile({ wanted, found, task: id, name, lines: caseLinesOf(casesDir, name), hash });
  return found;
}

/** One case file's windows against the wanted candidates. */
function windowsOfFile({ wanted, found, task, name, lines, hash }) {
  const ring = new LineRing();
  lines.forEach((raw, i) => {
    if (takeLine(ring, normLine(raw), raw, i + 1, hash)) found.push(...windowsEqual(wanted.get(ring.key()) ?? [], { task, name, w: ring.window() }));
  });
}

/** The candidates (same key) whose three normalised lines equal the case window `w`, as findings. */
const windowsEqual = (candidates, { task, name, w }) => candidates.filter(p => p.texts.every((t, k) => t === w.texts[k])).map(p => confirmedWindow(p, { task, name, w }));

/** Tier 4's candidates against the aligned chunks of the long case lines: the same key AND the same 64 characters. */
export function verifyChunks(pending, sources) {
  const wanted = byKey(pending.filter(p => p.tier === 'chunk'));
  const found = [];
  if (wanted.size === 0) return found;
  for (const { id, casesDir } of sources) for (const name of caseFilesOf(casesDir)) caseChunks({ wanted, found, task: id, name, lines: caseLinesOf(casesDir, name) });
  return found;
}

/** One case file's long lines: each aligned chunk whose key is wanted and whose text is the candidate's is a finding. */
function caseChunks({ wanted, found, task, name, lines }) {
  lines.forEach((raw, i) => {
    const n = normLine(raw);
    if (n.length < LONG_LINE_MIN) return;
    const equal = new Set(chunkOffsets(n).flatMap(at => equalChunks(wanted, n, at)));
    for (const p of equal) found.push(confirmed(p, { task, name, from: i + 1, to: i + 1 }));
  });
}

/** The candidates whose key and text are the aligned chunk of `n` at `at`. */
const equalChunks = (wanted, n, at) => (wanted.get(polyHash(n, at, at + CHUNK)) ?? []).filter(p => p.text === n.slice(at, at + CHUNK));

/** The candidates of the line and window tiers against the case files: the findings that are real (the chunk tier verifies per file, inside `scanText`, so its candidates never pile up). */
export const verifyCandidates = (pending, material) => [...verifyLines(pending, material.caseSources, material.hash), ...verifyWindows(pending, material.caseSources, material.hash)];

/** Tier 2's candidates against the case lines: those whose normalised text equals the case line are findings. */
export function verifyLines(pending, sources, hash) {
  const wanted = byKey(pending.filter(p => p.tier === 'line'));
  const found = [];
  if (wanted.size === 0) return found;
  for (const { id, casesDir } of sources) for (const name of caseFilesOf(casesDir)) matchFile({ wanted, found, task: id, name, lines: caseLinesOf(casesDir, name), hash });
  return found;
}

/** One case file's lines against the wanted candidates. */
function matchFile({ wanted, found, task, name, lines, hash }) {
  lines.forEach((raw, i) => {
    const n = normLine(raw);
    if (n.length < LINE_MIN) return;
    for (const p of wanted.get(hash(n)) ?? []) if (p.text === n) found.push(confirmed(p, { task, name, from: i + 1, to: i + 1 }));
  });
}
