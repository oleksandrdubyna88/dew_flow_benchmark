// Detection only: which of a session's records show it REACHING for something that would change its result - another run's refs,
// a foreign pull request, the other stack's template, upstream's solutions, the harness, a partner's files, the open web.
// What a box PREVENTS is E2.S2; this is the half that records every reach the box cannot stop (parent 7.4-7.6).
//
// A rule is a named entry of CONTAMINATION_RULES, so a test iterates them and a new rule cannot be added unexercised. A hit is
// { severity: 'high' | 'low', rule, source, where, excerpt }: the excerpt is at most 120 characters, and a credential in it is
// masked (token-scan.mjs) before anything else looks at the text. At most one hit per rule per record.
import { findOutsideAccesses } from '../../policy-ab-quick/lib/leak.mjs';
import { EGRESS_HOSTS } from './egress-hosts.mjs';
import { maskTokens } from './token-scan.mjs';

const EXCERPT_MAX = 120;
const EXCERPT_LEAD = 30;
const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);
const FILE_TOOLS = new Set(['Read', 'Edit', 'Write', 'NotebookEdit']);
const SEARCH_TOOLS = new Set(['Glob', 'Grep']);
const WEB_TOOLS = new Set(['WebFetch', 'WebSearch']);
const MCP_PREFIX = 'mcp__';
const UPSTREAM_REPOS = ['mame/ai-coding-lang-bench', 'banteg/minigit-bench'];
// verbs of `gh pr` that take a pull request number (`list --limit 5` and `create --title "x 3"` do not)
const PR_VERBS = 'view|checkout|diff|checks|comment|review|merge|close|reopen|edit|ready|lock|unlock|update-branch';

const escapeRe = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const lower = s => String(s ?? '').toLowerCase();
const globalOf = re => (re.global ? re : new RegExp(re.source, `${re.flags}g`));
const isHit = found => found !== null && found !== undefined;
const at = (index, severity) => (severity ? { index, severity } : { index });

/** The first match of `re` in `text` for which `accept(match)` holds, as `{ index }`, or null. */
function firstMatch(text, re, accept = () => true) {
  const m = [...text.matchAll(globalOf(re))].find(accept);
  return m ? at(m.index) : null;
}

// ---- the text rules (a call's text or an mcp answer) -------------------------------------------------------------------------

// `run/<x>/` as a ref: not inside a longer path (`src/run/helpers/`), unless it follows refs/heads/, origin/ or remotes/<r>/
const RUN_REF = /(?:(?<![\w/.-])|(?<=\brefs\/heads\/)|(?<=\borigin\/)|(?<=\bremotes\/[\w.-]+\/))run\/([A-Za-z0-9._-]+)\//g;

const foreignRunRef = (text, ctx) => firstMatch(text, RUN_REF, m => m[1] !== ctx.rid);

const otherRunId = (text, ctx) => (ctx.runIdPattern ? firstMatch(text, ctx.runIdPattern, m => m[0] !== ctx.runId) : null);

function foreignPr(text, ctx) {
  const own = new Set((ctx.ownPrs ?? []).map(Number));
  const numbered = [
    /\bpulls?\/(\d+)\b/g,
    new RegExp(`\\bgh\\s+pr\\s+(?:${PR_VERBS})\\b[^\\n;|&]*?\\s#?(\\d+)(?=\\s|$)`, 'g'),
  ];
  const found = numbered.map(re => firstMatch(text, re, m => !own.has(Number(m[1])))).filter(isHit);
  return found.sort((a, b) => a.index - b.index)[0] ?? null;
}

const otherStackTemplate = (text, ctx) => (ctx.otherStack
  ? firstMatch(text, new RegExp(`\\btemplate/(?:process|bare)-${escapeRe(ctx.otherStack)}\\b`, 'g'))
  : null);

