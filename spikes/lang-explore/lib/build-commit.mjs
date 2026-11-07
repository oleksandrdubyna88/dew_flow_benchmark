// The atomic build commit point of a quick-run session's scoring (F1.S2, section 1 and 2.7). The clean copy is made and built in a temporary folder `score/build-<token>/`, the result is validated, the folder is renamed into
// place as `score/tree/`, and ONLY THEN the durable completion record `score/build.json` is written (`wx`), naming the hash of the tree it describes. A pass runs only against a tree whose record exists and whose hash matches
// the tree now (`committedTree`); a `build-*` folder or a `tree/` without a record is uncommitted state and is removed before the build is made again (`discardUncommitted`). A build that really ran and exited non-zero is
// committed too (`ok: false`); a build that never began commits nothing and is an environment failure. Nothing here is ever rewritten: the record is written once.
import { createHash, randomBytes } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, readlinkSync, renameSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { removeArea } from './score.mjs';

export const BUILD_FILE = 'build.json';
export const TREE_DIR = 'tree';
const TEMP_PREFIX = 'build-';

const DEFAULT_IO = Object.freeze({ rename: renameSync, writeNew: (file, text) => writeFileSync(file, text, { flag: 'wx' }) });

/** The relative paths of the files (and links) under `dir`, in name order. */
function filesUnder(dir, rel = '') {
  return readdirSync(path.join(dir, rel), { withFileTypes: true }).sort((a, b) => (a.name < b.name ? -1 : 1)).flatMap(e => {
    const r = rel === '' ? e.name : `${rel}/${e.name}`;
    return e.isDirectory() ? filesUnder(dir, r) : [r];
  });
}

const contentOf = file => (lstatSync(file).isSymbolicLink() ? Buffer.from(`link:${readlinkSync(file)}`) : readFileSync(file));

/** The sha256 over every file of `dir`: its name, a NUL, its bytes (a link: its target), a NUL, in name order (the shape of `casesTreeHash`). */
export function treeHash(dir) {
  const h = createHash('sha256');
  for (const rel of filesUnder(dir)) {
    h.update(`${rel}\0`);
    h.update(contentOf(path.join(dir, rel)));
    h.update('\0');
  }
  return h.digest('hex');
}

const isDir = p => statSync(p, { throwIfNoEntry: false })?.isDirectory() === true;
const treeOf = work => path.join(work, TREE_DIR);
const buildFileOf = work => path.join(work, BUILD_FILE);
const tempFolders = work => (existsSync(work) ? readdirSync(work).filter(n => n.startsWith(TEMP_PREFIX)).sort() : []);

/** Remove the uncommitted build state: every `build-*` folder and a `tree/` that has no completion record. -> the names removed. */
export function discardUncommitted(work) {
  const gone = [...tempFolders(work), ...(isDir(treeOf(work)) && !existsSync(buildFileOf(work)) ? [TREE_DIR] : [])];
  gone.forEach(name => removeArea(path.join(work, name)));
  return gone.sort();
}

const mismatch = ({ recorded, live }) => fail(`build-record-mismatch: build.json names the tree ${recorded} and the tree on disk hashes to ${live}`);

/** The completion record against the tree it names: `ok(record)`, `ok(null)` when there is no record, `fail('build-record-mismatch: ...')` when the tree is not the one recorded (or is gone). */
export function committedTree(work) {
  if (!existsSync(buildFileOf(work))) return ok(null);
  const record = JSON.parse(readFileSync(buildFileOf(work), 'utf8'));
  const live = isDir(treeOf(work)) ? treeHash(treeOf(work)) : 'no tree on disk';
  return live === record.treeSha256 ? ok(record) : mismatch({ recorded: record.treeSha256, live });
}

const recordOf = ({ built, prepared, treeSha256, committedAt }) => Object.freeze({
  treeSha256, ok: built.ok, exit: built.exit, timedOut: built.timedOut === true, wallMs: built.wallMs, tail: built.tail, frozenChanges: prepared.frozen, harnessDrift: prepared.drift, nonRegularLeftOut: prepared.left, committedAt,
});

/** Why a build's answer cannot be committed, or null: its exit is not known (and it did not time out), or the tree is not a directory. */
const unfitProblem = ({ built, folder }) => {
  if (!isDir(folder)) return `the built tree ${folder} is not a directory`;
  return Number.isInteger(built.exit) || built.timedOut === true ? null : 'the build ended with no known exit';
};

async function buildInFolder({ folder, prepare, build }) {
  const prepared = await prepare(folder);
  if (!prepared.ok) return prepared;
  const built = await build({ tree: folder });
  if (!built.ok) return built;
  const unfit = unfitProblem({ built: built.value, folder });
  return unfit ? fail(unfit, { kind: 'environment' }) : ok({ prepared: prepared.value, built: built.value });
}

/**
 * Make the clean copy (`prepare(dest)` -> `ok({ frozen, drift, left })`), build it (`build({ tree })` -> `ok({ ok, exit, timedOut, wallMs, tail })` | environment `fail`), validate, rename into `work/tree`, then write `work/build.json`
 * (`wx`). `io` (`rename`, `writeNew`) and `now` are seams. -> `ok(record)` | an environment `fail` (the temporary folder is removed; nothing is committed).
 */
export async function commitBuild({ work, prepare, build, io = DEFAULT_IO, now = () => new Date() }) {
  mkdirSync(work, { recursive: true });
  const folder = path.join(work, `${TEMP_PREFIX}${randomBytes(4).toString('hex')}`);
  try {
    const done = await buildInFolder({ folder, prepare, build });
    if (!done.ok) throw Object.assign(new Error(done.reason), { answer: done });
    io.rename(folder, treeOf(work));
    const record = recordOf({ ...done.value, treeSha256: treeHash(treeOf(work)), committedAt: now().toISOString() });
    io.writeNew(buildFileOf(work), `${JSON.stringify(record, null, 2)}\n`);
    return ok(record);
  } catch (e) {
    removeArea(folder);
    return e.answer ?? fail(`committing the build stopped on an unexpected ${e?.name ?? 'error'}: ${e?.message ?? e}`, { kind: 'environment' });
  }
}
