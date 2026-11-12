// The archive key and its backup (F2.S1 item 5, bench PR 4a). The key is a mode-600 file read by v1's `readKey` and held in memory only: nothing here formats the key's bytes, and the registry holds only `keySha256`, the sha256 of
// the key file. Before ANY clear case is removed, the backup copy of the key is checked: its folder is on the share's mount (`findmnt` through a seam, run as a program with an argument list and a timeout), the copy is a regular
// file and it hashes to `keySha256`. There is no override. The backup path and the share's mount have no default in this code; the operator names them (`--key-backup`, `--share-mount`, or the private file `heldout-backup.json`).
import { createHash, randomBytes } from 'node:crypto';
import * as nodeFs from 'node:fs';
import path from 'node:path';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { ownerAlive, processStartTime } from '../../minigit-bench/lib/session-state.mjs';

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

/** The mount point findmnt printed on its FIRST line (`--noheadings --output TARGET`), or a fail for a non-zero exit or an empty answer. */
function mountFrom(answer, realDir) {
  const mount = answer.stdout.split('\n')[0].trim();
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

const KEY_BYTES = 32;

/** Open the backup without following a link: `ok(fd)` | `fail(why)` (a missing file, a link, anything the system refuses). */
function openNoFollow(backupFile, fs) {
  try {
    return ok(fs.openSync(backupFile, nodeFs.constants.O_RDONLY | nodeFs.constants.O_NOFOLLOW));
  } catch (e) {
    return fail(e.code === 'ENOENT' ? 'the file does not exist' : `it cannot be opened without following a link (${e.code})`);
  }
}

/** The key's bytes of an open descriptor: `ok(bytes)` when it is a regular file of the size of a key, else a `fail(why)`. Everything is read from this one descriptor. */
function readKeyBytes(fd, fs) {
  const stat = fs.fstatSync(fd);
  if (!stat.isFile() || stat.size !== KEY_BYTES) return fail(`it is not a regular file of the size of a key (${KEY_BYTES} bytes)`);
  const bytes = Buffer.alloc(KEY_BYTES + 1);
  const got = fs.readSync(fd, bytes, 0, bytes.length, 0);
  return got === KEY_BYTES ? ok(bytes.subarray(0, KEY_BYTES)) : fail('it changed while it was read');
}

/** The bytes of the backup, read through ONE handle (`O_NOFOLLOW`, then `fstat` and `read` of that descriptor): a link put in the copy's place, or a file swapped after a look, cannot be what is hashed. */
function backupBytes(backupFile, fs) {
  const opened = openNoFollow(backupFile, fs);
  if (!opened.ok) return opened;
  try {
    return readKeyBytes(opened.value, fs);
  } finally {
    fs.closeSync(opened.value);
  }
}

/** The sentence refusing a backup file that is not a regular file hashing to `keySha256`, or null. */
function fileProblem({ task, backupFile, keySha256, fs }) {
  const bytes = backupBytes(backupFile, fs);
  if (!bytes.ok) return unverified({ task, backupFile, why: bytes.reason });
  return sha256Hex(bytes.value) === keySha256 ? null : unverified({ task, backupFile, why: 'its sha256 is not the entry\'s keySha256' });
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

const TEMP_NAME = base => new RegExp(`^\\.${base.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\.(\\d+)\\.(\\d+)\\.[0-9a-f]{8}\\.tmp$`);

/** Remove the temporary copies a killed run left beside the backup, once their owner is gone (`isAlive(pid, startTime)`, from the name); a running owner's, and any name of another shape, are left. */
function removeStaleTemps({ backupFile, isAlive, fs }) {
  const pattern = TEMP_NAME(path.basename(backupFile));
  for (const name of fs.readdirSync(path.dirname(backupFile))) {
    const m = pattern.exec(name);
    if (m !== null && !isAlive(Number(m[1]), m[2])) fs.rmSync(path.join(path.dirname(backupFile), name), { force: true });
  }
}

/** The answers of a file system that has no hard links (WSL's drvfs mount of a Windows or NAS drive answers EPERM). */
const NO_HARD_LINKS = new Set(['EPERM', 'ENOTSUP', 'EOPNOTSUPP', 'ENOSYS']);

/** Write `key` to `file` by an exclusive create (`wx`: a name that is there is never replaced, it fails with EEXIST), flushed. -> true when written, false when the name was taken. */
function createExclusive({ key, file, fs }) {
  let fd;
  try {
    fd = fs.openSync(file, 'wx', 0o600);
  } catch (e) {
    if (e.code === 'EEXIST') return false;
    throw e;
  }
  try { fs.writeSync(fd, key); fs.fsyncSync(fd); } finally { fs.closeSync(fd); }
  return true;
}

/**
 * Publish the copy whole or not at all: a temporary name of this process beside it (mode 600, flushed), then a hard link that fails when the name exists (a copy that is there is never replaced), then the temporary name is removed.
 * A share with no hard links (`NO_HARD_LINKS`) gets the copy by an exclusive create of its own name instead, which also never replaces a copy that is there; the key is one write of 32 bytes, and a copy cut short by a crash
 * inside it does not hash to the key, so the caller refuses it naming the path (never used, never overwritten). The mode on such a share is its own access list's, not 600.
 */
function publishCopy({ key, backupFile, fs, pid, startTime }) {
  const temp = path.join(path.dirname(backupFile), `.${path.basename(backupFile)}.${pid}.${startTime}.${randomBytes(4).toString('hex')}.tmp`);
  const fd = fs.openSync(temp, 'wx', 0o600);
  try { fs.writeSync(fd, key); fs.fsyncSync(fd); } finally { fs.closeSync(fd); }
  try {
    fs.linkSync(temp, backupFile);
  } catch (e) {
    if (NO_HARD_LINKS.has(e.code)) {
      fs.unlinkSync(temp);
      createExclusive({ key, file: backupFile, fs });
      return;
    }
    if (e.code !== 'EEXIST') throw e;
  }
  fs.unlinkSync(temp);
}

/** The copy, made when there is none (an existing one is verified by the caller, never overwritten). A backup folder that is gone is a refusal naming it. -> `ok(null)` | `fail`. */
function ensureCopy(a) {
  try {
    removeStaleTemps(a);
  } catch (e) {
    if (e.code === 'ENOENT') return fail(`the key backup folder ${path.dirname(a.backupFile)} is not there: it was removed or unmounted while the copy was made; mount or restore it, then run again`);
    throw e;
  }
  if (a.fs.lstatSync(a.backupFile, { throwIfNoEntry: false }) === undefined) publishCopy(a);
  return ok(null);
}

/**
 * The first seal's copy of the key to the private backup, verified. The copy is published whole or not at all (`publishCopy`); a copy already there is verified, never overwritten: one that hashes to the key is accepted (a resume), another
 * one is a refusal naming it. The mount is checked BEFORE the copy: a share that is not mounted would take the copy onto the root disk. `pid`, `startTime` and `isAlive` name the owner of the temporary copy. -> `ok(null)` | `fail(sentence)`.
 */
export async function makeBackup({ task, key, backupFile, shareMount, mountOf: mountFn = mountOf, fs = nodeFs, pid = process.pid, startTime = processStartTime(process.pid), isAlive = ownerAlive }) {
  const named = namedProblem({ task, backupFile, shareMount });
  const misplaced = named ?? (await mountProblem({ task, backupFile, shareMount, mountOf: mountFn, fs }));
  if (misplaced !== null) return fail(misplaced);
  const copied = ensureCopy({ key, backupFile, fs, pid, startTime, isAlive });
  if (!copied.ok) return copied;
  const problem = fileProblem({ task, backupFile, keySha256: sha256Hex(key), fs });
  return problem === null ? ok(null) : fail(problem);
}
