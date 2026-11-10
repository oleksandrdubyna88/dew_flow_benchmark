// Where the key backup and the share's mount are named when the flags are absent (F2.S2 item 8, bench PR 4a). Every entry point that may remove a stale clear folder needs both paths, so the operator may keep them in ONE private
// file, `~/.config/lang-v2/heldout-backup.json`: `{ "keyBackup": "<file>", "shareMount": "<dir>" }`, mode 600, owned by the operator, outside git. It holds two paths and no secret. This code has no default for either path: a
// flag wins over the file, and with neither the check refuses naming what is missing (`heldout-key.mjs`).
import { lstatSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const backupConfigFile = home => path.join(home, '.config', 'lang-v2', 'heldout-backup.json');

const isPath = value => typeof value === 'string' && value !== '';

/** The two paths of a parsed file, or a `fail` naming the file. */
function pathsOf(file, json) {
  return isPath(json?.keyBackup) && isPath(json?.shareMount) ? ok({ backupFile: json.keyBackup, shareMount: json.shareMount }) : fail(`${file} must hold {"keyBackup": "<file>", "shareMount": "<dir>"}`);
}

const unsafeFile = (stat, uid) => !stat.isFile() || stat.uid !== uid || (stat.mode & 0o077) !== 0;

/** The sentence refusing a config folder that is not the operator's or is writable by group or others (another user could replace the file), or null. */
function folderProblem(dir, uid) {
  const stat = lstatSync(dir, { throwIfNoEntry: false });
  if (stat === undefined) return null;
  return unsafeFolder(stat, uid) ? `${dir} must be a folder owned by you and not writable by group or others (chmod go-w)` : null;
}

const unsafeFolder = (stat, uid) => !stat.isDirectory() || stat.uid !== uid || (stat.mode & 0o022) !== 0;

/** A `fail` for a config folder or file that another user could have replaced, else null. */
function unsafeConfig(file, stat, uid) {
  const folder = folderProblem(path.dirname(file), uid);
  if (folder !== null) return fail(folder);
  return unsafeFile(stat, uid) ? fail(`${file} must be a regular file owned by you with mode 600`) : null;
}

/** `ok(null)` for no file, `ok({ backupFile, shareMount })`, or a `fail` for a config folder that is not the operator's or is writable by others, a file that is not a regular file of the operator's with mode 600, or that does not parse. */
export function readBackupConfig(home, { uid = process.getuid() } = {}) {
  const file = backupConfigFile(home);
  const stat = lstatSync(file, { throwIfNoEntry: false });
  if (stat === undefined) return ok(null);
  const unsafe = unsafeConfig(file, stat, uid);
  if (unsafe !== null) return unsafe;
  try {
    return pathsOf(file, JSON.parse(readFileSync(file, 'utf8')));
  } catch {
    return fail(`${file} is not valid JSON`);
  }
}

const resolved = (values, flag) => (isPath(values[flag]) ? path.resolve(values[flag]) : undefined);

/** The backup file and the share's mount of a command: the flags, else the private file; `ok({ backupFile, shareMount })` (either may be undefined: the check names what is missing) | `fail`. */
export function backupSettings(values, home) {
  const flags = { backupFile: resolved(values, 'key-backup'), shareMount: resolved(values, 'share-mount') };
  if (flags.backupFile !== undefined && flags.shareMount !== undefined) return ok(flags);
  const config = readBackupConfig(home);
  if (!config.ok) return config;
  return ok({ backupFile: flags.backupFile ?? config.value?.backupFile, shareMount: flags.shareMount ?? config.value?.shareMount });
}
