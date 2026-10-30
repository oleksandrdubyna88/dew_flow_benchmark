// The stream's tool_use blocks as the contamination scan's `calls`: `[{ agent: 'main' | 'child', tool, input }]`. One definition, shared by the smoke (smoke-run.mjs) and
// the bare runner (bare-collect.mjs); moved out of smoke-run.mjs in E3b after E2.S2 merged.

export function callsOf(events) {
  return events.filter(e => e.type === 'assistant').flatMap(e => (e.message?.content ?? [])
    .filter(b => b.type === 'tool_use').map(b => ({ agent: e.parent_tool_use_id ? 'child' : 'main', tool: b.name, input: b.input ?? {} })));
}
