#!/usr/bin/env node
// F2.S2 (b): the task repository's rulesets, read or applied.
//
//   node spikes/lang-bench/github-rulesets.mjs [--check]      read the rulesets and say `equal`, `absent` or `different` for each of the three; change nothing (the default)
//   node spikes/lang-bench/github-rulesets.mjs --apply        create the absent ones, never update or delete one; read every one back
//
// Exit 0 equal (or applied and read back equal); 1 a ruleset is different or absent, or the apply refused; 2 a usage error; 3 `gh` could not be run or read.
// The write is the owner's own `gh` login, run by the team lead at freeze step (e) 3: no token file is read and no token is in the child's environment. The merge settings are reported and never change the exit.
import { fileURLToPath } from 'node:url';
import { TASK_REPO, applyRulesets, readMergeSettings, rulesetsReadBack, strangerLine } from './lib/github-lang.mjs';
import { runBounded } from '../minigit-bench/lib/run.mjs';

const USAGE = [
  'usage: node spikes/lang-bench/github-rulesets.mjs [--check] [--repo <owner/name>]',
  '       node spikes/lang-bench/github-rulesets.mjs --apply [--repo <owner/name>]',
].join('\n');
const EXIT = Object.freeze({ done: 0, different: 1, usage: 2, gh: 3 });

/** The flags: `{ mode, repo }` or `{ usage }` (a sentence). */
function parseArgs(argv) {
  const modes = [];
  let repo = TASK_REPO;
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === '--check' || arg === '--apply') modes.push(arg.slice(2));
    else if (arg === '--repo') repo = argv[++index] ?? '';
    else return { usage: `${JSON.stringify(arg)} is not an accepted argument` };
  }
  if (new Set(modes).size > 1) return { usage: '--check and --apply are exclusive' };
  if (!/^[A-Za-z0-9][A-Za-z0-9_.-]*\/[A-Za-z0-9_.-]+$/.test(repo) || repo.split('/').some(part => /^\.+$/.test(part))) return { usage: `--repo wants owner/name, got ${JSON.stringify(repo)}` };
  return { mode: modes[0] ?? 'check', repo };
}

function sayStates(states, say) {
  for (const state of states) {
    say(`${state.name}: ${state.status}`);
    for (const problem of state.problems) say(`  - ${problem}`);
  }
  const ids = states.filter(state => state.id !== undefined).map(state => `${state.name}=${state.id}`);
  if (ids.length > 0) say(`ids: ${ids.join(' ')}`);
}

async function sayMergeSettings(repo, run, say) {
  const merge = await readMergeSettings(repo, run);
  if (!merge.ok) say(`merge settings (report only): could not be read: ${merge.reason}`);
  else say(merge.value.length === 0 ? 'merge settings: ok' : `merge settings (report only): ${merge.value.join('; ')}`);
}

async function check({ repo, run, say, complain }) {
  const back = await rulesetsReadBack({ repo, run });
  if (!back.ok) {
    complain(`github-rulesets: ${back.reason}`);
    return back.kind === 'config' ? EXIT.usage : EXIT.gh;
  }
  sayStates(back.value.states, say);
  for (const stranger of back.value.strangers) say(strangerLine(stranger));
  await sayMergeSettings(repo, run, say);
  return back.value.equal ? EXIT.done : EXIT.different;
}

async function apply({ repo, run, say, complain }) {
  const applied = await applyRulesets({ repo, run, say });
  if (!applied.ok) {
    complain(`github-rulesets: ${applied.reason}`);
    return { env: EXIT.gh, config: EXIT.usage }[applied.kind] ?? EXIT.different;
  }
  say(applied.value.outcomes.some(entry => entry.outcome === 'created') ? 'read back after the writes: equal' : 'read back: equal');
  sayStates(applied.value.readBack.states, say);
  await sayMergeSettings(repo, run, say);
  return EXIT.done;
}

/** The command: -> the exit code. `run`, `say` and `complain` are the seams of the tests (the real `runBounded`, stdout and stderr). */
export async function main({ argv, run = runBounded, say = line => process.stdout.write(`${line}\n`), complain = line => process.stderr.write(`${line}\n`) }) {
  const parsed = parseArgs(argv);
  if (parsed.usage !== undefined) {
    complain(`github-rulesets: ${parsed.usage}\n${USAGE}`);
    return EXIT.usage;
  }
  const context = { repo: parsed.repo, run, say, complain };
  return parsed.mode === 'apply' ? apply(context) : check(context);
}

if (process.argv[1] !== undefined && fileURLToPath(import.meta.url) === process.argv[1]) process.exitCode = await main({ argv: process.argv.slice(2) });
