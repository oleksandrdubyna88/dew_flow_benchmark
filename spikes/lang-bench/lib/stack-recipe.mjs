// F1.S3 (b), review round: the recipe hash of ONE stack. A record's `recipeSha256` hashes the whole recipe file, so editing the other stack, or the `kinds` of a package, moves it and no check can hold it equal after a one-stack rebuild;
// it stays in the record as history. `stackRecipeSha256` hashes only what decides how that stack's snapshot is built: `env`, `resolve`, `steps`, `capture` and the project tree (`projectSha256`, the C# seed). `stackRecipeProblems` holds the
// registered value equal to today's for every registered stack. A record made before the field is checked through the recipe text its whole-file hash names (the working file, or an earlier version in git); where no such text is found the
// stack is `unchecked` and says NOT CHECKED, once per stack: a failure that can never be cured (the registered snapshots are not rebuilt for it) would be ignored, and a silent pass would be a lie.
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const RECIPE_REL = 'spikes/lang-bench/inputs/cache-recipe-lang.json';
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const sortKeys = x => (Array.isArray(x) ? x.map(sortKeys) : x !== null && typeof x === 'object' ? Object.fromEntries(Object.keys(x).sort().map(k => [k, sortKeys(x[k])])) : x);

/** The hash of what builds one stack's snapshot: its `env`, `resolve`, `steps` and `capture` (keys in any order) and the tree hash of its project seed (`null` for a stack with none). */
export function stackRecipeSha256({ spec, projectSha256 = null }) {
  const { env = null, resolve = null, steps = null, capture = null } = spec;
  return sha256(JSON.stringify(sortKeys({ env, resolve, steps, capture, projectSha256 })));
}

const git = args => spawnSync('git', ['-C', REPO, ...args], { timeout: 30_000, maxBuffer: 64 * 1024 * 1024, env: { PATH: process.env.PATH ?? '/usr/bin:/bin', LANG: 'C.UTF-8', GIT_TERMINAL_PROMPT: '0' } });

/** The recipe text whose sha256 is `hash`: the working file, else an earlier version of it in git. `null` when none is found. */
export function findRecipeByHash(hash) {
  const now = readFileSync(path.join(REPO, RECIPE_REL));
  if (sha256(now) === hash) return now;
  const log = git(['log', '--format=%H', '--', RECIPE_REL]);
  if (log.status !== 0) return null;
  for (const commit of log.stdout.toString('utf8').split('\n').filter(Boolean)) {
    const shown = git(['show', `${commit}:${RECIPE_REL}`]);
    if (shown.status === 0 && sha256(shown.stdout) === hash) return shown.stdout;
  }
  return null;
}

/** The registered value of a record made before the field, worked out from the recipe text its whole-file hash names: `{ hash, how }` | `{ unchecked }`. */
function derivedRegistered({ stack, rec, findRecipe }) {
  const text = typeof rec.recipeSha256 === 'string' ? findRecipe(rec.recipeSha256) : null;
  const spec = text === null ? undefined : JSON.parse(text.toString('utf8')).stacks?.[stack];
  if (spec === undefined) return { unchecked: `NOT CHECKED: the ${stack} record has no stackRecipeSha256 (it was registered before the field existed) and no recipe text with its whole-file hash ${rec.recipeSha256} was found in the working tree or in git history` };
  return { hash: stackRecipeSha256({ spec, projectSha256: rec.projectSha256 ?? null }), how: 'from the recipe text of the recorded whole-file hash' };
}

/**
 * Every registered stack against today's recipe. `record` is the whole record, `recipe` the parsed recipe, `treeHashOf()` the tree hash of the C# project seed today, `findRecipe(hash)` a seam for finding an earlier recipe text.
 * -> `{ problems: [text], unchecked: [{ stack, why }], verified: [{ stack, how }] }`.
 */
export function stackRecipeProblems({ record, recipe, treeHashOf, findRecipe = findRecipeByHash }) {
  const out = { problems: [], unchecked: [], verified: [] };
  for (const [stack, spec] of Object.entries(recipe.stacks)) {
    const rec = record[stack];
    if (rec === undefined) continue;
    const today = stackRecipeSha256({ spec, projectSha256: spec.project === undefined ? null : treeHashOf(spec.project) });
    const registered = typeof rec.stackRecipeSha256 === 'string' ? { hash: rec.stackRecipeSha256, how: 'recorded' } : derivedRegistered({ stack, rec, findRecipe });
    if (registered.unchecked !== undefined) out.unchecked.push({ stack, why: registered.unchecked });
    else if (registered.hash === today) out.verified.push({ stack, how: registered.how });
    else out.problems.push(`the ${stack} snapshot was registered under a recipe hashing to ${registered.hash} (${registered.how}), and today's recipe for it hashes to ${today}: the recipe no longer builds what was registered`);
  }
  return out;
}
