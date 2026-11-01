// The pin of runner/ (E1.S8a): a version, the sha256 of every file, and one manifest hash, as the `runner` key of
// inputs/specs.d/runner.json. The workspace copy of the runner must equal this tree (E1.S3's check reuses verifyPin).
// Only `*.py` regular files are pinned; a Python cache folder is ignored; anything else in runner/ is refused, so the frozen
// tree is exactly its list of files.
import { createHash } from 'node:crypto';
import { lstatSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

const ok = value => ({ ok: true, value });
const fail = reason => ({ ok: false, reason });
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const VERSION_FILE = 'rs_report.py';
const VERSION_LINE = /^RUNNER_VERSION = "([0-9]+\.[0-9]+\.[0-9]+[0-9A-Za-z.+-]*)"$/m;
const IGNORED = new Set(['__pycache__']);

/** The entries of the folder as `{ name, error? }`: a `.py` regular file is fine, a cache folder is ignored, anything else is an error. */
function classify(dir, name) {
  if (IGNORED.has(name)) return null;
  const stat = lstatSync(path.join(dir, name));
  return stat.isFile() && name.endsWith('.py') ? { name } : { name, error: `${name} is not a regular .py file (a symbolic link, a folder or another kind of file)` };
}

/** `ok([{ path, sha256 }])` for the runner folder, sorted by path, or `fail`. */
export function listFiles(dir, readNames = readdirSync) {
  const entries = readNames(dir).sort().map(name => classify(dir, name)).filter(Boolean);
  const bad = entries.find(e => e.error);
  if (bad) return fail(bad.error);
  if (entries.length === 0) return fail(`${dir} holds no .py file`);
  return ok(entries.map(e => ({ path: e.name, sha256: sha256(readFileSync(path.join(dir, e.name))) })));
}

/** The text `sha256sum` would print for the files: the one thing the manifest hash is taken over. */
export const manifestText = files => files.map(f => `${f.sha256}  runner/${f.path}\n`).join('');

function versionOf(dir) {
  try {
    return VERSION_LINE.exec(readFileSync(path.join(dir, VERSION_FILE), 'utf8'))?.[1] ?? null;
  } catch {
    return null;
  }
}

/** `ok({ runner: { version, files, manifestSha256 } })` for the runner folder. */
export function computePin(dir, readNames = readdirSync) {
  const listed = listFiles(dir, readNames);
  if (!listed.ok) return listed;
  const version = versionOf(dir);
  if (version === null) return fail(`${VERSION_FILE} has no \`RUNNER_VERSION = "x.y.z"\` line`);
  return ok({ runner: { version, files: listed.value, manifestSha256: sha256(manifestText(listed.value)) } });
}

function differences(pinned, now) {
  const byPath = new Map(pinned.files.map(f => [f.path, f.sha256]));
  const changed = now.files.filter(f => byPath.has(f.path) && byPath.get(f.path) !== f.sha256).map(f => `${f.path} changed`);
  const added = now.files.filter(f => !byPath.has(f.path)).map(f => `${f.path} is not in the pin`);
  const gone = pinned.files.filter(f => !now.files.some(n => n.path === f.path)).map(f => `${f.path} is missing`);
  return [...changed, ...added, ...gone];
}

/** `{ ok: true }` when the runner folder is exactly what `record.runner` pins, else `{ ok: false, reason }` naming each difference. */
export function verifyPin(dir, record) {
  const now = computePin(dir);
  if (!now.ok) return now;
  const found = differences(record.runner, now.value.runner);
  if (found.length > 0) return fail(`the runner differs from its pin: ${found.join('; ')}`);
  const same = record.runner.manifestSha256 === now.value.runner.manifestSha256 && record.runner.version === now.value.runner.version;
  return same ? { ok: true } : fail('the pin\'s version or manifest hash differs from the runner folder');
}
