// The public texts that exempt aid shingles (see aidNeedles): the parent plan and the stories document. They are read from the
// PUBLISHED ref of the bench repository (`git show origin/main:<path>`), never from the working tree: the branch under check can edit
// its working tree, and a leak pasted into a plan there would exempt itself. A text that cannot be read fails closed.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runGit } from './push-git.mjs';

export const PUBLIC_PLAN_PATHS = Object.freeze(['todo/PLAN_lang_comparison_v2.md', 'todo/PLAN_lang_comparison_v2_E1.md']);
export const PUBLIC_REF = 'origin/main';

/** ok([text]) of every path at `ref` in `repo`, or an `env` fail naming the first one that cannot be read. */
export async function readPublicPlans({ repo, ref = PUBLIC_REF, paths = PUBLIC_PLAN_PATHS }) {
  const texts = [];
  for (const file of paths) {
    const shown = await runGit(repo, ['show', `${ref}:${file}`], { maxBytes: 16 * 1024 * 1024 });
    if (!shown.ok) return fail(`the public text ${file} cannot be read at ${ref} of ${repo}: ${shown.reason}; the check does not run without it`, { kind: 'env' });
    texts.push(shown.value.stdout);
  }
  return ok(texts);
}
