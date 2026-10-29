// The B-* assumptions of the bare-session smoke (E2.S2) as pure rules over a loaded smoke: each returns
// `{ verdict: 'holds' | 'refuted' | 'not exercised', observed }`. `observed` carries the numbers or names the verdict rests on.
// SMOKE.md's table is `renderVerdicts` of `computeSmokeVerdicts` over the SANITISED fixture, and a test compares the two byte for byte.
import { renderVerdicts } from '../capture/verdicts.mjs';
import { RULES, viewsOf } from '../capture/verdict-rules.mjs';
import { brokerView } from './broker.mjs';
import { crosscheck } from './crosscheck.mjs';
import { EGRESS_HOSTS, KNOWN_REFUSED } from './egress-hosts.mjs';
import { egressSummary } from './egress.mjs';
import { OVER_CAP_FACTOR } from './budget.mjs';
import { liveSumOf } from './otel-receiver.mjs';
import { INIT_UNREPORTED } from './sandbox.mjs';
import { partition, spansFrom } from './timeline.mjs';

export const SMOKE_CAP_USD = 0.2;
export const BEGIN = '<!-- verdicts:begin -->';

export const SMOKE_ASSUMPTIONS = Object.freeze([
  ['B-R1', 'the init read-back matches every field of step 6'],
  ['B-R2', 'init.tools lacks exactly INIT_UNREPORTED'],
  ['B-N1', 'every allowed CONNECT names a host of EGRESS_HOSTS.bare; every refused host is in KNOWN_REFUSED or is named in the report'],
  ['B-N2', 'the proxy log holds no non-CONNECT line from the CLI; the only plain-HTTP traffic is the box\'s own loopback'],
  ['B-S2', 'every API call went through the broker: broker.ndjson holds every api_request O saw, and api.anthropic.com was never allowed'],
  ['B-S3', 'the box held only the dummy: the real token was counted 0 times in cmdline and environ over at least 5 samples, token.txt holds the dummy or nothing, no file of the out dir holds the real token'],
  ['B-$1', 'B.spentUsd = E.total_cost_usd within $0.000001 (T5), and B = O\'s usd'],
  ['B-O1', 'OTel records reached the receiver through the socket; T2 passes'],
  ['B-T1', 'box/config/projects holds main and one subagent transcript; T3 and T4 pass under crosscheck-policy.json'],
  ['B-E1', 'an envelope; T1 passes; cost at most $0.20'],
  ['B-A1', 'audit Pre/Post pair by tool_use_id; partition sums to the wall to the ms; unpaired is empty'],
  ['B-A2', 'the PostToolUse payload carries duration_ms'],
  ['B-A3', 'every O api_request carries effort, the child\'s (by agent_id) is high, and SubagentStart carries the same agent_id'],
  ['B-H1', 'hook events still carry no timestamp (E1 A-H1)'],
  ['B-K1', 'after exit no host process carries the cell marker, the janitor ended 0, state.json.phase is closed, no socket file is left'],
  ['B-K2', '(only when a session was stopped by endCell) a record for the in-flight request arrived after the SIGTERM'],
  ['B-C1', 'contaminationHits and tokenHits over the smoke: none'],
]);

const holds = observed => ({ verdict: 'holds', observed });
const refuted = observed => ({ verdict: 'refuted', observed });
const notExercised = observed => ({ verdict: 'not exercised', observed });
const verdictOf = (ok, observed) => (ok ? holds(observed) : refuted(observed));
const canonical = n => (n === 'Task' ? 'Agent' : n);
const names = list => [...new Set(list)].join(', ') || 'none';

