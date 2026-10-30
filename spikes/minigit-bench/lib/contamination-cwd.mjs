// The working directory of a shell command, simple command by simple command: the one ordered pass the contamination scan's path rules (`outside-path`, contamination-reach.mjs) and its link rules
// (contamination-links.mjs) both read. A `cd` reads nothing: it only moves the directory every LATER relative path word is resolved from. Lexical (nothing is followed on the host), and
// the same approximation as leak.mjs for what it cannot see: a command is split at `;`, `&`, `|` and a newline, quotes ignored (a heredoc body is a run of segments, a `cd` in it still counts).
//
// The directory is an absolute path, or `null` = UNKNOWN: after `cd -`, `popd`, `pushd` with no directory, `cd $OLDPWD`, a variable, a glob, a substitution or `~user`. A relative word cannot be placed then.
// A `( ... )` group keeps its `cd` to itself: the directory before the group comes back at its `)` (counted at the segment's edges; a `$( ... )` inside a segment is not a group).
import path from 'node:path';

const posix = path.posix;
const SEGMENT = /[;&|\n]+/;
const OPAQUE = /[$*?[\]{}`()]/;
const HOME_FORM = /^(\$\{?HOME\}?|~)(?=\/|$)/;
const LEAD = /^[\s({]*/;
const TRAIL = /[)}\s]+$/;
const CD_WORDS = new Set(['cd', 'pushd', 'popd']);
const CD_FLAG = /^(?:-[LPe@]+|--)$/;

export const unquote = w => w.replace(/^(["'`])(.*)\1$/, '$2');
export const wordsOf = segment => (segment.match(/"[^"]*"|'[^']*'|\S+/g) ?? []).map(unquote);
export const homeOf = ctx => ctx.boxHome ?? ctx.home ?? '/';

/** Whether a word names its own base: an absolute path or a home form (`~`, `$HOME`, `${HOME}`). */
export const isRooted = w => w.startsWith('/') || HOME_FORM.test(w);

/** Whether the scan can place a word: no variable (`$HOME` apart), glob, substitution or `~user`. */
export const resolvable = w => w !== undefined && !OPAQUE.test(w.replace(HOME_FORM, '')) && !/^~[^/]/.test(w);

/** The absolute path of a word, from the directory `cwd`; null when the word is relative and the directory is unknown. */
export function absOf(word, cwd, ctx) {
  if (isRooted(word)) return posix.resolve(word.replace(HOME_FORM, homeOf(ctx)));
  return cwd === null ? null : posix.resolve(cwd, word);
}

const unplaceable = dir => dir === '-' || /^\+\d+$/.test(dir) || !resolvable(dir);
const noDirectory = (command, ctx) => (command === 'cd' ? homeOf(ctx) : null); // `cd` goes HOME; `pushd` with none swaps and `popd` pops: unknown

/** Where a `cd`, `pushd` or `popd` ends up, from `cwd`: an absolute path, or null. */
function cdTarget(words, cwd, ctx) {
  if (words[0] === 'popd') return null;
  const dir = words.slice(1).find(w => !CD_FLAG.test(w));
  if (dir === undefined) return noDirectory(words[0], ctx);
  return unplaceable(dir) ? null : absOf(dir, cwd, ctx);
}

const opensOf = segment => (segment.match(LEAD)[0].match(/\(/g) ?? []).length;
const closesOf = segment => (segment.replace(/\$\([^()]*\)/g, '').match(/\)+\s*$/)?.[0].trim().length ?? 0);

const openScopes = (state, segment) => ({ cwd: state.cwd, saved: [...state.saved, ...Array(opensOf(segment)).fill(state.cwd)] });

function closeScopes(state, closes) {
  if (closes === 0 || state.saved.length === 0) return state;
  const keep = Math.max(0, state.saved.length - closes);
  return { cwd: state.saved[keep], saved: state.saved.slice(0, keep) };
}

/** One simple command: `{ segment, words, cwd, cd }` - `cwd` the directory it runs in, `cd` `{ to }` when it moves the directory (`to` null when unknown), else undefined. */
function stepOf(state, segment, ctx) {
  const opened = openScopes(state, segment);
  const words = wordsOf(segment.replace(LEAD, '').replace(TRAIL, ''));
  const cd = CD_WORDS.has(words[0]) ? { to: cdTarget(words, opened.cwd, ctx) } : undefined;
  const moved = cd ? { ...opened, cwd: cd.to } : opened;
  return { state: closeScopes(moved, closesOf(segment)), step: { segment, words, cwd: opened.cwd, cd } };
}

/** Every simple command of `text`, in order, each with the directory the commands before it left: the tree root to begin with (`ctx.treeRoot`). */
export function shellSteps(text, ctx) {
  const done = text.split(SEGMENT).reduce((acc, segment) => {
    const next = stepOf(acc.state, segment, ctx);
    return { state: next.state, steps: [...acc.steps, next.step] };
  }, { state: { cwd: ctx.treeRoot, saved: [] }, steps: [] });
  return done.steps;
}
