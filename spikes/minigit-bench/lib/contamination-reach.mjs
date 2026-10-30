// What `outside-path` and the link rules share (contamination.mjs, contamination-links.mjs): the tool sets, "is this path at or under a protected root", "does this call walk DOWN from the paths it names", and the
// paths a call reaches, each shell word resolved from the directory the `cd` segments before it left (contamination-cwd.mjs).
import { findOutsideAccesses } from '../../policy-ab-quick/lib/leak.mjs';
import { isRooted, resolvable, shellSteps, unquote } from './contamination-cwd.mjs';

export const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);
export const SEARCH_TOOLS = new Set(['Glob', 'Grep']);

export const under = (resolved, root) => resolved === root || resolved.startsWith(root.endsWith('/') ? root : `${root}/`);
export const holds = (resolved, root) => root.startsWith(resolved === '/' ? '/' : `${resolved}/`); // `resolved` is a directory ABOVE `root`

/** A shell command that walks DOWN from the paths it names: find, tree, du, tar, a recursive flag. A plain listing, a cat or a stat of a directory does not; a `cd` reads nothing (it moves the base of the words after it). */
export const WALKS = /(?:^|[\s;&|(])(?:find|tree|du|tar|rsync|zip|rg|fd|ag)(?=[\s;&|)]|$)|\s-[A-Za-z]*[rR][A-Za-z]*(?=\s|$)|--recursive/;

/** Whether one simple command walks. */
export const segmentWalks = segment => WALKS.test(` ${segment}`);

/** Whether the call reads everything BELOW the directories it names: a search tool always does, a shell command when it walks. */
export const walksDown = (text, call) => SEARCH_TOOLS.has(call.tool) || (SHELL_TOOLS.has(call.tool) && WALKS.test(text));

/** The protected roots a reach is judged against: a partner's dirs, the harness's, the operator's home. High when the path is at or under one, or (a walk) a directory that holds one: `find /`. */
export const isHigh = (resolved, roots, walks) => roots.some(root => under(resolved, root) || (walks && holds(resolved, root)));

const REDIRECT = /^\d*(?:<<-?|[<>]+)&?/;
const ASSIGNMENT = /^[A-Za-z_]\w*=/;
const isPathWord = w => w !== '' && !w.startsWith('-') && !isRooted(w);
const isPlaceable = w => resolvable(w) && !w.includes('://') && !ASSIGNMENT.test(w);

/** The relative path words of a simple command: its arguments less options, redirections and what is no path (a variable, a url, an assignment). The rooted words are the tokenizer's (leak.mjs). */
const relativeWords = step => step.words.slice(1).map(w => unquote(w.replace(REDIRECT, ''))).filter(w => isPathWord(w) && isPlaceable(w));

/** What a relative word of a walk reaches when its directory is unknown: anything. The root holds every protected root, so the hit is high, as `find /` is. */
const unplaced = (call, word) => ({ agent: call.agent, tool: call.tool, path: word, resolved: '/', excerpt: `directory unknown: ${word}` });

/** The relative word of a walking simple command whose directory is unknown (the conservative case), or undefined. */
const lostWord = (step, walks, words) => (step.cwd === null && walks && segmentWalks(step.segment) ? words[0] : undefined);

/** The reaches of one simple command, its relative words resolved from the directory it runs in (leak.mjs: `cwd`, `paths`). */
function stepReaches(step, call, walks, opts) {
  const words = relativeWords(step);
  const piece = { agent: call.agent, tool: call.tool, input: { command: step.segment }, cwd: step.cwd, paths: step.cwd === null ? [] : words };
  const found = findOutsideAccesses([piece], opts);
  const lost = lostWord(step, walks, words);
  return lost === undefined ? found : [...found, unplaced(call, lost)];
}

/**
 * Every path outside the work tree a call reaches, `{ agent, tool, path, resolved, excerpt? }`. A shell command is read one simple command at a time: a `cd` is no reach (it only moves the base), and the relative words
 * and the `..` chains after it are resolved from where it moved to. `walks` = the call walks down (`walksDown`).
 */
export function reachesOf(call, ctx, walks) {
  const opts = { treeRoot: ctx.treeRoot, home: ctx.boxHome ?? ctx.home, flavour: 'posix', allow: ctx.allow ?? [], isLink: ctx.isLink };
  if (!SHELL_TOOLS.has(call.tool)) return findOutsideAccesses([call], opts);
  return shellSteps(String(call.input?.command ?? ''), ctx).filter(step => !step.cd).flatMap(step => stepReaches(step, call, walks, opts));
}
