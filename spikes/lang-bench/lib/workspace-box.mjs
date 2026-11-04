// E1.S3: the build box of a workspace. It is the execution layer's own box (`boxArgv` with `prog: null`: a fresh /tmp, the pinned toolchains read-only, `--die-with-parent`), started inside the
// loopback-only network namespace of a holder of its own, so a build has no registry to fall back on. The registered snapshot is bound READ-ONLY at its own path; what a tool needs to WRITE to (the
// .NET CLI home, the npm cache) is a copy made inside the box, on its own tmpfs, before the command is `exec`ed. No copy of a cache is ever made on the host: the layer's box has no writable bind but
// the workspace, and a cache copied into the workspace would be a workspace file.
//
//   nsenter -t <holder> -U -n --preserve-credentials  ->  bwrap <boxArgv ...>  ->  bash -c SEED_SCRIPT seed <from> <to> <command...>
//
// Measured (2026-11-04): the snapshot's `nuget` part works read-only as NUGET_PACKAGES; the `dotnet` part must be in DOTNET_CLI_HOME (else each build ends with a stderr line about verifying
// workloads); npm reads tarballs from a read-only cache by integrity, but anything that reads a packument fails with a misleading ENOTCACHED, so the whole `npm` part is copied to /tmp/npm first.
import { randomBytes } from 'node:crypto';
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { NPM_OFFLINE_ENV } from './cache-offline.mjs';
import { boxArgv } from './exec-layer/box.mjs';
import { trackChild } from './exec-layer/cleanup.mjs';
import { assertNoCaseKeys, boxEnv } from './exec-layer/env.mjs';
import { childMarkerOf } from './exec-layer/names.mjs';
import { nsenterArgv, startHolder, stopHolder } from './exec-layer/netns.mjs';
import { HOLD_MARGIN_S } from './exec-layer/prefix.mjs';

const MIB = 1024 * 1024;
const BOX_TMP = '/tmp';
const SEED_DOTNET = path.posix.join(BOX_TMP, '.dotnet');
const SEED_NPM = path.posix.join(BOX_TMP, 'npm');
/** The values are positional arguments, never text of the script. */
export const SEED_SCRIPT = 'from="$1"; to="$2"; shift 2; mkdir -p "$to" && cp -r "$from"/. "$to"/ && chmod -R u+w "$to" && exec "$@"';

/** The C# keys of a build box: the build servers off, no first-run noise, the certificate revocation check offline. `NUGET_PACKAGES` is added from the snapshot. */
const CSHARP_ENV = Object.freeze({
  DOTNET_CLI_USE_MSBUILD_SERVER: '0',
  MSBUILDDISABLENODEREUSE: '1',
  UseSharedCompilation: 'false',
  DOTNET_GENERATE_ASPNET_CERTIFICATE: 'false',
  DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE: '1',
  NUGET_CERT_REVOCATION_MODE: 'offline',
  TESTINGPLATFORM_TELEMETRY_OPTOUT: '1',
});

/** `~/rsd/lang-v2/caches`, where the registered snapshots live. */
export const defaultCaches = () => path.join(os.homedir(), 'rsd', 'lang-v2', 'caches');

/** `ok(<caches>/<dirName>)` of the stack's registered snapshot, or a `fail` naming the stack and the missing folder. */
export function snapshotDirOf({ stack, record, caches }) {
  const dirName = record?.[stack]?.dirName;
  if (typeof dirName !== 'string') return fail(`the record has no snapshot for the stack ${JSON.stringify(stack)}`);
  const dir = path.join(caches, dirName);
  return existsSync(dir) ? ok(dir) : fail(`the registered ${stack} snapshot folder ${dir} is missing`);
}

function csharpBox(snap, seed) {
  const [nuget, dotnet] = [path.join(snap, 'nuget'), path.join(snap, 'dotnet')];
  return { extraRo: [nuget, dotnet], seed: seed ? { from: dotnet, to: SEED_DOTNET } : null, extra: { ...CSHARP_ENV, NUGET_PACKAGES: nuget } };
}

function nodeBox(snap, seed) {
  const npm = path.join(snap, 'npm');
  return { extraRo: [npm], seed: seed ? { from: npm, to: SEED_NPM } : null, extra: { ...NPM_OFFLINE_ENV, npm_config_cache: seed ? SEED_NPM : npm } };
}

/**
 * What a stack's build box binds, seeds and sets, from its snapshot folder `snap`: `{ extraRo, seed: { from, to } | null, extra }`. `seed: false` is the wrong shape that only the tests ask for: nothing is copied,
 * and a Node box then points npm at the read-only snapshot itself.
 */
export const snapshotBoxOf = ({ stack, snap, seed = true }) => (stack === 'csharp' ? csharpBox(snap, seed) : nodeBox(snap, seed));

/** The environment of a build box: the layer's base plus the stack's extras. NO stack and no memory limit go to `boxEnv`, so no heap key is set (T-L14); a case key would throw. */
export function workspaceBuildEnv({ dirs, stack, snap, seed = true }) {
  const env = boxEnv({ dirs, extra: snapshotBoxOf({ stack, snap, seed }).extra });
  assertNoCaseKeys(env, 'the workspace build box');
  return env;
}

const seeded = (seed, command) => ['bash', '-c', SEED_SCRIPT, 'seed', seed.from, seed.to, ...command];

/** The whole argv of one command in the box, the holder's `nsenter` first. */
export function buildBoxArgv({ holder, view, dirs, stack, snap, command, seed = true, dieWithParent }) {
  const shape = snapshotBoxOf({ stack, snap, seed });
  const box = boxArgv({ view, dirs, env: workspaceBuildEnv({ dirs, stack, snap, seed }), prog: null, extraRo: shape.extraRo, ...(dieWithParent === undefined ? {} : { dieWithParent }) });
  return [...nsenterArgv(holder), ...box, ...(shape.seed === null ? command : seeded(shape.seed, command))];
}

/**
 * One command in the build box of a workspace `view`, with the whole output and the exit code. Its own holder (the loopback-only namespace) lives for exactly this call and ends in `finally`. The command
 * runs under `runBounded` with `timeoutMs`, which kills the whole process group; `onChild` tracks it so a stop signal of the CLI ends it too. -> `ok({ code, stdout, stderr, timedOut, ms })` | `fail`.
 * The box carries `--die-with-parent` (it has no scope, `prog: null`): if the process that started it is killed with SIGKILL and cannot clean up, bwrap's death signal ends the box and everything in it. The timeout
 * of `runBounded` covers the ordinary case. `seed: false` and `dieWithParent: false` exist only so the tests can build the wrong shapes; a production caller never passes them.
 */
export async function inBuildBox({ owned, view, dirs, stack, record, caches = defaultCaches(), command, timeoutMs = 600_000, seed = true, dieWithParent }) {
  const snap = snapshotDirOf({ stack, record, caches });
  if (!snap.ok) return snap;
  const holder = await startHolder({ name: childMarkerOf(owned, 'wsh', randomBytes(4).toString('hex')), boundSeconds: timeoutMs / 1000 + HOLD_MARGIN_S });
  if (!holder.ok) return holder;
  try {
    const argv = buildBoxArgv({ holder: holder.value, view, dirs, stack, snap: snap.value, command, seed, dieWithParent });
    const started = Date.now();
    const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs, maxBytes: 16 * MIB, env: { PATH: '/usr/sbin:/usr/bin:/bin' }, onChild: trackChild });
    return ran.ok ? ok({ ...ran.value, ms: Date.now() - started }) : ran;
  } finally {
    await stopHolder(holder.value);
  }
}
