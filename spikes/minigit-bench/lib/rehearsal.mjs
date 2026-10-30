// The free v2 rehearsal (E3b.S4, G-14; review m4): `run --what rehearse-v2`. The v2 path (the workspace filled from a v1 tree, the second-phase launch, the scorer at phase v2, the records) has
// never run before the first real v2 session, and a defect there would cost a paid cell. So it runs ONCE for free on an A/A twin's tree: the REAL box, broker, proxy and scorer, with a STUB
// CLI (`stub-cli.mjs`) talking to a loopback stub upstream (`stub-upstream.mjs`) and a FAKE token. Nothing is paid, the operator's token file is never read, no canary is planted in the
// operator's folders, nothing is measured (no quiet check, no spend gate) and the ledger line is a `rehearsal` of $0. G-14 is computed, never typed, and written to `<series>/rehearsal-v2.json`.
import { randomBytes } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { agentEntries, readAgentFileUnder } from './agent-files.mjs';
import { pilotRid } from './bare-plan.mjs';
import { leafProblems } from './bare-record.mjs';
import { readSessionRecord, writeJson } from './bare-records-io.mjs';
import { EXIT, runLoop } from './bare-series.mjs';
import { sha256Of } from './inputs.mjs';
import { ok } from './result.mjs';
import { planSession, writeRunMap } from './run-map.mjs';
import { layoutPaths } from './sandbox.mjs';
import { stubClaude } from './stub-cli.mjs';
import { chunksOf, sseAnswer, sseBody, stubUpstream } from './stub-upstream.mjs';
import { excludedDirs } from './score-deps.mjs';
import { pinnedInputsChanged } from './workspace.mjs';

/** The A/A twin whose v1 tree the rehearsal starts from: the first C# twin of the A/A that ended with data. `null` when there is none. */
export const twinOf = map => Object.values(map.sessions).find(s => s.kind === 'aa' && s.stack === 'csharp' && s.phase === 'v1' && s.data === true && (s.status === 'ran' || s.status === 'scored')) ?? null;

export const rehearsalRefusal = map => (twinOf(map) ? null : 'rehearse-v2 needs an A/A twin that ended with data: run `--what aa` first (the rehearsal uses its v1 tree)');

// ---- G-14 -----------------------------------------------------------------------------------------------------------------------

const V2_INPUTS = Object.freeze(['SPEC-v2.txt', 'test-v2.sh']);
const RECORD_SECTIONS = Object.freeze(['run', 'cost', 'time', 'quality', 'process', 'loc', 'egress', 'packages', 'workspace']);

/** `{ [rel]: sha256 }` of the regular files under `root`, skipping directories named in `skip` at any depth. */
function fileHashes(root, skip = []) {
  const rows = agentEntries(root).filter(e => e.kind === 'file' && !e.rel.split(path.sep).slice(0, -1).some(seg => skip.includes(seg)));
  return Object.fromEntries(rows.map(e => { const bytes = readAgentFileUnder(root, e.rel); return [e.rel, Buffer.isBuffer(bytes) ? sha256Of(bytes) : `unreadable: ${bytes?.unreadable ?? 'absent'}`]; }));
}

/** G-14, check 1: the v2 workspace is the twin's tree minus the stack's exclusion (the files byte for byte), plus exactly the pinned v2 inputs. -> `{ id, ok, detail }`. */
function workspaceCheck({ stack, twinWork, v2Work, hashes }) {
  const expected = fileHashes(twinWork, excludedDirs(stack));
  const actual = fileHashes(v2Work);
  const pinned = pinnedInputsChanged({ work: v2Work, hashes, phase: 'v2' }).filter(c => V2_INPUTS.includes(c.path)).map(c => c.path);
  const problems = [
    ...Object.keys(expected).filter(f => !(f in actual)).map(f => `${f} of the twin is missing from the v2 workspace`),
    ...Object.keys(actual).filter(f => !(f in expected) && !V2_INPUTS.includes(f)).map(f => `${f} is in the v2 workspace but is excluded or not in the twin's tree`),
    ...Object.keys(expected).filter(f => f in actual && actual[f] !== expected[f]).map(f => `${f} differs from the twin's file`),
    ...V2_INPUTS.filter(f => !(f in actual)).map(f => `${f} is absent`),
    ...pinned.map(f => `${f} is not the pinned bytes`),
  ];
  return { id: 'workspace', ok: problems.length === 0, detail: problems.length === 0 ? `${Object.keys(actual).length} file(s): the twin's tree minus ${excludedDirs(stack).join('/ ')}/ plus ${V2_INPUTS.join(', ')}` : problems.join('; ') };
}

function recordCheck(record) {
  const problems = record ? RECORD_SECTIONS.flatMap(s => (record[s] === undefined ? [`${s}: absent`] : leafProblems(record[s], s))) : ['no record'];
  return { id: 'record', ok: problems.length === 0, detail: problems.length === 0 ? 'every field is known, not captured with a reason, or n/a (bare)' : problems.join('; ') };
}

