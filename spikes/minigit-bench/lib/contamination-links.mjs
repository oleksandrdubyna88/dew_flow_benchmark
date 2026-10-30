// The two link rules of the contamination scan. The box binds the harness and the home's parents read-only at their own paths, and a symbolic link in the agent's work tree can point at them: `ln -s ../.. up && grep -r token up`
// reads the harness and the home, while the path scan sees only the word `up`. Two layers, both lexical (the target is resolved from the link's own directory, never followed on the host):
//   link-out  - a call that CREATES a link (`ln -s`, `cp -s`, a Node or Python `symlink`) whose target is outside the work tree and outside what the box binds on purpose: high, whatever happens next;
//   link-walk - a walk (grep -r, find, tar, du, cp -r, rsync, tree, `cd`, the Grep and Glob tools) whose path, or any component of it, is a link that leaves them: a link this session's calls created (also one it deleted
//               afterwards), or one the work tree holds at collection (`ctx.readLink`, an `lstat` + `readlink` on the host).
// A word the scan cannot resolve (a variable, a glob, a substitution) is skipped, and relative paths are taken from the work tree, moved by a `cd` of the same command: the same approximation as leak.mjs.
import path from 'node:path';
import { SEARCH_TOOLS, SHELL_TOOLS, WALKS, under, walksDown } from './contamination-reach.mjs';

