// The working directory of a shell command, simple command by simple command: the one ordered pass the contamination scan's path rules (`outside-path`, contamination-reach.mjs) and its link rules
// (contamination-links.mjs) both read. A `cd` reads nothing: it only moves the directory every LATER relative path word is resolved from. Lexical (nothing is followed on the host), and
// the same approximation as leak.mjs for what it cannot see: a command is split at `;`, `&`, `|` and a newline, quotes ignored. A heredoc body or here-string is DATA, not commands (contamination-heredoc.mjs): it
// is no step of its own but one `data` step (`{ segment: '', words: [], data }`), whose text only the link rule reads (a script's `symlink(` call).
//
// The directory is an absolute path, or `null` = UNKNOWN: after `cd -`, `popd`, `pushd` with no directory, `cd $OLDPWD`, a variable, a glob, a substitution or `~user`. A relative word cannot be placed then.
// A `( ... )` group keeps its `cd` to itself: the directory before the group comes back at its `)` (counted at the segment's edges; a `$( ... )` inside a segment is not a group).
import path from 'node:path';
import { PLACEHOLDER, stripData } from './contamination-heredoc.mjs';

const posix = path.posix;
const SEPARATORS = /([;&|\n]+)/; // captured: the separator before a segment says whether a pipe feeds it
const PIPE = /^\|&?\n*$/; // `|` and `|&`, not `||`, `&&`, `;` or a newline
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

/** One simple command: `{ segment, words, cwd, cd }` (`shellSteps` adds `piped`) - `cwd` the directory it runs in, `cd` `{ to }` when it moves the directory (`to` null when unknown), else undefined. */
function stepOf(state, segment, ctx) {
  const opened = openScopes(state, segment);
  const words = wordsOf(segment.replace(LEAD, '').replace(TRAIL, ''));
  const cd = CD_WORDS.has(words[0]) ? { to: cdTarget(words, opened.cwd, ctx) } : undefined;
  const moved = cd ? { ...opened, cwd: cd.to } : opened;
  return { state: closeScopes(moved, closesOf(segment)), step: { segment, words, cwd: opened.cwd, cd } };
}

/** A heredoc body is no command: a step with no words, in the directory it is written in. */
const dataStep = (state, text) => ({ state, step: { segment: '', words: [], cwd: state.cwd, cd: undefined, data: text } });

/** `[{ segment, piped }]`: the simple commands of a command line, `piped` when a single pipe (`|`, `|&`) stands before it, so the command before it feeds it. */
function pieces(commands) {
  const parts = commands.split(SEPARATORS);
  return parts.filter((_, i) => i % 2 === 0).map((segment, k) => ({ segment, piped: k > 0 && PIPE.test(parts[2 * k - 1]) }));
}

/**
 * Every simple command of `text`, in order, each with the directory the commands before it left: the tree root to begin with (`ctx.treeRoot`). The heredoc bodies and here-string operands are not commands
 * (contamination-heredoc.mjs): a body is one `data` step.
 */
export function shellSteps(text, ctx) {
  const { text: commands, data } = stripData(text);
  const steps = []; // built here and appended to in place: a copy per segment would make a command of 30,000 segments quadratic; the result is a new array, nothing outside is touched
  let state = { cwd: ctx.treeRoot, saved: [] };
  for (const { segment, piped } of pieces(commands)) {
    const body = PLACEHOLDER.exec(segment.trim());
    const next = body ? dataStep(state, data[Number(body[1])]) : stepOf(state, segment, ctx);
    state = next.state;
    steps.push({ ...next.step, piped });
  }
  return steps;
}
