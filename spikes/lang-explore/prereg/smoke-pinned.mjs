#!/usr/bin/env node
// The bare-session smoke on the PINNED CLI of Stage 1 (F3 PR 5c): the replacing evidence of the language comparison's pilot row G-4 is B-T1 over `spikes/minigit-bench/test/fixtures/real/claude-<version>-bare-smoke/`, found by its `meta.json`
// `cliVersion`, and the only fixture is v1's (CLI 2.1.310). v1's smoke takes its CLI pin from `inputs/stacks.json`, so it cannot capture one on 2.1.329; this wrapper gives v1's own `main` the pin and the copy of `explore.json`'s `cli`
// entry (`smokeInputs({ claude, cliPin })`) and copies nothing else of v1. The binary v1 runs is the verified copy's absolute path, never one found on PATH.
//   node spikes/lang-explore/prereg/smoke-pinned.mjs --out <dir outside every git tree> [--dry-run] [--explore <file>]
//   node spikes/lang-explore/prereg/smoke-pinned.mjs --from sanitise|verdicts --raw <the out dir of the paid run> [--explore <file>]
// ONE paid session (about $0.11, cap $0.20), the owner's approval of 2026-11-20 for the pilot's G-4: the first form. It checks the copy (`cliPinProblems`: it is there, its sha256 is the pin's, its mode is 0555) and the fixture folder BEFORE anything
// runs, then runs v1's smoke, and then makes the fixture from the raw capture, which v1 keeps under `--out`, outside git.
//   The fixture is made beside its folder: v1's `sanitise` writes `.claude-<version>-bare-smoke.pending`, v1's `verdicts` writes SMOKE.md into it, and only then is it renamed to `claude-<version>-bare-smoke/`. A failure or a kill never leaves a
//   partial fixture folder, and the raw capture is never written to.
//   A failure after the paid session never needs a second one: the second form redoes the later steps from the raw capture, and launches nothing. `--from sanitise` redoes sanitise and verdicts (a stale `.pending` folder is removed first);
//   `--from verdicts` redoes verdicts and the rename on the `.pending` folder a failed verdicts step kept. Both refuse a raw dir that holds no completed run of the pinned CLI (its meta.json: outcome ok, `cliVersion` the pin), naming it.
//   `--dry-run` (first form only): runs every check of the copy (it is there, sha256, mode 0555, the version's form), does NOT refuse a fixture folder that exists (it says the folder exists, a real run would refuse it), reads the token as v1's
//   dry run does (that rule is v1's: a home without a token is v1's refusal) and launches nothing.
// Exit 0 done | 3 the environment (the copy, the raw dir or the fixture folder is refused; v1's own: no token, preflight, bad out dir) | 4 usage | 5 `explore.json` cannot be read, or v1's session, sanitise or verdicts failed.
import { existsSync, readFileSync, readdirSync, renameSync, rmSync } from 'node:fs';
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
const FROM = Object.freeze(['run', 'sanitise', 'verdicts']);
export const FIXTURES_DIR = path.join(V1, 'test', 'fixtures', 'real');
/** The folder name `aa-verdicts` looks a smoke fixture up by: the CLI version it was captured on. */
export const fixtureNameOf = version => `claude-${version}-bare-smoke`;
/** Where the fixture is built before it is renamed to its name: a hidden sibling of the fixture folder (the same volume, so the rename is one step). */
export const stagingOf = fixture => path.join(path.dirname(fixture), `.${path.basename(fixture)}.pending`);
const USAGE_RUN = 'usage: smoke-pinned.mjs --out <dir outside every git tree> [--dry-run] [--explore <file>]';
const USAGE_RESUME = 'usage: smoke-pinned.mjs --from sanitise|verdicts --raw <the out dir of the paid run> [--explore <file>] (it launches nothing: no --out, no --dry-run)';
const COMMAND = 'node spikes/lang-explore/prereg/smoke-pinned.mjs --out ~/rsd/lang-v2/smoke/<UTC stamp>';

const fromOf = v => v.from ?? 'run';
/** Each way the flags can be wrong, as `[is it, the sentence]`. */
const USAGE_CHECKS = Object.freeze([
  [v => v.check, `there is no --check\n${USAGE_RUN}`],
  [v => !FROM.includes(fromOf(v)), `--from is one of ${FROM.join(', ')}\n${USAGE_RUN}`],
  [v => fromOf(v) === 'run' && (!v.out || v.raw), USAGE_RUN],
  [v => fromOf(v) !== 'run' && (!v.raw || v.out || v['dry-run']), USAGE_RESUME],
]);
/** The usage sentence when the flags are not a run and not a resume (or what the parser said), or null. */
const usageProblem = flags => (flags.ok ? USAGE_CHECKS.find(([wrong]) => wrong(flags.value))?.[1] ?? null : flags.reason);

