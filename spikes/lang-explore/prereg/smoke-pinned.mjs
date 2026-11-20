#!/usr/bin/env node
// The bare-session smoke on the PINNED CLI of Stage 1 (F3 PR 5c): the replacing evidence of the language comparison's pilot row G-4 is B-T1 over `spikes/minigit-bench/test/fixtures/real/claude-<version>-bare-smoke/`, found by its `meta.json`
// `cliVersion`, and the only fixture is v1's (CLI 2.1.310). v1's smoke takes its CLI pin from `inputs/stacks.json`, so it cannot capture one on 2.1.329; this wrapper gives v1's own `main` the pin and the copy of `explore.json`'s `cli`
// entry (`smokeInputs({ claude, cliPin })`) and copies nothing else of v1. It checks the copy's sha256 (`cliPinProblems`, the pin step's own check) before anything runs, then runs v1's smoke, `sanitise` and `verdicts` in turn:
// the sanitised capture lands in `claude-<version>-bare-smoke/` and `SMOKE.md` is v1's computed table.
//   node spikes/lang-explore/prereg/smoke-pinned.mjs --out <dir outside every git tree> [--dry-run] [--explore <file>]
// ONE paid session (about $0.11, cap $0.20), the owner's approval of 2026-11-20 for the pilot's G-4; `--dry-run` plans it and launches nothing. The out dir, the token and every other check are v1's.
// Exit 0 done | 3 the environment (the copy is refused, the fixture folder is not empty, v1's own: no token, preflight, bad out dir) | 4 usage | 5 `explore.json` cannot be read, or v1's session or sanitise failed.
import { existsSync, readdirSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { main as v1Main } from '../../minigit-bench/smoke-session.mjs';
import { loadExplore, pinnedPaths } from '../lib/inputs.mjs';
import { INPUTS, V1 } from '../lib/paths.mjs';
import { EXIT, parseFlags, said } from './generator.mjs';
import { cliPinProblems } from './pin-cli.mjs';

const ENVIRONMENT = 3;
const VERSION = /^\d+\.\d+\.\d+$/;
export const FIXTURES_DIR = path.join(V1, 'test', 'fixtures', 'real');
/** The folder name `aa-verdicts` looks a smoke fixture up by: the CLI version it was captured on. */
export const fixtureNameOf = version => `claude-${version}-bare-smoke`;
const USAGE = 'usage: smoke-pinned.mjs --out <dir outside every git tree> [--dry-run] [--explore <file>]';
const COMMAND = 'node spikes/lang-explore/prereg/smoke-pinned.mjs --out ~/rsd/lang-v2/smoke/<UTC stamp>';

/** v1's `io` (two streams) over one `say`: a line of text is said without its newline. */
const ioOver = say => {
  const stream = { write: text => say(text.replace(/\n$/, '')) };
  return { out: stream, err: stream };
};

/** The sentence for a fixture folder that has something in it (v1's sanitise refuses to write into it, and by then the session is paid), or null. */
const fixtureProblem = fixture => (existsSync(fixture) && readdirSync(fixture).length > 0 ? `${fixture} is not empty: a fixture is never overwritten; remove it or pick another version first` : null);

/** The sentences against going on: the version is not a version, the copy is not the pinned one, or the fixture folder is taken. `[]` when the run may start. */
function problemsOf({ explore, home, fixture }) {
  if (!VERSION.test(explore.cli.version)) return [`explore.json cli.version must be x.y.z, got '${explore.cli.version}': it names a folder`];
  return [...cliPinProblems({ explore, home }), fixtureProblem(fixture)].filter(Boolean);
}

/** What the run needs: `ok({ version, pin, fixture })` | `fail` naming what is wrong (`exit` 5 when `explore.json` cannot be read, 3 for the rest). */
function planOf({ file, home, fixturesDir }) {
  const loaded = loadExplore(file);
  if (!loaded.ok) return fail(loaded.reason, { exit: EXIT.input });
  const explore = loaded.value;
  const fixture = path.join(fixturesDir, fixtureNameOf(explore.cli.version));
  const problems = problemsOf({ explore, home, fixture });
  return problems.length > 0 ? fail(problems.join('\n'), { exit: ENVIRONMENT }) : ok({ version: explore.cli.version, pin: { claude: pinnedPaths(explore, home).claudeBinary, cliPin: explore.cli.version }, fixture });
}

/** v1's verbs in the order they run: the session (a dry run is the planning of it, alone), then the fixture, then its verdicts. */
const stepsOf = ({ out, fixture, dryRun }) => (dryRun
  ? [['run', '--out', out, '--dry-run']]
  : [['run', '--out', out], ['sanitise', '--from', out, '--to', fixture], ['verdicts', '--fixture', fixture]]);

const closing = ({ dryRun, out, fixture }) => (dryRun
  ? `dry run: would sanitise ${out} into ${fixture} and write SMOKE.md there; nothing was launched`
  : `smoke fixture written: ${fixture} (SMOKE.md there; commit it by path)`);

/** The steps one after another through v1's `main`; the first that does not end 0 stops the rest and its code is the answer. Says what the run ended with. */
async function runSteps({ steps, smoke, opts, say, out, done }) {
  for (const argv of steps) {
    const code = await smoke(argv, opts);
    if (code !== 0) return said(say, `stopped after ${argv[0]} (exit ${code}); what the session left, if anything, is in ${out}`, code);
  }
  return said(say, done, EXIT.done);
}

/** The usage sentence when the flags are not what the command takes (a missing `--out`, `--check`, which it has no use for, or what the parser said), or null. */
function usageProblem(flags) {
  if (!flags.ok) return flags.reason;
  return flags.value.check || !flags.value.out ? USAGE : null;
}

/** The command: -> the exit code. `home`, `smoke` (v1's `main`) and `fixturesDir` are the tests' seams. */
export async function main(argv, say = line => process.stdout.write(`${line}\n`), { home = os.homedir(), smoke = v1Main, fixturesDir = FIXTURES_DIR } = {}) {
  const flags = parseFlags(argv, { 'dry-run': { type: 'boolean' }, explore: { type: 'string' } });
  const usage = usageProblem(flags);
  if (usage) return said(say, usage, EXIT.usage);
  const plan = planOf({ file: flags.value.explore ?? path.join(INPUTS, 'explore.json'), home, fixturesDir });
  if (!plan.ok) return said(say, plan.reason, plan.exit);
  const [out, dryRun] = [path.resolve(flags.value.out), flags.value['dry-run'] === true];
  const opts = { io: ioOver(say), home, pin: plan.value.pin, command: COMMAND };
  return runSteps({ steps: stepsOf({ out, fixture: plan.value.fixture, dryRun }), smoke, opts, say, out, done: closing({ dryRun, out, fixture: plan.value.fixture }) });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
