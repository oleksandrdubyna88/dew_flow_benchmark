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
//   The staging folder is bound to its raw capture: before sanitise runs, `<pending>.source.json` (beside the folder, never in it, removed at the rename) records the raw dir, meta.json's `rid` and `sessionId` and the sha256 of meta.json, and
//   `--from verdicts --raw <dir>` refuses a raw capture that is not that one, naming both.
//   One paid session only: a real run or a resume first takes the per-version lock `.claude-<version>-bare-smoke.lock` beside the fixture folder (v1's `acquireGuardedLock`: O_EXCL, the owner's pid and start time; a lock whose owner is gone is taken
//   over), BEFORE the fixture check, and holds it through run, sanitise, verdicts and rename; a live holder refuses a second run or resume, naming it. The lock is released on every way out. A dry run takes none.
//   `--dry-run` (first form only): runs every check of the copy (it is there, sha256, mode 0555, the version's form), does NOT refuse a fixture folder that exists (it says the folder exists, a real run would refuse it), reads the token as v1's
//   dry run does (that rule is v1's: a home without a token is v1's refusal) and launches nothing.
// Exit 0 done | 3 the environment (the copy, the raw dir or the fixture folder is refused; v1's own: no token, preflight, bad out dir) | 4 usage | 5 `explore.json` cannot be read, or v1's session, sanitise or verdicts failed.
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { shellQuote } from '../../minigit-bench/capture/capture-plan.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { acquireGuardedLock } from '../../minigit-bench/lib/series-lock.mjs';
import { main as v1Main } from '../../minigit-bench/smoke-session.mjs';
import { loadExplore, pinnedPaths } from '../lib/inputs.mjs';
import { INPUTS, V1 } from '../lib/paths.mjs';
import { EXIT, parseFlags, said, writeAtomically } from './generator.mjs';
import { cliPinProblems } from './pin-cli.mjs';

const ENVIRONMENT = 3;
const VERSION = /^\d+\.\d+\.\d+$/;
const FROM = Object.freeze(['run', 'sanitise', 'verdicts']);
export const FIXTURES_DIR = path.join(V1, 'test', 'fixtures', 'real');
/** The folder name `aa-verdicts` looks a smoke fixture up by: the CLI version it was captured on. */
export const fixtureNameOf = version => `claude-${version}-bare-smoke`;
/** Where the fixture is built before it is renamed to its name: a hidden sibling of the fixture folder (the same volume, so the rename is one step). */
export const stagingOf = fixture => path.join(path.dirname(fixture), `.${path.basename(fixture)}.pending`);
/** The record of the raw capture a staging folder was made from: a sibling of the folder, so the published fixture never holds it. */
export const recordOf = staging => `${staging}.source.json`;
/** The per-version lock of the one paid session: a hidden sibling of the fixture folder. */
export const lockFileOf = fixture => path.join(path.dirname(fixture), `.${path.basename(fixture)}.lock`);
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

/** The raw dir's meta.json: `ok({ meta, text })` | `fail(sentence)` when it cannot be read or is not JSON. */
function readMeta(raw) {
  try {
    const text = readFileSync(path.join(raw, 'meta.json'), 'utf8');
    return ok({ meta: JSON.parse(text), text });
  } catch (e) {
    return fail(`${raw} holds no readable meta.json (${e.code ?? e.message}): it is not a finished run of the smoke`);
  }
}

/** The sentence for a raw dir that is not a finished run of the pinned CLI (its meta.json: it reads, the outcome is ok, the CLI that ran was the pinned one), or null. */
function rawProblem(raw, version) {
  const read = readMeta(raw);
  if (!read.ok) return read.reason;
  const { meta } = read.value;
  if (meta.outcome?.ok !== true) return `${raw} holds no completed run: its meta.json outcome is '${meta.outcome?.label ?? 'missing'}', not ok`;
  return meta.cliVersion === version ? null : `${raw} was captured on CLI ${meta.cliVersion}, the pin is ${version}`;
}

/** What identifies a raw capture: its dir, the run's `rid` and `sessionId`, the sha256 of its meta.json. */
const identityOf = (raw, { meta, text }) => ({ raw, rid: meta.rid ?? null, sessionId: meta.sessionId ?? null, metaSha256: createHash('sha256').update(text).digest('hex') });
const describeIdentity = i => `${i.raw} (rid ${i.rid}, session ${i.sessionId}, meta.json sha256 ${String(i.metaSha256).slice(0, 12)})`;

/** The fixture folder's gate: a real run or a resume refuses a folder that has something in it; a dry run only says so. -> an exit code, or null to go on. */
function fixtureGate({ fixture, dry, say }) {
  if (!fixtureTaken(fixture)) return null;
  if (!dry) return said(say, takenSentence(fixture), ENVIRONMENT);
  say(`note: the fixture folder ${fixture} exists and is not empty: a real run would refuse it`);
  return null;
}

/** The record of `staging`: `ok(identity)` | `fail` when there is none or it cannot be read. */
function recordRead(staging) {
  try {
    return ok(JSON.parse(readFileSync(recordOf(staging), 'utf8')));
  } catch {
    return fail(`there is no record of which raw capture ${staging} was made from: run again with --from sanitise`);
  }
}

