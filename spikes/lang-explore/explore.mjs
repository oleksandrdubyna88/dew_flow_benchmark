#!/usr/bin/env node
// The exploratory pilot's command line (plan todo/PLAN_lang_explore_task1.md): task 1 on the v1 bare harness, three C# and TypeScript pairs. Exploratory, not evidence.
//
//   node spikes/lang-explore/explore.mjs prepare                       the ONLY step that uses the network: TypeScript 6.0.3, the Node cache snapshot, the C# snapshot re-hashed, the spin calibration
//   node spikes/lang-explore/explore.mjs calibrate                     FREE: both references through the judge at the 30 s ceiling, the limits (TL, ML), every mutant against its predicted cases
//   node spikes/lang-explore/explore.mjs rehearse                      FREE: one pair end to end with v1's stub CLI and stub upstream, a fake token; exit 0 when every criterion holds
//   node spikes/lang-explore/explore.mjs init                          creates a series (the mask draw is made and recorded)
//   node spikes/lang-explore/explore.mjs quiet [--accept-noisy <why>]  the machine-quiet check alone
//   node spikes/lang-explore/explore.mjs run --pair k --series <dir> [--rerun csharp|node] [--accept-noisy <why>]     SPENDS MONEY: one pair
//   node spikes/lang-explore/explore.mjs readback [--series <dir>]     what each session was launched with, against the registered prompt and environment
//   node spikes/lang-explore/explore.mjs status [--series <dir>]       node spikes/lang-explore/explore.mjs note [--out <file>] [--force]     node spikes/lang-explore/explore.mjs prune [--series <dir>]
//   node spikes/lang-explore/explore.mjs leakcheck [--range A..B]      v1's leak check with the pilot's held-out outputs as the needles
//
// Exit codes: 0 done | 2 a stop | 3 a resumable halt (the machine not quiet, the preflight) | 4 usage, configuration or a refusal | 5 failed | 130 / 143 cancelled by SIGINT / SIGTERM.
// `run` and `rehearse` re-execute themselves under `taskset -c 6,7` (an env marker stops a loop), so the broker, the receiver, the proxy and the sampler sit on the harness core, as v1's runner does.
import { spawn } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { calibrateCommand } from './lib/calibrate.mjs';
import { EXIT, cmdInit, cmdLeakcheck, cmdNote, cmdPrune, cmdQuiet, cmdReadback, cmdRehearse, cmdRun, cmdStatus } from './lib/commands.mjs';
import { prepare } from './lib/prepare.mjs';
import { loadWorld } from './lib/world.mjs';

const OPTIONS = { pair: { type: 'string' }, series: { type: 'string' }, rerun: { type: 'string' }, 'accept-noisy': { type: 'string' }, out: { type: 'string' }, range: { type: 'string' }, force: { type: 'boolean' } };
const REEXEC = new Set(['run', 'rehearse']);

async function cmdPrepare(values, io) {
  const r = await prepare({ say: io.say });
  if (r.ok) { io.say(`prepare done: ${JSON.stringify(r.value)}`); return EXIT.done; }
  io.say(`prepare stopped at ${r.step}: ${r.reason}`);
  return r.resumable ? EXIT.halt : EXIT.failed;
}

async function cmdCalibrate(values, io) {
  const r = await calibrateCommand({ say: io.say, worldFn: loadWorld });
  if (r.ok) { io.say(`calibrated: TL ${r.value.calibration.limits.timeMs} ms, ML ${r.value.calibration.limits.memoryBytes} bytes; written to ${r.value.file}`); return EXIT.done; }
  io.say(`calibration stopped: ${r.reason}`);
  return EXIT.failed;
}

const COMMANDS = Object.freeze({ prepare: cmdPrepare, calibrate: cmdCalibrate, rehearse: cmdRehearse, init: cmdInit, quiet: cmdQuiet, run: cmdRun, status: cmdStatus, readback: cmdReadback, note: cmdNote, prune: cmdPrune, leakcheck: cmdLeakcheck });

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
