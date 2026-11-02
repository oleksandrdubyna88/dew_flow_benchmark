// E1.S8b: the execution layer's identity (D36, D43): the sha256 of everything that decides the sandbox and every launch — every regular file of `lib/exec-layer/` (`scope-entry.sh` included) AND every module
// outside it that the layer imports, found by WALKING the static relative imports (fix batch 1, K0/O3): v1's `bwrap.mjs` builds the box's argv, `run.mjs` launches every child, `find-on-path.mjs`, `result.mjs`,
// `preflight-box.mjs` and what they import. Entries are sorted paths relative to the spikes folder; the `execLayer` key of `inputs/specs.d/exec-layer.json` holds them with `treeSha256`, the hash of what
// `sha256sum` would print for them (`<sha>  <path>`, one line each, in path order); `specs-record.mjs` assembles `inputs/specs.json`. A link or any other kind of entry inside the layer is refused: a hash
// that did not cover what runs would be no identity. The memory policy file (`inputs/specs.d/memory-policy.json`, D55) is part of it: the series' one memory policy is an identity of the series (parent §6.1). A test file or a fixture is not part of the layer.
import { createHash } from 'node:crypto';
import { lstatSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';
import { POLICY_FILE } from './policy.mjs';

export const LAYER_DIR = fileURLToPath(new URL('.', import.meta.url)).replace(/\/$/, '');
/** The folder the identity's paths are relative to: `spikes/`. */
export const SPIKES_ROOT = path.resolve(LAYER_DIR, '..', '..', '..');
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const IMPORT = /(?:^|\n)\s*(?:import|export)\b[^'"\n]*?from\s+['"](\.[^'"]+)['"]/g;
const SOURCE = /\.m?js$/;

function walk(dir, prefix = '') {
  return readdirSync(dir).sort().flatMap(name => {
    const relative = prefix === '' ? name : `${prefix}/${name}`;
    const stat = lstatSync(path.join(dir, name));
    return stat.isDirectory() ? walk(path.join(dir, name), relative) : [{ relative, regular: stat.isFile() }];
  });
}

/** The relative static imports of one source file, resolved to absolute paths. */
const importsOf = file => [...readFileSync(file, 'utf8').matchAll(IMPORT)].map(match => path.resolve(path.dirname(file), match[1]));

/** Every file reachable by relative imports from `starts`, not counting the starts themselves. */
function reachable(starts) {
  const seen = new Set();
  const stack = [...starts];
  while (stack.length > 0) {
    for (const target of importsOf(stack.pop())) {
      if (seen.has(target) || !SOURCE.test(target)) continue;
      seen.add(target);
      stack.push(target);
    }
  }
  return [...seen];
}

/** `ok({ execLayer: { treeSha256, files: [{ path, sha256 }] } })` for the layer folder, what it imports and the memory policy file (D55: the policy is part of the identity; the real layer folder lists it, another folder only when `policyFile` is given), or `fail(reason)` naming an entry that is not a regular file. */
export function computeIdentity(dir, { root = SPIKES_ROOT, policyFile = dir === LAYER_DIR ? POLICY_FILE : null } = {}) {
  const entries = walk(dir);
  const bad = entries.find(entry => !entry.regular);
  if (bad) return fail(`${bad.relative} is not a regular file (a link or another kind of entry): the hash would not cover what runs`);
  const own = entries.map(entry => path.join(dir, entry.relative));
  const inside = file => file.startsWith(`${dir}${path.sep}`);
  const listed = [...new Set([...own, ...reachable(own.filter(file => SOURCE.test(file))).filter(file => !inside(file)), ...(policyFile === null ? [] : [policyFile])])];
  const files = listed.map(file => ({ path: path.relative(root, file).split(path.sep).join('/'), sha256: sha256(readFileSync(file)) })).sort((a, b) => (a.path < b.path ? -1 : 1));
  const manifest = files.map(file => `${file.sha256}  ${file.path}\n`).join('');
  return ok({ execLayer: { treeSha256: sha256(manifest), files } });
}

function differences(pinned, now) {
  const byPath = new Map(pinned.files.map(file => [file.path, file.sha256]));
  const changed = now.files.filter(file => byPath.has(file.path) && byPath.get(file.path) !== file.sha256).map(file => `${file.path} changed`);
  const added = now.files.filter(file => !byPath.has(file.path)).map(file => `${file.path} is not in the identity`);
  const gone = pinned.files.filter(file => !now.files.some(other => other.path === file.path)).map(file => `${file.path} is missing`);
  return [...changed, ...added, ...gone];
}

/** `{ ok: true }` when the layer is exactly what `record.execLayer` names, else `{ ok: false, reason }` naming each difference. */
export function verifyIdentity(dir, record, options) {
  const now = computeIdentity(dir, options);
  if (!now.ok) return now;
  const found = differences(record.execLayer, now.value.execLayer);
  if (found.length > 0) return fail(`the layer differs from its identity: ${found.join('; ')}`);
  return record.execLayer.treeSha256 === now.value.execLayer.treeSha256 ? { ok: true } : fail('the identity\'s tree hash differs from the layer');
}