/** Everything the rules read, computed once. */
export function smokeViews(smoke, ctx) {
  const v = viewsOf({ events: smoke.events, sink: smoke.sink, hooks: smoke.audit, transcripts: smoke.transcripts, meta: smoke.meta, argv: smoke.argv }, ctx);
  const B = brokerView(smoke.brokerLines);
  const c = crosscheck({ E: v.E, O: v.O, T: v.T, B, table: ctx.table, policy: ctx.policy, cliVersion: v.cliVersion });
  const built = spansFrom({ otel: v.O, audit: smoke.audit, stamped: smoke.stamped });
  const t = smoke.stamped.map(s => s.t);
  const wall = t.length > 1 ? { startMs: Math.min(...t), endMs: Math.max(...t) } : null;
  return { ...v, smoke, B, c, built, part: wall ? partition(wall, built.spans) : { known: false, why: 'no stamped stream' }, egress: egressSummary(smoke.egressLines), apiRecords: v.records.filter(r => r.name === 'api_request') };
}

const initOf = v => v.events.find(e => e.type === 'system' && e.subtype === 'init');
const claudeToolsOf = v => (v.smoke.argv.claude ?? []).at(-1)?.split(',') ?? [];

const ruleR2 = v => {
  const reported = new Set((initOf(v)?.tools ?? []).map(t => canonical(typeof t === 'string' ? t : t?.name)));
  const missing = claudeToolsOf(v).map(canonical).filter(t => !reported.has(t));
  const ok = missing.length === INIT_UNREPORTED.length && missing.every(t => INIT_UNREPORTED.includes(t));
  return verdictOf(ok, `--tools asked for ${claudeToolsOf(v).length}, init reported ${reported.size}; not reported: ${names(missing)} (INIT_UNREPORTED: ${INIT_UNREPORTED.join(', ')})`);
};

const ruleN1 = v => {
  const badAllowed = Object.keys(v.egress.allowed).filter(h => !EGRESS_HOSTS.bare.includes(h));
  const unknownRefused = Object.keys(v.egress.refused).filter(h => !KNOWN_REFUSED.includes(h));
  const observed = `allowed: ${JSON.stringify(v.egress.allowed)}; refused: ${JSON.stringify(v.egress.refused)}; allowed outside the bare list: ${names(badAllowed)}; refused outside KNOWN_REFUSED: ${names(unknownRefused)}`;
  return verdictOf(badAllowed.length === 0 && unknownRefused.length === 0, observed);
};

const ruleS2 = v => {
  const o = liveSumOf(v.apiRecords.map(r => ({ ...r, receivedAt: 0 }))).requests;
  const apiAllowed = Object.keys(v.egress.allowed).includes('api.anthropic.com');
  return verdictOf(o > 0 && v.B.forwarded >= o && !apiAllowed, `O saw ${o} api_request record(s); the broker forwarded ${v.B.forwarded} and settled ${v.B.settled.length}; api.anthropic.com allowed at the proxy: ${apiAllowed}`);
};

/**
 * What the session's own `printenv CLAUDE_CODE_OAUTH_TOKEN > token.txt` may have left: the dummy (the spec's prediction), or NOTHING. Corrected
 * 2026-10-30 from the smoke itself: CLI 2.1.310's Bash tool does not pass the credential variable to its subprocesses, so `printenv` exits 1
 * and the file is empty. Empty is safe; anything else, the real token included, is not.
 */
const tokenTxtIsSafe = t => t?.present === true && (t.equalsDummy === true || t.bytes === 0);
const tokenTxtWord = t => (!t?.present ? 'absent' : t.equalsDummy ? 'holds the dummy' : t.bytes === 0 ? 'is empty (the CLI\'s Bash tool scrubs the variable: printenv found nothing to write)' : `holds ${t.bytes} byte(s) that are not the dummy`);

const ruleS3 = v => {
  const s = v.smoke.meta.samplers;
  const k = v.smoke.checks;
  const ok = s.samples >= 5 && s.realInCmdline === 0 && s.realInEnviron === 0 && tokenTxtIsSafe(k.tokenTxt) && (k.realTokenScan?.hits ?? ['unknown']).length === 0;
  return verdictOf(ok, `${s.samples} samples: real in cmdline ${s.realInCmdline}, in environ ${s.realInEnviron}; dummy in environ ${s.dummyInEnviron}; token.txt ${tokenTxtWord(k.tokenTxt)}; real-token hits over ${k.realTokenScan?.scanned} file(s): ${k.realTokenScan?.hits?.length}`);
};

