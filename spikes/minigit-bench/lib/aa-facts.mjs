// What `aa-verdicts` reads (E3b.S4), at the edge: the series' run-map, ledger, aliases baseline and rehearsal file, the dated lines of `amendments.md`, and for each session that counts its
// record and the evidence files a record does not carry (the read-back, the stamped stream, the sampler's file). Read only; every session directory goes through `loadSessionDir`, the one
// loader that refuses a link. Nothing here decides a verdict (aa-rules.mjs does).
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { parseAmendments } from './amendments.mjs';
import { readLedger } from './ledger.mjs';
import { fail, ok } from './result.mjs';
import { readRunMap } from './run-map.mjs';
import { loadSessionDir } from './smoke-load.mjs';

const jsonOr = (file, fallback) => {
  try { return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : fallback; } catch { return fallback; }
};

const latest = list => list.toSorted((a, b) => a.attempt - b.attempt).at(-1) ?? null;

/** The sessions the G table is read from: each A/A twin's latest attempt, and the Node pilot's latest. (A rehearsal session is a stub: it is G-14's, never a subject here.) */
export function subjectEntries(map) {
  const all = Object.values(map.sessions ?? {});
  const twins = Object.values(Object.groupBy(all.filter(s => s.kind === 'aa'), s => s.twin ?? s.rid)).map(latest);
  const pilots = all.filter(s => s.kind === 'pilot');
  return { aa: twins, pilot: pilots.length > 0 ? [latest(pilots)] : [] };
}

/** The lines of an ndjson file that parse; `[]` for a file that is not there. */
function ndjson(file) {
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').split('\n').filter(Boolean).flatMap(l => { try { return [JSON.parse(l)]; } catch { return []; } });
}

/** The phase sampler's samples that fall inside one session's window (`meta.startedAt` to `meta.endedAt`). */
function samplesIn({ seriesDir, entry, meta }) {
  const [from, to] = [Date.parse(meta?.startedAt), Date.parse(meta?.endedAt)];
  const all = ndjson(path.join(seriesDir, 'pairs', `p${entry.pair}-${entry.phase}-cpu.ndjson`));
  return all.filter(s => s.t >= from && s.t <= to);
}

/** One session's facts: its entry, its record (null when absent), what its directory holds (null when it cannot be loaded), the sampler window and whether a `score.json` exists. */
function factsOf({ seriesDir, entry }) {
  const dir = path.join(seriesDir, 's', entry.rid);
  const loaded = loadSessionDir(dir);
  const value = loaded.ok ? loaded.value : null;
  return { rid: entry.rid, entry, record: jsonOr(path.join(dir, 'record.json'), null), loaded: value, loadWhy: loaded.ok ? null : loaded.reason, samples: samplesIn({ seriesDir, entry, meta: value?.meta }), hasScore: existsSync(path.join(dir, 'score', 'score.json')) };
}

/** The committed smoke fixture's B-T1 on the same CLI, or null when it cannot be read. `compute` is `computeSmokeVerdicts` over the loaded fixture (the edge supplies it). */
export function smokeEvidence({ fixtureDir, compute }) {
  try {
    const row = compute(fixtureDir).find(r => r.id === 'B-T1');
    return row ? { ok: row.verdict === 'holds', cli: JSON.parse(readFileSync(path.join(fixtureDir, 'meta.json'), 'utf8')).cliVersion, fixture: `test/fixtures/real/${path.basename(fixtureDir)}`, observed: row.observed } : null;
  } catch { return null; }
}

/** `ok(facts)` for a series, or `fail(reason)` when the run-map is not there. `inputsDir` holds `amendments.md` and `quiet.json`. */
export function readSeriesFacts({ seriesDir, inputsDir }) {
  const map = readRunMap(path.join(seriesDir, 'run-map.json'));
  if (!map.ok) return fail(map.reason);
  const amendmentsFile = path.join(inputsDir, 'amendments.md');
  const subjects = subjectEntries(map.value);
  return ok({
    seriesDir, map: map.value, ledger: readLedger(path.join(seriesDir, 'ledger.ndjson')), baseline: jsonOr(path.join(seriesDir, 'aliases.json'), null),
    rehearsal: jsonOr(path.join(seriesDir, 'rehearsal-v2.json'), null), quiet: jsonOr(path.join(inputsDir, 'quiet.json'), {}),
    amendments: parseAmendments(existsSync(amendmentsFile) ? readFileSync(amendmentsFile, 'utf8') : ''),
    subjects: { aa: subjects.aa.map(entry => factsOf({ seriesDir, entry })), pilot: subjects.pilot.map(entry => factsOf({ seriesDir, entry })) },
  });
}
