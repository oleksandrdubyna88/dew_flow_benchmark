// Fixtures of the quick-run leak tests: a held-out side in the shape of `quick-heldout-shape.md` (cases, traps, a generator, and `refs-NN` beside it) with INVENTED content, in a temp home. Not a test file.
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { leakMaterial } from '../lib/leak.mjs';
import { refsDirOf } from '../lib/heldout-quick.mjs';
import { reviewHash, reviewedPath } from '../lib/leak-reviewed.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';

export const EXPLORE = loadExplore().value;
export const quickTask = id => resolveTask(EXPLORE, id).value;

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };

/**
 * A quick task's private side in `home`: `cases` is `[{ id, input, output }]`, `traps` case ids, `refs` `{ 'relative/path': text | Buffer }` under `refs-NN`, `notes` the private test notes. -> the paths.
 */
export function writeQuickHeldout({ home, task, cases, traps = [], refs = {}, gen = '# invented generator\n', notes = 'invented notes about the order of invented events' }) {
  const paths = taskPaths(task, home);
  const casesDir = path.join(paths.heldoutDir, 'cases');
  for (const c of cases) { put(path.join(casesDir, `${c.id}.in`), c.input); put(path.join(casesDir, `${c.id}.out`), c.output); }
  put(path.join(casesDir, 'cases.txt'), `${cases.map(c => `${c.id} bytes R1`).join('\n')}\n`);
  if (traps.length > 0) put(path.join(paths.heldoutDir, 'traps.txt'), `${traps.join('\n')}\n`);
  put(path.join(paths.heldoutDir, 'gen.py'), gen);
  const refsDir = refsDirOf(paths.heldoutDir);
  for (const [rel, text] of Object.entries(refs)) put(path.join(refsDir, rel), text);
  put(paths.notesFile, notes);
  return { ...paths, casesDir, refsDir };
}

/** A temp home. */
export const quickHome = () => tmpDir('q-leak');

/** One entry of the reviewed list for `text`, hashed by the code's own function (the list holds the hash and the words of the reason, never the text). */
export const reviewedEntry = (kind, text, over = {}) => ({ sha256: reviewHash(kind, text), kind, reason: 'invented text, read by a person and found generic', reviewedAt: '2026-11-06T09:00:00Z', by: 'team lead', ...over });

/** The reviewed list of a temp home as the given text (or as `{ entries }` when given a list), at the place the check reads it. */
export function writeReviewed(home, listOrText) {
  put(reviewedPath(home), typeof listOrText === 'string' ? listOrText : JSON.stringify({ entries: listOrText }));
}

/** The pins restricted to the tasks in `ids`: a scan over every task of these pins reads exactly these folders (a task of the pins whose folder is absent is a refusal, so a test that holds fewer folders names fewer tasks). */
export const exploreOf = ids => ({ ...EXPLORE, tasks: Object.fromEntries(ids.map(id => [id, EXPLORE.tasks[id]])) });

/** A held-out record file (`heldout-frozen.json`) in a temp folder, registering a sealed archive for each task in `ids` (an invented hash: counts and hashes only, as the real record is; `entry` replaces fields of every task's entry). -> its path. */
export function writeFrozen(ids, over = {}, entry = {}) {
  const file = path.join(tmpDir('q-frozen'), 'heldout-frozen.json');
  const tasks = Object.fromEntries(ids.map(id => [id, { archiveSha256: 'a'.repeat(64), caseCount: 2, trapCount: 1, ...entry }]));
  put(file, JSON.stringify({ tasks, ...over }));
  return file;
}

/** The pins restricted to the tasks whose held-out folder is in `home`: what a scan over every task of the pins reads when the home holds only these (an absent task is a refusal since step 2b, never a skip). */
export const exploreFor = home => ({ ...EXPLORE, tasks: Object.fromEntries(Object.entries(EXPLORE.tasks).filter(([id]) => existsSync(taskPaths(quickTask(id), home).heldoutDir))) });

/**
 * The path of a held-out record that does not exist, in a new temp folder: "no task is sealed". A fixture home holds invented folders, so it must never be read against the
 * committed `inputs/heldout-frozen.json`, which lists the really sealed tasks (task 05 since 2026-11-12): over it, an invented folder of a sealed task reads as changed after
 * sealing, and an absent one as `sealed: open it first`.
 */
export const noRecord = () => path.join(tmpDir('q-frozen-none'), 'heldout-frozen.json');

/** `leakMaterial` over every task whose folder `home` holds (the options are `leakMaterial`'s, `home` required; give `only` and the pins are the full ones). The record is `noRecord()` unless `frozenFile` is given. */
export const presentMaterial = options => leakMaterial({ frozenFile: noRecord(), ...options, explore: options.only ? EXPLORE : exploreFor(options.home) });