// the fork's `data` branch (upstream's generated solutions) in a git or URL context
const DATA_BRANCH = [
  /\brefs\/(?:heads|remotes\/[\w.-]+)\/data(?![\w./-])/g,
  /\/tree\/data(?![\w./-])/g,
  /\bgit\b[^\n;|&]*\b(?:fetch|checkout|switch|ls-remote|clone)\b[^\n;|&]*\s(?:origin\/)?data(?=\s|$|["'`])/g,
];

function upstreamSolutions(text) {
  const named = UPSTREAM_REPOS.map(name => firstMatch(text, new RegExp(escapeRe(name), 'gi')));
  const found = [...named, ...DATA_BRANCH.map(re => firstMatch(text, re))].filter(isHit);
  return found.sort((a, b) => a.index - b.index)[0] ?? null;
}

const GITHUB_REPO = /(?:github\.com|raw\.githubusercontent\.com)[/:]([\w.-]+)\/([\w.-]+)([^\s"'`<>)]*)/gi;

/** Whether a github URL that names minigit stays inside our own origin and our own run's refs. */
function staysHome(m, ctx) {
  const [, owner, repo, rest] = m;
  const isRaw = lower(m[0]).startsWith('raw.');
  const same = lower(`${owner}/${repo.replace(/\.git$/i, '')}`) === lower(String(ctx.ownOrigin ?? ''));
  if (!same) return false;
  const own = escapeRe(ctx.ownPrefix ?? '\0');
  if (isRaw) return new RegExp(`^/${own}`).test(rest);
  return rest === '' || /^\/pull\/\d+/.test(rest) || new RegExp(`^/(?:tree|blob|commits?|compare)/${own}`).test(rest);
}

// a url `upstream-solutions` already reports (upstream's own repositories, the fork's data branch): one reach, one hit
const minigitElsewhere = (text, ctx) => firstMatch(text, GITHUB_REPO,
  m => /minigit/i.test(`${m[1]}/${m[2]}`) && !isHit(upstreamSolutions(m[0])) && !staysHome(m, ctx));

const harnessName = text => firstMatch(text, /dew_flow_benchmark/g);

// ---- the call rules ----------------------------------------------------------------------------------------------------------

const urlHost = url => lower(url.replace(/^[a-z]+:\/\//i, '').replace(/^[^@/]*@/, '').split(/[/:?#]/)[0]);

function webHosts(ctx) {
  const arm = ctx.arm ?? 'bare'; // the registries every arm shares, unless the caller names its arm
  if (!Object.hasOwn(EGRESS_HOSTS, arm)) throw new Error(`contamination: arm '${arm}' is not one of ${Object.keys(EGRESS_HOSTS).join(', ')}`);
  return new Set(EGRESS_HOSTS[arm]);
}

function web(text, ctx, call) {
  if (WEB_TOOLS.has(call.tool)) return at(0);
  const allowed = webHosts(ctx);
  const fetcher = /\b(?:curl|wget)\b[^\n;|&]*/g;
  for (const segment of text.matchAll(fetcher)) {
    const foreign = [...segment[0].matchAll(/[a-z][a-z0-9+.-]*:\/\/[^\s"'`<>)]+/gi)].find(u => !allowed.has(urlHost(u[0])));
    if (foreign) return at(segment.index + foreign.index);
  }
  return null;
}

function outsidePath(text, ctx, call) {
  const found = findOutsideAccesses([call], { treeRoot: ctx.treeRoot, home: ctx.home, flavour: 'posix', allow: ctx.allow ?? [] });
  if (found.length === 0) return null;
  const protectedRoots = [...(ctx.partnerPaths ?? []), ...(ctx.harnessPaths ?? []), ctx.home].filter(Boolean);
  const high = found.find(f => protectedRoots.some(root => f.resolved === root || f.resolved.startsWith(root.endsWith('/') ? root : `${root}/`)));
  const first = high ?? found[0];
  return { ...at(Math.max(0, text.indexOf(first.path)), high ? 'high' : 'low'), excerpt: first.resolved };
}

/** The rules, in report order. `find(text, ctx, call)` returns `{ index, severity?, excerpt? }` or null; `scope` says what it reads. */
export const CONTAMINATION_RULES = Object.freeze([
  { rule: 'foreign-run-ref', severity: 'high', scope: 'text', find: foreignRunRef },
  { rule: 'other-run-id', severity: 'high', scope: 'text', find: otherRunId },
  { rule: 'foreign-pr', severity: 'high', scope: 'text', find: foreignPr },
  { rule: 'other-stack-template', severity: 'high', scope: 'text', find: otherStackTemplate },
  { rule: 'upstream-solutions', severity: 'high', scope: 'text', find: upstreamSolutions },
  { rule: 'minigit-elsewhere', severity: 'high', scope: 'text', find: minigitElsewhere },
  { rule: 'harness-name', severity: 'high', scope: 'text', find: harnessName },
  { rule: 'outside-path', severity: 'high', scope: 'call', find: outsidePath }, // 'low' when the path is under none of the protected roots
  { rule: 'web', severity: 'low', scope: 'call', find: web },
]);

// ---- assembling the hits ----------------------------------------------------------------------------------------------------

/** What a call REACHES, as text: a command, a path, a url, a query - never the content an edit writes. */
function callText(call) {
  const input = call.input ?? {};
  if (SHELL_TOOLS.has(call.tool)) return String(input.command ?? '');
  if (FILE_TOOLS.has(call.tool)) return String(input.file_path ?? input.notebook_path ?? '');
  if (SEARCH_TOOLS.has(call.tool)) return [input.pattern, input.path].filter(Boolean).join(' ');
  if (call.tool === 'WebFetch') return String(input.url ?? '');
  if (call.tool === 'WebSearch') return String(input.query ?? '');
  return typeof call.tool === 'string' && call.tool.startsWith(MCP_PREFIX) ? JSON.stringify(input) : '';
}

const excerptAt = (text, index) => text.slice(Math.max(0, index - EXCERPT_LEAD)).replace(/\s+/g, ' ').trim().slice(0, EXCERPT_MAX);

function hitsOf(record, ctx) {
  const text = maskTokens(record.text, ctx.ownSecrets);
  return CONTAMINATION_RULES.filter(r => r.scope === 'text' || record.call).flatMap(r => {
    const found = r.find(text, ctx, record.call);
    if (!isHit(found)) return [];
    const excerpt = maskTokens(found.excerpt ?? excerptAt(text, found.index), ctx.ownSecrets).slice(0, EXCERPT_MAX);
    return [{ severity: found.severity ?? r.severity, rule: r.rule, source: record.source, where: record.where, excerpt }];
  });
}

/**
 * Pure. `calls` = `[{ agent, tool, input, source? }]` (the stream's tool_use blocks, main and forwarded children, and the audit);
 * `mcpResults` = the text of every tool_result whose tool_use named an `mcp__` tool (a string, or `{ text, where? }`);
 * `ctx` = { rid, runId, runIdPattern, ownPrefix, ownOrigin, ownPrs, otherStack, partnerPaths, harnessPaths, treeRoot, home,
 * allow, arm?, ownSecrets? }. `arm` picks the egress list the `web` rule reads; absent means the registries every arm shares.
 */
export function contaminationHits({ calls = [], mcpResults = [], ctx }) {
  const fromCalls = calls.map(call => ({ text: callText(call), call, source: call.source ?? 'call', where: `${call.agent ?? 'main'}:${call.tool}` }));
  const fromResults = mcpResults.map((r, i) => {
    const text = typeof r === 'string' ? r : String(r?.text ?? '');
    return { text, call: null, source: 'mcp', where: typeof r === 'object' && r?.where ? r.where : `mcp:${i}` };
  });
  return [...fromCalls, ...fromResults].flatMap(record => hitsOf(record, ctx));
}
