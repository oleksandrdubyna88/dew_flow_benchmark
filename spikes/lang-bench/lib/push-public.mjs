// The public texts that exempt aid shingles (see aidNeedles): a six-word run that is already published cannot leak anything. They are
// read from the PUBLISHED ref of the bench repository (`git show origin/main:<path>`), never from the working tree: the branch under
// check can edit its working tree, and a leak pasted into a plan there would exempt itself. The two plans are required; every other
// `.md` file at that ref joins them (the plans' paths and titles are quoted all over the published notes, and a window that a published
// note already holds is public whichever note it is). A text that cannot be read, or a listing that fails, fails closed.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { BLOB_MAX_BYTES, runGit } from './push-git.mjs';

export const PUBLIC_PLAN_PATHS = Object.freeze(['todo/PLAN_lang_comparison_v2.md', 'todo/PLAN_lang_comparison_v2_E1.md']);
export const PUBLIC_REF = 'origin/main';
const READ_AT_A_TIME = 8;

const unreadable = (what, ref, repo, reason) => fail(`the public text ${what} cannot be read at ${ref} of ${repo}: ${reason}; the check does not run without it`, { kind: 'env' });

async function showAll(repo, ref, files) {
  const texts = [];
  for (let i = 0; i < files.length; i += READ_AT_A_TIME) {
    const batch = await Promise.all(files.slice(i, i + READ_AT_A_TIME).map(file => runGit(repo, ['show', `${ref}:${file}`], { maxBytes: BLOB_MAX_BYTES })));
    const bad = batch.findIndex(r => !r.ok);
    if (bad !== -1) return unreadable(files[i + bad], ref, repo, batch[bad].reason);
    texts.push(...batch.map(r => r.value.stdout));
  }
  return ok(texts);
}

/** ok([path]) of every `.md` file at `ref`. */
async function markdownAt(repo, ref) {
  const listing = await runGit(repo, ['ls-tree', '-r', '--name-only', '-z', ref], { maxBytes: 64 * 1024 * 1024 });
  return listing.ok ? ok(listing.value.stdout.split('\0').filter(f => f.endsWith('.md'))) : unreadable('listing', ref, repo, listing.reason);
}

/** ok([text]): the required `paths` first, then every other `.md` file at `ref`; or an `env` fail. */
export async function readPublicPlans({ repo, ref = PUBLIC_REF, paths = PUBLIC_PLAN_PATHS }) {
  const listed = await markdownAt(repo, ref);
  if (!listed.ok) return listed;
  return showAll(repo, ref, [...paths, ...listed.value.filter(f => !paths.includes(f))]);
}
