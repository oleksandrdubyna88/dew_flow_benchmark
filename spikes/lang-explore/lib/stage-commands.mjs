// The verbs of Stage 1 on the command line: `stage-init` (FREE), `stage-run` (SPENDS MONEY unless a rehearsal series or `--dry-run`), `stage-status` (FREE, read only) (F3.S1 (a), PR 3), and the recovery path `stage-archive`, `stage-free` and `stage-restore`
// (all FREE, each under the series lock, PR 4). Each takes the parsed flags and an `io` and returns an exit code: 0 done | 2 a stop | 3 a resumable halt | 4 usage, configuration or a refusal | 5 failed.
import path from 'node:path';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { heldoutAware } from './heldout-commands.mjs';
import { readToken } from './run.mjs';
import { ledgerFileOf } from './series.mjs';
import { archiveSeries, freeSeries, restoreSeries } from './stage-archive.mjs';
import { stageInit } from './stage-init.mjs';
import { stageRun } from './stage-run.mjs';
import { stageStatusLines } from './stage-status.mjs';
import { amendmentsReport, amendmentsText, readGates } from './stage-gates.mjs';
import { unreadReadbackSlots, slotsOf } from './stage-schedule.mjs';
import { mapAsRead, openReasonOf, openStage } from './stage-step.mjs';
import { loadWorld } from './world.mjs';

const refused = (io, reason) => { io.say(reason); return 4; };

async function initCommand(values, io) {
  const r = await stageInit({ values, io });
  io.say(r.message);
  return r.exit;
}

export const cmdStageInit = heldoutAware(initCommand);

async function runCommand(values, io) {
  const run = { ...io, worldFn: io.worldFn ?? loadWorld, tokenFn: io.tokenFn ?? (async home => readToken(home)), acceptNoisy: values['accept-noisy'] ?? null };
  const r = await stageRun({ values, io: run });
  io.say(r.message);
  (r.problems ?? []).forEach(p => io.say(`record problem: ${p}`));
  if (r.pairFile) io.say(`pair record: ${r.pairFile}`);
  return r.exit;
}

export const cmdStageRun = heldoutAware(runCommand);

const statusLines = ({ series, text }) => stageStatusLines({ state: series.state, map: mapAsRead({ series, read: { text } }), ledger: readLedger(ledgerFileOf(series.dir)) });

/** `stage-status --series <dir>`: the rounds, the slots, the pilot and the five spend lines, over the run-map as `stage-run` reads it (a dated verdict applied, `mapAsRead`); an amendments file that cannot be read is a refusal, as it is for `stage-run`. */
export function cmdStageStatus(values, io) {
  if (!values.series) return refused(io, 'stage-status needs --series <dir>');
  const series = openStage({ seriesDir: path.resolve(values.series), io });
  const text = series.ok ? amendmentsText(io) : series;
  if (!text.ok) return refused(io, text.reason);
  statusLines({ series: series.value, text: text.value }).forEach(l => io.say(l));
  amendmentsReport(io).forEach(l => io.say(l));
  return 0;
}

// ---- the recovery path: stage-archive, stage-free, stage-restore (PR 4) ----------------------------------------------------------------------------------------------------------------------------------------------------------

/** The Stage 1 series `--series` names, opened as `stage-run` opens it. -> `ok(series)` | `fail`. */
const seriesOfValues = ({ values, io, verb }) => (values.series ? openStage({ seriesDir: path.resolve(values.series), io }) : fail(`${verb} needs --series <dir>`));

/** The exit of a verb of the recovery path over its result: the lines of a success, the reason of a refusal (4). */
function finish({ io, result, lines }) {
  if (!result.ok) return refused(io, result.reason);
  lines(result.value).forEach(l => io.say(l));
  return 0;
}

const archiveLines = e => [`archived ${e.files} file(s), ${e.bytes} bytes, to ${e.copy}; the list ${e.listSha256.slice(0, 12)} is ${e.listFile}; slots finished: ${e.slotsFinished.join(', ') || 'none'}`];

/** `stage-archive --series <dir> --to <backup folder>`: copy the whole series to the private backup, verify the copy against the sha256 list and record it in `archive.json`. Deletes nothing. A series that has ended (stopped or finished) is archived as the LAST archive, which refuses a freed session. */
export function cmdStageArchive(values, io) {
  const series = seriesOfValues({ values, io, verb: 'stage-archive' });
  if (!series.ok) return refused(io, series.reason);
  if (!values.to) return refused(io, 'stage-archive needs --to <backup folder>: the private backup the copy goes to');
  const dir = series.value.dir;
  return finish({ io, result: archiveSeries({ series: dir, to: path.resolve(values.to), now: io.now ?? systemClock, final: openReasonOf({ dir, io }) === null }), lines: archiveLines });
}

/** The read-back slots whose dated lines are not there yet (a slot `stage-free` leaves alone): `ok([slot])` | `fail` (gates that cannot be read). */
function unreadSlotsOf({ series, io }) {
  const read = readGates({ series, io });
  if (!read.ok) return read;
  const { seed, seedRevealed, readbackLines } = read.value.gates;
  return ok(unreadReadbackSlots({ map: mapAsRead({ series, read: read.value }), slots: seedRevealed ? slotsOf({ seed }) : [], readbackLines }).map(row => row.slot));
}

const freeLines = r => [`freed ${r.removed.length} file(s), ${r.bytes} bytes, of ${r.sessions.length} session(s)`, ...r.kept.map(k => `kept ${k.path}: ${k.why}`)];

/** `stage-free --series <dir>`: remove the three cache folders of final sessions in finished slots, each file only when the verified archive proves it; a read-back slot waits for its dated lines. */
export function cmdStageFree(values, io) {
  const series = seriesOfValues({ values, io, verb: 'stage-free' });
  const unread = series.ok ? unreadSlotsOf({ series: series.value, io }) : series;
  if (!unread.ok) return refused(io, unread.reason);
  return finish({ io, result: freeSeries({ series: series.value.dir, unreadSlots: unread.value, now: io.now ?? systemClock }), lines: freeLines });
}

const restoreLines = r => [`restored ${r.restored.length} session(s): ${r.restored.join(', ') || 'none'}`];

/** `stage-restore --series <dir> [--rid <rid>]`: copy the freed folders of one session, or of every freed session, back from the archive, verified against `freed.json`'s list. */
export function cmdStageRestore(values, io) {
  const series = seriesOfValues({ values, io, verb: 'stage-restore' });
  if (!series.ok) return refused(io, series.reason);
  return finish({ io, result: restoreSeries({ series: series.value.dir, rid: values.rid }), lines: restoreLines });
}
