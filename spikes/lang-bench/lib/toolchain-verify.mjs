// E1.S5: reading the toolchain store back, and recording the tree hashes of what the installer registered. `verifyStore` is the shallow check
// the preflight can afford (present, sealed, one SDK) plus, with `deep`, the byte-level check against the recorded tree hash.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadStacksLang } from './toolchain-pins.mjs';
import { sdkVersionsIn, treeRecord, writablePaths } from './toolchain-store.mjs';

const SHOWN_WRITABLE = 5;

function directoryProblems({ name, dirName, dir, stacks, deep }) {
  const writable = writablePaths(dir);
  const sdks = name === 'dotnetSdk' ? sdkVersionsIn(dir) : [stacks.pins.dotnetSdk];
  const recorded = stacks.treeSha256?.[name];
  const actual = deep && recorded ? treeRecord(dir) : recorded;
  return [
    ...(writable.length > 0 ? [`${dirName} is writable at ${writable.slice(0, SHOWN_WRITABLE).join(', ')}${writable.length > SHOWN_WRITABLE ? ` (and ${writable.length - SHOWN_WRITABLE} more)` : ''}: it must be sealed read-only`] : []),
    ...(sdks.length === 1 && sdks[0] === stacks.pins.dotnetSdk ? [] : [`${dirName} holds SDK ${sdks.join(', ') || '(none)'}; only ${stacks.pins.dotnetSdk} is allowed`]),
    ...(deep && !recorded ? [`${dirName}: no tree hash recorded in stacks-lang.json, so the bytes cannot be checked`] : []),
    ...(deep && recorded && actual !== recorded ? [`${dirName}: tree hash ${actual} differs from the recorded ${recorded}`] : []),
  ];
}

/** `{ ok, problems }` for the store at `storeRoot`: every directory present and sealed, the SDK directory holding only the pinned SDK, and with `deep` each tree hash equal to the record. */
export function verifyStore({ stacks, storeRoot, deep = false }) {
  const problems = Object.entries(stacks.directories).flatMap(([name, dirName]) => {
    const dir = path.join(storeRoot, dirName);
    return existsSync(dir) ? directoryProblems({ name, dirName, dir, stacks, deep }) : [`${dirName} is not in the store at ${storeRoot}`];
  });
  return { ok: problems.length === 0, problems };
}

/**
 * The text of stacks-lang.json with `treeSha256` holding `trees` merged over what is recorded -> ok(text) | fail. A hash already recorded is never moved
 * (a tree that changed is a finding, not an update). The file is `JSON.stringify(json, null, 2)` plus a newline, the one serialisation (a test pins that the committed file round-trips through it).
 */
export function recordTrees(text, trees) {
  let json;
  try {
    json = JSON.parse(text);
  } catch (e) {
    return fail(`stacks-lang.json is not JSON (${e.message})`);
  }
  const existing = json.treeSha256 ?? {};
  const moved = Object.keys(trees).find(name => existing[name] !== undefined && existing[name] !== trees[name]);
  if (moved) return fail(`treeSha256.${moved} is recorded as ${existing[moved]}, the store now has ${trees[moved]}: a recorded hash is never moved`);
  const merged = Object.fromEntries(Object.entries({ ...existing, ...trees }).sort(([a], [b]) => a.localeCompare(b)));
  const out = `${JSON.stringify({ ...json, treeSha256: merged }, null, 2)}\n`;
  const reread = loadStacksLang(JSON.parse(out));
  return reread.ok && JSON.stringify(JSON.parse(out).treeSha256) === JSON.stringify(merged) ? ok(out) : fail(`recording the tree hashes would have produced an invalid stacks-lang.json: ${reread.reason ?? 'the block did not land'}`);
}
