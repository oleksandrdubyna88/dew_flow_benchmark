// Writing the records of the bare runner (E3b.S1 step 9): `<session>/record.json` for a session and `<series>/pairs/p<k>.json` for a pair. The views are recomputed
// from the session's own files, so a record can be rebuilt after a crash or a fix without launching anything; nothing in a record is typed.
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { layoutPaths } from './sandbox.mjs';
import { ok } from './result.mjs';
import { cellOf, pairRecord, sessionRecord } from './bare-record.mjs';
import { collectSession } from './bare-collect.mjs';
import { treeSize } from './cache-snapshot.mjs';
import { locOfTree } from './loc.mjs';
import { packagesOf, readNpmPackages, readNugetPackages } from './packages.mjs';

const writeJson = (file, value) => {
  mkdirSync(path.dirname(file), { recursive: true });
  const tmp = `${file}.${process.pid}.tmp`;
  writeFileSync(tmp, `${JSON.stringify(value, null, 2)}\n`);
  renameSync(tmp, file);
};

/** Cache bytes after the session minus the registered snapshot bytes (nuget + npm + the dotnet first-run part): the cross-check of the download bytes. `null` when the run knows no snapshot size. */
const cacheGrowthOf = ({ ctx, layout }) => (ctx.snapshotBytes == null ? null : treeSize(layout.nuget) + treeSize(layout.npm) + treeSize(path.join(layout.home, '.dotnet')) - ctx.snapshotBytes);

/** Rebuild and write one session's record from its directory. -> `ok(record)` | the collect's `fail`. `entry` is the run-map entry; `score` the session's score.json or null. */
export function writeSessionRecord({ ctx, entry, score }) {
  const layout = layoutPaths(ctx.sessionsRoot, entry.rid);
  const collected = collectSession({
    layout, plan: { token: ctx.token, dummy: entry.dummy }, launched: null, canaryTokens: entry.canaryTokens ?? [], aliasBaseline: ctx.aliasStore ? ctx.aliasStore.get() : ctx.aliasBaseline, handVerdicts: ctx.handVerdictsFor(entry.rid),
    retired: false, abortReason: entry.abortReason ?? null, partnerDirs: entry.partnerDirs ?? [], table: ctx.table, policy: ctx.policy, home: ctx.home,
  });
  if (!collected.ok) return collected;
  const c = collected.value;
  const packages = packagesOf({ npm: readNpmPackages(layout.npm), nuget: readNugetPackages(layout.nuget), snapshot: ctx.snapshotPackages, rules: ctx.packageRules });
  // The class is the RUN-MAP's (decided when the session ended, from the evidence and the dated lines of that time): a record rebuilt later never re-classifies it. A recomputation that
  // disagrees (a hand verdict dated since) is reported as a discrepancy beside the class and flagged, for the operator to act on through the schedule.
  const discrepancies = entry.class !== undefined && entry.class !== c.outcome.class
    ? [{ kind: 'class', runMap: entry.class, recomputed: c.outcome.class, why: c.outcome.detail?.reason ?? 'the evidence now reads differently' }] : [];
  const outcome = {
    ...c.outcome, class: entry.class ?? c.outcome.class, data: entry.data ?? c.outcome.data,
    flags: [...c.outcome.flags, ...(entry.run?.flags ?? []), ...(entry.workspaceFlags ?? []), ...(entry.scoreFlags ?? []), ...packages.flags, ...(discrepancies.length > 0 ? ['class discrepancy'] : []), ...(entry.reclassified ? ['reclassified by hand'] : [])],
  };
  const record = sessionRecord({
    ids: { pair: entry.pair, stack: entry.stack, phase: entry.phase, attempt: entry.attempt, rid: entry.rid, kind: entry.kind, label: entry.label },
    outcome, run: { ...entry.run, harnessIdentity: entry.run?.harnessIdentity ?? ctx.harnessIdentity, preregTag: ctx.preregTag, cliVersion: c.meta.cliVersion ?? ctx.cliVersion },
    views: c.views, cost: c.cost, aliasRecord: c.alias, aliasCheck: c.aliasCheck, score, cacheGrowthBytes: cacheGrowthOf({ ctx, layout }), loc: existsSync(layout.work) ? locOfTree(layout.work) : null, packages, meta: c.meta, discrepancies, reclassified: entry.reclassified ?? null, workspace: entry.workspaceExcluded ? { phase: entry.phase, excludedDirs: entry.workspaceExcluded } : null,
  });
  writeJson(path.join(layout.dir, 'record.json'), record);
  return ok(record);
}

/** The record of a session read back from disk, or null. */
export const readSessionRecord = (ctx, rid) => {
  const file = path.join(ctx.sessionsRoot, rid, 'record.json');
  return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null;
};

/** The session that counts for a (stack, phase): the latest attempt with data, else the latest attempt. */
export function finalSession(map, pair, stack, phase) {
  const all = Object.values(map.sessions).filter(s => s.pair === pair && s.kind === 'data' && s.stack === stack && s.phase === phase).sort((a, b) => a.attempt - b.attempt);
  return all.findLast(s => s.data === true) ?? all.at(-1) ?? null;
}

/** Write `pairs/p<k>.json` from the sessions' records. -> the pair record. */
export function writePairRecord({ ctx, map, pair }) {
  const stacks = ['csharp', 'node'];
  const rec = (stack, phase) => { const s = finalSession(map, pair, stack, phase); return s ? readSessionRecord(ctx, s.rid) : null; };
  const cells = Object.fromEntries(stacks.map(s => [s, cellOf(rec(s, 'v1'), rec(s, 'v2'))]));
  const entries = Object.values(map.sessions).filter(s => s.pair === pair && s.kind === 'data');
  const pairEntry = map.pairs?.[pair] ?? {};
  const record = pairRecord({
    pair, cells,
    masks: Object.fromEntries(entries.map(e => [e.rid, e.run?.mask?.value ?? e.run?.mask ?? null])),
    spawnStamps: Object.fromEntries(entries.map(e => [e.rid, e.run?.spawnStampMs ?? null])),
    skewsMs: Object.fromEntries(entries.filter(e => e.run?.startSkewMs != null).map(e => [e.rid, e.run.startSkewMs])),
    barrierAtMs: pairEntry.barrierAtMs ?? null,
    labels: Object.fromEntries(entries.map(e => [e.rid, { stack: e.stack, phase: e.phase, attempt: e.attempt, label: e.label, class: e.class ?? null }])),
    quality: Object.fromEntries(['v1', 'v2'].map(ph => [ph, Object.fromEntries(stacks.map(s => [s, rec(s, ph)?.quality ?? null]))])),
    flags: [...new Set(entries.flatMap(e => e.run?.flags ?? []))],
  });
  writeJson(path.join(ctx.seriesDir, 'pairs', `p${pair}.json`), record);
  return record;
}

export { writeJson };
