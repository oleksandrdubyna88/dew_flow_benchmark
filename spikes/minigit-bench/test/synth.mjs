// Test plumbing for the Claude counters: the real two-children stream, and views of it as transcript files.
// Everything built here is derived from a REAL fixture or labelled SYNTHETIC where a file holds it; none of it stands in for
// the step 0 capture (`test/fixtures/real/claude-2.1.310-capture/`).
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spikeDir } from './helpers.mjs';

export const TWO_CHILDREN = fileURLToPath(new URL('../../../tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-two-children-models.ndjson', import.meta.url));
export const REFUSAL_FALLBACK = fileURLToPath(new URL('../../../tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-model-refusal-fallback.ndjson', import.meta.url));
export const CAPTURE_DIR = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-capture');
export const SKIP_CAPTURE = 'awaits the E1.S3 step 0 capture';

export const parseLines = text => text.split('\n').filter(l => l.trim() !== '').map(l => JSON.parse(l));
export const readEvents = file => parseLines(readFileSync(file, 'utf8'));
export const readFixtureEvents = name => readEvents(path.join(spikeDir, 'test', 'fixtures', name));
export const hasCapture = () => existsSync(path.join(CAPTURE_DIR, 'CAPTURE.md'));

/** A test that needs the capture calls this first: it skips with the one reason until the capture is committed. */
export function captureGate(t) {
  if (hasCapture()) return true;
  t.skip(SKIP_CAPTURE);
  return false;
}

/** The stream's assistant events as transcript files: main -> `<sid>.jsonl`, each child -> `<sid>/subagents/agent-<id>.jsonl`. */
export function asTranscripts(events, sid = 'sess-1') {
  const lines = events.filter(e => e.type === 'assistant').map(e => ({ ...e, requestId: e.request_id }));
  const main = lines.filter(e => !e.parent_tool_use_id);
  const children = Map.groupBy(lines.filter(e => e.parent_tool_use_id), e => e.parent_tool_use_id);
  const file = (relPath, evs) => ({ relPath, text: evs.map(e => JSON.stringify(e)).join('\n') + '\n' });
  return [
    file(`${sid}.jsonl`, main),
    ...[...children].map(([id, evs]) => file(`${sid}/subagents/agent-${id.slice(-8)}.jsonl`, evs)),
  ];
}

/** A copy of the events with `fn` applied to each (a new array; the events are not mutated). */
export const mapEvents = (events, fn) => events.map(e => fn(structuredClone(e)));

// ---- OTLP/HTTP JSON payloads (SYNTHETIC: the OTLP JSON encoding, with the attribute names of the plan's A-O1 row) --------------

const anyValue = v => {
  if (typeof v === 'string') return { stringValue: v };
  if (typeof v === 'boolean') return { boolValue: v };
  if (Number.isInteger(v)) return { intValue: String(v) };
  return { doubleValue: v };
};

/** One OTLP log record; `attrs` is a plain object (an `undefined` value omits the attribute), `name` goes in the body, ints are encoded as strings as OTLP JSON does. */
export function otlpRecord({ name, tsMs, attrs }) {
  return {
    timeUnixNano: String(BigInt(tsMs) * 1_000_000n),
    severityNumber: 9,
    body: { stringValue: `claude_code.${name}` },
    attributes: [{ key: 'event.name', value: { stringValue: name } }, ...Object.entries(attrs).filter(([, v]) => v !== undefined).map(([key, v]) => ({ key, value: anyValue(v) }))],
  };
}

export const otlpPayload = records => ({
  resourceLogs: [{
    resource: { attributes: [{ key: 'service.name', value: { stringValue: 'claude-code' } }] },
    scopeLogs: [{ scope: { name: 'com.anthropic.claude_code.events' }, logRecords: records.map(otlpRecord) }],
  }],
});

/** A complete api_request attribute set; override what a test varies. */
export const apiAttrs = (over = {}) => ({
  model: 'claude-opus-5-5', input_tokens: 10, output_tokens: 20, cache_read_tokens: 300, cache_creation_tokens: 40, cost_usd: 0.01,
  duration_ms: 1500, query_source: 'main', request_id: 'req_1', 'session.id': 'sess-1', ...over,
});
