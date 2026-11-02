// E1.S8b: the execution layer's identity (D36, D43): the sha256 of its tree — every regular file of `lib/exec-layer/`, `scope-entry.sh` included, sorted by path — as the `execLayer` key of
// `inputs/specs.d/exec-layer.json` (this story's own writer file; `specs-record.mjs` assembles `inputs/specs.json`). The manifest text is what `sha256sum` prints for the files, one line
// per file in path order, and `treeSha256` is the hash of that text; the runner's pin (`runner-pin.mjs`) is built the same way. A link or any other kind of entry is refused: a hash that
// did not cover what runs would be no identity. Nothing else is in the tree: a test file or a fixture is not part of the layer.
import { createHash } from 'node:crypto';
import { lstatSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';

export const LAYER_DIR = fileURLToPath(new URL('.', import.meta.url));
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

function walk(dir, prefix = '') {
  return readdirSync(dir).sort().flatMap(name => {
    const relative = prefix === '' ? name : `${prefix}/${name}`;
    const stat = lstatSync(path.join(dir, name));
    if (stat.isDirectory()) return walk(path.join(dir, name), relative);
    return [{ relative, regular: stat.isFile() }];
  });
}

/** `ok({ execLayer: { treeSha256, files: [{ path, sha256 }] } })` for the tree, or `fail(reason)` naming an entry that is not a regular file. */
export function computeIdentity(dir) {
  const entries = walk(dir);
  const bad = entries.find(entry => !entry.regular);
  if (bad) return fail(`${bad.relative} is not a regular file (a link or another kind of entry): the hash would not cover what runs`);
  const files = entries.map(entry => ({ path: entry.relative, sha256: sha256(readFileSync(path.join(dir, entry.relative))) }));
  const manifest = files.map(file => `${file.sha256}  lib/exec-layer/${file.path}\n`).join('');
  return ok({ execLayer: { treeSha256: sha256(manifest), files } });
}

function differences(pinned, now) {
  const byPath = new Map(pinned.files.map(file => [file.path, file.sha256]));
  const changed = now.files.filter(file => byPath.has(file.path) && byPath.get(file.path) !== file.sha256).map(file => `${file.path} changed`);
  const added = now.files.filter(file => !byPath.has(file.path)).map(file => `${file.path} is not in the identity`);
  const gone = pinned.files.filter(file => !now.files.some(other => other.path === file.path)).map(file => `${file.path} is missing`);
  return [...changed, ...added, ...gone];
}

/** `{ ok: true }` when the tree is exactly what `record.execLayer` names, else `{ ok: false, reason }` naming each difference. */
export function verifyIdentity(dir, record) {
  const now = computeIdentity(dir);
  if (!now.ok) return now;
  const found = differences(record.execLayer, now.value.execLayer);
  if (found.length > 0) return fail(`the layer differs from its identity: ${found.join('; ')}`);
  return record.execLayer.treeSha256 === now.value.execLayer.treeSha256 ? { ok: true } : fail('the identity\'s tree hash differs from the tree');
}
