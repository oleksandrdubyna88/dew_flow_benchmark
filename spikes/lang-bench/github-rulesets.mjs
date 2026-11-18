#!/usr/bin/env node
// F2.S2 (b): the task repository's rulesets, read or applied.
//
//   node spikes/lang-bench/github-rulesets.mjs [--check]      read the rulesets and say `equal`, `absent` or `different` for each of the three; change nothing (the default)
//   node spikes/lang-bench/github-rulesets.mjs --apply --dry-run-record <file>     create the absent ones, never update or delete one; read every one back
//
// `--apply` starts only when the owner's go of 2026-11-08 holds, and checks it before any `gh` call: `spikes/lang-explore/inputs/packages.json` shows a passed package for each of the six tasks (04, 05, 06, 07, 08, 10: an `identity`
// and an `archiveSha256` of 64 hex, `referenceVerdicts` csharp and node both `accepted`), and the dry-run record named by `--dry-run-record` is a file whose FIRST line is exactly
// `dry run clean <yyyy-mm-dd> packages <sha256 of packages.json>` (a real date; the sha256 of the bytes of packages.json as they are now, so a record made before the registry changed is refused; the lines after the first are free text).
// Exit 0 equal (or applied and read back equal); 1 a ruleset is different or absent, or the apply refused (a prerequisite not met, a ruleset present and different); 2 a usage error; 3 `gh` could not be run or read.
// The write is the owner's own `gh` login, run by the team lead at freeze step (e) 3: no token file is read and no token is in the child's environment. The merge settings are reported and never change the exit.
import { createHash } from 'node:crypto';
import { closeSync, openSync, readFileSync, readSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { DRY_RUN_LINE, PACKAGE_TASKS, TASK_REPO, applyGateProblems, applyRulesets, readMergeSettings, rulesetsReadBack, strangerLine } from './lib/github-lang.mjs';
import { runBounded } from '../minigit-bench/lib/run.mjs';

/** The registry of the packages the apply's go reads: `spikes/lang-explore/inputs/packages.json`. */
export const DEFAULT_PACKAGES_FILE = fileURLToPath(new URL('../lang-explore/inputs/packages.json', import.meta.url));
const USAGE = [
  'usage: node spikes/lang-bench/github-rulesets.mjs [--check]',
  '       node spikes/lang-bench/github-rulesets.mjs --apply --dry-run-record <file>',
  'accepted flags: --check (the default: read, change nothing), --apply (create the absent rulesets), --dry-run-record <file> (required with --apply, refused with --check); any other flag is a usage error (exit 2).',
  `The repository is fixed in the data (${TASK_REPO}); no flag names another.`,
  `--apply starts only when spikes/lang-explore/inputs/packages.json shows a passed package (an identity and a sealed archive, both references accepted) for each of the six tasks (${PACKAGE_TASKS.join(', ')}) and the file named by --dry-run-record has "${DRY_RUN_LINE}" as its first line, the sha256 being that of packages.json's bytes now.`,
].join('\n');
const EXIT = Object.freeze({ done: 0, different: 1, usage: 2, gh: 3 });

const MODE_FLAGS = new Set(['--check', '--apply']);

/** The flag at `index` applied to `flags`: `{ flags, next }` (`next` is the index after it and its value) or `{ usage }` for an argument that is not accepted. */
function readFlag(flags, argv, index) {
  const arg = argv[index];
  if (MODE_FLAGS.has(arg)) return { flags: { ...flags, modes: [...flags.modes, arg.slice(2)] }, next: index + 1 };
  if (arg === '--dry-run-record') return { flags: { ...flags, record: argv[index + 1] ?? '' }, next: index + 2 };
  return { usage: `${JSON.stringify(arg)} is not an accepted argument` };
}

/** The flags as written, in order: `{ modes, record }` (`record` undefined when the flag is absent) or `{ usage }` for the first argument that is not accepted. The repository is not a flag: it is `TASK_REPO`, in the data. */
function collectFlags(argv) {
  let flags = { modes: [], record: undefined };
  let index = 0;
  while (index < argv.length) {
    const read = readFlag(flags, argv, index);
    if (read.usage !== undefined) return read;
    ({ flags, next: index } = read);
  }
  return flags;
}

const applyProblem = record => (record ? null : '--apply needs --dry-run-record <file>');
const checkProblem = record => (record === undefined ? null : '--dry-run-record belongs to --apply');

/** The sentence a combination of flags breaks, or `null`. */
function modeProblem({ modes, record }) {
  if (new Set(modes).size > 1) return '--check and --apply are exclusive';
  return modes[0] === 'apply' ? applyProblem(record) : checkProblem(record);
}

/** The flags: `{ mode, record }` or `{ usage }` (a sentence). */
function parseArgs(argv) {
  const flags = collectFlags(argv);
  if (flags.usage !== undefined) return flags;
  const problem = modeProblem(flags);
  return problem === null ? { mode: flags.modes[0] ?? 'check', record: flags.record } : { usage: problem };
}

/** `packages.json`: `{ packages, packagesSha256 }`, the parsed object and the sha256 of the very bytes that were parsed; both `null` when it cannot be read or parsed. */
function readPackages(file) {
  try {
    const bytes = readFileSync(file);
    return { packages: JSON.parse(bytes.toString('utf8')), packagesSha256: createHash('sha256').update(bytes).digest('hex') };
  } catch {
    return { packages: null, packagesSha256: null };
  }
}

/** The first line of the dry-run record (without a trailing carriage return), or `null` when the file cannot be read. Only its first 4 KiB are read. */
function readFirstLine(file) {
  let fd;
  try {
    fd = openSync(file, 'r');
    const buffer = Buffer.alloc(4096);
    const text = buffer.toString('utf8', 0, readSync(fd, buffer, 0, buffer.length, 0));
    return text.split('\n')[0].replace(/\r$/, '');
  } catch {
    return null;
  } finally {
    if (fd !== undefined) closeSync(fd);
  }
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

const READING = 'reading the task repository\'s rulesets…';

async function check({ repo, run, say, complain, progress }) {
  progress(READING);
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

async function apply({ repo, run, say, complain, progress, record, packagesFile }) {
  const { packages, packagesSha256 } = readPackages(packagesFile);
  const recordFirstLine = readFirstLine(record);
  const unmet = applyGateProblems({ packages, packagesSha256, recordFirstLine });
  if (unmet.length > 0) {
    for (const problem of unmet) complain(`github-rulesets: refused: ${problem}`);
    complain('github-rulesets: refused: the apply needs the owner\'s go of 2026-11-08 (all six packages registered, sealed and accepted, and a dry run clean against this registry); nothing was read or written.');
    return EXIT.different;
  }
  progress(READING);
  const applied = await applyRulesets({ repo, run, say, packages, packagesSha256, recordFirstLine });
  if (!applied.ok) {
    complain(`github-rulesets: ${applied.reason}`);
    return { env: EXIT.gh, config: EXIT.usage }[applied.kind] ?? EXIT.different;
  }
  say(applied.value.outcomes.some(entry => entry.outcome === 'created') ? 'read back after the writes: equal' : 'read back: equal');
  sayStates(applied.value.readBack.states, say);
  await sayMergeSettings(repo, run, say);
  return EXIT.done;
}

/** The command: -> the exit code. `run`, `say`, `complain`, `progress` and `packagesFile` are the seams of the tests (the real `runBounded`, stdout, stderr, stderr again for the one progress line before the first read, and `lang-explore/inputs/packages.json`). */
export async function main({ argv, run = runBounded, say = line => process.stdout.write(`${line}\n`), complain = line => process.stderr.write(`${line}\n`), progress = line => process.stderr.write(`${line}\n`), packagesFile = DEFAULT_PACKAGES_FILE }) {
  const parsed = parseArgs(argv);
  if (parsed.usage !== undefined) {
    complain(`github-rulesets: ${parsed.usage}\n${USAGE}`);
    return EXIT.usage;
  }
  const context = { repo: TASK_REPO, run, say, complain, progress, record: parsed.record, packagesFile };
  return parsed.mode === 'apply' ? apply(context) : check(context);
}

if (process.argv[1] !== undefined && fileURLToPath(import.meta.url) === process.argv[1]) process.exitCode = await main({ argv: process.argv.slice(2) });
