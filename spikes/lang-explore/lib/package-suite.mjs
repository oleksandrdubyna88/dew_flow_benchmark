// The checks of a package's suite as text in, findings out (F2.S1, P4 and P5; scenarios 15 to 19): the R-ids of `cases.txt` against the corrected spec, the comparison words, the manifest against the folder, `traps.txt` against the trap
// table, and the trap table's rows against the cases and the witnesses. The held-out reader's own findings (a pair with no line, a line with no pair) are reused by `loadPackage`, not written again here. Every sentence names cases and
// traps, so it stays on the host: it goes to the terminal and the private record, never to a public file.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { parseSpec } from '../../lang-bench/lib/conformance-spec.mjs';
import { rowsOf } from './package-manifest.mjs';

/** FORMAT 3.1's comparison words: `bytes`, `normalised`, `json`, `relational`, or several of the last three joined by `+`, each at most once (the runner's own rule, `rs_samples.py` `comparison_ok`). */
const COMPARISONS = Object.freeze(['bytes', 'normalised', 'json', 'relational']);
const COMBINABLE = Object.freeze(['normalised', 'json', 'relational']);
const R_ID = /^R([1-9]\d*)$/;

const comparisonOk = word => {
  const parts = word.split('+');
  return parts.length === 1 ? COMPARISONS.includes(word) : new Set(parts).size === parts.length && parts.every(p => COMBINABLE.includes(p));
};

/** The requirement numbers of a spec text, read by the conformance checker's own parser: a `Set` of numbers, or `null` when the text cannot be read as a spec. */
export function specRIds(specText) {
  const spec = parseSpec(Buffer.from(specText, 'utf8'));
  return spec.ok ? new Set(spec.value.requirements.map(r => r.n)) : null;
}

const wordProblems = (id, comparison) => (comparisonOk(comparison ?? '') ? [] : [`case ${id}: the comparison word "${comparison ?? ''}" is not one FORMAT 3.1 allows`]);
const ridProblems = (id, rids, known) => [
  ...(rids.length === 0 ? [`case ${id} names no requirement id`] : []),
  ...rids.filter(r => !R_ID.test(r)).map(r => `case ${id} lists "${r}", which is not an R-id`),
  ...rids.filter(r => R_ID.test(r) && !known.has(Number(R_ID.exec(r)[1]))).map(r => `case ${id} cites ${r}, which the corrected spec does not have (R1 to R${Math.max(0, ...known)})`),
];
const lineProblems = (id, comparison, rids, known) => [...wordProblems(id, comparison), ...ridProblems(id, rids, known)];

/** The problems of the lines of `cases.txt`: a comparison word FORMAT 3.1 does not allow, no R-id, an R-id the spec has no requirement for. `rIds` is `specRIds`'s answer. */
export function casesTxtProblems({ text, rIds }) {
  const known = rIds ?? new Set();
  return text.split('\n').map(l => l.trim()).filter(l => l !== '' && !l.startsWith('#')).flatMap(line => {
    const [id, comparison, ...rids] = line.split(/\s+/);
    return lineProblems(id, comparison, rids, known);
  });
}

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const readOrNull = file => (existsSync(file) ? readFileSync(file) : null);

/** What differs between one case's two files and the hashes the manifest records. */
function caseProblem(id, entry, casesDir) {
  const pair = ['in', 'out'].map(ext => [ext, readOrNull(path.join(casesDir, `${id}.${ext}`))]);
  if (pair.every(([, bytes]) => bytes === null)) return `case ${id} is in the manifest and has no pair in the folder`;
  const changed = pair.filter(([ext, bytes]) => bytes === null || sha256(bytes) !== entry[ext]).map(([ext]) => ext);
  return changed.length === 0 ? null : `case ${id} differs from the manifest (${changed.join(' and ')}): it changed after the generator wrote it`;
}

/** The problems of the case folder against the manifest's per-case hashes: a case changed after `gen.py` ran, a manifest entry with no pair, a pair the manifest does not list. */
export function folderProblems({ manifest, casesDir }) {
  const inFolder = [...new Set(readdirSync(casesDir).filter(n => /^h\d{3}\.(in|out)$/.test(n)).map(n => n.slice(0, 4)))].sort();
  const listed = Object.keys(manifest.cases);
  return [
    ...listed.map(id => caseProblem(id, manifest.cases[id], casesDir)).filter(p => p !== null),
    ...inFolder.filter(id => !listed.includes(id)).map(id => `case ${id} is in the folder and not in the manifest`),
  ];
}

