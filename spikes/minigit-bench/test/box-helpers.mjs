// Test plumbing for the agent box: a session layout under the (hidden) home so no bind lands in the host /tmp, the harness files the box
// needs, and a probe that runs a command in the box and refuses to read a box that did not start as a passing probe.
import assert from 'node:assert/strict';
import { copyFileSync, existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { agentBoxArgv, agentBoxPrefix, agentEnv, dummyToken, sessionLayout } from '../lib/sandbox.mjs';
import { runBounded } from '../lib/run.mjs';
import { findOnPath } from '../lib/find-on-path.mjs';
import { homeDir, spikeDir } from './helpers.mjs';

export const nodeDir = path.dirname(path.dirname(process.execPath));
export const TS_DIR = path.join(os.homedir(), 'rsd', 'minigit', 'toolchains', 'typescript-7.0.2');
export const sleep = ms => new Promise(r => setTimeout(r, ms));

let counter = 0;
/** A fresh session layout on disk with `box-init.mjs` installed; the root sits under the home (which the box hides) so bind mountpoints never land in the host /tmp. */
export function newSession({ rid = `s${++counter}`, root = homeDir('as') } = {}) {
  const made = sessionLayout(root, rid);
  assert.equal(made.ok, true, made.reason);
  copyFileSync(path.join(spikeDir, 'harness', 'box-init.mjs'), made.value.files.boxInit);
  return made.value;
}

export const boxOptions = (layout, over = {}) => ({ layout, arm: 'bare', toolchains: [], claudeBinary: process.execPath, nodeDir, ...over });
export const envFor = (layout, over = {}) => agentEnv({ layout, dummyToken: dummyToken('probe'), cellId: `cell-${layout.rid}`, nodeDir, ...over });

/** The argv without one single-token flag (the control that shows a probe CAN see what the flag hides). */
export function withoutFlag(argv, flag) {
  const i = argv.indexOf(flag);
  assert.ok(i >= 0, `${flag} is in the argv`);
  return [...argv.slice(0, i), ...argv.slice(i + 1)];
}
/** The argv without one `<flag> <path>` pair. */
export function withoutPair(argv, flag, p) {
  const i = argv.findIndex((a, j) => a === flag && argv[j + 1] === p);
  assert.ok(i >= 0, `${flag} ${p} is in the argv`);
  return [...argv.slice(0, i), ...argv.slice(i + 2)];
}
/** The argv without one `<flag> <src> <dest>` triple (a bind). */
export function withoutBind(argv, flag, src) {
  const i = argv.findIndex((a, j) => a === flag && argv[j + 1] === src);
  assert.ok(i >= 0, `${flag} ${src} is in the argv`);
  return [...argv.slice(0, i), ...argv.slice(i + 3)];
}

const SENTINEL = 'BOX-ALIVE';

/** Run `argv` (bwrap first) with `env`, the probe `script` appended through `sh -c`; asserts the box ran it to its sentinel. */
export async function probeArgv(prefix, script, env, { timeoutMs = 30_000 } = {}) {
  const r = await runBounded(prefix[0], [...prefix.slice(1), 'sh', '-c', `${script}\necho ${SENTINEL}`], { env, timeoutMs });
  assert.equal(r.ok, true, r.reason);
  assert.match(r.value.stdout, new RegExp(`${SENTINEL}\\n$`), `the box did not run the probe: ${r.value.stderr}`);
  return { ...r.value, out: r.value.stdout.replace(new RegExp(`${SENTINEL}\\n$`), '') };
}

/** Probe inside the agent box built by `agentBoxPrefix` (no box-init: no listeners). */
export const probe = (layout, script, { over = {}, env = envFor(layout) } = {}) => probeArgv(agentBoxPrefix(boxOptions(layout, over)), script, env);

/** Probe through the real `box-init.mjs` (the three forwarders up), as the launched session runs. */
export async function probeInit(layout, script, { over = {}, env = envFor(layout) } = {}) {
  const argv = agentBoxArgv({ ...boxOptions(layout, over), command: ['sh', '-c', `${script}\necho ${SENTINEL}`] });
  const r = await runBounded(argv[0], argv.slice(1), { env, timeoutMs: 60_000 });
  assert.equal(r.ok, true, r.reason);
  assert.match(r.value.stdout, new RegExp(`${SENTINEL}\\n$`), `the box did not run the probe: ${r.value.stderr}`);
  return { ...r.value, out: r.value.stdout.replace(new RegExp(`${SENTINEL}\\n$`), '') };
}

export const onHost = (cmd, args, timeoutMs = 20_000) => runBounded(cmd, args, { timeoutMs });
export const hostHas = name => findOnPath(name) !== null;
export const exists = existsSync;
