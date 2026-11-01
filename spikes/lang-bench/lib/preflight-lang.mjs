// E1.S5: the preflight of the language comparison v2 — what must be true of the box before a gate, a reference or a session runs on it:
// exactly SDK 10.0.403 (runtime 10.0.14), Node 26.13.0, TypeScript at the pinned 6.0 patch, the pinned Python binary, the German locale generated.
// `judgeProbes` is pure (probe outputs in, problems out, each naming the pin and the value seen); `preflightLang` runs the probes INSIDE a bwrap box
// that binds the pinned directories read-only, built from v1's primitives (`spikes/minigit-bench/lib/bwrap.mjs`) until the execution layer's box exists.
import { accessSync, constants, existsSync, realpathSync, statSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { bwrapFacts } from '../../minigit-bench/lib/host-checks.mjs';
import { findOnPath } from '../../minigit-bench/lib/find-on-path.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { runApphostProbe } from './apphost-probe.mjs';
import { boxArgv, defaultBoxPath, runInBox, storeDirs } from './preflight-box.mjs';
import { verifyStore } from './toolchain-verify.mjs';

const PROBE_MS = 60_000;
const lines = text => text.split('\n').map(l => l.trim()).filter(Boolean);
const plainVersion = text => /\d+\.\d+\.\d+/.exec(text ?? '')?.[0] ?? null;
const normalisedLocale = name => name.toLowerCase().replace(/-/g, '');
const didNotRun = (stacks, name) => `${name}: \`${stacks.probes[name].join(' ')}\` did not run (or failed) inside the box`;

function nodeProblem(out, stacks) {
  const seen = plainVersion(out);
  return seen === stacks.pins.node ? null : `node is ${seen ?? '(unreadable)'}, the pin is ${stacks.pins.node}`;
}

function sdkProblem(out, stacks) {
  const versions = lines(out).map(plainVersion);
  const ok1 = versions.length === 1 && versions[0] === stacks.pins.dotnetSdk;
  return ok1 ? null : `dotnet --list-sdks lists ${versions.join(', ') || 'no SDK'}; the box must hold exactly the pinned SDK ${stacks.pins.dotnetSdk}, no second one`;
}

function runtimeProblem(out, stacks) {
  const versions = [...new Set(lines(out).map(l => plainVersion(l.split(/\s+/)[1])))];
  return versions.length > 0 && versions.every(v => v === stacks.pins.dotnetRuntime) ? null : `dotnet --list-runtimes lists runtime ${versions.join(', ') || '(none)'}; the pin is ${stacks.pins.dotnetRuntime}`;
}

const outsideLine = (seen, stacks) => seen !== null && !seen.startsWith(`${stacks.typescriptLine}.`);

function typescriptProblem(out, stacks) {
  const seen = /Version (\d+\.\d+\.\d+)/.exec(out ?? '')?.[1] ?? null;
  if (seen === stacks.pins.typescript) return null;
  if (outsideLine(seen, stacks)) return `typescript is ${seen}, outside the ${stacks.typescriptLine} line (the pin is ${stacks.pins.typescript}): the type-aware lint supports only ${stacks.typescriptLine}.x`;
  return `typescript is ${seen ?? '(unreadable)'}, the pin is ${stacks.pins.typescript}`;
}

function pythonProblem(out, stacks) {
  const seen = /^([0-9a-f]{64})\b/.exec(out ?? '')?.[1] ?? null;
  return seen === stacks.python.sha256 ? null : `${stacks.python.path} has sha256 ${seen ?? '(unreadable)'}, the pin is ${stacks.python.sha256}`;
}

function localeProblem(out, stacks) {
  const generated = lines(out).some(name => normalisedLocale(name) === normalisedLocale(stacks.locale));
  return generated ? null : `the locale ${stacks.locale} is not generated on this host: run \`sudo locale-gen de_DE.UTF-8\` (then \`locale -a\` must list ${stacks.locale})`;
}

const CHECKS = Object.freeze({ node: nodeProblem, dotnetSdks: sdkProblem, dotnetRuntimes: runtimeProblem, tsc: typescriptProblem, pythonSha256: pythonProblem, locales: localeProblem });

/** What was read, for the record: absent is `null`, never a guessed value. */
function factsFrom(probes, stacks) {
  return {
    node: plainVersion(probes.node),
    dotnetSdks: lines(probes.dotnetSdks ?? '').map(plainVersion),
    dotnetRuntimes: [...new Set(lines(probes.dotnetRuntimes ?? '').map(l => plainVersion(l.split(/\s+/)[1])))],
    typescript: /Version (\d+\.\d+\.\d+)/.exec(probes.tsc ?? '')?.[1] ?? null,
    pythonSha256: /^([0-9a-f]{64})\b/.exec(probes.pythonSha256 ?? '')?.[1] ?? null,
    locale: lines(probes.locales ?? '').find(name => normalisedLocale(name) === normalisedLocale(stacks.locale)) ?? null,
  };
}

/** The probes' outputs (`{ node, dotnetSdks, dotnetRuntimes, tsc, pythonSha256, locales }`, stdout text or `null` for a probe that did not run) against the pins. */
export function judgeProbes({ probes, stacks }) {
  const problems = Object.entries(CHECKS).map(([name, check]) => (probes[name] === null || probes[name] === undefined ? didNotRun(stacks, name) : check(probes[name], stacks))).filter(Boolean);
  return { ok: problems.length === 0, problems, facts: factsFrom(probes, stacks) };
}

const isExecutableFile = file => {
  try {
    accessSync(file, constants.X_OK);
    return statSync(file).isFile();
  } catch {
    return false;
  }
};

/**
 * Every `dotnet` the box's PATH reaches, against the one pinned: a second one (the distribution's, found first or behind the pinned one) is a problem,
 * because `dotnet` on a PATH is whichever comes first and an unattended upgrade can change what that is. One real file listed twice is one dotnet.
 */
export function dotnetCandidates({ boxPath, pinnedDotnet }) {
  const pinned = existsSync(pinnedDotnet) ? realpathSync(pinnedDotnet) : pinnedDotnet;
  const byReal = new Map();
  for (const dir of boxPath) {
    const candidate = path.join(dir, 'dotnet');
    if (isExecutableFile(candidate) && !byReal.has(realpathSync(candidate))) byReal.set(realpathSync(candidate), candidate);
  }
  const others = [...byReal].filter(([real]) => real !== pinned);
  return {
    problems: [
      ...(byReal.size === 0 ? [`no dotnet on the box's PATH (${boxPath.join(':')}); the pinned one is ${pinnedDotnet}`] : []),
      ...others.map(([real, shown]) => `a second dotnet is on the box's PATH: ${shown}${real === shown ? '' : ` (${real})`}; the pin is only ${pinnedDotnet}`),
      ...(byReal.size > 0 && !byReal.has(pinned) ? [`the pinned dotnet ${pinnedDotnet} is not on the box's PATH (${boxPath.join(':')})`] : []),
    ],
  };
}

export { boxArgv, defaultBoxPath };

/** One probe's stdout from inside the box (the exe is looked up on the BOX's PATH), or `null` when it did not run, failed or timed out. */
async function probeInBox({ prefix, argv }) {
  const r = await runInBox(prefix, argv);
  return r !== null && r.code === 0 ? r.stdout : null;
}

async function hostBwrapProblems() {
  const run = async (exe, argv) => {
    const r = await runBounded(exe, argv, { timeoutMs: PROBE_MS });
    return r.ok ? r.value : null;
  };
  return bwrapFacts({ bwrap: await run('bwrap', ['--version']), unshare: await run('unshare', ['-Ur', 'true']) }).problems;
}

const refused = problems => ({ ...fail(`the preflight found ${problems.length} problem(s)`), problems });
const FATAL_HOST = /^(bwrap|user namespaces)/;

/** The host's bwrap and the box PATH's dotnet candidates: `{ problems, fatal }` (fatal: there is no box to probe in). */
async function environmentProblems({ dirs, pathDirs }) {
  const problems = [...await hostBwrapProblems(), ...dotnetCandidates({ boxPath: pathDirs, pinnedDotnet: path.join(dirs.dotnetSdk, 'dotnet') }).problems];
  return { problems, fatal: findOnPath('bwrap') === null || problems.some(p => FATAL_HOST.test(p)) };
}

async function probeAndJudge({ stacks, dirs, pathDirs, operatorHome }) {
  const prefix = boxArgv({ bwrap: findOnPath('bwrap'), dirs, boxPath: pathDirs, operatorHome });
  const probes = Object.fromEntries(await Promise.all(Object.entries(stacks.probes).map(async ([name, argv]) => [name, await probeInBox({ prefix, argv })])));
  return judgeProbes({ probes, stacks });
}

/**
 * `preflightLang({ stacks, storeRoot, boxPath?, operatorHome?, withApphost? })` -> `ok({ facts })` or a fail whose `problems` lists everything wrong (all of it, not the first).
 * `boxPath` is the PATH the box under test will have (default: the pinned directories alone); a PATH that also reaches another `dotnet` is refused.
 * `withApphost: false` skips the apphost probe (a few seconds); only a test of something else says so.
 */
export async function preflightLang({ stacks, storeRoot, boxPath, operatorHome = os.homedir(), withApphost = true }) {
  const dirs = storeDirs(stacks, storeRoot);
  const shallow = verifyStore({ stacks, storeRoot });
  if (!shallow.ok) return refused(shallow.problems);
  const pathDirs = boxPath ?? defaultBoxPath(dirs);
  const environment = await environmentProblems({ dirs, pathDirs });
  if (environment.fatal) return refused(environment.problems);
  const verdict = await probeAndJudge({ stacks, dirs, pathDirs, operatorHome });
  const apphost = withApphost ? await runApphostProbe({ stacks, dirs, boxPath: pathDirs, operatorHome }) : { problems: [], root: null };
  const all = [...environment.problems, ...verdict.problems, ...apphost.problems];
  return all.length === 0 ? ok({ facts: { ...verdict.facts, apphostDotnetRoot: apphost.root } }) : refused(all);
}
