// The world of the Stage 1 guard tests (F3.S1 (a), PR 4): a run-map of kind `data` from a script, a series folder over it with session folders that hold the three cache folders and a few records, and a private backup folder.
// Nothing real is read or written: every folder is an owned temporary one (`common.test-leftovers`), every byte is invented, and no held-out case, trap name or case id exists here. Not a test file.
import { createHash } from 'node:crypto';
import { chmodSync, lstatSync, mkdirSync, readdirSync, readFileSync, readlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { openSeries, planSession, transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { runMapFileOf } from '../lib/series.mjs';
import { slotsOf } from '../lib/stage-schedule.mjs';

export const owned = ownedFor({ token: 'bench' });
export const SEED = 'lv2-0123456789abcdef';
export const SLOTS = slotsOf({ seed: SEED });
export const GOOD = { class: 'completed', data: true, scored: true };
export const VOID = { class: 'void: harness', data: false, scored: false };
export const RAN = { class: 'completed', data: true, scored: false };
export const MIB = 1024 * 1024;

/** The rid of the session of slot `slot`, `stack`, `attempt` (the schedule's own rid for the fixture seed). */
export const ridOf = (slot, stack, attempt = 1) => SLOTS[slot - 1].rids[stack][attempt - 1];

/** A run-map of kind `data` from a script of `[slot, stack, attempt, end]`: `end` is `planned`, `launching` or the patch of a `ran` session (scored when `end.scored`). */
export function mapWith(script) {
  let map = openSeries({ seriesId: 'x-test', caps: {} });
  for (const [slot, stack, attempt, end] of script) {
    const rid = ridOf(slot, stack, attempt);
    map = planSession(map, { rid, pair: slot, stack, phase: 'v1', attempt, kind: 'data', label: attempt === 1 ? 'paired' : 'ran unpaired', mask: SLOTS[slot - 1].masks[stack], task: SLOTS[slot - 1].task, round: SLOTS[slot - 1].round }).value;
    if (end === 'planned') continue;
    map = transition(map, rid, 'launching', {}).value;
    if (end === 'launching') continue;
    map = transition(map, rid, 'ran', { class: end.class, data: end.data }).value;
    if (end.scored) map = transition(map, rid, 'scored', {}).value;
  }
  return map;
}

/** Slots 1 to `last` finished: both stacks scored. */
export const finished = last => Array.from({ length: last }, (_, i) => i + 1).flatMap(slot => [[slot, 'csharp', 1, GOOD], [slot, 'node', 1, GOOD]]);

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };

/** What a session folder holds in the fixture: the three cache folders (the closed list a recovery may free), the workspace tree, the score folder and the records no recovery may touch. `cacheBytes` sizes each cache file. */
export function writeSession(seriesDir, rid, { cacheBytes = 1000 } = {}) {
  const dir = path.join(seriesDir, 's', rid);
  const blob = byte => Buffer.alloc(cacheBytes, byte);
  for (const [rel, byte] of [['box/nuget/pkg-a/a.nupkg', 1], ['box/nuget/pkg-a/b.dll', 2], ['box/npm/_cacache/c.tgz', 3], ['box/home/.dotnet/d.dat', 4]]) { mkdirSync(path.dirname(path.join(dir, rel)), { recursive: true }); writeFileSync(path.join(dir, rel), blob(byte)); }
  for (const [rel, text] of [['work/src/main.cs', 'class Program {}\n'], ['score/score.json', '{"ok":true}\n'], ['score/passes/p01.json', '{"verdict":"accepted"}\n'], ['record.json', '{"rid":"' + rid + '"}\n'], ['meta.json', '{}\n'], ['plan.json', '{}\n'], ['argv.json', '{}\n'], ['streams/out.log', 'line\n']]) put(path.join(dir, rel), text);
  return dir;
}

/** A series folder over `map`: its run-map, a ledger, the series' own root files and one session folder per session of the map. -> the folder. */
export function seriesFolder(t, map, { cacheBytes = 1000 } = {}) {
  const dir = owned.ownedDir(t, 'stageseries');
  writeRunMap(runMapFileOf(dir), map);
  put(path.join(dir, 'ledger.ndjson'), '');
  put(path.join(dir, 'private', 'note.txt'), 'private\n');
  put(path.join(dir, 'pairs', 'p1.json'), '{}\n');
  for (const rid of Object.keys(map.sessions)) writeSession(dir, rid, { cacheBytes });
  return dir;
}

/** A private backup folder (the NAS stands in). */
export const backupFolder = t => owned.ownedDir(t, 'stagebackup');

/** Make a tree read-only the way a sealed cache is (the caches of a box may be). */
export function sealReadOnly(dir) { chmodSync(dir, 0o555); }

/** The pre-registration's answer for the pilot (open point 2, answered): the A/A is task 05 on Node, the daemon pair is task 10, the web pair is task 04. */
export const PILOT = { aa: { task: '05', stack: 'node' }, daemon: '10', web: '04' };

/** Add the six sessions of a finished pilot (the A/A's two twins, the daemon pair and the web pair, each scored) to `map`; `skip` names kinds to leave out (`aa`, `pilot`). -> the new map. */
export function withPilot(map, { skip = [], tasks = PILOT } = {}) {
  const rows = [['aa', tasks.aa.task, tasks.aa.stack, 'a'], ['aa', tasks.aa.task, tasks.aa.stack, 'b'], ...['csharp', 'node'].flatMap(stack => [['pilot', tasks.daemon, stack], ['pilot', tasks.web, stack]])];
  return rows.filter(([kind]) => !skip.includes(kind)).reduce((m, [kind, task, stack, twin], i) => {
    const rid = `p${String(i).padStart(5, '0')}`;
    const planned = planSession(m, { rid, stack, phase: 'v1', attempt: 1, kind, task, twin, mask: '0-5', label: 'test' }).value;
    return transition(transition(transition(planned, rid, 'launching', {}).value, rid, 'ran', { class: 'completed', data: true }).value, rid, 'scored', {}).value;
  }, map);
}

// ---- helpers of the archive tests ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------


/** The sha256 of a file or text, written here on purpose: the tests do not use the module's own hash. */
export const sha256Text = text => createHash('sha256').update(text).digest('hex');

/** `{ 'rel/path': 'sha256' | 'link:<sha256 of the target>' }` of every file and link under `dir`, by the test's own walk (never following a link). */
export function snapshotOf(dir, { skip = [] } = {}) {
  const found = {};
  const walk = rel => {
    for (const name of readdirSync(path.join(dir, rel)).sort()) {
      const child = rel === '' ? name : `${rel}/${name}`;
      if (skip.includes(child)) continue;
      const st = lstatSync(path.join(dir, child));
      if (st.isDirectory()) walk(child);
      else found[child] = st.isSymbolicLink() ? `link:${sha256Text(readlinkSync(path.join(dir, child)))}` : sha256Text(readFileSync(path.join(dir, child)));
    }
  };
  walk('');
  return found;
}

/** The three cache folders of a session folder, as files: the paths a recovery may free (relative to the series). */
export const cacheFilesOf = rid => ['box/nuget/pkg-a/a.nupkg', 'box/nuget/pkg-a/b.dll', 'box/npm/_cacache/c.tgz', 'box/home/.dotnet/d.dat'].map(rel => `s/${rid}/${rel}`);
