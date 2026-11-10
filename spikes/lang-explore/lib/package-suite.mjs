// The checks of a package's suite as text in, findings out (F2.S1, P4 and P5; scenarios 15 to 19): the R-ids of `cases.txt` against the corrected spec, the comparison words, the manifest against the folder, `traps.txt` against the trap
// table's held-out ids, and the trap table's rows against the cases, the spec's visible samples and the witnesses (a row names at least one held-out case and may name visible samples beside it: every witness pass runs them too; parent entry 10.1). The held-out reader's own findings (a pair with no line, a line with no pair) are reused by `loadPackage`, not written again here. Every sentence names cases and
// traps, so it stays on the host: it goes to the terminal and the private record, never to a public file.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { parseSpec } from '../../lang-bench/lib/conformance-spec.mjs';
import { isHeldOutId, isSampleId, rowsOf } from './package-manifest.mjs';
import { maxOf } from './package-max.mjs';
import { quickSamples } from './workspace-quick.mjs';

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
  ...rids.filter(r => R_ID.test(r) && !known.has(Number(R_ID.exec(r)[1]))).map(r => `case ${id} cites ${r}, which the corrected spec does not have (R1 to R${maxOf([...known]) ?? 0})`),
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

/** The problems of `traps.txt` (the case ids `loadHeldoutQuick` read) against the held-out ids of the trap table's rows: a visible sample id is never in `traps.txt`. */
export function trapsTxtProblems({ manifest, traps }) {
  const union = new Set(rowsOf(manifest).flatMap(r => r.cases).filter(isHeldOutId));
  return [
    ...traps.filter(id => !isHeldOutId(id)).map(id => `traps.txt names ${id}, which is no held-out case: a visible sample id is never in traps.txt`),
    ...traps.filter(id => isHeldOutId(id) && !union.has(id)).map(id => `traps.txt names case ${id}, which no row of the trap table names`),
    ...[...union].filter(id => !traps.includes(id)).sort().map(id => `case ${id} is in the trap table and not in traps.txt`),
  ];
}

/** The ids of the visible samples a spec text holds (`['s01', ...]`, the samples every pass runs), or `null` when its samples cannot be read: never an empty list in its place. */
export function specSampleIds(specText) {
  try { return quickSamples(specText).map(s => s.id); } catch { return null; }
}

const witnessesOf = (manifest, row) => Object.entries(manifest.witnesses ?? {}).filter(([, w]) => w.trap === row.trap && w.stack === row.stack).map(([id]) => id);

const SAMPLES_ONLY = name => `${name} names no held-out case: a trap needs at least one, and visible samples only stand beside it`;

/** The problems of a row's held-out part: none at all, samples only, or a held-out id not in `cases/`. */
function heldProblems(name, caseIds, row) {
  const held = row.cases.filter(isHeldOutId);
  if (row.cases.length === 0) return [`${name} names no case`];
  if (held.length === 0) return [SAMPLES_ONLY(name)];
  return held.filter(id => !caseIds.includes(id)).map(id => `${name} names case ${id}, which is not in cases/`);
}

/** The problems of a row's sample ids: one the pinned spec does not hold, listing the samples it holds (`sampleIds` is `specSampleIds`'s answer; `null` is reported once, by `trapTableProblems`). */
const sampleProblems = (name, sampleIds, row) => (sampleIds === null ? [] : row.cases.filter(id => isSampleId(id) && !sampleIds.includes(id)).map(id => `${name} names sample ${id}, which is not one of the samples the pinned spec holds (${sampleIds.join(', ')})`));

/** The problems of a row's ids that are neither a held-out case id nor a sample id (the manifest shape refuses them first; this keeps the pure check from passing them in silence). */
const idShapeProblems = (name, row) => row.cases.filter(id => !isHeldOutId(id) && !isSampleId(id)).map(id => `${name} names ${id}, which is neither a held-out case id nor a sample id`);

/** The problems of one row: its case ids and exactly one witness. */
function rowProblems(manifest, caseIds, sampleIds, row) {
  const name = `trap ${row.trap} on ${row.stack}`;
  const witnesses = witnessesOf(manifest, row);
  return [
    ...heldProblems(name, caseIds, row),
    ...idShapeProblems(name, row),
    ...sampleProblems(name, sampleIds, row),
    ...(witnesses.length === 0 ? [`${name} has no witness (missing the ${row.stack} stack)`] : []),
    ...(witnesses.length > 1 ? [`${name} has ${witnesses.length} witnesses (${witnesses.join(', ')}): exactly one per trap and stack`] : []),
  ];
}

