// The held-out side, as the harness reads it (plan section 4): `~/rsd/lang-v2/explore/heldout/`, private, outside every git tree and never bound into a box. This module only READS it: the case directory,
// the two reference sources, the mutants and the manifest, each file checked against the manifest's sha256 before anything uses it. Nothing it returns is ever written to a public file.
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { heldoutDir } from './paths.mjs';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const filesUnder = (root, rel = '') => readdirSync(path.join(root, rel), { withFileTypes: true }).flatMap(e => (e.isDirectory() ? filesUnder(root, path.join(rel, e.name)) : [path.join(rel, e.name)]));

/** The sentences saying which files of the held-out side differ from the manifest (a file added, removed or changed since `gen.py` wrote it). */
export function manifestProblems(dir, manifest) {
  const listed = Object.keys(manifest.files);
  const present = filesUnder(dir).filter(f => f !== 'manifest.json' && !f.endsWith('.pyc'));
  return [
    ...listed.filter(f => !present.includes(f)).map(f => `${f} is in the manifest and missing`),
    ...present.filter(f => !listed.includes(f)).map(f => `${f} is present and not in the manifest`),
    ...listed.filter(f => present.includes(f) && sha256(readFileSync(path.join(dir, f))) !== manifest.files[f]).map(f => `${f} differs from the manifest`),
  ];
}

/** One path's permission problem as a sentence, or null: directories 0700, files 0600, owned by the caller. */
const nameIn = (dir, p) => path.relative(dir, p) || '/';

function modeProblem(dir, p) {
  const st = statSync(p);
  const want = st.isDirectory() ? 0o700 : 0o600;
  return (st.mode & 0o777) === want && st.uid === process.getuid() ? null : `${nameIn(dir, p)} has mode ${(st.mode & 0o777).toString(8)} or another owner (want ${want.toString(8)})`;
}

/** The permission problems of the private area, every file and every directory. */
export function permissionProblems(dir) {
  const files = filesUnder(dir).map(f => path.join(dir, f));
  const dirs = new Set([dir, ...files.map(f => path.dirname(f))]);
  return [...dirs, ...files].map(p => modeProblem(dir, p)).filter(Boolean);
}

/** `ok(heldout)` with `{ dir, manifest, casesDir, refDir(stack), mutantDir(id), traps, caseIds }`, or a `fail` naming what is absent or changed. */
export function loadHeldout(home = os.homedir()) {
  const dir = heldoutDir(home);
  if (!existsSync(path.join(dir, 'manifest.json'))) return fail(`the held-out side is not there (${dir}/manifest.json): run gen.py there`, { absent: true });
  const manifest = JSON.parse(readFileSync(path.join(dir, 'manifest.json'), 'utf8'));
  const problems = [...manifestProblems(dir, manifest), ...permissionProblems(dir)];
  if (problems.length > 0) return fail(`the held-out side does not match its manifest: ${problems.join('; ')}`);
  return ok(Object.freeze({
    dir, manifest, casesDir: path.join(dir, 'cases'), traps: manifest.traps, caseIds: Object.keys(manifest.cases).sort(),
    refDir: stack => path.join(dir, 'refs', stack), mutantDir: id => path.join(dir, 'mutants', id),
  }));
}

/** Copy an overlay directory (the reference or a mutant source tree) over a workspace directory. */
export function overlay(from, to) {
  mkdirSync(to, { recursive: true });
  cpSync(from, to, { recursive: true });
}

/** The reference or mutant workspace: the frozen files, then the overlay's source files on top. */
export function writeSolution({ files, overlayDir, dest }) {
  for (const [rel, f] of Object.entries(files)) {
    mkdirSync(path.dirname(path.join(dest, rel)), { recursive: true });
    writeFileSync(path.join(dest, rel), f.bytes, { mode: f.mode });
  }
  overlay(overlayDir, dest);
}
