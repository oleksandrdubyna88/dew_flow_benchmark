// Pure core of summarize-cell: stream events + audit records + cell.json -> summary object.
const MAIN = 'main';
import { createHash } from 'node:crypto';
import os from 'node:os';
import { findOutsideAccesses } from './leak.mjs';
import { costFromMessages } from './pricing.mjs';
import { scanTokens, tamperFindings } from './scan.mjs';

const NETWORK = [/\bgh\s/, /git\s+clone/, /git\s+(fetch|push|pull|ls-remote|remote)\b/, /\bcurl\b/, /\bwget\b/, /Invoke-(WebRequest|RestMethod)/i, /https:\/\//, /\bnpm\s+(i|install)\b/, /\bnpx\b/, /dotnet\s+restore/];

const AGENT_TOOLS = new Set(['Agent', 'Task']);
const GUARD_MARKER = 'subagent-models.md:';

export const parseNdjson = text => text.split('\n').map(l => l.trim()).filter(Boolean).flatMap(l => { try { return [JSON.parse(l)]; } catch { return []; } });

export const effortLevel = e => (typeof e === 'string' ? e : (e?.level ?? null));

const blocks = ev => (Array.isArray(ev?.message?.content) ? ev.message.content : []);

function toolUses(events) {
  return events.flatMap(ev => ev.type === 'assistant'
    ? blocks(ev).filter(b => b.type === 'tool_use').map(b => ({ ...b, parent: ev.parent_tool_use_id ?? null, model: ev.message?.model ?? null }))
    : []);
}

function toolResults(events) {
  return events.flatMap(ev => ev.type === 'user'
    ? blocks(ev).filter(b => b.type === 'tool_result').map(b => ({ id: b.tool_use_id, isError: b.is_error === true, text: resultText(b.content) }))
    : []);
}

function resultText(content) {
  if (typeof content === 'string') return content;
  return Array.isArray(content) ? content.map(c => c?.text ?? '').join('\n') : '';
}

export function resultEnvelope(events) {
  const r = events.findLast(e => e.type === 'result');
  if (!r) return { present: false };
  return {
    present: true, subtype: r.subtype ?? null, is_error: r.is_error ?? null,
    total_cost_usd: r.total_cost_usd ?? null, duration_ms: r.duration_ms ?? null, duration_api_ms: r.duration_api_ms ?? null,
    num_turns: r.num_turns ?? null, modelUsage: r.modelUsage ?? {},
  };
}

function denialKind(result) {
  if (!result?.isError && !/denied|blocked|refus/i.test(result?.text ?? '')) return null;
  if (result.text.includes(GUARD_MARKER)) return 'model-guard';
  return result.isError ? 'other-error' : null;
}

/** One entry per Agent/Task launch: its arguments, outcome, and the models its child messages ran on. */
export function agentCalls(events) {
  const uses = toolUses(events);
  const results = new Map(toolResults(events).map(r => [r.id, r]));
  return uses.filter(u => AGENT_TOOLS.has(u.name)).map(u => {
    const childMsgs = uses.filter(c => c.parent === u.id);
    const result = results.get(u.id);
    return {
      id: u.id, launchedBy: u.parent ?? MAIN,
      subagent_type: u.input?.subagent_type ?? null, model: u.input?.model ?? null, effortRequested: u.input?.effort ?? null,
      description: u.input?.description ?? '', denied: denialKind(result), hasResult: Boolean(result),
      resolvedModels: [...new Set(events.filter(e => e.type === 'assistant' && e.parent_tool_use_id === u.id).map(e => e.message?.model).filter(Boolean))],
      childToolCalls: childMsgs.length,
    };
  });
}

export function childrenPerModel(calls) {
  const counts = {};
  for (const c of calls.filter(x => x.denied === null && x.hasResult)) {
    for (const m of c.resolvedModels.length > 0 ? c.resolvedModels : ['unresolved']) counts[m] = (counts[m] ?? 0) + 1;
  }
  return counts;
}

export function effortByAgent(audit) {
  const out = {};
  for (const r of audit.filter(x => x.event === 'PreToolUse')) {
    const key = r.agent_id ?? MAIN;
    out[key] = [...new Set([...(out[key] ?? []), effortLevel(r.effort) ?? 'missing'])];
  }
  return out;
}

export const effortFlags = byAgent => Object.entries(byAgent).filter(([, lv]) => lv.some(l => l !== 'high')).map(([agent, levels]) => ({ agent, levels }));

export function instructionsLoaded(audit, cellJson) {
  const loads = audit.filter(r => r.event === 'InstructionsLoaded').map(r => ({ file_path: r.file_path, load_reason: r.load_reason, memory_type: r.memory_type, sha256: r.sha256 }));
  const key = '.claude/rules/shared/common/subagent-models.md';
  const hit = loads.find(l => (l.file_path ?? '').replace(/\\/g, '/').endsWith(key));
  return { loads, ruleLoaded: Boolean(hit), loadReason: hit?.load_reason ?? null, shaMatchesOverlay: Boolean(hit) && hit.sha256 === (cellJson?.overlay?.[key] ?? null) };
}

export function editsBy(audit) {
  const edits = audit.filter(r => r.event === 'PreToolUse' && ['Edit', 'Write', 'NotebookEdit'].includes(r.tool_name));
  const summarize = list => ({ calls: list.length, files: [...new Set(list.map(r => r.input?.file_path).filter(Boolean))].sort(), approxLines: list.reduce((n, r) => n + (r.input?.added ?? 0) + (r.input?.removed ?? 0), 0) });
  return { main: summarize(edits.filter(r => !r.agent_id)), children: summarize(edits.filter(r => r.agent_id)) };
}

/** Uniform tool calls: audit records when there are any, otherwise the stream's tool_use blocks. */
export function toolCalls(audit, events) {
  const fromAudit = audit.filter(r => r.event === 'PreToolUse' && r.tool_name).map(r => ({ agent: r.agent_id ?? MAIN, tool: r.tool_name, input: r.input ?? {} }));
  if (fromAudit.length > 0) return fromAudit;
  return toolUses(events).map(u => ({ agent: u.parent ?? MAIN, tool: u.name, input: u.input ?? {} }));
}

export function leakSuspects(calls, { treeRoot, home }) {
  const network = calls.filter(c => ['Bash', 'PowerShell'].includes(c.tool)).flatMap(c => {
    const reasons = NETWORK.filter(re => re.test(c.input?.command ?? '')).map(re => `network:${re.source}`);
    return reasons.length > 0 ? [{ agent: c.agent, tool: c.tool, reasons, command: (c.input.command ?? '').slice(0, 400) }] : [];
  });
  const outside = treeRoot ? findOutsideAccesses(calls, { treeRoot, home }).map(o => ({ agent: o.agent, tool: o.tool, reasons: [`outside:${o.resolved}`], command: o.path })) : [];
  return [...outside, ...network];
}

export function initInfo(events) {
  const i = events.find(e => e.type === 'system' && e.subtype === 'init');
  if (!i) return { present: false };
  const { cwd, ...rest } = i;
  const names = list => (list ?? []).map(x => (typeof x === 'string' ? x : x?.name ?? JSON.stringify(x)));
  return {
    present: true, model: rest.model ?? null, cwdSha256: createHash('sha256').update(cwd ?? '').digest('hex'),
    tools: names(rest.tools), agents: names(rest.agents), skills: names(rest.skills), plugins: names(rest.plugins), mcpServers: names(rest.mcp_servers),
    slashCommands: names(rest.slash_commands).length,
  };
}

/** Agent calls that ran minus the distinct child agent ids the audit log saw. */
export function unobservedChildren(calls, audit) {
  const ran = calls.filter(c => c.denied === null && c.hasResult).length;
  const seen = new Set(audit.map(r => r.agent_id).filter(Boolean)).size;
  return Math.max(0, ran - seen);
}

function costOf(events, result) {
  if (result.present && result.total_cost_usd !== null) return { costSource: 'envelope', usd: result.total_cost_usd };
  const m = costFromMessages(events);
  return { costSource: 'messages', usd: m.usd, estimated: true, messageUsage: m.tokensByModel };
}

export function summarize({ events, audit, cellJson, treeDir, home = os.homedir(), canaryTokens = [], reference = {}, texts = {}, excludedFiles = [] }) {
  const calls = agentCalls(events);
  const byAgent = effortByAgent(audit);
  const result = resultEnvelope(events);
  const all = toolCalls(audit, events);
  const sources = (names) => names.map(n => ({ source: n, text: texts[n] ?? '' }));
  const cost = costOf(events, result);
  return {
    cell: cellJson?.cell ?? null,
    result, ...cost,
    init: initInfo(events),
    agentCalls: calls,
    childrenPerModel: childrenPerModel(calls),
    unobservedChildren: unobservedChildren(calls, audit),
    denials: { modelGuard: calls.filter(c => c.denied === 'model-guard').length, other: calls.filter(c => c.denied === 'other-error').length },
    effortByAgent: byAgent,
    effortNotHigh: effortFlags(byAgent),
    mainSessionEffortRecorded: Boolean(byAgent[MAIN]),
    instructions: instructionsLoaded(audit, cellJson),
    edits: editsBy(audit),
    leakSuspects: leakSuspects(all, { treeRoot: treeDir, home }),
    canary: scanTokens(sources(['stream', 'audit', 'stderr', 'candidateDiff', 'excludedDiff']), { canary: canaryTokens }),
    referenceOnly: scanTokens(sources(['stream', 'candidateDiff', 'excludedDiff']), reference),
    tamper: tamperFindings(excludedFiles, all),
  };
}
