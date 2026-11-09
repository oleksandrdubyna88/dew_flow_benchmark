#!/usr/bin/env node
// The exploratory pilot's command line (plans research/PLAN_lang_explore_task1.md and research/PLAN_lang_explore_olympiad.md): olympiad tasks (01, 02, 03, 09) on the v1 bare harness, three C# and TypeScript pairs each. Exploratory, not evidence.
// `--task NN` names the task on every command that works on one (a missing or unknown task is exit 4); a series records its task and a command given another task refuses.
//
//   node spikes/lang-explore/explore.mjs prepare                       the ONLY step that uses the network: TypeScript 6.0.3, the Node cache snapshot, the C# snapshot re-hashed, the spin calibration
//   node spikes/lang-explore/explore.mjs calibrate --task NN           FREE: the oracle (brute.py, handcheck.md), both references through the judge at the 30 s ceiling, the limits (TL, ML), every mutant against its predicted cases
//   node spikes/lang-explore/explore.mjs rehearse --task NN            FREE: one pair end to end with v1's stub CLI and stub upstream, a fake token; exit 0 when every criterion holds
//   node spikes/lang-explore/explore.mjs init --task NN                creates a series of the task (the mask draw is made and recorded)
//   node spikes/lang-explore/explore.mjs quiet [--accept-noisy <why>]  the machine-quiet check alone
//   node spikes/lang-explore/explore.mjs run --task NN --pair k --series <dir> [--rerun csharp|node] [--accept-noisy <why>] [--allow-over <the owner's words and date>]     SPENDS MONEY: one pair
//   node spikes/lang-explore/explore.mjs readback --task NN [--series <dir>]   what each session was launched with, against the registered prompt and environment
//   node spikes/lang-explore/explore.mjs status --task NN [--series <dir>]     node spikes/lang-explore/explore.mjs note --task NN [--out <file>] [--force]     node spikes/lang-explore/explore.mjs prune --task NN [--series <dir>]
//   node spikes/lang-explore/explore.mjs rescore --task NN --series <dir> --rid R [--wait S]   FREE: an ended void whose evidence now classes as data is scored and recorded (the ledger's spend is unchanged)
//   node spikes/lang-explore/explore.mjs replay --task NN --series <dir> --rid R --case ID [--times N]   FREE and DIAGNOSTIC: reads a rejected case again on the current runner; changes no record, verdict or spend; writes s/<rid>/replay/<time>.json only
//   node spikes/lang-explore/explore.mjs repartition --series <dir>   FREE: the time partition of every kept session recomputed from its retained evidence with v1's classifier as it is, into a NEW dated file in <dir>/partition-recomputed/; no record changes
//   node spikes/lang-explore/explore.mjs leakcheck [--task NN] [--range A..B] [--task-repo <path>] [--task-base <ref>]   v1's leak check with the held-out outputs, reference lines and notes' shingles of EVERY task (or of the one named) as the needles,
//       over the bench repository AND the task repository (`--task-repo`, else the variable LANG_V2_TASK_REPO; with neither it refuses): its files at HEAD, its history over what `--task-base` (default origin/main) does not hold, its staged
//       changes and its working tree. `--candidate-repo <path> --candidate <commit>` scans ONE commit about to be published (its tree, message and diff); with no `--task-repo` it is the only thing scanned
//   node spikes/lang-explore/explore.mjs heldout-open --task NN [--key-file <file>] [--key-backup <file> --share-mount <dir>]   FREE: the clear held-out folder of a sealed quick task made from its archive (the key's sha256 is checked before any decryption), held for the operator until heldout-close
//   node spikes/lang-explore/explore.mjs heldout-close --task NN --key-backup <file> --share-mount <dir> [--key-file <file>]   FREE: the clear folder removed, only after the key's backup copy is verified on the share's mount (there is no override); a folder held by another live process is refused
//   node spikes/lang-explore/explore.mjs heldout-check --tasks NN,NN [--key-file <file>] [--key-backup <file> --share-mount <dir>]   FREE and read only: the registry entry, the archive's hash, the key and the unsealed tree of each task, as counts
//       the backup file and the mount may also come from the private file ~/.config/lang-v2/heldout-backup.json (mode 600); the flags win. An entry point that must remove a stale clear folder needs them too.
//
// Exit codes: 0 done | 2 a stop | 3 a resumable halt (the machine not quiet, the preflight) | 4 usage, configuration or a refusal | 5 failed | 130 / 143 cancelled by SIGINT / SIGTERM.
// `run` and `rehearse` re-execute themselves under `taskset -c 6,7` (an env marker stops a loop), so the broker, the receiver, the proxy and the sampler sit on the harness core, as v1's runner does.
import { spawn } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { calibrateCommand } from './lib/calibrate.mjs';
import { cmdHeldoutCheck, cmdHeldoutClose, cmdHeldoutOpen } from './lib/heldout-commands.mjs';
import { EXIT, cmdInit, cmdLeakcheck, cmdNote, cmdPrune, cmdQuiet, cmdReadback, cmdRehearse, cmdRepartition, cmdReplay, cmdRescore, cmdRun, cmdStatus } from './lib/commands.mjs';
import { prepare } from './lib/prepare.mjs';
import { taskFromValues } from './lib/tasks.mjs';
import { isQuick } from './lib/quick.mjs';
import { loadWorld } from './lib/world.mjs';

