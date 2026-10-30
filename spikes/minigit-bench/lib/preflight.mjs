// The preflight (E2.S2 step 5): what must be true of the host before any paid session. The I/O half of host-checks.mjs: it runs the pinned
// tools' probes INSIDE a box built by agentBoxArgv's prefix, with no proxy and no broker running (so the probes cannot reach anything),
// and installs the read-only TypeScript toolchain when asked and absent. Failures are values: `{ ok: false, problems }`.
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { bwrapFacts, toolchainFacts, toolchainInstallPlan } from './host-checks.mjs';
import { runBounded } from './run.mjs';
import { fail, ok } from './result.mjs';
import { agentBoxPrefix, agentEnv, dummyToken, sessionLayout } from './sandbox.mjs';
import { secretStatus } from './secrets.mjs';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';

const PROBE_MS = 60_000;
const INSTALL_MS = 300_000;

const run = async (exe, argv, env) => {
  const r = await runBounded(exe, argv, { timeoutMs: PROBE_MS, ...(env ? { env } : {}) });
  return r.ok ? r.value : null;
};

/** Run the install plan's steps in order; stops at the first failing step. -> ok() | fail(reason). */
async function install(plan) {
  for (const step of plan.steps) {
    const r = await runBounded(step.exe, step.argv, { timeoutMs: INSTALL_MS });
    if (!r.ok || r.value.code !== 0) return fail(`${step.exe} ${step.argv.join(' ')} failed: ${r.ok ? r.value.stderr.slice(-300) : r.reason}`);
  }
  return ok(null);
}

/** Probe outputs from inside a throwaway box: `{ claude, node, dotnetSdks, tsc }` (stdout, '' when the probe failed to run). */
async function probesInBox({ nodeDir, tsDir, claudeBinary, stacks }) {
  const root = mkdtempSync(path.join(os.tmpdir(), 'mgb-preflight-'));
  try {
    const made = sessionLayout(root, 'p');
    if (!made.ok) return made;
    const layout = made.value;
    const prefix = agentBoxPrefix({ layout, arm: 'bare', toolchains: [tsDir], claudeBinary, nodeDir });
    const env = agentEnv({ layout, dummyToken: dummyToken('preflight'), cellId: 'preflight', nodeDir, tsDir });
    const out = {};
    for (const [name, argv] of Object.entries(stacks.probes)) {
      const command = name === 'claude' ? [claudeBinary, ...argv.slice(1)] : argv;
      const r = await run(prefix[0], [...prefix.slice(1), ...command], env);
      out[name] = r && r.code === 0 ? r.stdout : '';
    }
    return ok(out);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

/**
 * -> `ok({ facts, bwrap, token })` or `fail` with `problems` listing everything wrong (all of it, not the first). `install: true` installs the
 * read-only TypeScript toolchain when absent (npm install --prefix, then chmod -R a-w); otherwise its absence is a problem.
 */
export async function preflight({ home, nodeDir, tsDir, claudeBinary, stacks, install: doInstall = false }) {
  const problems = [];
  const bwrap = bwrapFacts({ bwrap: await run('bwrap', ['--version']), unshare: await run('unshare', ['-Ur', 'true']) });
  problems.push(...bwrap.problems);
  const plan = toolchainInstallPlan({ dir: tsDir, exists: existsSync, version: stacks.pins.typescript });
  if (plan !== null && !doInstall) problems.push(`the TypeScript toolchain is absent at ${tsDir}: install it (npm install --prefix ${tsDir} typescript@${stacks.pins.typescript}, then chmod -R a-w)`);
  if (plan !== null && doInstall) {
    const done = await install(plan);
    if (!done.ok) problems.push(done.reason);
  }
  const token = secretStatus({ home, parts: TOKEN_FILE_PARTS, label: 'token' });
  if (!token.present) problems.push(token.why);
  let facts = null;
  if (bwrap.ok && existsSync(path.join(tsDir, 'node_modules', '.bin', 'tsc'))) {
    const probes = await probesInBox({ nodeDir, tsDir, claudeBinary, stacks });
    if (!probes.ok) problems.push(probes.reason);
    else {
      const read = toolchainFacts(probes.value, stacks.pins);
      facts = read.facts;
      problems.push(...read.mismatches.map(m => m.why));
    }
  }
  return problems.length === 0 ? ok({ facts, bwrap, token }) : { ...fail(`the preflight found ${problems.length} problem(s)`), problems };
}
