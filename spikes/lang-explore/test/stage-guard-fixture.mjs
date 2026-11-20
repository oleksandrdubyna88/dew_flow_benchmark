// The world of the Stage 1 guard tests (F3.S1 (a), PR 4): an invented task repository with W on top, an invented bench root with the frozen record over it, the live identities and the manifest that equal each other, and the guard's arguments. The tests of the guard
// and the wiring tests of `stage-run` share it. Nothing real is read or written. Not a test file.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { entryOfSeal } from '../lib/heldout-registry.mjs';
import { RECORD_PATHS } from '../lib/stage-guard.mjs';
import { IDENTITY_NAMES } from '../lib/stage-identity.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';
import { FORMAT, TAG, committedCheckout, folder, freezeTaskList, git, sha256, specOf } from './quick-freeze-fixture.mjs';

export const hex = (n, c) => c.repeat(n);
export const HASHES_TEXT = 'invented hash list\n';
export const REF = (task, stack) => sha256(`reference ${task} ${stack}`);
export const put = (file, value) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, typeof value === 'string' ? value : `${JSON.stringify(value, null, 2)}\n`); };
const leak = commit => ({ commit, exit: 0, date: '2026-11-20' });

/** The three record files, the packages file and the hash list of an invented bench repository root, consistent with the invented task repository `c` (a committed checkout: the tag at C and W on top). */
export function writeRoot(root, c, over = {}) {
  const tasks = freezeTaskList();
  const specStates = {
    specs: Object.fromEntries(tasks.map(task => [task.id, { path: task.spec.path, sha256: sha256(specOf(task.id)), state: 'frozen', gate: `inputs/gates/spec-${task.id}.md`, rIds: ['R1'], commitSha: c.repo.commit, tagObjectSha: git(c.dir, 'rev-parse', TAG) }])),
    freeze: {
      tag: TAG, tagObjectSha: git(c.dir, 'rev-parse', TAG), commitSha: c.repo.commit, workspaceCommit: c.head, formatSha256: sha256(FORMAT), hashesLangSha256: sha256(HASHES_TEXT),
      leakcheck: { freezeCommit: leak(c.repo.commit), scratchWorkspace: leak(hex(40, '9')), workspaceCommit: leak(c.head) }, state: 'accepted', provisionalFrom: '2026-11-20', acceptedOn: '2026-11-21', superseded: [], workspacesCheck: { commit: c.head, exit: 0, date: '2026-11-21' },
    },
  };
  const heldoutFrozen = { tasks: Object.fromEntries(FREEZE_TASKS.map(id => [id, entryOfSeal({ archive: `heldout-${id}/suite.enc`, archiveSha256: hex(64, '1'), treeSha256: hex(64, '2'), caseCount: 19, trapCount: 7, manifestSha256: hex(64, '3'), keySha256: hex(64, '4'), sealedOn: '2026-11-12' })])) };
  const memoryLimit = { bytes: 536870912, mib: 512, decidedOn: '2026-11-07', by: 'invented', note: 'invented', registration: { date: '2026-11-18', references: FREEZE_TASKS.flatMap(task => ['csharp', 'node'].map(stack => ({ task, stack, treeSha256: REF(task, stack) }))) } };
  const packages = Object.fromEntries(FREEZE_TASKS.map(id => [id, { refsTreeSha256: { csharp: REF(id, 'csharp'), node: REF(id, 'node') } }]));
  const files = { specStates, heldoutFrozen, memoryLimit, ...over };
  for (const [name, rel] of Object.entries(RECORD_PATHS)) put(path.join(root, rel), files[name]);
  put(path.join(root, 'spikes/lang-explore/inputs/packages.json'), over.packages ?? packages);
  put(path.join(root, 'spikes/lang-bench/inputs/hashes-lang.txt'), over.hashes ?? HASHES_TEXT);
  return { specStates, heldoutFrozen, memoryLimit, packages };
}

export const LIVE = Object.fromEntries(IDENTITY_NAMES.map((name, i) => [name, { value: sha256(`live ${name} ${i}`) }]));
export const MANIFEST = { identities: Object.fromEntries(Object.entries(LIVE).map(([name, v]) => [name, v.value])) };
export const FAKES = { manifest: MANIFEST, live: async () => LIVE, harnessAccepted: [], preregGuard: async () => [] };

/** A whole world: an invented task repository with W on top, a bench root over it, and the guard's arguments. */
export async function world(t, over = {}) {
  const c = await committedCheckout(t);
  const repo = folder(t, 'benchroot');
  const files = writeRoot(repo, c, over);
  return { c, repo, files, args: { repo, series: folder(t, 'series'), taskRepo: c.dir, paid: false, requireSeed: false, deps: { ...FAKES, pinnedTaskSha: c.head } } };
}
