// The bare seed, as commit-reveal (E3b.S3, S4): `commitSeed` draws `mg-` + 16 random hex into a mode-600 file OUTSIDE git and writes only its sha256 to
// `inputs/seed.sha256` (which the tag freezes); `revealSeed` copies the seed to `inputs/seed.txt` after the A/A, and refuses one that does not hash to the committed value.
// The seed fixes the data rids, pair 1's masks and the launch and scoring order, so it must not be known before the pre-registration is public.
import { createHash, randomBytes } from 'node:crypto';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { sdkCheckOfTexts } from './prereg-consistency.mjs';
import { fail, ok } from './result.mjs';

export const SEED_SHAPE = /^mg-[0-9a-f]{16}$/;
const hashOf = seed => createHash('sha256').update(seed).digest('hex');

/** -> `ok({ hash })` after writing the seed file (exclusive, mode 600) and `<inputsDir>/seed.sha256`; refuses to overwrite either. */
export function commitSeed({ seedFile, inputsDir, random = () => randomBytes(8).toString('hex') }) {
  const hashFile = path.join(inputsDir, 'seed.sha256');
  if (existsSync(seedFile)) return fail(`${seedFile} already exists: a seed is committed once`);
  if (existsSync(hashFile)) return fail(`${hashFile} already exists: the committed hash is frozen by the pre-registration tag`);
  const files = ['prompts-bare.json', 'stacks.json'].map(f => path.join(inputsDir, f));
  const absent = files.find(f => !existsSync(f));
  if (absent) return fail(`${absent} does not exist: the seed is committed last, with every other registered file in place`);
  const consistent = sdkCheckOfTexts({ promptsText: readFileSync(files[0], 'utf8'), stacksText: readFileSync(files[1], 'utf8') });
  if (!consistent.ok) return fail(`${consistent.reason} (seed-commit is the last step before the tag)`);
  const seed = `mg-${random()}`;
  mkdirSync(path.dirname(seedFile), { recursive: true, mode: 0o700 });
  writeFileSync(seedFile, `${seed}\n`, { mode: 0o600, flag: 'wx' });
  chmodSync(seedFile, 0o600);
  writeFileSync(hashFile, `${hashOf(seed)}\n`);
  return ok({ hash: hashOf(seed) });
}

/** -> `ok({ seed })` after writing `<inputsDir>/seed.txt`; refuses a seed that does not hash to `seed.sha256` or an existing `seed.txt`. */
export function revealSeed({ seedFile, inputsDir }) {
  const hashFile = path.join(inputsDir, 'seed.sha256');
  const out = path.join(inputsDir, 'seed.txt');
  if (!existsSync(seedFile)) return fail(`${seedFile} does not exist: nothing to reveal`);
  if (!existsSync(hashFile)) return fail(`${hashFile} does not exist: the seed was never committed`);
  if (existsSync(out)) return fail(`${out} already exists`);
  const seed = readFileSync(seedFile, 'utf8').trim();
  if (!SEED_SHAPE.test(seed)) return fail(`the seed in ${seedFile} is not mg- and 16 hex`);
  if (hashOf(seed) !== readFileSync(hashFile, 'utf8').trim()) return fail('the seed does not hash to the committed seed.sha256: it is not the seed that was committed');
  writeFileSync(out, `${seed}\n`);
  return ok({ seed });
}