/** The problems of `traps.txt` (the case ids `loadHeldoutQuick` read) against the union of the trap table's cases. */
export function trapsTxtProblems({ manifest, traps }) {
  const union = new Set(rowsOf(manifest).flatMap(r => r.cases));
  return [
    ...traps.filter(id => !union.has(id)).map(id => `traps.txt names case ${id}, which no row of the trap table names`),
    ...[...union].filter(id => !traps.includes(id)).sort().map(id => `case ${id} is in the trap table and not in traps.txt`),
  ];
}

const witnessesOf = (manifest, row) => Object.entries(manifest.witnesses ?? {}).filter(([, w]) => w.trap === row.trap && w.stack === row.stack).map(([id]) => id);

/** The problems of one row: at least one existing case and exactly one witness. */
function rowProblems(manifest, caseIds, row) {
  const name = `trap ${row.trap} on ${row.stack}`;
  const missing = row.cases.filter(id => !caseIds.includes(id));
  const witnesses = witnessesOf(manifest, row);
  return [
    ...(row.cases.length === 0 ? [`${name} names no case`] : []),
    ...missing.map(id => `${name} names case ${id}, which is not in cases/`),
    ...(witnesses.length === 0 ? [`${name} has no witness (missing the ${row.stack} stack)`] : []),
    ...(witnesses.length > 1 ? [`${name} has ${witnesses.length} witnesses (${witnesses.join(', ')}): exactly one per trap and stack`] : []),
  ];
}

/** The witnesses that belong to no row: their trap is not in the table, or the row does not list their stack. */
const strayWitnesses = (manifest, rows) => Object.entries(manifest.witnesses ?? {}).filter(([, w]) => !rows.some(r => r.trap === w.trap && r.stack === w.stack)).map(([id, w]) => `witness ${id} is for trap ${w.trap} on ${w.stack}, which is no row of the table`);

/** The trap table against the cases and the witnesses: every trap-and-stack row names at least one existing case and exactly one witness, and every witness has its row. */
export function trapTableProblems({ manifest, caseIds }) {
  const rows = rowsOf(manifest);
  return [...rows.flatMap(row => rowProblems(manifest, caseIds, row)), ...strayWitnesses(manifest, rows)];
}

const ESCAPES = Object.freeze({ n: '\n', r: '\r', t: '\t', 0: '\0', '\\': '\\', '"': '"' });
const decoded = body => body
  .replace(/\\x[0-9a-fA-F]{2}/g, 'x')
  .replace(/\\u\{([0-9a-fA-F]+)\}|\\u([0-9a-fA-F]{4})/g, (_, a, b) => String.fromCodePoint(parseInt(a ?? b, 16)))
  .replace(/\\(.)/g, (_, c) => ESCAPES[c] ?? c);

/** S, the largest expected reply body of a suite: the longest quoted string of any expected transcript (`.out`), escapes decoded, in bytes. A count, never a case. */
export function largestReplyBody(casesDir) {
  const strings = readdirSync(casesDir).filter(n => n.endsWith('.out')).flatMap(n => [...readFileSync(path.join(casesDir, n), 'utf8').matchAll(/"((?:[^"\\\n]|\\.)*)"/g)].map(m => Buffer.byteLength(decoded(m[1]))));
  return strings.length === 0 ? 0 : Math.max(...strings);
}

/** Every problem of the suite checks (the sentences of scenarios 15 to 18), in order. `pkg` is `loadPackage`'s value. */
export function suiteProblems(pkg) {
  const rIds = specRIds(pkg.specText);
  return [
    ...(rIds === null ? ['the corrected spec cannot be read by the conformance checker\'s parser: its requirement ids are unknown'] : []),
    ...casesTxtProblems({ text: readFileSync(path.join(pkg.casesDir, 'cases.txt'), 'utf8'), rIds }),
    ...folderProblems({ manifest: pkg.manifest, casesDir: pkg.casesDir }),
    ...trapsTxtProblems({ manifest: pkg.manifest, traps: pkg.held.traps }),
    ...trapTableProblems({ manifest: pkg.manifest, caseIds: pkg.held.caseIds }),
  ];
}
