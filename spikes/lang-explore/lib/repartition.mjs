// `explore.mjs repartition --series <dir>` (F1.S1 (b)): FREE. The time partition of every kept session of a series, recomputed from the evidence it retained (`stream.stamped.ndjson`, `otel/`, `meta.json` and the audit log)
// with v1's classifier as it is now, by v1's own path (`loadSessionDir`, `smokeViews`: the spans and the partition, never a second partition). The old categories are read from each `record.json`, which is never changed. The result
// is one new file in `<series>/partition-recomputed/` (see repartition-store.mjs); a run over unchanged inputs finds the complete result of the same fingerprint, reports its path and writes nothing. A session whose evidence is not
// there is listed with its reason, never as a zero.
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { loadSessionDir } from '../../minigit-bench/lib/smoke-load.mjs';
import { smokeViews } from '../../minigit-bench/lib/smoke-verdicts.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { V1 } from './paths.mjs';
import { RESULT_DIR, findComplete, sweepTemps, writeResult } from './repartition-store.mjs';

export { RESULT_DIR };

/** The v1 files the partition is made by: the classifier first. Their bytes are part of the fingerprint. */
const CODE_FILES = Object.freeze(['lib/classify.mjs', 'lib/spans.mjs', 'lib/timeline.mjs', 'lib/categories.mjs']);
/** The evidence of a session that the partition reads; all four must be regular files. */
const EVIDENCE = Object.freeze(['stream.stamped.ndjson', 'otel/otel-raw.ndjson', 'meta.json', 'box/out/audit.ndjson']);
const RECORD = 'record.json';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const isRegular = file => lstatSync(file, { throwIfNoEntry: false })?.isFile() === true;
const sum = categories => Object.values(categories).reduce((a, b) => a + b, 0);

/** `{ rel: sha256 }` of the regular files among `rels` under `dir`, and the names of those that are not there or not regular. */
function hashesOf(dir, rels) {
  const present = rels.filter(rel => isRegular(path.join(dir, rel)));
  return { hashes: Object.fromEntries(present.map(rel => [rel, sha256(readFileSync(path.join(dir, rel)))])), missing: rels.filter(rel => !present.includes(rel)) };
}

const oldOf = record => {
  const part = record?.time?.partition;
  return part?.known === true ? { known: true, categories: part.value.categories } : { known: false, why: part?.why ?? 'the record has no partition' };
};

/** The session's `record.json` (read, never written): `ok(record)` | `fail(reason)`. */
function readRecord(dir) {
  try {
    return ok(JSON.parse(readFileSync(path.join(dir, RECORD), 'utf8')));
  } catch (error) {
    return fail(`${RECORD} could not be read: ${error.message}`);
  }
}

/** The recomputed session, or a reason. Anything the loaders refuse (a link, a malformed line) is a reason, not a throw. */
function recompute({ dir, policy }) {
  try {
    const loaded = loadSessionDir(dir);
    if (!loaded.ok) return fail(loaded.reason);
    const part = smokeViews(loaded.value, policy).part;
    return part.known ? ok({ wallMs: part.wallMs, categories: part.categories }) : fail(`the partition is unknown: ${part.why}`);
  } catch (error) {
    return fail(`the evidence could not be read: ${error.message}`);
  }
}

/** One session folder -> `{ session | skipped, hashes }`. */
function sessionOf({ dir, rid, policy }) {
  const evidence = hashesOf(dir, [...EVIDENCE, RECORD]);
  const skip = why => ({ skipped: { rid, why }, hashes: evidence.hashes });
  if (evidence.missing.length > 0) return skip(`not recomputed, missing or not a regular file: ${evidence.missing.join(', ')}`);
  const record = readRecord(dir);
  const made = record.ok ? recompute({ dir, policy }) : record;
  if (!made.ok) return skip(`not recomputed, ${made.reason}`);
  const session = { rid, stack: record.value.ids?.stack ?? null, old: oldOf(record.value), new: made.value, sumsToWall: sum(made.value.categories) === made.value.wallMs };
  return { session, hashes: evidence.hashes };
}

const codeOf = () => Object.fromEntries(CODE_FILES.map(rel => [rel, sha256(readFileSync(path.join(V1, rel)))]));

/** The fingerprint of the inputs: the classifier and its neighbours' bytes and every session's evidence bytes, in a fixed order. */
const fingerprintOf = (code, evidence) => sha256(JSON.stringify({ code, evidence: Object.keys(evidence).sort().map(rid => [rid, evidence[rid]]) }));

const ridsOf = seriesDir => {
  const sessions = path.join(seriesDir, 's');
  return lstatSync(sessions, { throwIfNoEntry: false })?.isDirectory() === true ? readdirSync(sessions, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => e.name).sort() : null;
};

/** The body of a result: complete, its fingerprint, the code hashes, and per session the stack, the old and the new categories and whether the new ones sum to the wall. */
function bodyOf({ seriesDir, code, fingerprint, made, now }) {
  return {
    complete: true, fingerprint, generatedAt: now().toISOString(), series: path.basename(seriesDir),
    classifier: { file: 'spikes/minigit-bench/lib/classify.mjs', sha256: code['lib/classify.mjs'] }, codeSha256: code,
    sessions: made.filter(m => m.session).map(m => m.session), skipped: made.filter(m => m.skipped).map(m => m.skipped),
  };
}

/**
 * Recompute the partitions of the series `seriesDir`. `now` and `genId` are seams (the clock; the 8 hex digit generation id), `owned` is `ownedFor({ token: 'bench' })`.
 * -> `ok({ file, wrote, sessions, skipped, stale: { removed, live, unknown } })` | `fail(reason)`.
 */
export async function repartitionSeries({ seriesDir, now = systemClock, genId, owned = ownedFor({ token: 'bench' }) }) {
  const rids = ridsOf(seriesDir);
  if (rids === null) return fail(`${seriesDir} is not a series folder: it holds no s/ folder of sessions`);
  const policy = crosscheckPolicyFrom(V1, {});
  if (!policy.ok) return policy;
  const made = rids.map(rid => sessionOf({ dir: path.join(seriesDir, 's', rid), rid, policy: policy.value }));
  const code = codeOf();
  const fingerprint = fingerprintOf(code, Object.fromEntries(rids.map((rid, i) => [rid, made[i].hashes])));
  const folder = path.join(seriesDir, RESULT_DIR);
  const stale = sweepTemps(folder, owned);
  const existing = findComplete(folder, fingerprint);
  const body = bodyOf({ seriesDir, code, fingerprint, made, now });
  const file = existing ?? writeResult({ folder, body, now, genId, owned });
  return ok({ file, wrote: existing === null, sessions: body.sessions.length, skipped: body.skipped, stale });
}