const rule$1 = v => {
  const oUsd = liveSumOf(v.apiRecords.map(r => ({ ...r, receivedAt: 0 }))).usd;
  const ok = v.c.t5.status === 'pass' && Math.abs(v.B.spentUsd - oUsd) <= 0.000001;
  return verdictOf(ok, `T5 ${v.c.t5.status}: ${v.c.t5.detail ?? v.c.t5.why}; B ${v.B.spentUsd}, O ${oUsd}`);
};

const ruleT1 = v => {
  const files = v.smoke.transcripts.map(t => t.relPath);
  const subagent = files.filter(f => f.includes('subagents/')).length;
  const main = files.length - subagent;
  return verdictOf(main >= 1 && subagent >= 1 && v.c.t3.status === 'pass' && v.c.t4.status === 'pass', `${main} main and ${subagent} subagent transcript file(s); T3 ${v.c.t3.status}${v.c.t3.why ? ` (${v.c.t3.why})` : ''}, T4 ${v.c.t4.status}${v.c.t4.why ? ` (${v.c.t4.why})` : ''}`);
};

const ruleA1 = v => {
  const sum = v.part.known ? Object.values(v.part.categories).reduce((a, b) => a + b, 0) : null;
  const ok = v.built.unpaired.length === 0 && v.part.known && sum === v.part.wallMs && v.smoke.audit.length > 0;
  return verdictOf(ok, `${v.smoke.audit.length} audit record(s); unpaired ${v.built.unpaired.length}; partition ${v.part.known ? `sums to ${sum} ms of a ${v.part.wallMs} ms wall` : v.part.why}`);
};

const ruleA2 = v => {
  const posts = v.smoke.audit.filter(r => r.event === 'PostToolUse');
  if (posts.length === 0) return notExercised('no PostToolUse record in the audit');
  const withDuration = posts.filter(r => Number.isFinite(r.duration_ms)).length;
  return verdictOf(withDuration === posts.length, `${withDuration} of ${posts.length} PostToolUse record(s) carry duration_ms; where absent, spans come from the Pre/Post timestamps (E1's method) beside O's tool_result.duration_ms`);
};

const ruleA3 = v => {
  const recs = v.apiRecords;
  const children = recs.filter(r => r.attrs.agent_id);
  const starts = v.smoke.audit.filter(r => r.event === 'SubagentStart');
  const joined = children.every(r => starts.some(s => s.agent_id === r.attrs.agent_id));
  const ok = recs.length > 0 && recs.every(r => r.attrs.effort) && children.length > 0 && children.every(r => r.attrs.effort === 'high') && joined;
  return verdictOf(ok, `${recs.length} api_request record(s), efforts ${names(recs.map(r => r.attrs.effort ?? 'absent'))}; ${children.length} from a subagent (by agent_id); SubagentStart records ${starts.length}; every child joined: ${joined}`);
};

const ruleK1 = v => {
  const k = v.smoke.checks;
  const ok = k.survivors === 0 && v.smoke.meta.janitorEnded === 0 && k.statePhase === 'closed' && k.socketsLeft === 0;
  return verdictOf(ok, `processes carrying the marker after exit ${k.survivors}; janitor ended ${v.smoke.meta.janitorEnded}; state.json.phase ${k.statePhase}; socket files left ${k.socketsLeft}`);
};

const ruleK2 = v => {
  if (v.smoke.meta.stopped === null) return notExercised('the smoke ended by itself: no endCell, so no SIGTERM to measure');
  const stopAt = v.smoke.checks.stoppedAt ?? null;
  const after = stopAt === null ? 0 : v.smoke.brokerLines.filter(l => l.event === 'settled' && Date.parse(l.t) > stopAt).length;
  return verdictOf(after > 0, `stopped by ${v.smoke.meta.stopped}; broker settled lines after the SIGTERM: ${after}`);
};

