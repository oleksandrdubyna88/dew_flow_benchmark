// Pure: what the resolver layout adds to a cell's record. In this layout the rules reach a session through the tree's own
// SessionStart hook (load-instructions.mjs -> the conventions' resolver), not through native rule discovery, so the loaded
// context is not trimmed and "the rule was reached" is a covariate with two independent signals, not a single fact.
const slashes = p => String(p ?? '').replace(/\\/g, '/');

/** The first main-session assistant message's usage: its cache write is the context this arm's rules cost at session start. */
export function firstTurnContext(events) {
  const first = events.find(e => e.type === 'assistant' && !e.parent_tool_use_id && e.message?.usage);
  if (!first) return { present: false };
  const u = first.message.usage;
  const cacheCreation = u.cache_creation_input_tokens ?? 0;
  const cacheRead = u.cache_read_input_tokens ?? 0;
  const input = u.input_tokens ?? 0;
  return { present: true, model: first.message.model ?? null, cacheCreation, cacheRead, input, total: cacheCreation + cacheRead + input };
}

const isSessionStart = ev => ev.type === 'system' && /^hook_(started|response)$/.test(ev.subtype ?? '')
  && (ev.hook_event === 'SessionStart' || /SessionStart/.test(ev.hook_name ?? ''));

/** Signal 1: the SessionStart hook's output as the stream reports it (needs --include-hook-events). */
export function sessionStartSignal(events, ruleName = 'subagent-models') {
  const started = events.filter(e => isSessionStart(e) && e.subtype === 'hook_started');
  const responses = events.filter(e => isSessionStart(e) && e.subtype === 'hook_response');
  const output = responses.map(r => String(r.output ?? r.stdout ?? '')).join('\n');
  return {
    hookEventsPresent: started.length + responses.length > 0,
    responses: responses.length,
    outputChars: output.length,
    mentionsRule: output.includes(ruleName),
    incomplete: output.includes('Instruction loading is INCOMPLETE'),
    exitCodes: responses.map(r => r.exit_code ?? null),
  };
}

const READS = new Set(['Read']);
const SHELLS = new Set(['Bash', 'PowerShell']);

/** Signal 2: a tool call that opened the rule file itself, plus resolver invocations (explain/read) the session ran. */
export function ruleReadSignal(calls, mount, ruleRel = 'common/subagent-models.md') {
  const suffix = `${slashes(mount)}/${ruleRel}`.toLowerCase();
  const reads = calls.filter(c => READS.has(c.tool) && slashes(c.input?.file_path).toLowerCase().endsWith(suffix));
  const shellMentions = calls.filter(c => SHELLS.has(c.tool) && slashes(c.input?.command).toLowerCase().includes(suffix));
  const resolverRuns = calls.filter(c => SHELLS.has(c.tool) && /rules\.mjs\s+(explain|read)\b/.test(c.input?.command ?? ''));
  return {
    readOfRule: reads.length > 0, readAgents: [...new Set(reads.map(c => c.agent))].sort(),
    shellMentionsOfRule: shellMentions.length, resolverRuns: resolverRuns.length,
  };
}

/** The covariate: reached by either signal. Both stay in the record. */
export function resolverRecord(events, calls, mount) {
  const sessionStart = sessionStartSignal(events);
  const ruleRead = ruleReadSignal(calls, mount);
  return {
    mount, sessionStart, ruleRead,
    ruleReached: sessionStart.mentionsRule || ruleRead.readOfRule || ruleRead.shellMentionsOfRule > 0,
    context: firstTurnContext(events),
  };
}