/** `--from verdicts`: the kept staging folder must be there and be bound to THIS raw capture. -> `ok` | `fail` naming the recorded capture and the given one. */
function boundTo({ staging, identity }) {
  if (!existsSync(staging)) return fail(`there is no sanitised copy at ${staging} to run the verdicts on: run again with --from sanitise`);
  const recorded = recordRead(staging);
  if (!recorded.ok) return recorded;
  const same = Object.keys(identity).every(key => recorded.value[key] === identity[key]);
  return same ? ok(null) : fail(`${staging} was made from ${describeIdentity(recorded.value)}; the raw capture given is ${describeIdentity(identity)}, not the capture it was made from. Give the recorded one, or redo from the start with --from sanitise`);
}

/** `--from sanitise`: no staging folder (a stale one is a killed run's, removed) and a fresh record, written before sanitise runs so a staging folder never exists without it. -> `ok` | `fail`. */
function freshStaging({ staging, identity }) {
  mkdirSync(path.dirname(staging), { recursive: true });
  rmSync(staging, { recursive: true, force: true });
  rmSync(recordOf(staging), { force: true });
  const written = writeAtomically(recordOf(staging), `${JSON.stringify(identity, null, 2)}\n`);
  return written.ok ? ok(null) : written;
}

/** The staging folder and its record made ready for `from`. -> `ok` | `fail`. */
function prepared({ from, raw, staging }) {
  const read = readMeta(raw);
  if (!read.ok) return read;
  const identity = identityOf(raw, read.value);
  return from === 'verdicts' ? boundTo({ staging, identity }) : freshStaging({ staging, identity });
}

/** v1's verbs that make the fixture from the raw capture, from `from` on: sanitise into the staging folder, then the verdicts on it. */
const verbsFrom = ({ from, raw, staging }) => [...(from === 'sanitise' ? [['sanitise', '--from', raw, '--to', staging]] : []), ['verdicts', '--fixture', staging]];

const resumeLine = ({ step, raw, explore }) => `node spikes/lang-explore/prereg/smoke-pinned.mjs --explore ${shellQuote(explore)} --from ${step} --raw ${shellQuote(raw)}`;
/** What is said when a step after the paid session fails: the raw capture is untouched and the way on needs no second session. */
const stoppedAfter = ({ verb, code, raw, explore }) => `stopped after ${verb} (exit ${code}); no fixture folder was made and the raw capture ${raw} is untouched. Redo it without a second session: ${resumeLine({ step: verb, raw, explore })}`;

/** A step after the paid session failed: a sanitise that failed leaves no staging folder (what it wrote is not whole), a verdicts that failed keeps it; says the way on. -> the step's exit code. */
function stopped({ verb, code, staging, raw, ctx }) {
  if (verb === 'sanitise') [staging, recordOf(staging)].forEach(file => rmSync(file, { recursive: true, force: true }));
  return said(ctx.say, stoppedAfter({ verb, code, raw, explore: ctx.explore }), code);
}

/** The staging folder renamed to the fixture's name: the last step. -> `ok` | `fail` (the staging folder is kept for `--from verdicts`). */
function renamedIntoPlace({ staging, fixture, raw, explore }) {
  try {
    renameSync(staging, fixture);
    rmSync(recordOf(staging), { force: true });
    return ok(fixture);
  } catch (e) {
    return fail(`cannot rename ${staging} to ${fixture}: ${e.code ?? e.message}; the sanitised copy is kept. Redo the last step: ${resumeLine({ step: 'verdicts', raw, explore })}`);
  }
}

const concluded = ({ say, done }) => (done.ok ? said(say, `smoke fixture written: ${done.value} (SMOKE.md there; commit it by path)`, EXIT.done) : said(say, done.reason, EXIT.input));

/** The fixture from the raw capture: the verbs from `from` on, each through v1's `main` (the first that does not end 0 stops the rest), then the rename. */
async function finish({ from, raw, plan, ctx }) {
  const staging = stagingOf(plan.fixture);
  const ready = prepared({ from, raw, staging });
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

/** The per-version lock of the one paid session: `ok({ release })` | `fail` naming a live holder. A lock whose owner is gone is taken over. */
function lockTaken({ plan }) {
  const lock = lockFileOf(plan.fixture);
  mkdirSync(path.dirname(lock), { recursive: true });
  const busy = o => fail(`a smoke on CLI ${plan.version} is in use by pid ${o.pid} (started ${o.startTime}): it holds ${lock}. One paid session only: wait for it to end, then redo what it left with --from sanitise|verdicts --raw <its out dir>`);
  return acquireGuardedLock({ dir: path.dirname(lock), name: path.basename(lock), busy });
}

/** A dry run launches nothing and takes no lock; a run or a resume holds the lock through every step and releases it on every way out. */
async function dispatch({ v, plan, ctx }) {
  if (v['dry-run'] === true) return freshRun({ v, plan, ctx });
  const locked = lockTaken({ plan });
  if (!locked.ok) return said(ctx.say, locked.reason, ENVIRONMENT);
  try {
    return await (fromOf(v) === 'run' ? freshRun : resumed)({ v, plan, ctx });
  } finally {
    locked.value.release();
  }
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
  return dispatch({ v: flags.value, plan: plan.value, ctx });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
