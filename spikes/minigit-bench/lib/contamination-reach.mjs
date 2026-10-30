// What `outside-path` and the link rules share (contamination.mjs, contamination-links.mjs): the tool sets, "is this path at or under a protected root", "does this simple command walk DOWN from the paths it names", and the
// paths a call reaches, each shell word resolved from the directory the `cd` segments before it left (contamination-cwd.mjs). A heredoc body is data, never a command (contamination-heredoc.mjs).
import { findOutsideAccesses } from '../../policy-ab-quick/lib/leak.mjs';
import { isRooted, resolvable, shellSteps, unquote } from './contamination-cwd.mjs';
import { fedWalk } from './contamination-pipe.mjs';

export const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);
export const SEARCH_TOOLS = new Set(['Glob', 'Grep']);

export const under = (resolved, root) => resolved === root || resolved.startsWith(root.endsWith('/') ? root : `${root}/`);
export const holds = (resolved, root) => root.startsWith(resolved === '/' ? '/' : `${resolved}/`); // `resolved` is a directory ABOVE `root`

/** A shell command that walks DOWN from the paths it names: find, tree, du, tar, a recursive flag. A plain listing, a cat or a stat of a directory does not; a `cd` reads nothing (it moves the base of the words after it). */
export const WALKS = /(?:^|[\s;&|(])(?:find|tree|du|tar|rsync|zip|rg|fd|ag)(?=[\s;&|)]|$)|\s-[A-Za-z]*[rR][A-Za-z]*(?=\s|$)|--recursive/;

const ASSIGNMENT = /^[A-Za-z_]\w*=/;
const commandWordOf = segment => segment.replace(/^[\s({]+/, '').split(/\s+/).find(w => !ASSIGNMENT.test(w)) ?? '';

/** Whether ONE simple command walks - never the whole call: a `-r` in one segment makes no other segment's words walks. `rm -rf` deletes and reads nothing: its path words are judged as any reach. */
export const segmentWalks = segment => commandWordOf(segment) !== 'rm' && WALKS.test(` ${segment}`);

/** The protected roots a reach is judged against: a partner's dirs, the harness's, the operator's home. High when the path is at or under one, or (a walk) a directory that holds one: `find /`. */
export const isHigh = (resolved, roots, walks) => roots.some(root => under(resolved, root) || (walks && holds(resolved, root)));

const REDIRECT = /^\d*(?:<<-?|[<>]+)&?/;
const isPathWord = w => w !== '' && !w.startsWith('-') && !isRooted(w);
const isPlaceable = w => resolvable(w) && !w.includes('://') && !ASSIGNMENT.test(w);

const GLOB = /[*?[]/;
const isGlobWord = w => GLOB.test(w) && !/[$`(){}]/.test(w);

/**
 * The relative path words of a simple command: its arguments less options, redirections and what is no path (a variable, a url, an assignment). The rooted words are the tokenizer's (leak.mjs). A glob word (`ho*`) is
 * a path word only for a walk: it is judged as the walk of its fixed prefix (`fixedPrefix`); a plain listing of it was never judged.
 */
const relativeWords = (step, walks) => step.words.slice(1).map(w => unquote(w.replace(REDIRECT, ''))).filter(w => isPathWord(w) && (isPlaceable(w) || (walks && isGlobWord(w))));

/** The fixed prefix of a glob path, the directory a walk of it starts from: the glob `/*` gives `/`, `/home/*` gives `/home`, and `/home/j*` plus a tail gives `/home`; a path with no glob is itself. */
function fixedPrefix(p) {
  const at = p.search(GLOB);
  if (at < 0) return p;
  const cut = p.lastIndexOf('/', at);
  return cut <= 0 ? '/' : p.slice(0, cut);
}

// `//`, `///`, `/*`, `/**`: a comment mark. In a quoted string (`grep -rn "a // b" src`) it is text, never the path `/`; in a word position (`find // -name x`) it is a path.
const QUOTED = /"[^"]*"|'[^']*'/g;
const COMMENT_MARK = /^\/(?:\/+|\*[^/]*)$/;
const isQuotedMark = (segment, f) => COMMENT_MARK.test(f.path) && !segment.replace(QUOTED, ' ').includes(f.path);

/** What a relative word of a walk reaches when its directory is unknown: anything. The root holds every protected root, so the hit is high, as `find /` is. */
const unplaced = (call, word) => ({ agent: call.agent, tool: call.tool, path: word, resolved: '/', walks: true, excerpt: `directory unknown: ${word}` });

/** The relative word of a walking simple command whose directory is unknown (the conservative case), or undefined. */
const lostWord = (step, walks, words) => (step.cwd === null && walks ? words[0] : undefined);

/** What a pipe into a wrapped walker reaches when its producer cannot be resolved (it reads a file or a variable): anything, as a walk of `/` does. */
const unknownProducer = call => ({ agent: call.agent, tool: call.tool, path: '|', resolved: '/', walks: true, excerpt: 'a pipe feeds a walker from an unknown producer' });

const pieceOf = (step, call, words) => ({ agent: call.agent, tool: call.tool, input: { command: step.segment }, cwd: step.cwd, paths: step.cwd === null ? [] : words });

/** The reaches of one simple command, its relative words resolved from the directory it runs in (leak.mjs: `cwd`, `paths`); each says whether THIS command walks (`forceWalk`: a walker it feeds through a pipe does). */
function stepReaches(step, call, opts, forceWalk = false) {
  const walks = forceWalk || segmentWalks(step.segment);
  const words = relativeWords(step, walks);
  const found = findOutsideAccesses([pieceOf(step, call, words)], opts).filter(f => !isQuotedMark(step.segment, f)).map(f => ({ ...f, walks, resolved: walks ? fixedPrefix(f.resolved) : f.resolved }));
  const lost = lostWord(step, walks, words);
  return lost === undefined ? found : [...found, unplaced(call, lost)];
}

const ownReaches = (step, call, opts) => (step.cd || step.data !== undefined ? [] : stepReaches(step, call, opts));

/** A wrapped walker that a pipe feeds (`echo / | xargs find`): its producers' paths are judged as a walk, and an unknown producer is high. */
function pipedReaches(steps, i, call, opts) {
  const fed = fedWalk(steps, i);
  if (fed === null) return [];
  return [...fed.listers.flatMap(producer => stepReaches(producer, call, opts, true)), ...(fed.unresolvable ? [unknownProducer(call)] : [])];
}

/**
 * Every path outside the work tree a call reaches, `{ agent, tool, path, resolved, walks, excerpt? }`; `walks` = the command that names it reads below it. A shell command is read one simple command at a time, its heredoc
 * bodies left out: a `cd` is no reach (it only moves the base), and the relative words and the `..` chains after it are resolved from where it moved to. A search tool walks below its path.
 */
export function reachesOf(call, ctx) {
  const opts = { treeRoot: ctx.treeRoot, home: ctx.boxHome ?? ctx.home, flavour: 'posix', allow: ctx.allow ?? [], isLink: ctx.isLink };
  if (!SHELL_TOOLS.has(call.tool)) return findOutsideAccesses([call], opts).map(f => ({ ...f, walks: SEARCH_TOOLS.has(call.tool) }));
  const steps = shellSteps(String(call.input?.command ?? ''), ctx);
  return steps.flatMap((step, i) => [...ownReaches(step, call, opts), ...pipedReaches(steps, i, call, opts)]);
}
