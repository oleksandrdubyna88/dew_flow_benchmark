// The world of the seal, open, close and reader tests (bench PR 4a): an invented quick task in a temp home, an invented key in a 700 folder, a backup folder inside an invented share, a `mountOf` that knows where that share is,
// a registry file path, an invented `manifest.json`, and spies on the two decryption seams. Nothing real is read: no key of the operator, no private folder, no share. Not a test file.
//
// Every invented case carries one distinctive token (`TOKEN`), so a test can scan what a verb said or wrote for it (D41).
import { mkdirSync, readFileSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { generateKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { unseal } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { unpackTo } from '../../minigit-bench/lib/heldout-archive.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { taskPaths } from '../lib/tasks.mjs';
import { quickHome, quickTask, writeQuickHeldout } from './quick-leak-fixture.mjs';

export const TOKEN = 'invented-case-QX7';
export const IDS = Object.freeze(['h001', 'h002', 'h003']);
export const TRAPS = Object.freeze(['h002']);
export const CASES = Object.freeze(IDS.map((id, i) => ({ id, input: `scenario ${i}\n${TOKEN}-in-${id}\n`, output: `transcript ${i}\n${TOKEN}-out-${id}\n` })));

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };

/** `mountOf` for a share at `share`: a folder under it is on the share's mount, any other folder is on the root mount. */
export const mountFor = share => async realDir => ok(realDir === share || realDir.startsWith(`${share}${path.sep}`) ? share : '/');

/** A decryption seam that counts its calls. */
function spied(fn, spies, name) {
  return (...args) => { spies[name] += 1; return fn(...args); };
}

/**
 * A sealed-task world. `ids` are the invented case ids (default three), `task` the quick task id. -> `{ home, task, heldoutDir, casesDir, keyFile, backupFile, shareMount, frozenFile, manifestFile, spies, args, say }`,
 * where `args` is what `sealPackage`, `openClearFolder`, ... take (every seam is an argument). `over` replaces entries of `args`.
 */
export function world({ taskId = '04', cases = CASES, traps = TRAPS, over = {} } = {}) {
  const task = quickTask(taskId);
  const home = quickHome();
  const paths = writeQuickHeldout({ home, task, cases, traps });
  const keyFile = path.join(tmpDir('q-key'), 'heldout.key');
  generateKey(keyFile);
  const shareMount = realpathSync(tmpDir('q-share'));
  const backupDir = path.join(shareMount, 'backup');
  mkdirSync(backupDir, { mode: 0o700 });
  const manifestFile = path.join(taskPaths(task, home).heldoutDir, 'manifest.json');
  put(manifestFile, '{"coverage":"invented"}\n');
  const spies = { unseal: 0, unpackTo: 0, mounts: 0 };
  const frozenFile = path.join(tmpDir('q-frozen'), 'heldout-frozen.json');
  const args = {
    home, task, frozenFile, keyFile, backupFile: path.join(backupDir, 'heldout.key.bak'), shareMount, manifestFile, now: () => new Date('2026-11-09T09:00:00.000Z'),
    mountOf: async dir => { spies.mounts += 1; return mountFor(shareMount)(dir); }, unseal: spied(unseal, spies, 'unseal'), unpackTo: spied(unpackTo, spies, 'unpackTo'), retry: { tries: 3, sleepMs: 1 }, hooks: {}, ...over,
  };
  return { home, task, taskId, heldoutDir: taskPaths(task, home).heldoutDir, casesDir: paths.casesDir, keyFile, backupFile: args.backupFile, shareMount, frozenFile, manifestFile, spies, args };
}

/** Every file under `dir` (relative, sorted), recursively. */
export const filesUnder = dir => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => path.join(path.relative(dir, e.parentPath), e.name)).sort();

/** The text of every file under the folders given, joined: what a scan for a token reads. */
export const textUnder = (...dirs) => dirs.flatMap(d => filesUnder(d).map(f => readFileSync(path.join(d, f), 'latin1'))).join('\n');

export { fail, ok };
