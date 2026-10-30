// A synthetic series for `aa-verdicts` (E3b.S4): the files the bare runner leaves (run-map, ledger, aliases, rehearsal-v2.json, the sampler file, and per session a record and the evidence
// files the verdicts re-read), every one GOOD, and `over` to break exactly one thing. Under the fake home (the caller imports fake-home.mjs first). Nothing here is a real session.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { tmpDir } from './helpers.mjs';

/** The 12 hex of the fixture series' harness identity (the run-map's is `'e'.repeat(64)`): what a G-12 replacing line must name. */
export const RUN_IDENTITY = 'e'.repeat(12);
export const T0 = Date.parse('2026-10-30T10:00:00Z');
const WALL_MS = 46200;
export const known = value => ({ known: true, value });
const unknown = why => ({ known: false, why });
const PASS = { status: 'pass', verdict: 'ok' };
const dir = (...p) => path.join(...p);
const lines = rows => `${rows.map(r => JSON.stringify(r)).join('\n')}\n`;

/** Deep merge of plain objects; an array or a scalar in `over` replaces. */
export function merge(base, over = {}) {
  const out = { ...base };
  for (const [k, v] of Object.entries(over)) out[k] = v && typeof v === 'object' && !Array.isArray(v) && base[k] && typeof base[k] === 'object' && !Array.isArray(base[k]) ? merge(base[k], v) : v;
  return out;
}

/** One good session record, the fields the verdicts read (the real record has more). */
export const goodRecord = ({ rid, stack = 'csharp', kind = 'aa' }) => ({
  ids: { pair: 0, stack, phase: 'v1', attempt: 1, rid, kind, label: 'paired' }, class: 'completed', data: true, flags: [], detail: { brokerRefused: { count: 0, backoffMs: 0 } },
  run: { maskObserved: known(true), quiet: known({ quiet: true, flags: [], numbers: { samples: 12 }, waitedMs: 0 }), spin: known({ runs: 30, medianMs: 22 }) },
  cost: {
    cliTotalUsd: known(0.2), brokerUsd: known(0.2), stopRuleUsd: known(0.2), headlineUsd: known({ usd: 0.2 }), censoredAtUsd: unknown('not censored'),
    checks: known(Object.fromEntries(['t1', 't2', 't3', 't4', 't5'].map(t => [t, PASS]))),
    aliases: known({ aliases: { opus: 'claude-opus-5-5' }, internal: [], fallbacks: [], unresolved: [], check: { verdict: 'ok', reasons: [] } }),
  },
  time: {
    wallMs: known(WALL_MS), modelApiMs: known(40000), activeMs: known(46100), activeMinusModelApiMs: known(6100),
    partition: known({ categories: { build_test: 6000, model_api: 40000, other_tool: 100, unattributed: 100 }, unattributed: 100, waits: 0 }),
    children: known({ launched: 0, maxConcurrent: 0, openEnded: 0, unknownStart: 0, perModel: {} }),
  },
  quality: { buildOk: known(true), visible: known([{ script: 'test-v1.sh', passed: 11, failed: 0, expectedTotal: 11, hundredPercent: true }]), heldout: known({ percent: 100, defects: 0 }), upstreamOriginal: known([]), latency: known({ medianMs: 5 }) },
  egress: known({ allowed: {}, refused: { 'api.anthropic.com': 12 }, nonConnect: 0, tlsRefused: {}, bytes: {}, bytesCaptured: true, cacheGrowthBytes: 1 }),
  packages: known({ packages: [], added: [], flagged: [] }), loc: known({ product: 100, test: 0, files: 1 }),
});

/** The evidence files of one session dir, as `loadSessionDir` reads them. `over.files` replaces a file's rows (or removes it with null). */
function writeSessionFiles(root, { rid, mask = '0-5', files = {} }) {
  const s = dir(root, 's', rid);
  const defaults = {
    'stream.ndjson': lines([{ type: 'system', subtype: 'init' }, { type: 'result', subtype: 'success', is_error: false }]),
    'stream.stamped.ndjson': lines([{ t: T0 + 1000, line: '{}' }, { t: T0 + 1000 + WALL_MS, line: '{}' }]),
    'readback.json': `${JSON.stringify({ ok: true })}\n`,
    'meta.json': `${JSON.stringify({ rid, startedAt: new Date(T0).toISOString(), endedAt: new Date(T0 + 50000).toISOString(), wallMs: 50000, stopped: null, samplers: { realInCmdline: 0, realInEnviron: 0 }, mask })}\n`,
    'broker.ndjson': lines([{ event: 'settled', status: 200, seq: 1 }]), 'egress.ndjson': lines([{ t: T0, host: 'api.anthropic.com' }]),
    'otel/otel-raw.ndjson': lines([{ name: 'api_request' }]), 'box/out/audit.ndjson': lines([{ event: 'PostToolUse' }]), 'transcripts/main.jsonl': lines([{ type: 'assistant' }]),
    'score/score.json': `${JSON.stringify({ build: { ok: true } })}\n`,
  };
  for (const [rel, text] of Object.entries({ ...defaults, ...files })) {
    if (text === null) continue;
    mkdirSync(path.dirname(dir(s, rel)), { recursive: true });
    writeFileSync(dir(s, rel), text);
  }
}

