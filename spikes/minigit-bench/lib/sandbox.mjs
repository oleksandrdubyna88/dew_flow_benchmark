// The agent's box (E2.S2): its own network namespace, one door out. The layout of a session directory outside every git tree, the bwrap argv
// that builds the box, and the environment the agent runs with. Pure except `sessionLayout`, which claims the directory.
//
// What the box holds: only an explicit list of binds (never the session dir: it also holds the instruments); no operator home, /mnt, /run,
// host /tmp; no network but three loopback forwarders in its OWN namespace (box-init.mjs) to unix sockets the harness holds outside; and
// NO credential: CLAUDE_CODE_OAUTH_TOKEN is a DUMMY, the real one lives in the broker on the host (broker.mjs). A secret is never on argv
// (`--setenv` lands in /proc/<pid>/cmdline, readable by every user): the environment travels in the spawn's `env`, never as `--setenv`.
import { existsSync, mkdirSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { FIXED_GIT } from '../../policy-ab-quick/lib/env.mjs';
import { otelEnv } from './otel-receiver.mjs';
import { bind, chdir, devProc, hiddenPaths, isolation, roBind, roRoot, tmpfs } from './bwrap.mjs';
import { fail, ok } from './result.mjs';

export const BOX_PROXY_PORT = 3128;
export const BOX_API_PORT = 8787;
export const BOX_OTEL_PORT = 4318;
/** sun_path: 108 bytes accepted, 109 refused with EINVAL (kernel 6.18.54, Node 26.12.1, measured 2026-10-29). */
export const SOCKET_PATH_MAX_BYTES = 108;
/** Tools passed in `--tools` that CLI 2.1.310's init event does not report (dated 2.1.310). */
export const INIT_UNREPORTED = Object.freeze(['TodoWrite']);
/** Bare-arm: the one host path of `gh` this machine has; a path the host lacks is not mounted over (bwrap cannot create it on a ro root). */
const GH_PATHS = Object.freeze(['/usr/bin/gh', '/usr/local/bin/gh']);

/** The dummy credential a box holds: it opens nothing, and is fresh per session. */
export const dummyToken = rand => `sk-ant-oat01-dummy-${rand}`;

/** The one literal key list of the agent's environment (a test diffs it against `agentEnv`'s keys; `extra` adds its own). */
export const AGENT_ENV_KEYS = Object.freeze([
  'ANTHROPIC_BASE_URL', 'BENCH_CELL', 'CLAUDE_CODE_DISABLE_BACKGROUND_TASKS', 'CLAUDE_CODE_ENABLE_TELEMETRY', 'CLAUDE_CODE_OAUTH_TOKEN', 'CLAUDE_CONFIG_DIR',
  'DISABLE_AUTOUPDATER', 'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_NOLOGO',
  'GIT_AUTHOR_EMAIL', 'GIT_AUTHOR_NAME', 'GIT_COMMITTER_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_CONFIG_GLOBAL', 'GIT_CONFIG_NOSYSTEM', 'GIT_TERMINAL_PROMPT',
  'HOME', 'HTTPS_PROXY', 'HTTP_PROXY', 'LANG', 'NO_PROXY', 'NUGET_PACKAGES',
  'OTEL_EXPORTER_OTLP_ENDPOINT', 'OTEL_EXPORTER_OTLP_PROTOCOL', 'OTEL_LOGS_EXPORTER', 'OTEL_LOGS_EXPORT_INTERVAL', 'OTEL_LOG_TOOL_DETAILS', 'OTEL_LOG_USER_PROMPTS',
  'OTEL_METRICS_EXPORTER', 'OTEL_METRIC_EXPORT_INTERVAL', 'PATH', 'TMPDIR', 'npm_config_cache',
]);

/**
 * The three build-server keys, OFF. NOT in the base environment (E3b seam 2): banteg's bare cells ran with the .NET defaults (servers and node reuse on), and the reason to
 * switch them off, that no cell builds on its partner's node, is already met by the box's own pid and ipc namespaces. An arm that wants them off passes this through `extra`
 * (the process arm decides in E3; parent section 8). A server left running ends with the box: bwrap's init exits with the command and the pid namespace takes the rest.
 */
export const BUILD_SERVERS_OFF = Object.freeze({ DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false' });

const RID = /^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$/;

/** The paths of one session, PURE: `<root>/<rid>/...` (the layout of the story). Nothing in a path the model sees names the arm or the stack. */
export function layoutPaths(root, rid) {
  const dir = path.join(root, rid);
  const at = (...p) => path.join(dir, ...p);
  return Object.freeze({
    rid, root, dir,
    harness: at('harness'), net: at('net'), otel: at('otel'), transcripts: at('transcripts'),
    work: at('box', 'work'), home: at('box', 'home'), config: at('box', 'config'), tmp: at('box', 'tmp'), nuget: at('box', 'nuget'), npm: at('box', 'npm'), out: at('box', 'out'),
    sockets: Object.freeze({ proxy: at('net', 'proxy.sock'), api: at('net', 'api.sock'), otel: at('net', 'otel.sock') }),
    files: Object.freeze({
      settings: at('harness', 'settings.json'), mcp: at('harness', 'mcp.json'), audit: at('harness', 'audit.mjs'), boxInit: at('harness', 'box-init.mjs'), gitconfig: at('harness', 'gitconfig'),
      auditLog: at('box', 'out', 'audit.ndjson'), state: at('state.json'), stream: at('stream.ndjson'), streamStamped: at('stream.stamped.ndjson'), stderr: at('stderr.txt'),
      egressLog: at('egress.ndjson'), egress: at('egress.json'), brokerLog: at('broker.ndjson'), broker: at('broker.json'),
      argv: at('argv.json'), meta: at('meta.json'), readback: at('readback.json'), watchdog: at('watchdog.ndjson'),
    }),
  });
}

const DIRS = Object.freeze(['harness', 'net', 'otel', 'box/work', 'box/home', 'box/config', 'box/tmp', 'box/nuget', 'box/npm', 'box/out']);

/**
 * Claim `<root>/<rid>/` with a NON-recursive mkdir (D-21): EEXIST is a refusal, a rid is never reused; then create the layout's
 * directories. The longest socket path must fit sun_path, counted in UTF-8 bytes. `{ ok, value: layout } | { ok: false, reason }`.
 */
export function sessionLayout(root, rid) {
  if (!path.isAbsolute(root)) return fail(`the runs root must be an absolute path, got '${root}'`);
  if (!RID.test(rid)) return fail(`the rid '${rid}' must be 1-64 letters, digits, '-' or '_' (nothing in a path the model sees names the arm or the stack)`);
  const layout = layoutPaths(root, rid);
  const longest = Buffer.byteLength(layout.sockets.proxy, 'utf8');
  if (longest > SOCKET_PATH_MAX_BYTES) return fail(`the socket path ${layout.sockets.proxy} is ${longest} bytes; a unix socket path holds at most ${SOCKET_PATH_MAX_BYTES}: use a shorter runs root or rid`);
  mkdirSync(root, { recursive: true, mode: 0o700 });
  try {
    mkdirSync(layout.dir, { mode: 0o700 });
  } catch (e) {
    if (e.code === 'EEXIST') return fail(`the session directory ${layout.dir} already exists (EEXIST): a rid is never reused`);
    throw e;
  }
  for (const d of DIRS) mkdirSync(path.join(layout.dir, d), { recursive: true, mode: d === 'net' ? 0o700 : 0o755 });
  return ok(layout);
}

/**
 * The environment the agent runs with (the same for every arm; the process arm adds its own keys through `extra`): a fixed allow-list, never the parent's. `PATH` is constructed, so no `/mnt` entry and no WSL
 * interop variable can exist. The credential is the DUMMY; the real token is not a parameter of this function. `extra` is the process arm's slot: it may add keys, never replace one of the base (that throws).
 */
export function agentEnv({ layout, dummyToken: dummy, cellId, nodeDir, tsDir = null, extra = {} }) {
  const proxy = `http://127.0.0.1:${BOX_PROXY_PORT}`;
  const base = {
    PATH: [`${nodeDir}/bin`, ...(tsDir ? [`${tsDir}/node_modules/.bin`] : []), '/usr/local/bin', '/usr/bin', '/bin'].join(':'),
    HOME: layout.home, TMPDIR: '/tmp', LANG: 'C.UTF-8',
    CLAUDE_CODE_OAUTH_TOKEN: dummy, ANTHROPIC_BASE_URL: `http://127.0.0.1:${BOX_API_PORT}`, CLAUDE_CONFIG_DIR: layout.config,
    CLAUDE_CODE_DISABLE_BACKGROUND_TASKS: '1', DISABLE_AUTOUPDATER: '1',
    ...otelEnv(BOX_OTEL_PORT),
    HTTPS_PROXY: proxy, HTTP_PROXY: proxy, NO_PROXY: '127.0.0.1,localhost',
    DOTNET_CLI_HOME: layout.home, DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    NUGET_PACKAGES: layout.nuget, npm_config_cache: layout.npm,
    BENCH_CELL: cellId, GIT_CONFIG_GLOBAL: layout.files.gitconfig, ...FIXED_GIT,
  };
  // compared in lower case: `https_proxy` is read by curl and others next to `HTTPS_PROXY`, so a different spelling of a base key replaces it as well
  const baseByLower = new Map(Object.keys(base).map(k => [k.toLowerCase(), k]));
  const clash = Object.keys(extra).filter(k => baseByLower.has(k.toLowerCase()));
  if (clash.length > 0) throw new Error(`agentEnv: extra may only ADD keys; ${clash.map(k => (baseByLower.get(k.toLowerCase()) === k ? k : `${k} (the base key ${baseByLower.get(k.toLowerCase())}, in another case)`)).join(', ')} ${clash.length === 1 ? 'is' : 'are'} already set by the base environment (the dummy credential, the proxy and the exporter wiring are not an arm's to replace)`);
  return Object.freeze({ ...base, ...extra });
}

/** The forwarder specs `box-init.mjs` takes: the box's loopback port -> the harness's unix socket. */
export const forwarderSpecs = layout => Object.freeze([
  `${BOX_PROXY_PORT}:${layout.sockets.proxy}`, `${BOX_API_PORT}:${layout.sockets.api}`, `${BOX_OTEL_PORT}:${layout.sockets.otel}`,
]);

const bindPair = b => ['--bind', b.host, b.box];

/**
 * The read-write binds of the agent's box as `{ box, host }` pairs (the box's `/tmp` is `box/tmp`; the rest sit at the same path inside as
 * outside): the one table `agentBoxPrefix` binds from and the link classifier maps a link's target through.
 */
export const writableBinds = (layout, extraRw = []) => Object.freeze([
  { box: '/tmp', host: layout.tmp },
  ...[layout.work, layout.home, layout.config, layout.nuget, layout.npm, layout.out, ...extraRw].map(p => ({ box: p, host: p })),
]);

/**
 * The argv prefix (ending in `--`) of the agent's box: the filesystem read-only; the shared hide list replaced by empty tmpfs; then ONLY an
 * explicit list bound back in, AFTER the tmpfs so it shows. The box's `/tmp` is `box/tmp`, bound before anything else that may live under the
 * host's `/tmp`. Its own network, pid and ipc namespaces. No `--setenv`, no `--clearenv`: the environment is the spawn's `env`.
 */
export function agentBoxPrefix({ layout, arm, toolchains = [], claudeBinary, nodeDir, extraRo = [], extraRw = [], operatorHome = os.homedir(), exists = existsSync }) {
  return Object.freeze([
    'bwrap', ...roRoot(), ...devProc(),
    ...hiddenPaths({ operatorHome, exists }).flatMap(tmpfs),
    ...bindPair(writableBinds(layout)[0]),
    ...[nodeDir, ...toolchains, layout.harness, layout.net, ...extraRo].flatMap(roBind),
    ...roBind(claudeBinary),
    ...writableBinds(layout, extraRw).slice(1).map(b => b.host).flatMap(bind),
    ...(arm === 'bare' ? GH_PATHS.filter(p => exists(p)).flatMap(p => ['--ro-bind', '/dev/null', p]) : []),
    ...isolation(), '--unshare-ipc', ...chdir(layout.work), '--',
  ]);
}

/** The whole argv: the prefix, then `node box-init.mjs <forwarders> -- <command...>` (exe + argv, no shell inside the box either). */
export function agentBoxArgv(opts) {
  const { layout, nodeDir, command } = opts;
  return Object.freeze([...agentBoxPrefix(opts), `${nodeDir}/bin/node`, layout.files.boxInit, ...forwarderSpecs(layout), '--', ...command]);
}