/** The witnesses that belong to no row: their trap is not in the table, or the row does not list their stack. */
const strayWitnesses = (manifest, rows) => Object.entries(manifest.witnesses ?? {}).filter(([, w]) => !rows.some(r => r.trap === w.trap && r.stack === w.stack)).map(([id, w]) => `witness ${id} is for trap ${w.trap} on ${w.stack}, which is no row of the table`);

/**
 * The trap table against the cases, the spec's samples and the witnesses: every trap-and-stack row names at least one existing held-out case, only visible samples the pinned spec holds beside it, and exactly one witness; every witness
 * has its row. `sampleIds` is `specSampleIds`'s answer: a spec whose samples cannot be read is a problem, never "no samples".
 */
export function trapTableProblems({ manifest, caseIds, sampleIds }) {
  const rows = rowsOf(manifest);
  return [
    ...(sampleIds === null ? ['the samples of the spec cannot be read, so the sample ids of the trap table cannot be checked'] : []),
    ...rows.flatMap(row => rowProblems(manifest, caseIds, sampleIds, row)),
    ...strayWitnesses(manifest, rows),
  ];
}

const ESCAPES = Object.freeze({ n: '\n', r: '\r', t: '\t', 0: '\0', '\\': '\\', '"': '"' });
const decoded = body => body
  .replace(/\\x[0-9a-fA-F]{2}/g, 'x')
  .replace(/\\u\{([0-9a-fA-F]+)\}|\\u([0-9a-fA-F]{4})/g, (_, a, b) => String.fromCodePoint(parseInt(a ?? b, 16)))
  .replace(/\\(.)/g, (_, c) => ESCAPES[c] ?? c);

const STRING = /"((?:[^"\\\n]|\\.)*)"(?:\*(\d+))?/g;

/** The bytes of a quoted string item with its repeat count (`"x"*3`), escapes decoded. A `${...}` field counts its literal bytes only. */
const itemBytes = m => Buffer.byteLength(decoded(m[1])) * Number(m[2] ?? 1);

/** The bytes of a JSON value as the runner reads it (`body json V`): its serialized length; a value that is not JSON counts its literal text. */
function jsonBytes(text) {
  try { return Buffer.byteLength(JSON.stringify(JSON.parse(text))); } catch { return Buffer.byteLength(text); }
}

/** The bytes of one transcript line, or 0: a `header` is no body; `body json V` is V's serialized length; strings on a line are joined into one body, the sum of each item's decoded length times its count (`rs_lex.py` `take_str_expr`). */
function lineBytes(line) {
  const text = line.trim();
  if (text.startsWith('header ')) return 0;
  const json = /^body json (.*)$/.exec(text);
  return json === null ? [...text.matchAll(STRING)].reduce((sum, m) => sum + itemBytes(m), 0) : jsonBytes(json[1]);
}

const outFiles = dir => readdirSync(dir).filter(n => n.endsWith('.out')).map(n => path.join(dir, n));

/**
 * S, the largest expected reply body of a suite: over every expected transcript (`.out`) of the folders given (the held-out cases AND the visible samples), the bytes of a body as the runner reads it (see `lineBytes`), by a running
 * maximum. A `${...}` field counts its literal bytes only (its value is not known before the run). A count, never a case. `dirs` is a folder or a list of folders; one that is not there has nothing.
 */
export function largestReplyBody(dirs) {
  let best = 0;
  for (const dir of [dirs].flat().filter(d => existsSync(d))) {
    for (const file of outFiles(dir)) for (const line of readFileSync(file, 'utf8').split('\n')) best = Math.max(best, lineBytes(line));
  }
  return best;
}

/** Every problem of the suite checks (the sentences of scenarios 15 to 18), in order. `pkg` is `loadPackage`'s value. */
export function suiteProblems(pkg) {
  const rIds = specRIds(pkg.specText);
  return [
    ...(rIds === null ? ['the corrected spec cannot be read by the conformance checker\'s parser: its requirement ids are unknown'] : []),
    ...casesTxtProblems({ text: readFileSync(path.join(pkg.casesDir, 'cases.txt'), 'utf8'), rIds }),
    ...folderProblems({ manifest: pkg.manifest, casesDir: pkg.casesDir }),
    ...trapsTxtProblems({ manifest: pkg.manifest, traps: pkg.held.traps }),
    ...trapTableProblems({ manifest: pkg.manifest, caseIds: pkg.held.caseIds, sampleIds: specSampleIds(pkg.specText) }),
  ];
}
