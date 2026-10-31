// The workspaces of a bare session (E3b.S1 step 4). v1: an empty directory the runner fills with EXACTLY the pinned v1 inputs (SPEC-v1.txt, the FIXED
// test-v1.sh), never a clone of template/bare-* (the bare box reaches no GitHub). v2: a copy of the v1 session's tree minus build output, then the
// v2 inputs added; a v1 input the agent changed is KEPT and flagged `v1 inputs changed`, never repaired. Files are re-hashed against hashes.txt.
import { copyFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { verifyFiles } from './inputs.mjs';
import { fail, ok } from './result.mjs';
import { excludedDirs, nodeFs } from './score-deps.mjs';

/** The workspace file -> the path of its pinned original in the fetched cache and in hashes.txt (SPECs: upstream; scripts: the FIXED copies at the root). */
const PINNED = Object.freeze({
  'SPEC-v1.txt': 'upstream/SPEC-v1.txt', 'test-v1.sh': 'test-v1.sh', 'SPEC-v2.txt': 'upstream/SPEC-v2.txt', 'test-v2.sh': 'test-v2.sh',
});
const FILES = Object.freeze({ v1: ['SPEC-v1.txt', 'test-v1.sh'], v2: ['SPEC-v2.txt', 'test-v2.sh'] });

const expectedFor = (hashes, names) => names.map(name => ({ path: name, sha256: hashes.find(h => h.path === PINNED[name])?.sha256 ?? null }));

/** The pinned files of `phase` (v2 includes v1's) whose bytes in `work` are not the pinned ones: `[{ path, expected, actual }]` (`actual` null when absent). */
export function pinnedInputsChanged({ work, hashes, phase }) {
  const names = phase === 'v1' ? FILES.v1 : [...FILES.v1, ...FILES.v2];
  return verifyFiles(work, expectedFor(hashes, names)).filter(r => !r.ok).map(({ path: p, expected, actual }) => ({ path: p, expected, actual }));
}

function writeInputs({ inputsRoot, hashes, work, names }) {
  for (const name of names) copyFileSync(path.join(inputsRoot, PINNED[name]), path.join(work, name));
  const bad = verifyFiles(work, expectedFor(hashes, names)).filter(r => !r.ok);
  return bad.length === 0 ? ok(null) : fail(`the pinned inputs written to ${work} do not match hashes.txt: ${bad.map(b => b.path).join(', ')}`);
}

/** Fill an EMPTY `work` with exactly SPEC-v1.txt and test-v1.sh -> `ok({ files })` | `fail`. */
export function fillV1({ inputsRoot, hashes, work }) {
  mkdirSync(work, { recursive: true });
  const written = writeInputs({ inputsRoot, hashes, work, names: FILES.v1 });
  return written.ok ? ok({ files: FILES.v1 }) : written;
}

/**
 * `work` <- the v1 session's tree minus build output (`excludedDirs(stack)`, the scorer's own list: a C# tree loses bin/ obj/ node_modules/, a Node
 * tree keeps a real bin/ of source), then SPEC-v2.txt and test-v2.sh. -> `ok({ changed, flags, excludedDirs })`; `changed` lists the v1 inputs the agent changed and `excludedDirs` what this stack's copy left out (it goes into the session record).
 */
export function fillV2({ inputsRoot, hashes, v1Work, work, stack }) {
  const excluded = excludedDirs(stack);
  nodeFs.copyTree(v1Work, work, excluded);
  const written = writeInputs({ inputsRoot, hashes, work, names: FILES.v2 });
  if (!written.ok) return written;
  const changed = pinnedInputsChanged({ work, hashes, phase: 'v1' });
  return ok({ changed, flags: changed.length > 0 ? ['v1 inputs changed'] : [], excludedDirs: excluded });
}
