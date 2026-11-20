// What `stage-run` reads before it chooses a unit (F3.S1 (a), PR 3): the registered amendments (the dated lines of `stage-amendments.mjs`), the revealed seed and the guard. A rehearsal series is free and has its own seed in its state; a paid series needs a
// dated `pilot | GO` line for its data slots, the revealed seed (`inputs/prereg/seed.txt`, which must hash to the series' `seedSha256`) and the owner's go line for what it launches. The guard of a paid launch is PR 4's `stageGuard` (the frozen record, the task
// repository at it, every identity against the manifest) with PR 5's in its `deps` (`stage-prereg-guard.mjs`: the tag, the registered blobs, the clean tree, the harness identity, the seed for a data slot, the harness reader and the dated lines
// that accept a harness). Before PR 6 there is no manifest and no tag, so a paid series is refused with PR 5's own named problems. A rehearsal's guard passes by itself.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS, REPO } from './paths.mjs';
import { STAGE_AMENDMENTS_FILE, ownerGoLine, parseStageAmendments, pilotGoProblem, readStageAmendments, readbackLinesOf, reportLinesOf } from './stage-amendments.mjs';
import { STACK_ORDER, isRehearsal, seriesInputs } from './series.mjs';
import { stageGuard } from './stage-guard.mjs';
import { preregDeps } from './stage-prereg-guard.mjs';
import { READBACK_ROW, slotsOf } from './stage-schedule.mjs';

/** The revealed seed: `seed.txt` of the registered `prereg` folder, written by `seed-reveal` (PR 5) after the pilot. */
export const STAGE_SEED_FILE = path.join(INPUTS, 'prereg', 'seed.txt');

const sha256 = text => createHash('sha256').update(text).digest('hex');
const seam = (io, name, fallback) => io.stage?.[name] ?? fallback;

/** The task repository the series was made over (`stage-init` recorded its checkout in `inputs.json`), or `undefined` when the inputs do not say (the guard then asks for `LANG_V2_TASK_REPO`). */
const recordedTaskRepo = dir => { const inputs = seriesInputs(dir); return inputs.ok ? inputs.value.taskRepo?.root : undefined; };

/** The registered amendments' text for the guard, or '' when they cannot be read (`readGates` refuses an unreadable file before a guard is asked). */
const textForGuard = io => { const read = amendmentsText(io); return read.ok ? read.value : ''; };

/**
 * The arguments of `stageGuard` for a paid launch of `series`: this checkout, the series' task repository and PR 5's `deps` (`preregGuard`, the `harness` reader, `harnessAccepted`, `preregDeps`). `io.stage.guardArgs` replaces `repo` and
 * `taskRepo` and each of the `deps` by name (the seams of the tests: `preregGuard`, `readers`, `harnessAccepted`, `manifest`, `live`, `pinnedTaskSha`, `env`; a `deps` key set to `undefined` removes the default).
 */
function guardArgsOf({ series, io }) {
  const { deps = {}, ...over } = io.stage?.guardArgs ?? {};
  return { repo: REPO, taskRepo: recordedTaskRepo(series.dir), ...over, deps: { ...preregDeps({ series, text: textForGuard(io) }), ...deps } };
}

/** The guard of a paid launch: `stageGuard` (PR 4) over this checkout and the series' own task repository, with PR 5's guard in its `deps`: `({ paid, requireSeed }) -> ok(null) | fail(reason, { problems })`. */
export const stageGuardOf = ({ series, io }) => ({ paid, requireSeed }) => stageGuard({ ...guardArgsOf({ series, io }), series: series.dir, paid, requireSeed });

/** The guard of a series: a rehearsal's (free) passes; a paid series' is the one handed in (`io.stage.guardFn`), else PR 4's `stageGuard`. */
export const guardOf = ({ series, io }) => (isRehearsal(series.dir) ? async () => ok(null) : seam(io, 'guardFn', stageGuardOf({ series, io })));

/** The revealed seed: `ok(seed)`, `ok(null)` when it is not revealed yet, or a `fail` when the file is not the committed seed (it must hash to the series' `seedSha256`). */
function revealedSeed({ series, io }) {
  const file = seam(io, 'seedFile', STAGE_SEED_FILE);
  if (!existsSync(file)) return ok(null);
  const seed = readFileSync(file, 'utf8').trim();
  return sha256(seed) === series.state.seedSha256 ? ok(seed) : fail(`${file} does not hash to the series' seedSha256 (${String(series.state.seedSha256).slice(0, 12)}): it is not the seed that was committed`);
}

/** The read-back lines of a rehearsal: no judge reads it, so every session of a read-back slot counts as read back (`holds`), and the free round is not stopped at its read-back slot. */
const rehearsalLines = seed => slotsOf({ seed }).filter(s => s.readback).flatMap(s => STACK_ORDER.flatMap(stack => s.rids[stack].map(rid => ({ date: '2026-01-01', g: READBACK_ROW, verdict: 'holds', rid, evidence: 'rehearsal', who: 'rehearsal' }))));

/** The gates of a rehearsal series: its own seed, no pilot GO to wait for, and read-back lines that say `holds`. */
const rehearsalGates = series => ({ seed: series.state.rehearsalSeed, seedRevealed: true, pilotGo: true, readbackLines: rehearsalLines(series.state.rehearsalSeed) });

/** The text of the registered amendments file (`ok('')` when there is none): the one read `readGates`, the report and `stage-status` share. -> `ok(text)` | `fail`. */
export const amendmentsText = io => readStageAmendments(seam(io, 'amendmentsFile', STAGE_AMENDMENTS_FILE));

/** The report lines of the registered amendments (malformed lines of the four kinds, home paths): `stage-run` and `stage-status` say them. A file that cannot be read has none here (`readGates` refuses it). */
export function amendmentsReport(io) {
  const read = amendmentsText(io);
  return read.ok ? reportLinesOf({ text: read.value, home: io.home }) : [];
}

/** The gates of a paid series from the dated lines and the seed file. -> `ok(gates)` | `fail`. */
function paidGates({ series, io, lines, text }) {
  const seed = revealedSeed({ series, io });
  if (!seed.ok) return seed;
  return ok({ seed: seed.value, seedRevealed: seed.value !== null, pilotGo: pilotGoProblem({ lines }) === null, readbackLines: readbackLinesOf(text) });
}

/**
 * What the unit's choice reads: `ok({ text, lines, gates: { seed, seedRevealed, pilotGo, readbackLines } })` | `fail`. The registered amendments file is read again at every call (a dated line merged since counts at once).
 */
export function readGates({ series, io }) {
  const read = amendmentsText(io);
  if (!read.ok) return read;
  const lines = parseStageAmendments(read.value);
  if (isRehearsal(series.dir)) return ok({ text: read.value, lines, gates: rehearsalGates(series) });
  const gates = paidGates({ series, io, lines, text: read.value });
  return gates.ok ? ok({ text: read.value, lines, gates: gates.value }) : gates;
}

/** The owner's go for a launch: `ok(null)` for a rehearsal (free), else `ok(line)` or the refusal naming the scope. The scope of a data slot is its round; a pilot step's is the pilot. */
export function ownerGoFor({ series, unit, lines }) {
  if (isRehearsal(series.dir)) return ok(null);
  return ownerGoLine({ lines, scope: unit.kind === 'data' ? { round: unit.round } : { pilot: true } });
}
