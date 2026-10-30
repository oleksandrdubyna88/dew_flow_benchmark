// The rules of contamination.mjs that read git refs, pull request numbers and the fork's `data` branch out of a command or an
// answer. Text in, `{ index, severity? }` or null out; never a throw.
import { splitTopLevel, unquote, wordsOf } from './classify.mjs';

export const escapeRe = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
export const lower = s => String(s ?? '').toLowerCase();
export const isHit = found => found !== null && found !== undefined;
const globalOf = re => (re.global ? re : new RegExp(re.source, `${re.flags}g`));
const earliest = found => found.filter(isHit).sort((a, b) => a.index - b.index)[0] ?? null;
const words = segment => wordsOf(segment).map(unquote);
const isFlag = word => word.startsWith('-') && word.length > 1;

/** The first match of `re` in `text` for which `accept(match)` holds, as `{ index }`, or null. */
export function firstMatch(text, re, accept = () => true) {
  const m = [...text.matchAll(globalOf(re))].find(accept);
  return m ? { index: m.index } : null;
}

/** `[{ words, index }]`: every top-level command of `text` as unquoted words, with where its segment starts. */
function commands(text) {
  return splitTopLevel(text).map(segment => ({ words: words(segment), index: Math.max(0, text.indexOf(segment.trim())) }));
}

// ---- another run's refs ---------------------------------------------------------------------------------------------------

