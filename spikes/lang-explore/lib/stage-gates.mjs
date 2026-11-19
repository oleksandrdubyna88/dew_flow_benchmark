// What `stage-run` reads before it chooses a unit (F3.S1 (a), PR 3): the registered amendments (the dated lines of `stage-amendments.mjs`), the revealed seed and the guard. A rehearsal series is free and has its own seed in its state; a paid series needs a
// dated `pilot | GO` line for its data slots, the revealed seed (`inputs/prereg/seed.txt`, which must hash to the series' `seedSha256`) and the owner's go line for what it launches. The guard of a paid launch is PR 4's and PR 5's: THE DEFAULT REFUSES,
// so a Stage 1 series never runs with a guard that always passes.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS } from './paths.mjs';
import { STAGE_AMENDMENTS_FILE, ownerGoLine, parseStageAmendments, pilotGoProblem, readStageAmendments, readbackLinesOf } from './stage-amendments.mjs';
import { isRehearsal } from './series.mjs';

/** The revealed seed: `seed.txt` of the registered `prereg` folder, written by `seed-reveal` (PR 5) after the pilot. */
export const STAGE_SEED_FILE = path.join(INPUTS, 'prereg', 'seed.txt');

const sha256 = text => createHash('sha256').update(text).digest('hex');
const seam = (io, name, fallback) => io.stage?.[name] ?? fallback;

const GUARD_REFUSAL = 'stage-run: the Stage 1 guard (the frozen record, the identities, memory certification, the disk reserve and rule, the clear held-out folder, the tag and the registered blobs) is not merged yet: until it is, only a rehearsal series runs';

/** The guard a paid launch needs (PR 4 and PR 5 hand in the real one as `io.stage.guardFn`): the default refuses. `deps.guard` shape: `({ paid, requireSeed }) -> ok(null) | fail(reason)`. */
export const refusingGuard = async () => fail(GUARD_REFUSAL);

/** The guard of a series: a rehearsal's (free) passes; a paid series' is the one handed in, else the refusing one. */
export const guardOf = ({ series, io }) => (isRehearsal(series.dir) ? async () => ok(null) : seam(io, 'guardFn', refusingGuard));

/** The revealed seed: `ok(seed)`, `ok(null)` when it is not revealed yet, or a `fail` when the file is not the committed seed (it must hash to the series' `seedSha256`). */
function revealedSeed({ series, io }) {
  const file = seam(io, 'seedFile', STAGE_SEED_FILE);
  if (!existsSync(file)) return ok(null);
  const seed = readFileSync(file, 'utf8').trim();
  return sha256(seed) === series.state.seedSha256 ? ok(seed) : fail(`${file} does not hash to the series' seedSha256 (${String(series.state.seedSha256).slice(0, 12)}): it is not the seed that was committed`);
}

/** The gates of a rehearsal series: its own seed, no pilot GO to wait for, no line. */
const rehearsalGates = series => ({ seed: series.state.rehearsalSeed, seedRevealed: true, pilotGo: true, readbackLines: [] });

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
  const read = readStageAmendments(seam(io, 'amendmentsFile', STAGE_AMENDMENTS_FILE));
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