/** The sampler's file of the phase: one sample every 5 s over the sessions' window. */
const samplerLines = (every = 5000, spin = true) => lines(Array.from({ length: 11 }, (_, i) => ({ t: T0 + i * every, perCpu: [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0, 0, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2], foreignCpus: 0.1, ...(spin ? { spinMs: 22 } : {}) })));

const SESSIONS = [
  { rid: 'aa0001', stack: 'csharp', kind: 'aa', twin: 'a', mask: '0-5' },
  { rid: 'aa0002', stack: 'csharp', kind: 'aa', twin: 'b', mask: '8-13' },
  { rid: 'pi0001', stack: 'node', kind: 'pilot', twin: null, mask: '0-5' },
];

/**
 * Write a good series and return its directory. `over`: `records` ({ rid: partial record merged over the good one }), `files` ({ rid: { rel: text | null } }), `sessions` (the run-map rows to
 * keep; default the two twins and the pilot), `rehearsal` (the file's content, null for none), `sampler` ({ every, spin }), `aliases` (baseline), `ledgerSkip` (rids with no ledger line).
 */
export function makeSeries(over = {}) {
  const root = tmpDir('aa');
  const rows = over.sessions ?? SESSIONS;
  const map = {
    seriesId: 's-test', harnessIdentity: 'e'.repeat(64), pins: { claude: '2.1.310' }, caps: { capUsd: 6, wallMs: 3600000, stallMs: 2700000 }, bareBudgetUsd: 40, stopped: null,
    sessions: Object.fromEntries(rows.map(r => [r.rid, { rid: r.rid, pair: 0, stack: r.stack, phase: 'v1', attempt: 1, kind: r.kind, label: 'paired', mask: r.mask, twin: r.twin, status: 'scored', class: 'completed', data: true }])),
  };
  writeFileSync(dir(root, 'run-map.json'), `${JSON.stringify(map)}\n`);
  writeFileSync(dir(root, 'aliases.json'), `${JSON.stringify(over.aliases ?? { aliases: { opus: 'claude-opus-5-5' }, internal: [] })}\n`);
  writeFileSync(dir(root, 'ledger.ndjson'), lines(rows.filter(r => !(over.ledgerSkip ?? []).includes(r.rid)).map(r => ({ kind: r.kind === 'aa' ? 'void' : r.kind, usd: 0.2, what: 'completed', rid: r.rid }))));
  mkdirSync(dir(root, 'pairs'), { recursive: true });
  writeFileSync(dir(root, 'pairs', 'p0-v1-cpu.ndjson'), samplerLines(over.sampler?.every, over.sampler?.spin));
  if (over.rehearsal !== null) writeFileSync(dir(root, 'rehearsal-v2.json'), `${JSON.stringify(over.rehearsal ?? { holds: true, stacks: { csharp: { holds: true, checks: [] }, node: { holds: true, checks: [] } }, checks: [] })}\n`);
  for (const r of rows) {
    writeSessionFiles(root, { ...r, files: over.files?.[r.rid] });
    const rec = merge(goodRecord(r), over.records?.[r.rid]);
    writeFileSync(dir(root, 's', r.rid, 'record.json'), `${JSON.stringify(rec)}\n`);
  }
  return root;
}

/** An inputs folder with `amendments.md` (the dated lines) and the registered `quiet.json` (its interval). */
export function makeInputs(amendmentLines = []) {
  const root = tmpDir('aa-inputs');
  writeFileSync(dir(root, 'amendments.md'), `# Dated amendments\n\n${amendmentLines.join('\n')}\n`);
  writeFileSync(dir(root, 'quiet.json'), `${JSON.stringify({ intervalMs: 5000 })}\n`);
  return root;
}

/** The dated by-hand lines that make a good series GO: G-10 for a twin and the pilot, G-13 for each session, and G-12's replacing evidence. */
export const GOOD_LINES = Object.freeze([
  '2026-10-30 | G-10 | holds | aa0001 | scratch/aa0001-by-hand.txt | operator',
  '2026-10-30 | G-10 | holds | pi0001 | scratch/pi0001-by-hand.txt | operator',
  ...['aa0001', 'aa0002', 'pi0001'].map(rid => `2026-10-30 | G-13 | holds | ${rid} | scratch/${rid}-class.txt | operator`),
  `2026-10-30 | G-12 | holds | aa0001 | test/broker.test.mjs P-6 and test/budget.test.mjs green at harness ${RUN_IDENTITY} | operator`,
]);

/** The smoke seam: B-T1 holds on the same CLI. */
export const SMOKE_OK = Object.freeze({ ok: true, cli: '2.1.310', fixture: 'test/fixtures/real/claude-2.1.310-bare-smoke', observed: '1 main and 2 subagent transcript file(s); T3 pass, T4 pass' });