// `run/<x>/` or `run%2F<x>%2F`; the wildcard `run/*` ends at a `:` or a space
const RUN_OCCURRENCE = /run(?:\/|%2[Ff])([A-Za-z0-9._*-]+)(?:\/|%2[Ff]|(?=[:\s'"`]|$))/g;
// what makes the text before it a REF position: refs/heads/, origin/, a range dot, `=` (`?ref=`), `:`, `+`, a url's tree/ ...
const REF_PREFIX = /(?:refs\/heads\/|refs\/remotes\/[\w.-]+\/|\b(?:origin|upstream|fork)\/|\bremotes\/[\w.-]+\/|\.{2,3}|[=:+]|\b(?:tree|blob|commits?|compare|branches|heads)\/)$/;
const REF_VERB = /\b(?:fetch|checkout|switch|push|pull|ls-remote|show|log|diff|merge|rebase|cherry-pick|branch|rev-parse|clone)\b[^\n;|&]*?[\s'"]\+?$/;
// a bare mention (an answer's prose) counts only when it looks like a run id: a folder `run/output/` does not
const looksLikeRunId = x => x.length >= 4 && /\d/.test(x);

function isRunRef(text, m, ctx) {
  const x = m[1];
  if (x === ctx.rid) return false;
  const before = text.slice(0, m.index);
  if (REF_PREFIX.test(before) || REF_VERB.test(before)) return true;
  return x === '*' || (!/[\w/.-]$/.test(before) && looksLikeRunId(x));
}

export const foreignRunRef = (text, ctx) => firstMatch(text, RUN_OCCURRENCE, m => isRunRef(text, m, ctx));

// ---- pull requests --------------------------------------------------------------------------------------------------------

const PR_VERBS = new Set(['view', 'checkout', 'diff', 'checks', 'comment', 'review', 'merge', 'close', 'reopen', 'edit', 'ready', 'lock', 'unlock', 'update-branch']);
// `gh pr` options that take a value as the NEXT word (an `--opt=value` is one word)
const PR_VALUE_FLAGS = new Set(['--interval', '-i', '--body', '-b', '--body-file', '-F', '--title', '-t', '--json', '-q', '--jq', '--repo', '-R', '--color', '--template', '-T',
  '--subject', '-s', '--match-head-commit', '--author-email', '-A', '--base', '-B', '--add-label', '--add-assignee', '--add-reviewer', '--remove-label', '--milestone', '-m']);

/** The pull request a `gh pr <verb> ...` command names: the first positional after the verb, option values skipped. */
function prNumberOf(args) {
  for (let i = 0; i < args.length; i += 1) {
    if (isFlag(args[i])) { if (!args[i].includes('=') && PR_VALUE_FLAGS.has(args[i])) i += 1; continue; }
    return /^#?\d+$/.test(args[i]) ? Number(args[i].replace('#', '')) : null; // a branch or a url is not a number
  }
  return null;
}

function ghPrNumbers(command) {
  const g = command.words.indexOf('gh');
  const verb = g >= 0 && command.words[g + 1] === 'pr' ? command.words[g + 2] : null;
  return PR_VERBS.has(verb) ? [{ n: prNumberOf(command.words.slice(g + 3)), index: command.index }] : [];
}

function urlPrNumbers(text, ctx) {
  if (!ctx.ownOrigin) return []; // another repository's pull request is not ours to judge
  const own = new RegExp(`(?:github\\.com[/:]|\\brepos/)${escapeRe(ctx.ownOrigin)}(?:\\.git)?/pulls?/(\\d+)\\b`, 'gi');
  const graphql = /\bpullRequest\s*\(\s*number\s*:\s*(\d+)/g;
  return [...text.matchAll(own), ...text.matchAll(graphql)].map(m => ({ n: Number(m[1]), index: m.index }));
}

export function foreignPr(text, ctx) {
  const own = new Set((ctx.ownPrs ?? []).map(Number));
  const named = [...commands(text).flatMap(ghPrNumbers), ...urlPrNumbers(text, ctx)].filter(p => p.n !== null && !own.has(p.n));
  return earliest(named.map(p => ({ index: p.index })));
}

// ---- upstream's solutions: the fork's `data` branch -----------------------------------------------------------------------

export const UPSTREAM_REPOS = ['mame/ai-coding-lang-bench', 'banteg/minigit-bench'];

// `data` in a ref position of a url or a ref name, wherever it stands
const DATA_ANYWHERE = [
  /\brefs\/(?:heads|remotes\/[\w.-]+)\/data(?![\w./-])/g,
  /\b(?:origin|upstream|fork)\/data(?![\w.-])/g,
  /[?&]ref=data(?![\w./-])/g,
  /\/(?:tree|blob|tarball|zipball|zip|tar\.gz|trees|contents|branches|commits?)\/data(?![\w.-])/g,
  /\bclone\b[^\n;|&]*?(?:--branch[= ]|-b\s+)data(?![\w./-])/g,
];
const GIT_REF_VERBS = new Set(['fetch', 'pull', 'checkout', 'switch', 'ls-remote', 'clone', 'show', 'log', 'diff', 'merge', 'rebase', 'cherry-pick', 'rev-parse', 'archive', 'reset', 'branch']);
// a word that IS the branch: data, +data:x, a..data, origin/data:path, refs/heads/data, --branch=data
const DATA_WORD = /^[+^]?(?:[^:\s]*\.\.\.?)?(?:(?:origin|upstream|fork)\/|remotes\/[\w.-]+\/|refs\/(?:heads|remotes\/[\w.-]+)\/)?data(?:$|[:^~@{])/;
const FETCH_VALUE_FLAGS = new Set(['--depth', '--jobs', '-j', '--filter', '--shallow-since', '--shallow-exclude', '--negotiation-tip', '-o', '--server-option', '--upstream-tip']);

function gitCommand(command) {
  const g = command.words.indexOf('git');
  if (g < 0) return null;
  const v = command.words.findIndex((w, i) => i > g && GIT_REF_VERBS.has(w) && !isFlag(w));
  return v < 0 ? null : { verb: command.words[v], args: command.words.slice(v + 1), index: command.index };
}

const namesData = args => args.slice(0, args.includes('--') ? args.indexOf('--') : args.length).some(w => DATA_WORD.test(w.replace(/^--[\w-]+=/, '')));

/** A `git fetch` that names no refspec (a remote or nothing), or asks for everything: its default refspec includes the data branch. */
function fetchesEverything(g) {
  if (g.verb === 'clone') return g.args.includes('--mirror');
  if (g.verb !== 'fetch') return false;
  if (g.args.includes('--all') || g.args.includes('--mirror')) return true;
  const positional = g.args.filter((w, i) => !isFlag(w) && !FETCH_VALUE_FLAGS.has(g.args[i - 1]));
  return positional.length <= 1;
}

function dataFromGit(command) {
  const g = gitCommand(command);
  if (!g) return [];
  if (namesData(g.args)) return [{ index: command.index }];
  return fetchesEverything(g) ? [{ index: command.index, severity: 'low' }] : [];
}

export function upstreamSolutions(text) {
  const named = UPSTREAM_REPOS.map(name => firstMatch(text, new RegExp(escapeRe(name), 'gi')));
  const anywhere = DATA_ANYWHERE.map(re => firstMatch(text, re));
  const git = commands(text).flatMap(dataFromGit);
  const high = earliest([...named, ...anywhere, ...git.filter(h => !h.severity)]);
  return high ?? earliest(git);
}