/** v1's `io` (two streams) over one `say`: a line of text is said without its newline. */
const ioOver = say => {
  const stream = { write: text => say(text.replace(/\n$/, '')) };
  return { out: stream, err: stream };
};

const fixtureTaken = fixture => existsSync(fixture) && readdirSync(fixture).length > 0;
const takenSentence = fixture => `${fixture} is not empty: a fixture is never overwritten; remove it or pick another version first`;

/** The sentences against going on: the version is not a version, or the copy is not the pinned one (there, its sha256, mode 0555). `[]` when the copy may run. */
function copyProblems({ explore, home }) {
  if (!VERSION.test(explore.cli.version)) return [`explore.json cli.version must be x.y.z, got '${explore.cli.version}': it names a folder`];
  return cliPinProblems({ explore, home });
}

/** What the command needs: `ok({ version, pin, fixture })` | `fail` naming what is wrong (`exit` 5 when `explore.json` cannot be read, 3 for the rest). `pin.claude` is the copy's absolute path: the binary that was checked is the one that runs. */
function planOf({ file, home, fixturesDir }) {
  const loaded = loadExplore(file);
  if (!loaded.ok) return fail(loaded.reason, { exit: EXIT.input });
  const explore = loaded.value;
  const problems = copyProblems({ explore, home });
  if (problems.length > 0) return fail(problems.join('\n'), { exit: ENVIRONMENT });
  const { version } = explore.cli;
  return ok({ version, pin: { claude: path.resolve(pinnedPaths(explore, home).claudeBinary), cliPin: version }, fixture: path.join(fixturesDir, fixtureNameOf(version)) });
}

/** The sentence for a raw dir that is not a finished run of the pinned CLI (its meta.json: it reads, the outcome is ok, the CLI that ran was the pinned one), or null. */
function rawProblem(raw, version) {
  let meta;
  try { meta = JSON.parse(readFileSync(path.join(raw, 'meta.json'), 'utf8')); } catch (e) { return `${raw} holds no readable meta.json (${e.code ?? e.message}): it is not a finished run of the smoke`; }
  if (meta.outcome?.ok !== true) return `${raw} holds no completed run: its meta.json outcome is '${meta.outcome?.label ?? 'missing'}', not ok`;
  return meta.cliVersion === version ? null : `${raw} was captured on CLI ${meta.cliVersion}, the pin is ${version}`;
}

/** The fixture folder's gate: a real run or a resume refuses a folder that has something in it; a dry run only says so. -> an exit code, or null to go on. */
function fixtureGate({ fixture, dry, say }) {
  if (!fixtureTaken(fixture)) return null;
  if (!dry) return said(say, takenSentence(fixture), ENVIRONMENT);
  say(`note: the fixture folder ${fixture} exists and is not empty: a real run would refuse it`);
  return null;
}

/** The staging folder made ready for `from`: sanitise starts on none (a stale one is a killed run's, removed), verdicts needs the one a failed verdicts step kept. -> `ok` | `fail`. */
function stagingReady({ from, staging }) {
  if (from === 'verdicts') return existsSync(staging) ? ok(null) : fail(`there is no sanitised copy at ${staging} to run the verdicts on: run again with --from sanitise`);
  rmSync(staging, { recursive: true, force: true });
  return ok(null);
}

/** v1's verbs that make the fixture from the raw capture, from `from` on: sanitise into the staging folder, then the verdicts on it. */
const verbsFrom = ({ from, raw, staging }) => [...(from === 'sanitise' ? [['sanitise', '--from', raw, '--to', staging]] : []), ['verdicts', '--fixture', staging]];

const resumeLine = ({ step, raw, explore }) => `node spikes/lang-explore/prereg/smoke-pinned.mjs --explore ${explore} --from ${step} --raw ${raw}`;
/** What is said when a step after the paid session fails: the raw capture is untouched and the way on needs no second session. */
const stoppedAfter = ({ verb, code, raw, explore }) => `stopped after ${verb} (exit ${code}); no fixture folder was made and the raw capture ${raw} is untouched. Redo it without a second session: ${resumeLine({ step: verb, raw, explore })}`;

