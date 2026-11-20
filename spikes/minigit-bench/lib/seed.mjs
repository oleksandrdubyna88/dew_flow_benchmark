// The bare seed, as commit-reveal (E3b.S3, S4): `commitSeed` draws `mg-` + 16 random hex into a mode-600 file OUTSIDE git and writes only its sha256 to
// `inputs/seed.sha256` (which the tag freezes); `revealSeed` copies the seed to `inputs/seed.txt` after the A/A, and refuses one that does not hash to the committed value.
// The seed fixes the data rids, pair 1's masks and the launch and scoring order, so it must not be known before the pre-registration is public.
// The prefix and shape of the seed, the files that must be in place first, the consistency of those files and a CHECK of each drawn seed are arguments (language comparison v2's Stage 1 passes its own);
// the defaults are v1's, and a drawn seed the check refuses is dropped and another is drawn before any file is written.
// Every state a stopped run can leave is named and safe (coai plan round, PR 5, finding S): the seed file is written first and `seed.sha256` after it, whole, by a rename of a temp file, so a hash file is whole or absent;
//   a seed file with no hash file is a seed that was NEVER committed: a retry REFUSES and names the way out (the operator removes the private file by hand, then runs seed-commit again). The tool never moves or deletes a private
//   seed file itself, and never reuses one silently: it cannot know why the hash is gone (a checkout that dropped it would make that file the committed seed), so only a person chooses between removing it and restoring the hash;
//   a hash file with no seed file is a LOST seed: the commit and the reveal refuse loudly and touch nothing. `seed.txt` is written the same way and is safe to run again: the same content says "already revealed", other content is refused.
import { createHash, randomBytes } from 'node:crypto';
import { chmodSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { sdkCheckOfTexts } from './prereg-consistency.mjs';
import { fail, ok } from './result.mjs';

export const SEED_SHAPE = /^mg-[0-9a-f]{16}$/;
/** The seed's shape and the words a sentence names it by: v1's. */
const V1_SHAPE = Object.freeze({ regex: SEED_SHAPE, words: 'mg- and 16 hex' });
const DRAWS = 64;
const hashOf = seed => createHash('sha256').update(seed).digest('hex');

/** v1's consistency of the registered files: the SDK a C# prompt names against the pin. `files[0]` is the prompts, `files[1]` the pins. -> a sentence or null. */
function sdkConsistency(files) {
  const consistent = sdkCheckOfTexts({ promptsText: readFileSync(files[0], 'utf8'), stacksText: readFileSync(files[1], 'utf8') });
  return consistent.ok ? null : `${consistent.reason} (seed-commit is the last step before the tag)`;
}

/** The sentence for the registered files that are not all in place or that contradict each other, or null: the seed is committed last. */
function filesProblem(files, consistency) {
  const absent = files.find(f => !existsSync(f));
  return absent ? `${absent} does not exist: the seed is committed last, with every other registered file in place` : consistency(files);
}

/** Writes `text` to `file` whole: a temp file of the same folder (exclusive), then a rename over the target, so the target is whole or not there. -> `ok(file)` | `fail`; the temp file is removed when either step fails. */
function writeWhole({ file, text, rename }) {
  const temp = `${file}.writing-${process.pid}`;
  try {
    writeFileSync(temp, text, { flag: 'wx' });
    rename(temp, file);
    return ok(file);
  } catch (e) {
    rmSync(temp, { force: true });
    return fail(`cannot write ${file}: ${e.code ?? e.message}`);
  }
}

/** The sentence for a state a stopped run left (one of the two files without the other), or null. */
function partialProblem({ seedFile, hashFile }) {
  const [hasSeed, hasHash] = [existsSync(seedFile), existsSync(hashFile)];
  if (hasSeed === hasHash) return null;
  if (hasSeed) return `${seedFile} exists but ${hashFile} does not: that seed was never committed and is not reused; the private seed file at ${seedFile} can be removed by the operator, then run seed-commit again`;
  return `${hashFile} exists but ${seedFile} does not: the committed seed is LOST (the hash is frozen by the pre-registration tag, so it is not replaced): nothing was written`;
}

/** The sentence for a commit that must not happen, or null. */
function commitProblem({ seedFile, hashFile, files, consistency }) {
  const partial = partialProblem({ seedFile, hashFile });
  if (partial) return partial;
  if (existsSync(seedFile)) return `${seedFile} already exists: a seed is committed once`;
  return filesProblem(files, consistency);
}

/** The first drawn seed that is of the shape and that `check` accepts, at most `maxDraws` of them: `ok(seed)` or `fail` naming the last refusal. Nothing is written here. */
function drawnSeed(args, draw = 1) {
  const { prefix, random, shape, check, maxDraws } = args;
  const seed = `${prefix}${random()}`;
  const problem = shape.regex.test(seed) ? check(seed) : `the seed ${seed} is not ${shape.words}`;
  if (problem === null) return ok(seed);
  return draw >= maxDraws ? fail(`no seed passed the check in ${maxDraws} draws: ${problem}`) : drawnSeed(args, draw + 1);
}

/**
 * -> `ok({ hash })` after writing the seed file (exclusive, mode 600) and `<inputsDir>/seed.sha256`; refuses to overwrite either. `prefix`, `shape` (`{ regex, words }`), `files`, `consistency` and `check` are v1's unless
 * the caller passes its own; a drawn seed that is not of the shape or that `check` refuses is dropped and another is drawn (at most `maxDraws`), before anything is written.
 */
export function commitSeed({ seedFile, inputsDir, random = () => randomBytes(8).toString('hex'), prefix = 'mg-', shape = V1_SHAPE, files = ['prompts-bare.json', 'stacks.json'].map(f => path.join(inputsDir, f)), consistency = sdkConsistency, check = () => null, maxDraws = DRAWS, rename = renameSync }) {
  const hashFile = path.join(inputsDir, 'seed.sha256');
  const problem = commitProblem({ seedFile, hashFile, files, consistency });
  if (problem) return fail(problem);
  const drawn = drawnSeed({ prefix, random, shape, check, maxDraws });
  if (!drawn.ok) return drawn;
  const seed = drawn.value;
  mkdirSync(path.dirname(seedFile), { recursive: true, mode: 0o700 });
  writeFileSync(seedFile, `${seed}\n`, { mode: 0o600, flag: 'wx' });
  chmodSync(seedFile, 0o600);
  const written = writeWhole({ file: hashFile, text: `${hashOf(seed)}\n`, rename });
  return written.ok ? ok({ hash: hashOf(seed) }) : written;
}

/** The sentence for a reveal that must not happen (a file missing, or a seed that was never committed or is lost), or null. */
function revealProblem({ seedFile, hashFile }) {
  const partial = partialProblem({ seedFile, hashFile });
  if (partial) return partial;
  return existsSync(seedFile) ? null : `${seedFile} does not exist: nothing to reveal`;
}

/** The sentence for a seed that is not the committed one, or null. */
function seedProblem({ seed, seedFile, hashFile, shape }) {
  if (!shape.regex.test(seed)) return `the seed in ${seedFile} is not ${shape.words}`;
  return hashOf(seed) === readFileSync(hashFile, 'utf8').trim() ? null : 'the seed does not hash to the committed seed.sha256: it is not the seed that was committed';
}

/** `seed.txt` that is already there: `already revealed` (a refusal value that names itself, nothing written) for the committed seed, a refusal for any other content. */
function revealedAlready({ out, seed }) {
  if (readFileSync(out, 'utf8') === `${seed}\n`) return fail(`already revealed: ${out} holds the committed seed; nothing was written`, { alreadyRevealed: true });
  return fail(`${out} exists and differs from the committed seed: refused, nothing was written`);
}

/** The seed file's seed once the files, its shape and its hash are checked: `ok(seed)` | `fail(sentence)`. */
function checkedSeed({ seedFile, hashFile, shape }) {
  const missing = revealProblem({ seedFile, hashFile });
  if (missing) return fail(missing);
  const seed = readFileSync(seedFile, 'utf8').trim();
  const wrong = seedProblem({ seed, seedFile, hashFile, shape });
  return wrong ? fail(wrong) : ok(seed);
}

export function revealSeed({ seedFile, inputsDir, shape = V1_SHAPE, rename = renameSync }) {
  const hashFile = path.join(inputsDir, 'seed.sha256');
  const out = path.join(inputsDir, 'seed.txt');
  const checked = checkedSeed({ seedFile, hashFile, shape });
  if (!checked.ok) return checked;
  if (existsSync(out)) return revealedAlready({ out, seed: checked.value });
  const written = writeWhole({ file: out, text: `${checked.value}\n`, rename });
  return written.ok ? ok({ seed: checked.value }) : written;
}
