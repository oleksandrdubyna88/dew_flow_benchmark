// Pure core of summarize-cell: stream events + audit records + cell.json -> summary object.
const MAIN = 'main';
const NETWORK = [/\bgh\s/, /git\s+clone/, /git\s+fetch/, /\bcurl\b/, /Invoke-WebRequest/i, /https:\/\//];
const OUTSIDE = [/D:\\rsd/i, /D:\/rsd/i, /~\/\.claude/, /C:\\Users\\/i, /C:\/Users\//i, /\$env:USERPROFILE/i, /%USERPROFILE%/i];
const AGENT_TOOLS = new Set(['Agent', 'Task']);
const GUARD_MARKER = 'subagent-models.md:';
const PIN_MARKER = 'policy-effort-pin:';

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
  if (result.text.includes(PIN_MARKER)) return 'effort-pin';
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
      subagent_type: u.input?.subagent_type ?? null, model: u.input?.model ?? null, effort: u.input?.effort ?? null,
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

/** Normalise the tree path out so a command inside the tree is not an "outside path". */
function stripTree(text, treeDir) {
  if (!treeDir) return text;
  const variants = [treeDir, treeDir.replace(/\//g, '\\'), treeDir.replace(/\\/g, '/')];
  return variants.reduce((t, v) => t.split(v).join('<tree>'), text);
}

export function commandsFrom(audit, events) {
  const fromAudit = audit.filter(r => r.event === 'PreToolUse' && ['Bash', 'PowerShell'].includes(r.tool_name)).map(r => ({ agent: r.agent_id ?? MAIN, tool: r.tool_name, command: r.input?.command ?? '' }));
  const fromStream = toolUses(events).filter(u => ['Bash', 'PowerShell'].includes(u.name)).map(u => ({ agent: u.parent ?? MAIN, tool: u.name, command: u.input?.command ?? '' }));
  return fromAudit.length > 0 ? fromAudit : fromStream;
}

export function leakSuspects(commands, treeDir) {
  return commands.flatMap(c => {
    const text = stripTree(c.command, treeDir);
    const outside = OUTSIDE.filter(re => re.test(text)).map(re => `outside:${re.source}`);
    const network = NETWORK.filter(re => re.test(text)).map(re => `network:${re.source}`);
    const reasons = [...outside, ...network];
    return reasons.length > 0 ? [{ agent: c.agent, tool: c.tool, reasons, command: c.command.slice(0, 400) }] : [];
  });
}

export function canaryHits(rawTexts, tokens) {
  return tokens.filter(Boolean).flatMap(token => rawTexts.flatMap(({ source, text }) => (text.includes(token) ? [{ source, tokenPrefix: token.slice(0, 6) }] : [])));
}

export function summarize({ events, audit, cellJson, treeDir, tokens = [], rawTexts = [] }) {
  const calls = agentCalls(events);
  const byAgent = effortByAgent(audit);
  return {
    cell: cellJson?.cell ?? null, arm: cellJson?.arm ?? null,
    result: resultEnvelope(events),
    agentCalls: calls,
    childrenPerModel: childrenPerModel(calls),
    denials: { modelGuard: calls.filter(c => c.denied === 'model-guard').length, effortPin: calls.filter(c => c.denied === 'effort-pin').length, other: calls.filter(c => c.denied === 'other-error').length },
    effortByAgent: byAgent,
    effortNotHigh: effortFlags(byAgent),
    mainSessionEffortRecorded: Boolean(byAgent[MAIN]),
    instructions: instructionsLoaded(audit, cellJson),
    edits: editsBy(audit),
    leakSuspects: leakSuspects(commandsFrom(audit, events), treeDir),
    canaryHits: canaryHits(rawTexts, tokens),
  };
}
