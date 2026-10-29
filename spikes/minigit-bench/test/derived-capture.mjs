// A capture-SHAPED input built from the real 2.1.289 two-children stream plus SYNTHETIC OTLP records (the OTLP encoding, request ids and
// times taken from the stream). It exercises the verdict rules' code paths; it is NOT the step 0 capture and proves nothing about 2.1.310.
import { agentTimeline } from '../../policy-ab-quick/lib/parallel.mjs';
import { streamView, transcriptView } from '../lib/claude-usage.mjs';
import { TWO_CHILDREN, apiAttrs, asTranscripts, otlpPayload, readEvents } from './synth.mjs';

const REAL_SPELLING = { main: 'sdk', subagent: 'agent:builtin:general-purpose' };

export function derivedCapture({ stamp = 'end', durationMs = 6000, contentType = 'application/json', withTranscripts = true, spelling = 'plain', parentAgent = true } = {}) {
  const events = readEvents(TWO_CHILDREN);
  const timeline = agentTimeline(events);
  const parentOf = new Map(events.filter(e => e.type === 'assistant' && e.parent_tool_use_id).map(e => [e.message.id, e.parent_tool_use_id]));
  const agentIdOf = id => timeline.find(t => t.id === parentOf.get(id))?.agentId ?? undefined;
  const transcripts = withTranscripts ? asTranscripts(events) : [];
  const messages = streamView(events).messages;
  const requests = messages.map(m => ({
    name: 'api_request',
    tsMs: stamp === 'end' ? m.lastTsMs + 50 : m.lastTsMs - durationMs,
    attrs: apiAttrs({
      model: m.model, query_source: spelling === 'real' ? REAL_SPELLING[m.source] : m.source, request_id: m.requestId, input_tokens: m.input, output_tokens: m.output, cache_read_tokens: m.cacheRead,
      cache_creation_tokens: m.cacheWrite, cost_usd: 0.001, duration_ms: durationMs,
      ...(m.source === 'subagent' ? { agent_id: agentIdOf(m.id), parent_agent_id: parentAgent ? 'root-agent' : undefined } : {}),
    }),
  }));
  const aux = { name: 'api_request', tsMs: messages[0].lastTsMs, attrs: apiAttrs({ model: 'claude-haiku-4-5-20251001', query_source: 'auxiliary', request_id: 'req_aux', cost_usd: 0.001493, duration_ms: 300 }) };
  const toolIds = events.filter(e => e.type === 'assistant').flatMap(e => e.message.content.filter(b => b.type === 'tool_use').map(b => b.id));
  const toolRecords = toolIds.map(id => ({ name: 'tool_result', tsMs: messages[0].lastTsMs, attrs: { tool_name: 'Glob', tool_use_id: id, duration_ms: 50, success: true } }));
  const body = JSON.stringify(otlpPayload([...requests, aux, ...toolRecords]));
  return {
    events, transcripts, sink: [{ receivedAt: 1, path: '/v1/logs', contentType, body }],
    hooks: toolIds.map((id, i) => ({ t: i, hook_event_name: 'PreToolUse', tool_name: 'x', tool_use_id: id, agent_id: null, session_id: 's' })),
    meta: { cliVersion: '0.0.0-derived', date: '2026-10-29T00:00:00.000Z', sessionId: 'derived', exitCode: 0, signal: null, wallMs: 1, costUsd: 0.594582 },
    argv: { exe: 'claude', argv: ['-p'], envKeys: ['HOME', 'PATH'] },
    tCount: transcriptView(transcripts).messages.length,
  };
}
