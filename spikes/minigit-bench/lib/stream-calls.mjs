// The stream's tool_use blocks as the contamination scan's `calls`: `[{ agent: 'main' | 'child', agentId?, id?, tool, input, reset? }]`. One definition, shared by the smoke (smoke-run.mjs) and
// the bare runner (bare-collect.mjs); moved out of smoke-run.mjs in E3b after E2.S2 merged.
// The scan also reads the Bash tool's working directory across calls (contamination-session-cwd.mjs), so a call carries what places it: `agentId` (the stream's `parent_tool_use_id`: each subagent keeps its own shell
// directory) and `reset` (the directory the call's result says the CLI reset the shell to: RECORDED for the report only, never read by a judgement: a command can print that line). A field the stream does not hold is absent.

const RESET = /(?:^|\n)Shell cwd was reset to ([^\n]+?)\s*$/;

const textOf = content => (typeof content === 'string' ? content : (content ?? []).map(b => b?.text ?? '').join('\n'));

/** The results of the stream's tool calls: `Map<tool_use_id, text>`. */
const resultsOf = events => new Map(events.filter(e => e.type === 'user' && Array.isArray(e.message?.content))
  .flatMap(e => e.message.content.filter(b => b.type === 'tool_result').map(b => [b.tool_use_id, textOf(b.content)])));

const resetOf = text => (text === undefined ? undefined : RESET.exec(text)?.[1]);

const agentPart = e => (e.parent_tool_use_id ? { agent: 'child', agentId: e.parent_tool_use_id } : { agent: 'main' });
const optional = (key, value) => (value ? { [key]: value } : {});
const callOf = (e, b, results) => ({ ...agentPart(e), ...optional('id', b.id), tool: b.name, input: b.input ?? {}, ...optional('reset', resetOf(results.get(b.id))) });

export function callsOf(events) {
  const results = resultsOf(events);
  return events.filter(e => e.type === 'assistant').flatMap(e => (e.message?.content ?? []).filter(b => b.type === 'tool_use').map(b => callOf(e, b, results)));
}
