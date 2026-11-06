// Fixtures of the quick-run leak tests: a held-out side in the shape of `quick-heldout-shape.md` (cases, traps, a generator, and `refs-NN` beside it) with INVENTED content, in a temp home. Not a test file.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
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
