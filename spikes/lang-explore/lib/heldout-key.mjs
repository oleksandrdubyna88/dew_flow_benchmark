// The archive key and its backup (F2.S1 item 5, bench PR 4a). The key is a mode-600 file read by v1's `readKey` and held in memory only: nothing here formats the key's bytes, and the registry holds only `keySha256`, the sha256 of
// the key file. Before ANY clear case is removed, the backup copy of the key is checked: its folder is on the share's mount (`findmnt` through a seam, run as a program with an argument list and a timeout), the copy is a regular
// file and it hashes to `keySha256`. There is no override. The backup path and the share's mount have no default in this code; the operator names them (`--key-backup`, `--share-mount`, or the private file `heldout-backup.json`).
import { createHash } from 'node:crypto';
import * as nodeFs from 'node:fs';
import path from 'node:path';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

export const sha256Hex = bytes => createHash('sha256').update(bytes).digest('hex');

/** The one default the document allows: the archive key under the operator's home. */
export const defaultKeyFile = home => path.join(home, '.config', 'lang-v2', 'heldout.key');

const KEY_REMEDY = 'give the key\'s path with --key-file (the default is ~/.config/lang-v2/heldout.key); if the key is lost, restore it from its backup copy and never make a new one for a sealed task';
const FINDMNT_TIMEOUT_MS = 10_000;

/** `ok(key bytes)` | `fail`: the key file as `readKey` reads it, with the remedy of this tool. Not compared with any entry. */
export const readKeyFile = keyFile => readKey(keyFile, { remedy: KEY_REMEDY });

/** The key of an archive: `ok(bytes)` when the file is a valid key AND its sha256 is `keySha256`, checked before any decryption; else a `fail`. The bytes never go into a sentence. */
export function loadKey({ keyFile, keySha256, task }) {
  const key = readKeyFile(keyFile);
  if (!key.ok || sha256Hex(key.value) === keySha256) return key;
  return fail(`the key is not the one the archive was sealed with${task === undefined ? '' : ` (task ${task.id})`}: ${keyFile} hashes to another key than the registry's keySha256`);
}

/** The sentence refusing a key that is not the archive's, or null. */
export function keyProblem(args) {
  const key = loadKey(args);
  return key.ok ? null : key.reason;
}

/**
 * The mount a folder is on, through `findmnt --noheadings --output TARGET --target <real folder>`: `ok(mountPoint)` | `fail`. `run(exe, args, { timeoutMs })` is the process seam (`runBounded`: exe and argv, never a shell string, the
 * timeout kills the process group); the folder must be a real path (links resolved) or the answer is about the link.
 */
export const makeMountOf = (run = runBounded) => async realDir => {
  const r = await run('findmnt', ['--noheadings', '--output', 'TARGET', '--target', realDir], { timeoutMs: FINDMNT_TIMEOUT_MS });
  return r.ok ? mountFrom(r.value, realDir) : fail(`findmnt could not be run: ${r.reason}`);
};

/** The mount point findmnt printed, or a fail for a non-zero exit or an empty answer. */
function mountFrom(answer, realDir) {
  const mount = answer.stdout.trim();
  return answer.code === 0 && mount !== '' ? ok(mount) : fail(`findmnt exited ${answer.code} for ${realDir}: ${answer.stderr.trim() || 'no mount is named'}`);
}

export const mountOf = makeMountOf();

const unverified = ({ task, backupFile, why }) => `the key backup cannot be verified: ${backupFile ?? '(no path named)'} (${why}); mount or restore it, then run again (task ${task.id})`;

const realOrNull = (fs, dir) => { try { return fs.realpathSync(dir); } catch { return null; } };

/** The sentence refusing a backup folder that is not on the share's mount, or when the mount cannot be told, or null. The folder is resolved first: a link into the share proves nothing. */
async function mountProblem({ task, backupFile, shareMount, mountOf: mountFn, fs }) {
  const folder = realOrNull(fs, path.dirname(backupFile));
  const share = realOrNull(fs, shareMount) ?? path.resolve(shareMount);
  if (folder === null) return unverified({ task, backupFile, why: `its folder ${path.dirname(backupFile)} does not exist` });
  const found = await mountFn(folder);
  if (!found.ok) return unverified({ task, backupFile, why: found.reason });
  return found.value === share ? null : `the key backup folder is not on the mounted share: ${folder} resolves to the mount ${found.value}, expected ${share}`;
}

const notAKey = stat => !stat.isFile() || stat.size > 64;

/** Why a backup path holds no key (a missing file, a link, a directory, a file of the wrong size), or null. */
function whyNotAKey(stat) {
  if (stat === undefined) return 'the file does not exist';
  return notAKey(stat) ? 'it is not a regular file of the size of a key' : null;
}

/** The sentence refusing a backup file that is not a regular file hashing to `keySha256`, or null. */
function fileProblem({ task, backupFile, keySha256, fs }) {
  const why = whyNotAKey(fs.lstatSync(backupFile, { throwIfNoEntry: false }));
  if (why !== null) return unverified({ task, backupFile, why });
  return sha256Hex(fs.readFileSync(backupFile)) === keySha256 ? null : unverified({ task, backupFile, why: 'its sha256 is not the entry\'s keySha256' });
}

const isNamed = value => typeof value === 'string' && value !== '';

/** The sentence refusing a backup that is not named (there is no default path and no default mount), or null. */
function namedProblem({ task, backupFile, shareMount }) {
  if (!isNamed(backupFile)) return unverified({ task, backupFile, why: 'no backup file is named: give --key-backup' });
  return isNamed(shareMount) ? null : unverified({ task, backupFile, why: 'no share mount is named: give --share-mount' });
}

/**
 * Why a clear case may not be removed for lack of its key's backup (T4), or null when the copy is there: the backup folder is on the share's mount, the copy is a regular file and it hashes to `keySha256`. Every refusal names the
 * backup path and the task; the sentences are the document's. `mountOf(realFolder)` is the seam.
 */
export async function backupProblem({ task, backupFile, shareMount, keySha256, mountOf: mountFn = mountOf, fs = nodeFs }) {
  const named = namedProblem({ task, backupFile, shareMount });
  if (named !== null) return named;
  return (await mountProblem({ task, backupFile, shareMount, mountOf: mountFn, fs })) ?? fileProblem({ task, backupFile, keySha256, fs });
}

/** Write the copy `wx` (mode 600) when there is none. */
function ensureCopy({ key, backupFile, fs }) {
  if (fs.lstatSync(backupFile, { throwIfNoEntry: false }) !== undefined) return;
  const fd = fs.openSync(backupFile, 'wx', 0o600);
  try { fs.writeSync(fd, key); fs.fsyncSync(fd); } finally { fs.closeSync(fd); }
}

/**
 * The first seal's copy of the key to the private backup, verified. A copy already there is verified, never overwritten: equal is fine, another one is a refusal. The mount is checked BEFORE the copy: a share that is not mounted
 * would take the copy onto the root disk. -> `ok(null)` | `fail(sentence)`.
 */
export async function makeBackup({ task, key, backupFile, shareMount, mountOf: mountFn = mountOf, fs = nodeFs }) {
  const named = namedProblem({ task, backupFile, shareMount });
  const misplaced = named ?? (await mountProblem({ task, backupFile, shareMount, mountOf: mountFn, fs }));
  if (misplaced !== null) return fail(misplaced);
  ensureCopy({ key, backupFile, fs });
  const problem = fileProblem({ task, backupFile, keySha256: sha256Hex(key), fs });
  return problem === null ? ok(null) : fail(problem);
}
