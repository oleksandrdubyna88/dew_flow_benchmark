// The public texts that exempt aid shingles (see aidNeedles): a six-word run that is already published cannot leak anything. The set is an
// EXPLICIT, reviewed one: the two v2 plan files, plus whatever the reviewed list file `inputs/public-texts.json` names. It is NOT every
// markdown file of the repository: a private paragraph whose windows are spread over several public files would then pass window by window.
//
// Everything is read from the PUBLISHED ref of the bench repository (`origin/main`), never from the working tree (the branch under check can
// edit its working tree, and a leak pasted into a text there would exempt itself), each stage by ONE `git cat-file --batch` process. A text that
// cannot be read, a list that is not valid, or a listed file that is missing fails closed (`env`). A list file absent at the ref means "no extras".
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBatchCatFile } from './push-git.mjs';

export const PUBLIC_PLAN_PATHS = Object.freeze(['todo/PLAN_lang_comparison_v2.md', 'todo/PLAN_lang_comparison_v2_E1.md']);
export const PUBLIC_LIST_PATH = 'spikes/lang-bench/inputs/public-texts.json';
export const PUBLIC_REF = 'origin/main';

const closed = (what, ref, repo, reason) => fail(`the public text ${what} cannot be read at ${ref} of ${repo}: ${reason}; the check does not run without it`, { kind: 'env' });
const badList = (list, reason) => fail(`the public-texts list ${list} is not valid: ${reason}; the check does not run without it`, { kind: 'env' });
const SAFE_PATH = /^(?!\/)(?!.*(?:^|\/)\.\.(?:\/|$))[^\0\n]+\.(?:md|txt)$/;

/** ok([path]) from the list file's text: schema 1, `texts` a list of distinct repository-relative `.md` or `.txt` paths that do not climb out. */
export function parsePublicList(text, list = PUBLIC_LIST_PATH) {
  let file;
  try { file = JSON.parse(text); } catch { return badList(list, 'not JSON'); }
  if (file?.schema !== 1 || !Array.isArray(file.texts)) return badList(list, 'it needs schema 1 and a `texts` list');
  const bad = file.texts.find(p => typeof p !== 'string' || !SAFE_PATH.test(p));
  if (bad !== undefined) return badList(list, `${JSON.stringify(String(bad).slice(0, 80))} is not a repository-relative .md or .txt path`);
  return new Set(file.texts).size === file.texts.length ? ok(file.texts) : badList(list, 'a path is listed twice');
}

/** ok([text]) for `files` at `ref`, in order, from one batch process; a missing or oversize file is a fail naming it. */
async function readAt(repo, ref, files) {
  const read = await runBatchCatFile(repo, files.map(f => `${ref}:${f}`));
  if (!read.ok) return closed('batch', ref, repo, read.reason);
  const absent = files.find(f => !read.value.has(`${ref}:${f}`));
  return absent === undefined ? ok(files.map(f => read.value.get(`${ref}:${f}`).toString('utf8'))) : closed(absent, ref, repo, 'it is not there');
}

/** ok(path[]) of the list at `ref`: empty when the list file is absent; a ref that does not resolve is a fail (nothing could be read there). */
async function listedPaths(repo, ref, listPath) {
  const [head, list] = [`${ref}^{commit}`, `${ref}:${listPath}`];
  const read = await runBatchCatFile(repo, [head, list]);
  if (!read.ok) return closed(listPath, ref, repo, read.reason);
  if (read.missing.includes(head)) return closed('ref', ref, repo, 'it does not resolve to a commit');
  return read.missing.includes(list) ? ok([]) : parsePublicList(read.value.get(list).toString('utf8'), listPath);
}

/** ok([text]): the required plans first, then the reviewed list's files; or an `env` fail. */
export async function readPublicPlans({ repo, ref = PUBLIC_REF, paths = PUBLIC_PLAN_PATHS, listPath = PUBLIC_LIST_PATH }) {
  const extra = await listedPaths(repo, ref, listPath);
  return extra.ok ? readAt(repo, ref, [...paths, ...extra.value.filter(f => !paths.includes(f))]) : extra;
}