export const SMOKE_RULES = Object.freeze({
  'B-R1': v => verdictOf(v.smoke.readback?.ok === true, v.smoke.readback?.ok ? 'every checked field matched (cwd, tools, mcp_servers, permissionMode, version, apiKeySource, memory path, model)' : `mismatches: ${JSON.stringify(v.smoke.readback?.mismatches ?? 'no read-back')}`),
  'B-R2': ruleR2,
  'B-N1': ruleN1,
  'B-N2': v => verdictOf(v.egress.nonConnect === 0, `${v.egress.nonConnect} non-CONNECT line(s) at the proxy`),
  'B-S2': ruleS2,
  'B-S3': ruleS3,
  'B-$1': rule$1,
  'B-O1': v => verdictOf(v.O.present === true && v.c.t2.status === 'pass', `${v.apiRecords.length} api_request record(s) decoded; T2 ${v.c.t2.status}${v.c.t2.why ? ` (${v.c.t2.why})` : ''}`),
  'B-T1': ruleT1,
  'B-E1': v => verdictOf(v.E.known && v.c.t1.status === 'pass' && v.E.totalCostUsd <= SMOKE_CAP_USD, v.E.known ? `total_cost_usd ${v.E.totalCostUsd} (cap ${SMOKE_CAP_USD}, the 1.1 x gate ${SMOKE_CAP_USD * OVER_CAP_FACTOR}); T1 ${v.c.t1.status}` : `no usable envelope: ${v.E.why}`),
  'B-A1': ruleA1,
  'B-A2': ruleA2,
  'B-A3': ruleA3,
  'B-H1': v => RULES['A-H1'](v),
  'B-K1': ruleK1,
  'B-K2': ruleK2,
  'B-C1': v => verdictOf(v.smoke.checks.contamination?.length === 0 && v.smoke.checks.tokenHits?.length === 0, `contaminationHits ${v.smoke.checks.contamination?.length}, tokenHits ${v.smoke.checks.tokenHits?.length}`),
});

/** One row per assumption: `{ id, assumption, verdict, observed }`. A rule that throws is a defect, not a verdict. */
export function computeSmokeVerdicts(smoke, ctx) {
  const views = smokeViews(smoke, ctx);
  return SMOKE_ASSUMPTIONS.map(([id, assumption]) => ({ id, assumption, ...SMOKE_RULES[id](views) }));
}

/** The whole SMOKE.md. Nothing in the table is typed. */
export function renderSmoke({ meta, rows, command }) {
  const counts = rows.reduce((acc, r) => ({ ...acc, [r.verdict]: (acc[r.verdict] ?? 0) + 1 }), {});
  return [
    `# Bare-session smoke - claude ${meta.cliVersion ?? 'unknown version'}, ${String(meta.startedAt).slice(0, 10)}`,
    '',
    'One bare session through the agent box, the egress proxy and the credential broker (E2.S2), kept sanitised as a replay fixture. Every figure below is computed by `lib/smoke-verdicts.mjs`; nothing is typed.',
    '',
    `- Command: \`${command}\``,
    `- Session id: ${meta.sessionId ?? 'unknown'}`,
    `- Envelope cost: ${meta.envelopeCostUsd === null ? 'unknown' : `$${meta.envelopeCostUsd}`} - listed as pilot spend (parent plan section 13); the broker's ledger: $${meta.broker.spentUsd}`,
    `- Outcome: ${meta.outcome.label}; wall ${meta.wallMs} ms; stopped: ${meta.stopped ?? 'no'}`,
    `- Verdicts: ${Object.entries(counts).map(([k, n]) => `${n} ${k}`).join(', ')}`,
    '',
    '## Verdicts',
    '',
    renderVerdicts(rows),
    '',
  ].join('\n');
}