/** G-14, check 3: `score.json` at v2 has every step known: the build, both visible scripts of phase v2, the held-out slot and the upstream references. */
function scoreCheck(score) {
  const scripts = new Set((score?.visible ?? []).map(v => v.script));
  const problems = !score ? ['no score.json'] : [
    ...(typeof score.build?.ok === 'boolean' ? [] : ['the build step is not known']),
    ...['test-v2.sh', 'test-v1.sh'].filter(s => !scripts.has(s)).map(s => `the visible script ${s} was not scored`),
    ...(score.heldout?.known === true ? [] : [`the held-out step is not known${score.heldout?.why ? ` (${score.heldout.why})` : ''}`]),
    ...(Array.isArray(score.upstream) ? [] : ['the upstream references were not scored']),
  ];
  return { id: 'score', ok: problems.length === 0, detail: problems.length === 0 ? 'build, visible v2 and v1, held-out and upstream are all known' : problems.join('; ') };
}

/** `{ holds, checks: [{ id, ok, detail }] }` for the three things G-14 says: the workspace, the stub v2 session's record, and `score.json` at v2. */
export function g14Verdict({ stack, twinWork, v2Work, hashes, record, score }) {
  const checks = [workspaceCheck({ stack, twinWork, v2Work, hashes }), recordCheck(record), scoreCheck(score)];
  return { holds: checks.every(c => c.ok), checks };
}

// ---- the run ----------------------------------------------------------------------------------------------------------------------

/**
 * Run the rehearsal over the real wiring (`ctx`, `deps` from `buildRun`): plan one `rehearsal` session (C#, v2) whose v1 tree is the twin's, run the loop with the stub CLI and a fake token,
 * compute G-14, write `<series>/rehearsal-v2.json`. -> `{ exit, message, verdict?, map }`. The loop's quiet check and spend gate are skipped for this kind (nothing is measured or paid).
 */
export async function rehearseV2({ ctx, deps, map, say = () => {}, signal, random = () => randomBytes(4).toString('hex') }) {
  const twin = twinOf(map);
  if (!twin) return { exit: EXIT.config, message: rehearsalRefusal(map), map };
  const dir = path.join(ctx.seriesDir, 'rehearsal');
  mkdirSync(dir, { recursive: true });
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  try {
    // a rehearsal that stopped before it launched (the gates, say) leaves its session `planned`: the next run REUSES it (review N15), so a rid is not planned per attempt and no leftover is launched
    const waiting = Object.values(map.sessions).find(s => s.kind === 'rehearsal' && s.status === 'planned');
    const rid = waiting?.rid ?? pilotRid(random());
    const planned = waiting ? ok(map) : planSession(map, { rid, pair: 0, stack: 'csharp', phase: 'v2', attempt: 1, kind: 'rehearsal', label: 'paired', mask: '0-5', twin: `rehearse-${twin.rid}` });
    if (!planned.ok) return { exit: EXIT.failed, message: planned.reason, map };
    if (!waiting) writeRunMap(ctx.runMapFile, planned.value);
    const rehearsalCtx = {
      ...ctx, kind: 'rehearsal', pair: 0, rehearsalOf: twin.rid, token: `sk-ant-fake-${randomBytes(8).toString('hex')}`, claudeBinary: stubClaude(dir), aliasStore: null, aliasBaseline: null,
      handVerdictsFor: () => [], planOver: { upstream: stub.upstream, tickMs: 500, sampleMs: 500, settleMs: 500, graceMs: 2000 },
    };
    const rehearsalDeps = { ...deps, phase: { ...deps.phase, listProjectDirs: () => [] } };
    const r = await runLoop({ ctx: rehearsalCtx, deps: rehearsalDeps, map: planned.value, signal, say });
    if (r.exit !== EXIT.done) return r;
    const record = readSessionRecord(rehearsalCtx, rid);
    const scorePath = path.join(rehearsalCtx.sessionsRoot, rid, 'score', 'score.json');
    const score = (() => { try { return JSON.parse(readFileSync(scorePath, 'utf8')); } catch { return null; } })();
    const verdict = g14Verdict({ stack: 'csharp', twinWork: layoutPaths(ctx.sessionsRoot, twin.rid).work, v2Work: layoutPaths(ctx.sessionsRoot, rid).work, hashes: ctx.hashes, record, score });
    writeJson(path.join(ctx.seriesDir, 'rehearsal-v2.json'), { rid, twin: twin.rid, ...verdict, at: new Date().toISOString() });
    for (const c of verdict.checks) say(`G-14 ${c.id}: ${c.ok ? 'holds' : 'REFUTED'}: ${c.detail}`);
    return { exit: verdict.holds ? EXIT.done : EXIT.failed, message: verdict.holds ? 'G-14 holds: the v2 path ran end to end for free' : 'G-14 is refuted: fix the v2 path and rehearse again (free)', verdict, map: r.map };
  } finally {
    await stub.close();
  }
}