const posix = path.posix;
const SEGMENT = /[;&|\n]+/;
const OPAQUE = /[$*?[\]{}`()]/;
const SCRIPT_LINK = /symlink(?:Sync)?\(\s*(["'`])(.+?)\1\s*,\s*(["'`])(.+?)\3/g;

const unquote = w => w.replace(/^(["'`])(.*)\1$/, '$2');
const wordsOf = segment => (segment.match(/"[^"]*"|'[^']*'|\S+/g) ?? []).map(unquote);
const homeOf = ctx => ctx.boxHome ?? ctx.home ?? '/';
const absOf = (word, cwd, ctx) => posix.resolve(cwd, word.replace(/^(\$\{?HOME\}?|~)(?=\/|$)/, homeOf(ctx)));
const resolvable = w => w !== undefined && !OPAQUE.test(w);

/** Whether a simple command creates a symbolic link: `ln -s`, `ln -sf`, `ln --symbolic`, `cp -s`, `cp --symbolic-link`. */
const makesLink = words => ['ln', 'cp'].includes(words[0]) && words.slice(1).some(w => /^-[A-Za-z]*s[A-Za-z]*$/.test(w) || /^--symbolic(?:-link)?$/.test(w));

/** The operands of `ln -s <target> [<link>]`: `{ target, named }` (`named` undefined when the link is named after the target), or null when there is none or a word cannot be resolved. */
const operandsOf = words => {
  const given = words.slice(1).filter(w => !w.startsWith('-'));
  return given.length > 0 && given.slice(0, 2).every(resolvable) ? { target: given[0], named: given[1] } : null;
};

const linkPathOf = ({ target, named }) => {
  if (named === undefined) return posix.basename(target);
  return named.endsWith('/') ? `${named}${posix.basename(target)}` : named;
};

/** `{ linkAbs, targetAbs }` of a link command: the target is resolved from the link's own directory. */
function linkOf(words, cwd, ctx) {
  const operands = operandsOf(words);
  if (!operands) return null;
  const linkAbs = absOf(linkPathOf(operands), cwd, ctx);
  return { linkAbs, targetAbs: absOf(operands.target, posix.dirname(linkAbs), ctx) };
}

const madeBy = (words, cwd, ctx) => (makesLink(words) ? [linkOf(words, cwd, ctx)].filter(Boolean) : []);
const walkedBy = (segment, words, cwd, ctx) => (WALKS.test(` ${segment}`) ? words.filter(w => !w.startsWith('-') && resolvable(w)).map(w => absOf(w, cwd, ctx)) : []);

/** A `cd` moves the working directory and is itself a walk target (what is below it is read next). */
function cdStep(state, word, ctx) {
  if (!resolvable(word)) return state;
  const cwd = absOf(word, state.cwd, ctx);
  return { ...state, cwd, walks: [...state.walks, cwd] };
}

/** The state after one simple command: a `cd`, a link command (adds its link), a walking command (adds the paths it names). */
function step(state, segment, ctx) {
  const words = wordsOf(segment.trim());
  if (words[0] === 'cd') return cdStep(state, words[1], ctx);
  return { ...state, links: [...state.links, ...madeBy(words, state.cwd, ctx)], walks: [...state.walks, ...walkedBy(segment, words, state.cwd, ctx)] };
}

const shellState = (text, ctx) => text.split(SEGMENT).reduce((state, segment) => step(state, segment, ctx), { cwd: ctx.treeRoot, links: [], walks: [] });

/** The links a Node or Python call makes: `symlinkSync('<target>', '<link>')`, `os.symlink(...)`. */
const scriptLinks = (text, ctx) => [...text.matchAll(SCRIPT_LINK)].map(m => {
  const linkAbs = absOf(m[4], ctx.treeRoot, ctx);
  return { linkAbs, targetAbs: absOf(m[2], posix.dirname(linkAbs), ctx) };
});

const commandOf = call => String(call.input?.command ?? '');
const linksOfCall = (call, ctx) => (SHELL_TOOLS.has(call.tool) ? [...shellState(commandOf(call), ctx).links, ...scriptLinks(commandOf(call), ctx)] : []);

/** Every link the calls create, `{ at: index of the call, linkAbs, targetAbs }`: what a later call of the same session may walk through, even when the link is gone by the end. */
export const createdLinks = (calls, ctx) => calls.flatMap((call, at) => linksOfCall(call, ctx).map(l => ({ at, ...l })));

/** The box's `/tmp` is a host dir (`ctx.maps`, `{ host, box }` pairs whose paths differ): a target under it is judged at its host path. A path under the operator's home is never a box `/tmp` path (the home is hidden there). */
function toHost(p, ctx) {
  const m = under(p, ctx.home ?? '\0') ? undefined : (ctx.maps ?? []).find(x => under(p, x.box));
  return m ? posix.join(m.host, p.slice(m.box.length)) : p;
}

/** Whether a link target stays inside the work tree or what the box binds on purpose (`ctx.allow`). */
function stays(targetAbs, ctx) {
  const t = toHost(targetAbs, ctx);
  return under(t, ctx.treeRoot) || (ctx.allow ?? []).some(a => under(t, a.length > 1 ? a.replace(/\/$/, '') : a));
}

/** The target of a link the work tree holds on the host at `p` (never followed), or null. */
function hostTarget(p, ctx) {
  const raw = under(p, ctx.treeRoot) && ctx.readLink ? ctx.readLink(p) : null;
  return typeof raw === 'string' ? posix.resolve(posix.dirname(p), raw) : null;
}

/** The target of the link at `p`: one this session's calls created (the latest), else one the work tree holds on the host; null when `p` is no link. */
const targetAt = (p, ctx) => (ctx.links ?? []).findLast(l => l.linkAbs === p)?.targetAbs ?? hostTarget(p, ctx);

const prefixesOf = abs => abs.split('/').filter(Boolean).map((_, i, parts) => `/${parts.slice(0, i + 1).join('/')}`);

/** The first component of the path that is a link leaving the work tree and the binds: `{ linkAbs, targetAbs }`, or null. */
const leavingLink = (abs, ctx) => prefixesOf(abs).map(p => ({ linkAbs: p, targetAbs: targetAt(p, ctx) })).find(l => l.targetAbs !== null && !stays(l.targetAbs, ctx)) ?? null;

const shown = l => ({ index: 0, excerpt: `${l.linkAbs} -> ${l.targetAbs}` });

/** link-out: a call that creates a link to a path outside the work tree and the binds. */
export function linkOut(text, ctx, call) {
  const bad = linksOfCall(call, ctx).find(l => !stays(l.targetAbs, ctx));
  return bad ? shown(bad) : null;
}

const walkTargets = (call, ctx) => (SEARCH_TOOLS.has(call.tool) ? [absOf(String(call.input?.path ?? '.'), ctx.treeRoot, ctx)] : shellState(commandOf(call), ctx).walks);

/** link-walk: a walk whose path, or a component of it, is a link that leaves the work tree and the binds. `ctx.links` are the links the session's calls made up to this one. */
export function linkWalk(text, ctx, call) {
  if (!walksDown(text, call)) return null;
  const bad = walkTargets(call, ctx).map(t => leavingLink(t, ctx)).find(Boolean);
  return bad ? shown(bad) : null;
}