const OPTIONS = { task: { type: 'string' }, 'allow-over': { type: 'string' }, pair: { type: 'string' }, series: { type: 'string' }, rerun: { type: 'string' }, 'accept-noisy': { type: 'string' }, out: { type: 'string' }, range: { type: 'string' }, 'task-repo': { type: 'string' }, 'task-base': { type: 'string' }, 'candidate-repo': { type: 'string' }, candidate: { type: 'string' }, force: { type: 'boolean' }, rid: { type: 'string' }, wait: { type: 'string' }, case: { type: 'string' }, times: { type: 'string' }, tasks: { type: 'string' }, 'key-file': { type: 'string' }, 'key-backup': { type: 'string' }, 'share-mount': { type: 'string' } };
/** The commands that run or score under the two scoring CPUs: every scoring of the quick run, the diagnostic and the re-score included, so the numbers they compare are measured in the same conditions. */
export const REEXEC = new Set(['run', 'rehearse', 'rescore', 'replay']);

async function cmdPrepare(values, io) {
  const r = await prepare({ say: io.say });
  if (r.ok) { io.say(`prepare done: ${JSON.stringify(r.value)}`); return EXIT.done; }
  io.say(`prepare stopped at ${r.step}: ${r.reason}`);
  return r.resumable ? EXIT.halt : EXIT.failed;
}

async function cmdCalibrate(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  if (!given.ok) { io.say(given.reason); return EXIT.config; }
  if (isQuick(given.value.task)) { io.say(`task ${given.value.task.id} is a quick-run task: its time and build limits are fixed (explore.json quick.limits), its memory limit is the one registered value (inputs/memory-limit.json), and nothing is calibrated`); return EXIT.config; }
  const r = await calibrateCommand({ home: io.home, say: io.say, worldFn: io.worldFn ?? loadWorld, task: given.value.task });
  if (r.ok) { io.say(`calibrated task ${given.value.task.id}: TL ${r.value.calibration.limits.timeMs} ms, ML ${r.value.calibration.limits.memoryBytes} bytes; written to ${r.value.file}`); return EXIT.done; }
  io.say(`calibration stopped: ${r.reason}`);
  return EXIT.failed;
}

const COMMANDS = Object.freeze({ prepare: cmdPrepare, calibrate: cmdCalibrate, rehearse: cmdRehearse, init: cmdInit, quiet: cmdQuiet, run: cmdRun, status: cmdStatus, readback: cmdReadback, note: cmdNote, prune: cmdPrune, leakcheck: cmdLeakcheck, rescore: cmdRescore, replay: cmdReplay, repartition: cmdRepartition, 'heldout-open': cmdHeldoutOpen, 'heldout-close': cmdHeldoutClose, 'heldout-check': cmdHeldoutCheck });

/** SIGINT and SIGTERM abort the run: the reason and the exit code are recorded for the loop, then the controller aborts. */
function armSignals(abort, ctl) {
  for (const [sig, code] of [['SIGINT', 130], ['SIGTERM', 143]]) process.on(sig, () => { abort.reason = sig; abort.exit = code; ctl.abort(); });
}

/** `argv` -> the exit code. `seams` (`{ home, say, worldFn, tokenFn }`) are for tests only. */
export async function main(argv, seams = {}) {
  const [command, ...rest] = argv;
  let values;
  try { values = parseArgs({ args: rest, options: OPTIONS, allowPositionals: false }).values; } catch (e) { process.stderr.write(`explore: usage error: ${e.message}\n`); return EXIT.config; }
  if (!Object.hasOwn(COMMANDS, command ?? '')) { process.stderr.write(`explore: '${command ?? ''}' is not a command; the commands are ${Object.keys(COMMANDS).join(', ')}\n`); return EXIT.config; }
  const abort = { reason: null, exit: 130 };
  const ctl = new AbortController();
  armSignals(abort, ctl);
  return COMMANDS[command](values, { home: os.homedir(), say: line => process.stdout.write(`${line}\n`), abort, signal: ctl.signal, ...seams });
}

/** Re-execute under `taskset -c 6,7` unless the marker says this IS the re-executed process. */
function reexec() {
  const child = spawn('taskset', ['-c', '6,7', process.execPath, ...process.argv.slice(1)], { env: { ...process.env, EXPLORE_PINNED: '1' }, stdio: 'inherit' });
  for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => child.kill(sig));
  child.on('exit', (code, signal) => process.exit(code ?? (signal === 'SIGINT' ? 130 : 143)));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (!REEXEC.has(process.argv[2]) || process.env.EXPLORE_PINNED === '1') process.exitCode = await main(process.argv.slice(2));
  else reexec();
}
