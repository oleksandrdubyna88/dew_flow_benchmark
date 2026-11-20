// The three verbs of Stage 1 on the command line (F3.S1 (a), PR 3): `stage-init` (FREE), `stage-run` (SPENDS MONEY unless a rehearsal series or `--dry-run`), `stage-status` (FREE, read only). Each takes the parsed flags and an `io` and returns an exit code:
// 0 done | 2 a stop | 3 a resumable halt | 4 usage, configuration or a refusal | 5 failed.
import path from 'node:path';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { heldoutAware } from './heldout-commands.mjs';
import { readToken } from './run.mjs';
import { ledgerFileOf } from './series.mjs';
import { stageInit } from './stage-init.mjs';
import { stageRun } from './stage-run.mjs';
import { stageStatusLines } from './stage-status.mjs';
import { amendmentsReport } from './stage-gates.mjs';
import { openStage } from './stage-step.mjs';
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

/** `stage-status --series <dir>`: the rounds, the slots, the pilot and the five spend lines. */
export function cmdStageStatus(values, io) {
  if (!values.series) return refused(io, 'stage-status needs --series <dir>');
  const series = openStage({ seriesDir: path.resolve(values.series), io });
  if (!series.ok) return refused(io, series.reason);
  stageStatusLines({ state: series.value.state, map: series.value.map, ledger: readLedger(ledgerFileOf(series.value.dir)) }).forEach(l => io.say(l));
  amendmentsReport(io).forEach(l => io.say(l));
  return 0;
}
