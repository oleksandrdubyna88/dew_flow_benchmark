#!/usr/bin/env node
// The exploratory pilot's command line (plan todo/PLAN_lang_explore_task1.md): task 1 on the v1 bare harness, three C# and TypeScript pairs. Exploratory, not evidence.
//
//   node spikes/lang-explore/explore.mjs prepare                    the ONLY step that uses the network: TypeScript 6.0.3, the Node cache snapshot, the C# snapshot re-hashed, the spin calibration
//
// Exit codes: 0 done | 2 a stop | 3 a resumable halt (the machine not quiet, the preflight) | 4 usage or configuration | 5 failed.
import { parseArgs } from 'node:util';
import { prepare } from './lib/prepare.mjs';

const EXIT = Object.freeze({ done: 0, stop: 2, halt: 3, config: 4, failed: 5 });
const say = line => process.stdout.write(`${line}\n`);

async function cmdPrepare() {
  const r = await prepare({ say });
  if (r.ok) { say(`prepare done: ${JSON.stringify(r.value)}`); return EXIT.done; }
  say(`prepare stopped at ${r.step}: ${r.reason}`);
  return r.resumable ? EXIT.halt : EXIT.failed;
}

const COMMANDS = Object.freeze({ prepare: cmdPrepare });

export async function main(argv) {
  const { positionals } = parseArgs({ args: argv, allowPositionals: true, strict: false });
  const command = COMMANDS[positionals[0]];
  if (!command) { process.stderr.write(`explore: the commands are ${Object.keys(COMMANDS).join(', ')}\n`); return EXIT.config; }
  return command();
}

if (import.meta.url === `file://${process.argv[1]}`) process.exit(await main(process.argv.slice(2)));
