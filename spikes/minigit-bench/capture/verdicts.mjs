// The A-* table of a capture, computed and rendered. Pure. Nothing here is typed by hand: CAPTURE.md's table is `renderVerdicts` of
// `computeVerdicts` over the sanitised files, and a test compares the two byte for byte.
import { RULES, viewsOf } from './verdict-rules.mjs';

export const ASSUMPTIONS = Object.freeze([
  ['A-E1', 'modelUsage carries every child\'s model; usage is main-only'],
  ['A-E2', 'sum of modelUsage costUSD = total_cost_usd within $0.01'],
  ['A-S1', 'the stream repeats a message per content block; output is a placeholder'],
  ['A-T1', 'transcripts: a main file and one subagent file per child, each line with id, model, usage and the 5m/1h split'],
  ['A-T2', 'transcript output_tokens are final (within 1 % of modelUsage)'],
  ['A-J1', 'T and O name the same request: transcript request id = api_request request_id'],
  ['A-O1', 'OTLP/HTTP JSON on /v1/logs with an api_request record carrying the plan\'s attribute names'],
  ['A-O2', 'an api_request record\'s time marks the END of the request'],
  ['A-O3', 'O summed over all sources equals E per model and kind (0.5 % / 100 tokens, cost 1 %)'],
  ['A-O4', 'sum of O cost_usd = total_cost_usd within 1 %'],
  ['A-O5', 'child requests carry agent_id and parent_agent_id; agent_id = the Agent result\'s agentId'],
  ['A-H1', 'hook_started/hook_response carry hook_id and no timestamp; hook tool_use_ids are the stream\'s'],
  ['A-H2', 'with OTEL_LOG_TOOL_DETAILS=1, tool_result records carry tool_use_id and duration_ms'],
  ['A-M1', 'init.model, resolvedModel and modelUsage keys are exact ids; canonicalModel is present'],
  ['A-P1', 'the CLI bills every model at published rates, except the cache-read rate observed for this CLI version (none = published)'],
]);

export const BEGIN = '<!-- verdicts:begin -->';
export const END = '<!-- verdicts:end -->';

/** One row per assumption: `{ id, assumption, verdict, observed, decision? }`. A rule that throws is a defect, not a verdict. */
export function computeVerdicts(capture, ctx) {
  const views = viewsOf(capture, ctx);
  return ASSUMPTIONS.map(([id, assumption]) => ({ id, assumption, ...RULES[id](views) }));
}

const cell = s => String(s).replaceAll('|', '\\|').replace(/\s*\n\s*/g, ' ');

/** The markdown block that goes into CAPTURE.md, between two marker comments. */
export function renderVerdicts(rows) {
  const body = rows.map(r => `| ${r.id} | ${cell(r.assumption)} | ${r.verdict} | ${cell(r.observed)} |`);
  return [BEGIN, '| Id | Assumption | Verdict | Observed |', '|---|---|---|---|', ...body, END].join('\n');
}

/** The values the capture decides, for the policy file and the OTEL_STAMP_MARKS / HOOK_TIME_SOURCE constants. `null` = the capture did not decide it. */
export function decisionsOf(rows) {
  const row = id => rows.find(r => r.id === id);
  const t2 = row('A-T2').verdict;
  return {
    join: row('A-J1').verdict === 'holds' ? 'request_id' : 'query_source',
    t3OutputSource: t2 === 'holds' ? 'transcript' : t2 === 'refuted' ? 'otel' : null,
    stampMarks: row('A-O2').decision ?? 'unknown',
    hookTimeSource: row('A-H1').decision ?? 'unknown',
  };
}

/** The whole CAPTURE.md. */
export function renderCapture({ meta, argv, command, sanitiserSha256, rows }) {
  const counts = rows.reduce((acc, r) => ({ ...acc, [r.verdict]: (acc[r.verdict] ?? 0) + 1 }), {});
  return [
    `# Capture - claude ${meta.cliVersion ?? 'unknown version'}, ${String(meta.date).slice(0, 10)}`,
    '',
    'One tiny session on an unrelated task (E1.S3 step 0), kept sanitised as a replay fixture. Every figure below is computed by `capture/verdicts.mjs`; nothing is typed.',
    '',
    `- CLI version: ${meta.cliVersion ?? 'unknown'} (from the init event)`,
    `- Date: ${meta.date}`,
    `- Session id: ${meta.sessionId ?? 'unknown'}`,
    `- Command: \`${command}\``,
    `- CLI argv: \`claude ${argv.argv.join(' ')}\` (the prompt is on stdin)`,
    `- Environment keys (values never recorded): ${argv.envKeys.join(', ')}`,
    `- Cost: ${meta.costUsd === null ? 'unknown (no result event)' : `$${meta.costUsd}`} - listed as pilot spend (parent plan section 13)`,
    `- Wall: ${meta.wallMs} ms, exit code ${meta.exitCode}`,
    `- Sanitiser sha256: ${sanitiserSha256}`,
    `- Verdicts: ${Object.entries(counts).map(([k, n]) => `${n} ${k}`).join(', ')}`,
    '',
    '## Verdicts',
    '',
    renderVerdicts(rows),
    '',
  ].join('\n');
}

/** The verdict block of a CAPTURE.md text, or null. */
export function verdictBlockOf(text) {
  const from = text.indexOf(BEGIN);
  const to = text.indexOf(END);
  return from === -1 || to === -1 ? null : text.slice(from, to + END.length);
}
