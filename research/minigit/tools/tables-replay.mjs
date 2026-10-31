// Generated blocks that read data/replay.json (written by replay.mjs): the contamination scan replayed over every recorded stream.
import { DASH, int, short, stackName, table } from './format.mjs';

const total = (xs, k) => xs.reduce((a, x) => a + x[k], 0);

/** Per series: sessions replayed, real model sessions, high and low hits, and the lib files the replay ran with. */
export function replaySummary(replay) {
  const s = replay.summary;
  const rows = s.perSeries.map(p => [short(p.series), int(p.sessions), int(p.realModel), int(p.high), `${p.withHigh} (${p.realWithHigh} real model)`, int(p.low), int(p.withLow), int(p.calls)]);
  rows.push(['all', int(s.sessionsReplayed), int(s.realModelSessions), int(total(s.perSeries, 'high')), `${total(s.perSeries, 'withHigh')} (${s.realModelWithHigh} real model)`, int(total(s.perSeries, 'low')), int(total(s.perSeries, 'withLow')), int(total(s.perSeries, 'calls'))]);
  const lib = Object.entries(replay.extractedFrom.lib).map(([f, h]) => `${f} ${h}`).join(', ');
  return [
    table(['series', 'sessions', 'real model', 'high hits', 'sessions with a high hit', 'low hits', 'sessions with a low hit', 'calls'], rows),
    '',
    `Sessions in a run-map with no \`stream.ndjson\`: ${s.sessionsSkipped}. Real model sessions not recorded as \`completed\` or \`void: contaminated\`: ${s.notCompleted.length ? s.notCompleted.map(n => `${short(n.series)} ${n.rid} (${n.recordedClass})`).join(', ') : 'none'}. Lib files hashed (sha256, first 12): ${lib}.`,
  ].join('\n');
}

/** The sessions the run-map recorded as `void: contaminated`, beside what the current scan finds in the same stream. */
export function replayVoids(replay) {
  const s = replay.summary;
  return table(
    ['series', 'rid', 'stack', 'phase', 'recorded class', 'high hits now', 'low hits now'],
    s.recordedVoids.map(v => [short(v.series), v.rid, stackName(v.stack), v.phase ?? DASH, v.recordedClass, int(v.high), int(v.low)]),
  );
}
