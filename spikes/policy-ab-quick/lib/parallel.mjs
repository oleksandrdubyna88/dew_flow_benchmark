// Pure: what the children of one cell did and WHEN - concurrency, overlap, per-child cost, per-agent edits.
// Time comes from the stream's own event timestamps (a launch is the main message that holds the Agent tool_use; a child
// ends when the main session receives its tool_result). Whole-cell wall time is the CLI's envelope, never a sum of children.
import { costFromMessages } from './pricing.mjs';

const AGENT_TOOLS = new Set(['Agent', 'Task']);
export const EDIT_TOOLS = Object.freeze(['Edit', 'Write', 'MultiEdit', 'NotebookEdit']);
const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);
const MAIN = 'main';

const blocks = ev => (Array.isArray(ev?.message?.content) ? ev.message.content : []);
const ms = iso => (typeof iso === 'string' ? Date.parse(iso) : Number.NaN);
const finite = n => Number.isFinite(n);

/** One entry per Agent/Task launch by the main session: where it was launched, when, and how it ended. */
export function agentTimeline(events) {
  const launches = events.filter(e => e.type === 'assistant' && !e.parent_tool_use_id).flatMap(e => blocks(e)
    .filter(b => b.type === 'tool_use' && AGENT_TOOLS.has(b.name))
    .map(b => ({ id: b.id, messageId: e.message?.id ?? e.uuid ?? b.id, launchedAt: ms(e.timestamp), input: b.input ?? {} })));
  const done = new Map();
  for (const e of events.filter(x => x.type === 'user' && !x.parent_tool_use_id)) {
    const results = blocks(e).filter(b => b.type === 'tool_result');
    for (const r of results) done.set(r.tool_use_id, { endedAt: ms(e.timestamp), info: results.length === 1 ? e.tool_use_result ?? {} : {}, text: resultText(r.content), isError: r.is_error === true });
  }
  return launches.map(l => {
    const d = done.get(l.id);
    const info = d?.info ?? {};
    const agentId = info.agentId ?? /agentId:\s*([A-Za-z0-9]+)/.exec(d?.text ?? '')?.[1] ?? null;
    return {
      id: l.id, messageId: l.messageId, agentId, description: l.input.description ?? '', requestedModel: l.input.model ?? null,
      resolvedModel: info.resolvedModel ?? null, startMs: l.launchedAt, endMs: d?.endedAt ?? Number.NaN,
      reportedDurationMs: info.totalDurationMs ?? null, reportedTokens: info.totalTokens ?? null,
      toolStats: info.toolStats ?? null, finished: Boolean(d) && !d.isError,
    };
  });
}

function resultText(content) {
  if (typeof content === 'string') return content;
  return Array.isArray(content) ? content.map(c => c?.text ?? '').join('\n') : '';
}

/** Pure: Agent launches per assistant message (the stream repeats a message once per content block, so group by message id). */
export function launchesPerMessage(timeline) {
  const counts = new Map();
  for (const t of timeline) counts.set(t.messageId, (counts.get(t.messageId) ?? 0) + 1);
  const perMessage = [...counts.values()];
  return { messagesWithLaunches: perMessage.length, perMessage, maxPerMessage: perMessage.length === 0 ? 0 : Math.max(...perMessage) };
}

const spanOf = t => (finite(t.startMs) && finite(t.endMs) && t.endMs >= t.startMs ? [t.startMs, t.endMs] : null);
const overlaps = (a, b) => a[0] < b[1] && b[0] < a[1];

/** Pure: sweep the spans. maxConcurrent / overlapMs (time with >= 2 children) / unionMs (time with >= 1) - never a sum. */
export function sweepSpans(spans) {
  const points = spans.flatMap(([s, e]) => [[s, 1], [e, -1]]).sort((a, b) => a[0] - b[0] || a[1] - b[1]);
  let active = 0; let max = 0; let overlapMs = 0; let unionMs = 0; let last = 0;
  for (const [at, delta] of points) {
    if (active >= 1) unionMs += at - last;
    if (active >= 2) overlapMs += at - last;
    active += delta; last = at; max = Math.max(max, active);
  }
  return { maxConcurrent: max, overlapMs, unionMs };
}

/**
 * Pure: the children whose time overlaps another child's on the SAME model - their cost cannot be told apart. A group is a
 * CONNECTED COMPONENT of the overlap relation: when A overlaps B and B overlaps C (A and C apart), all three are one group,
 * because B's cost cannot be separated from either neighbour's.
 */
export function inseparableGroups(timeline) {
  const model = t => t.resolvedModel ?? t.requestedModel ?? 'unresolved';
  const spanned = timeline.map(t => ({ id: t.id, model: model(t), span: spanOf(t) })).filter(x => x.span);
  const parent = new Map(spanned.map(s => [s.id, s.id]));
  const find = id => (parent.get(id) === id ? id : find(parent.get(id)));
  for (const a of spanned) for (const b of spanned) if (a.id < b.id && a.model === b.model && overlaps(a.span, b.span)) parent.set(find(a.id), find(b.id));
  const components = Map.groupBy(spanned, s => find(s.id));
  const groups = new Map();
  for (const members of components.values()) {
    if (members.length < 2) continue;
    const ids = members.map(m => m.id).sort();
    for (const id of ids) groups.set(id, ids);
  }
  return groups;
}

