// The material of one quick task's package (F2.S1, section 3): `loadPackage` reads the held-out side (`loadHeldoutQuick`), the manifest (`package-manifest.mjs`), the pinned spec text (its sha256 checked against the pin of
// `inputs/explore.json`, so the package validates the text that will freeze), the two stack references and the witness overlays by `lstat` (a link is listed, never read through), and the hashes of all of them. `assembleReference`
// lays a reference on its stack's template in an owned temporary folder (a file the reference holds that the template freezes is refused); `packageIdentity` is the sha256 P8 names. Pure but for reading; no box here.
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { cleanupScope, ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { isFrozen } from '../../lang-bench/lib/frozen-set.mjs';
import { treeHash } from './build-commit.mjs';
import { loadHeldoutQuick } from './heldout-quick.mjs';
import { readManifest, rowsOf, STACKS } from './package-manifest.mjs';
import { OWNED_TOKEN } from './score-quick.mjs';
import { BENCH_TEMPLATES } from './workspace-quick.mjs';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

const kindOf = e => (e.isSymbolicLink() ? 'link' : (e.isDirectory() ? 'folder' : (e.isFile() ? 'file' : 'other')));
const entriesOf = (dir, rel) => readdirSync(path.join(dir, rel), { withFileTypes: true }).map(e => ({ rel: rel === '' ? e.name : `${rel}/${e.name}`, kind: kindOf(e) }));

/** What each kind of entry adds to a listing: a file is listed, a folder is walked, a link and anything else is a problem naming its path. */
const PLACE = Object.freeze({
  file: (e, out) => out.files.push(e.rel),
  folder: (e, out, todo) => todo.push(e.rel),
  link: (e, out) => out.problems.push(`${e.rel} is a link: a link is never read through`),
  other: (e, out) => out.problems.push(`${e.rel} is neither a file nor a folder`),
});

/** `{ files: [rel], problems: [sentence] }` of a folder (the files sorted), walked by `lstat`: a link or an entry that is not a file or a folder is a problem naming its path and is never followed. A folder that is not there has no files. */
export function listFolder(dir) {
  const out = { files: [], problems: [] };
  const todo = existsSync(dir) ? [''] : [];
  while (todo.length > 0) entriesOf(dir, todo.shift()).forEach(e => PLACE[e.kind](e, out, todo));
  return { files: out.files.sort(), problems: out.problems.sort() };
}

/** The sha256 of a folder's tree (names and bytes, in name order), or `null` for a folder that is not there. */
export const treeHashOf = dir => (existsSync(dir) ? treeHash(dir) : null);

const referenceOf = dir => ({ dir, ...listFolder(dir), treeSha256: treeHashOf(dir) });

/** The sentence refusing a spec text that is not the pinned one, or null. */
const specProblem = ({ task, text }) => (typeof text === 'string' && sha256(text) === task.spec.sha256 ? null : `the spec text of task ${task.id} has sha256 ${typeof text === 'string' ? sha256(text).slice(0, 12) : '(none)'} and the pin of inputs/explore.json says ${task.spec.sha256.slice(0, 12)}: the package validates the text that will freeze, so it is refused before any scoring`);

/**
 * `ok(pkg)` | `fail`. `pkg` = `{ task, home, held, dir, casesDir, manifest, manifestSha256, specText, specSha256, formatSha256, refs: { csharp, node }, mutantsDir, witnessesTreeSha256, twinsDir, twinsTreeSha256 }`, each `refs[stack]` being
 * `{ dir, files, problems, treeSha256 }`. The held-out side must load (a sealed task whose clear folder is absent is the caller's to open first), the manifest must be of its shape and the spec text must be the pinned one;
 * a reference that is not there is listed with no files, for the step that needs it to refuse.
 */
export function loadPackage({ home, task, world, frozenFile }) {
  const held = loadHeldoutQuick(home, task, frozenFile);
  if (!held.ok) return held;
  const manifest = readManifest(path.join(held.value.dir, 'manifest.json'));
  if (!manifest.ok) return manifest;
  const text = world.sources?.spec;
  const wrong = specProblem({ task, text });
  if (wrong) return fail(wrong);
  const { dir } = held.value;
  return ok(Object.freeze({
    task, home, held: held.value, dir, casesDir: held.value.casesDir, manifest: manifest.value.manifest, manifestSha256: manifest.value.sha256, specText: text, specSha256: sha256(text), formatSha256: task.format.sha256,
    refs: Object.fromEntries(STACKS.map(stack => [stack, referenceOf(path.join(held.value.refsDir, stack))])),
    mutantsDir: path.join(dir, 'mutants'), witnessesTreeSha256: treeHashOf(path.join(dir, 'mutants')), twinsDir: path.join(dir, 'twins'), twinsTreeSha256: treeHashOf(path.join(dir, 'twins')),
  }));
}

const owned = ownedFor({ token: OWNED_TOKEN });

/** The sentence refusing reference files that are not only what an agent would write: a link, or a file the template freezes (naming the path), or null. */
function referenceProblem({ stack, listing }) {
  if (listing.problems.length > 0) return `the reference holds ${listing.problems[0]}`;
  const frozen = listing.files.find(rel => isFrozen(rel, stack));
  return frozen === undefined ? null : `the reference holds the frozen file ${frozen}: a reference is only the files an agent would write, never a frozen file`;
}

/**
 * The reference laid on its stack's template: a folder of `stack`'s template (`<templates>/<stack>/<kind>/`), then every file of the reference over it, in an owned temporary folder of `scope` (`cleanupScope()`; the caller closes
 * it). A reference that holds a link or a frozen file is refused naming the path, and nothing is assembled. -> `ok({ dir, files })` (`files`: the union, sorted) | `fail`.
 */
export function assembleReference({ templates = BENCH_TEMPLATES, stack, kind = 'daemon', refDir, scope, parent }) {
  const listing = listFolder(refDir);
  const refused = referenceProblem({ stack, listing });
  if (refused) return fail(refused);
  const template = path.join(templates, stack, kind);
  if (!existsSync(template)) return fail(`the template ${template} is not there`);
  const dir = owned.ownedDir(scope ?? cleanupScope(), 'tree', parent === undefined ? undefined : { parent });
  cpSync(template, dir, { recursive: true });
  layFiles({ from: refDir, to: dir, files: listing.files });
  return ok({ dir, files: listFolder(dir).files });
}

/** Copy `files` (relative paths) from `from` over `to`, making their folders. */
export function layFiles({ from, to, files }) {
  for (const rel of files) { mkdirSync(path.dirname(path.join(to, rel)), { recursive: true }); cpSync(path.join(from, rel), path.join(to, rel)); }
}

/** The material hashes of a package, hashes and counts only (never a case id, a trap id or a path). */
export function materialOf(pkg) {
  return {
    specSha256: pkg.specSha256, formatSha256: pkg.formatSha256, heldoutTreeSha256: pkg.held.treeSha256, manifestSha256: pkg.manifestSha256,
    refsTreeSha256: { csharp: pkg.refs.csharp.treeSha256, node: pkg.refs.node.treeSha256 }, witnessesTreeSha256: pkg.witnessesTreeSha256, twinsTreeSha256: pkg.twinsTreeSha256,
    counts: { cases: pkg.held.caseIds.length, traps: Object.keys(pkg.manifest.traps ?? {}).length, rows: rowsOf(pkg.manifest).length, witnesses: Object.keys(pkg.manifest.witnesses ?? {}).length },
  };
}

const canonical = v => (Array.isArray(v) ? `[${v.map(canonical).join(',')}]` : (v !== null && typeof v === 'object' ? `{${Object.keys(v).sort().map(k => `${JSON.stringify(k)}:${canonical(v[k])}`).join(',')}}` : JSON.stringify(v ?? null)));

/** The fields the identity is a sha256 over (P8): the material and what the validation was made under. Measured numbers (walls, sizes) are not among them: a re-run of the same material has the same identity. */
const IDENTITY_FIELDS = Object.freeze(['specSha256', 'formatSha256', 'heldoutTreeSha256', 'manifestSha256', 'archiveSha256', 'refsTreeSha256', 'witnessesTreeSha256', 'twinsTreeSha256', 'counts', 'gate', 'memoryBytes', 'loadDurationS', 'pins']);

/** The package identity: a sha256 over the named fields of `material`, keys sorted. A byte of any hashed material moves it; nothing else does. */
export const packageIdentity = material => sha256(canonical(Object.fromEntries(IDENTITY_FIELDS.map(k => [k, material[k] ?? null]))));
