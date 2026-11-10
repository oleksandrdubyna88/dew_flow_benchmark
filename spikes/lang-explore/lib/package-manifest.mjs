// The manifest of a quick task's package (F2.S1, P4 and P5): `~/rsd/lang-v2/explore/heldout-NN/manifest.json`, read as data. It holds, besides the case count:
//
//   cases       { hNNN: { in: <sha256>, out: <sha256> } }                                   the per-case hashes of the pair's two files
//   traps       { <trap>: { stack: 'csharp' | 'node' | 'both', cases: [hNNN], mutants?: [<witness id>] } }   the trap coverage table; a `both` row is two rows, one per stack
//   witnesses   { <id>: { trap, stack: 'csharp' | 'node', kind: 'deterministic' | 'race' } }   one witness per trap-and-stack row; the overlay is `heldout-NN/mutants/<id>/`
//   load        { cases: [hNNN], durationS }                                                the load cases (each has a twin under `twins/`) and the load duration the spec states (the F-LOAD rule measures it)
//   references  free text per stack (the author's notes; never read by the tool)
//
// An unknown top-level key is refused naming the key. This module checks the SHAPE only; the table's meaning (every row has a case and one witness) is `package-suite.mjs`. Nothing here is ever written to a public file: the
// sentences name cases and traps, so they stay on the host (the records and the terminal), never in git.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const MANIFEST_KEYS = Object.freeze(['caseCount', 'cases', 'load', 'references', 'traps', 'witnesses']);
export const STACKS = Object.freeze(['csharp', 'node']);
export const KINDS = Object.freeze(['deterministic', 'race']);

const CASE_ID = /^h\d{3}$/;
const HASH = /^[0-9a-f]{64}$/;
const isObject = v => v !== null && typeof v === 'object' && !Array.isArray(v);
const idList = v => Array.isArray(v) && v.every(id => typeof id === 'string' && CASE_ID.test(id));
const pairOk = c => isObject(c) && HASH.test(c.in ?? '') && HASH.test(c.out ?? '');
const optionalList = v => v === undefined || Array.isArray(v);
const trapOk = t => isObject(t) && [...STACKS, 'both'].includes(t.stack) && idList(t.cases) && optionalList(t.mutants);
const witnessOk = w => isObject(w) && typeof w.trap === 'string' && STACKS.includes(w.stack) && KINDS.includes(w.kind);
const loadOk = l => isObject(l) && idList(l.cases) && Number.isInteger(l.durationS) && l.durationS > 0;
const everyEntry = (obj, ok_) => isObject(obj) && Object.values(obj).every(ok_);

/** Each shape rule as `[holds(manifest), the sentence]`. A rule whose key is absent holds (the key is optional until its step needs it). */
const SHAPE = Object.freeze([
  [m => Number.isInteger(m.caseCount) && m.caseCount >= 0, () => 'caseCount must be a whole number'],
  [m => everyEntry(m.cases, pairOk) && Object.keys(m.cases).every(id => CASE_ID.test(id)), () => 'cases must map each case id (h001...) to { in, out }, the sha256 of the two files'],
  [m => m.traps === undefined || everyEntry(m.traps, trapOk), () => 'traps must map each trap to { stack: csharp | node | both, cases: [case ids] }'],
  [m => m.witnesses === undefined || everyEntry(m.witnesses, witnessOk), () => 'witnesses must map each id to { trap, stack: csharp | node, kind: deterministic | race }'],
  [m => m.load === undefined || loadOk(m.load), () => 'load must be { cases: [case ids], durationS: a whole number of seconds }'],
  [m => Object.keys(m.cases ?? {}).length === m.caseCount, m => `caseCount ${m.caseCount} is not the ${Object.keys(m.cases ?? {}).length} cases listed`],
]);

/** The sentence naming a top-level key this module does not know, or null. */
export const unknownKeyProblem = m => { const key = Object.keys(m).find(k => !MANIFEST_KEYS.includes(k)); return key === undefined ? null : `manifest.json has the unknown key "${key}": the keys are ${MANIFEST_KEYS.join(', ')}`; };

/** The first shape problem of a parsed manifest, or null. */
export const shapeProblem = m => unknownKeyProblem(m) ?? SHAPE.find(([holds]) => !holds(m))?.[1](m) ?? null;

/** The parsed JSON of `bytes`, or `null` when it is not JSON. */
const parsed = bytes => { try { return JSON.parse(bytes.toString('utf8')); } catch { return null; } };

/** The bytes of `file`, or `null`. */
const bytesOf = file => { try { return readFileSync(file); } catch { return null; } };

/** `ok({ manifest, sha256 })` for a manifest file of the right shape; a file that is not there, not JSON or not of this shape is a `fail` naming the file. */
export function readManifest(file) {
  const bytes = bytesOf(file);
  if (bytes === null) return fail(`${file} cannot be read: the package needs its manifest`);
  const manifest = parsed(bytes);
  const problem = isObject(manifest) ? shapeProblem(manifest) : 'it is not a JSON object';
  return problem === null ? ok({ manifest, sha256: createHash('sha256').update(bytes).digest('hex') }) : fail(`${file}: ${problem}`);
}

/** The ids a quick task's manifest names, which must not appear as words in a public note: its witness ids and its trap names. A manifest that is not of this shape names none (it is refused elsewhere). */
export const quickManifestIds = manifest => (isObject(manifest) ? [...new Set([...Object.keys(manifest.witnesses ?? {}), ...Object.keys(manifest.traps ?? {})])] : []);

/** The rows of the trap table, one per trap and stack: `[{ trap, stack, cases }]` (a `both` row is two). */
export const rowsOf = manifest => Object.entries(manifest.traps ?? {}).flatMap(([trap, t]) => (t.stack === 'both' ? STACKS : [t.stack]).map(stack => ({ trap, stack, cases: t.cases })));

/** The cases a witness is registered to: the cases of its trap row. */
export const registeredOf = (manifest, witness) => manifest.traps?.[witness.trap]?.cases ?? [];
