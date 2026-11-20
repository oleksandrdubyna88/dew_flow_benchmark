// The seed of Stage 1 (F3.S1 (b), PR 5): v1's commit-reveal (`spikes/minigit-bench/lib/seed.mjs`) over Stage 1's files, with the check of the schedule. `seed-commit` draws a seed (`lv2-` and 16 hex), asks `seedProblem` of the schedule
// (`stage-schedule.mjs`: are the 120 possible rids distinct?) and draws another while it refuses, and only then writes the seed to `~/rsd/lang-v2/seeds/stage1.seed` (mode 600, outside git) and its sha256 to `inputs/prereg/seed.sha256`;
// `seed-reveal` copies it to `inputs/prereg/seed.txt` after the A/A. Until then only the hash is in the tree; the tag is made with the hash and without the seed.
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { commitSeed, revealSeed } from '../../minigit-bench/lib/seed.mjs';
import { REPO } from './paths.mjs';
import { PREREG_DIR, STAGE_PREREG_FILES } from './stage-lists.mjs';
import { seedProblem } from './stage-schedule.mjs';

/** The shape of a Stage 1 seed, and the words a sentence names it by. */
export const STAGE_SEED_SHAPE = Object.freeze({ regex: /^lv2-[0-9a-f]{16}$/, words: 'lv2- and 16 hex' });
/** Where the seed lives, outside every git tree: `~/rsd/lang-v2/seeds/stage1.seed`. */
export const stageSeedFile = home => path.join(home, 'rsd', 'lang-v2', 'seeds', 'stage1.seed');

/** The files the seed is committed after (it is the last step but the tag): every registered file but the seed's own two, as paths under `root`; a folder entry stands for its first matching file, or for a name that is not there. */
export function requiredFiles(root) {
  const own = new Set([`${PREREG_DIR}/seed.sha256`, `${PREREG_DIR}/seed.txt`]);
  return STAGE_PREREG_FILES.filter(entry => typeof entry !== 'string' || !own.has(entry)).map(entry => path.join(root, typeof entry === 'string' ? entry : folderFile(root, entry)));
}

/** A file of a folder entry that exists under `root`, else the folder's own description (a name that is not there, so the sentence names what is missing). */
function folderFile(root, entry) {
  const dir = path.join(root, entry.dir);
  const found = existsSync(dir) ? readdirSync(dir).sort().find(name => entry.pattern.test(name)) : undefined;
  return found === undefined ? `${entry.dir}/<${entry.what}>` : `${entry.dir}/${found}`;
}

/** The check of a drawn seed: the schedule's own (`seedProblem`), so no colliding seed is ever committed. */
export const stageSeedCheck = seedProblem;

/** -> `ok({ hash })` | `fail(reason)`. `root` is the repository, `home` the operator's home; `random` is the seam of a test (the draw's 16 hex). */
export function commitStageSeed({ root = REPO, home, random }) {
  return commitSeed({
    seedFile: stageSeedFile(home), inputsDir: path.join(root, PREREG_DIR), prefix: 'lv2-', shape: STAGE_SEED_SHAPE,
    files: requiredFiles(root), consistency: () => null, check: stageSeedCheck, ...(random ? { random } : {}),
  });
}

/** -> `ok({ seed })` | `fail(reason)`: `seed.txt` written next to the committed hash, after the A/A. */
export function revealStageSeed({ root = REPO, home }) {
  return revealSeed({ seedFile: stageSeedFile(home), inputsDir: path.join(root, PREREG_DIR), shape: STAGE_SEED_SHAPE });
}