/** A step after the paid session failed: a sanitise that failed leaves no staging folder (what it wrote is not whole), a verdicts that failed keeps it; says the way on. -> the step's exit code. */
function stopped({ verb, code, staging, raw, ctx }) {
  if (verb === 'sanitise') rmSync(staging, { recursive: true, force: true });
  return said(ctx.say, stoppedAfter({ verb, code, raw, explore: ctx.explore }), code);
}

/** The staging folder renamed to the fixture's name: the last step. -> `ok` | `fail` (the staging folder is kept for `--from verdicts`). */
function renamedIntoPlace({ staging, fixture, raw, explore }) {
  try {
    renameSync(staging, fixture);
    return ok(fixture);
  } catch (e) {
    return fail(`cannot rename ${staging} to ${fixture}: ${e.code ?? e.message}; the sanitised copy is kept. Redo the last step: ${resumeLine({ step: 'verdicts', raw, explore })}`);
  }
}

const concluded = ({ say, done }) => (done.ok ? said(say, `smoke fixture written: ${done.value} (SMOKE.md there; commit it by path)`, EXIT.done) : said(say, done.reason, EXIT.input));

/** The fixture from the raw capture: the verbs from `from` on, each through v1's `main` (the first that does not end 0 stops the rest), then the rename. */
async function finish({ from, raw, plan, ctx }) {
  const staging = stagingOf(plan.fixture);
  const ready = stagingReady({ from, staging });
  if (!ready.ok) return said(ctx.say, ready.reason, ENVIRONMENT);
  for (const argv of verbsFrom({ from, raw, staging })) {
    const code = await ctx.smoke(argv, ctx.opts);
    if (code !== 0) return stopped({ verb: argv[0], code, staging, raw, ctx });
  }
  return concluded({ say: ctx.say, done: renamedIntoPlace({ staging, fixture: plan.fixture, raw, explore: ctx.explore }) });
}

const runArgv = ({ out, dry }) => ['run', '--out', out, ...(dry ? ['--dry-run'] : [])];
const dryClosing = ({ out, fixture }) => `dry run: would run the session into ${out}, then sanitise it into ${stagingOf(fixture)}, write SMOKE.md there and rename it to ${fixture}; nothing was launched`;

/** The first form: the paid session (or its dry run), then the fixture. */
async function freshRun({ v, plan, ctx }) {
  const dry = v['dry-run'] === true;
  const gated = fixtureGate({ fixture: plan.fixture, dry, say: ctx.say });
  if (gated !== null) return gated;
  const out = path.resolve(v.out);
  const code = await ctx.smoke(runArgv({ out, dry }), ctx.opts);
  if (code !== 0) return said(ctx.say, `stopped after run (exit ${code}); what the session left, if anything, is in ${out}`, code);
  return dry ? said(ctx.say, dryClosing({ out, fixture: plan.fixture }), EXIT.done) : finish({ from: 'sanitise', raw: out, plan, ctx });
}

/** The second form: the later steps again from a raw capture that holds a completed run. Nothing is launched. */
async function resumed({ v, plan, ctx }) {
  const raw = path.resolve(v.raw);
  const problem = rawProblem(raw, plan.version);
  if (problem) return said(ctx.say, problem, ENVIRONMENT);
  const gated = fixtureGate({ fixture: plan.fixture, dry: false, say: ctx.say });
  return gated ?? finish({ from: v.from, raw, plan, ctx });
}

/** The command: -> the exit code. `home`, `smoke` (v1's `main`) and `fixturesDir` are the tests' seams. */
export async function main(argv, say = line => process.stdout.write(`${line}\n`), { home = os.homedir(), smoke = v1Main, fixturesDir = FIXTURES_DIR } = {}) {
  const flags = parseFlags(argv, { 'dry-run': { type: 'boolean' }, explore: { type: 'string' }, from: { type: 'string' }, raw: { type: 'string' } });
  const usage = usageProblem(flags);
  if (usage) return said(say, usage, EXIT.usage);
  const explore = flags.value.explore ?? path.join(INPUTS, 'explore.json');
  const plan = planOf({ file: explore, home, fixturesDir });
  if (!plan.ok) return said(say, plan.reason, plan.exit);
  const ctx = { say, smoke, explore, opts: { io: ioOver(say), home, pin: plan.value.pin, command: COMMAND } };
  return fromOf(flags.value) === 'run' ? freshRun({ v: flags.value, plan: plan.value, ctx }) : resumed({ v: flags.value, plan: plan.value, ctx });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