/** Per-child cost: the sum of the child's own assistant messages (an estimate), or `inseparable` with the group's estimate. */
export function childCosts(events, timeline) {
  const groups = inseparableGroups(timeline);
  const own = new Map(timeline.map(t => [t.id, costFromMessages(events.filter(e => e.parent_tool_use_id === t.id)).usd]));
  return new Map(timeline.map(t => {
    const group = groups.get(t.id);
    if (!group) return [t.id, { usd: own.get(t.id), estimated: true, source: 'child-messages', inseparable: false }];
    const members = new Set(group);
    const groupUsd = Math.round([...members].reduce((n, id) => n + (own.get(id) ?? 0), 0) * 1e4) / 1e4;
    return [t.id, { usd: null, inseparable: true, group: [...members], groupUsdEstimate: groupUsd, reason: 'two or more children share this model in overlapping time' }];
  }));
}

/** The cell-level parallelism record. `wallMs` is the whole cell, from the result envelope (or the launcher's clock). */
export function parallelism(events, { wallMs = null } = {}) {
  const timeline = agentTimeline(events);
  const sweep = sweepSpans(timeline.map(spanOf).filter(Boolean));
  const costs = childCosts(events, timeline);
  const timed = timeline.every(t => spanOf(t));
  return {
    children: timeline.length,
    launch: launchesPerMessage(timeline),
    timed,
    maxConcurrent: timed ? sweep.maxConcurrent : null,
    overlapMs: timed ? sweep.overlapMs : null,
    childUnionMs: timed ? sweep.unionMs : null,
    wallMs,
    perChild: timeline.map(t => ({
      id: t.id, agentId: t.agentId, description: t.description, model: t.requestedModel, resolvedModel: t.resolvedModel,
      startedAt: finite(t.startMs) ? new Date(t.startMs).toISOString() : null, endedAt: finite(t.endMs) ? new Date(t.endMs).toISOString() : null,
      spanMs: spanOf(t) ? t.endMs - t.startMs : null, reportedDurationMs: t.reportedDurationMs, reportedTokens: t.reportedTokens, toolStats: t.toolStats,
      finished: t.finished, cost: costs.get(t.id),
    })),
  };
}

// ---- edits per agent --------------------------------------------------------------------------------------------

const SHELL_WRITE = [/\bsed\s+(-[A-Za-z]*i|--in-place)/, /\bperl\s+-[A-Za-z]*i/, /(?<![0-9&<>=-])>>?\s*(?!&|\/dev\/null|nul\b|\$null)[^\s>|&=]/, /\btee\b/,
  /\b(Set-Content|Add-Content|Out-File|Remove-Item|Move-Item|Copy-Item|New-Item)\b/i, /\b(writeFileSync|appendFileSync|write_text|write_bytes)\b/, /\bopen\([^)]*['"][wa]\+?['"]/,
  /\bgit\s+(apply|mv|rm|checkout|restore)\b/, /(^|[\s;&|])(mv|cp|rm)\s/];

/** Pure: does a shell command look like it writes files? A heuristic - the count is of CALLS, never of lines. */
export const looksLikeShellWrite = command => typeof command === 'string' && SHELL_WRITE.some(re => re.test(command));

const emptyEdits = () => ({ calls: 0, files: [], approxLines: 0 });

function addEdit(acc, r) {
  return {
    calls: acc.calls + 1,
    files: [...new Set([...acc.files, r.input?.file_path].filter(Boolean))].sort(),
    approxLines: acc.approxLines + (r.input?.added ?? 0) + (r.input?.removed ?? 0),
  };
}

/**
 * Edits per agent from the audit log: edit-tool calls (calls, files, approximate lines) and shell-write calls (calls only).
 * Children usually edit through the shell, so the edit-tool count alone reads 0 for them - shellWriteCalls is the other half.
 * `call` names the Agent launch (stream tool_use id) whose agentId this audit id is, when the stream says.
 */
export function editsByAgent(audit, timeline = []) {
  const callOf = new Map(timeline.filter(t => t.agentId).map(t => [t.agentId, t]));
  const byAgent = {};
  for (const r of audit.filter(x => x.event === 'PreToolUse' && x.tool_name)) {
    const key = r.agent_id ?? MAIN;
    const cur = byAgent[key] ?? { editTools: emptyEdits(), shellWriteCalls: 0, toolCalls: 0 };
    byAgent[key] = {
      editTools: EDIT_TOOLS.includes(r.tool_name) ? addEdit(cur.editTools, r) : cur.editTools,
      shellWriteCalls: cur.shellWriteCalls + (SHELL_TOOLS.has(r.tool_name) && looksLikeShellWrite(r.input?.command) ? 1 : 0),
      toolCalls: cur.toolCalls + 1,
    };
  }
  return Object.fromEntries(Object.entries(byAgent).map(([k, v]) => {
    const t = callOf.get(k);
    return [k, { ...v, ...(k === MAIN ? {} : { call: t?.id ?? null, description: t?.description ?? null }) }];
  }));
}

/** Pure: main vs all-children totals from the per-agent map. */
export function sumEdits(byAgent) {
  const fold = keys => keys.reduce((acc, k) => ({
    calls: acc.calls + byAgent[k].editTools.calls,
    files: [...new Set([...acc.files, ...byAgent[k].editTools.files])].sort(),
    approxLines: acc.approxLines + byAgent[k].editTools.approxLines,
    shellWriteCalls: acc.shellWriteCalls + byAgent[k].shellWriteCalls,
  }), { calls: 0, files: [], approxLines: 0, shellWriteCalls: 0 });
  const keys = Object.keys(byAgent);
  return { main: fold(keys.filter(k => k === MAIN)), children: fold(keys.filter(k => k !== MAIN)) };
}
