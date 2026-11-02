// E1.S8b: the box's environment — ONE allow-list (parent D15, D35). `--clearenv` then exactly these keys, never a key of the host (V11).
//
// The base is the same for both stacks: the .NET settings of D15, `NODE_OPTIONS` empty (the AGENT box's value; a Node scoring box replaces it with D55's old-space flag, a .NET box keeps it), the declared culture
// `de_DE.UTF-8` (D35: no invariant switch of any kind) and the base time zone `UTC`. `HOME`, `TMPDIR` and `DOTNET_CLI_HOME` name the box's fresh `/tmp`: the box has no home.
// The documented per-stack keys are `DOTNET_ROOT` (the pinned SDK: an apphost reads it, and the box hides the distribution's dotnet) and `PATH` (the pinned toolchains, then the
// system tools; never a `/mnt` entry). A scenario's `env` line may set another `TZ` for one program; any other base key is not an extra's to replace.
const BASE_TMP = '/tmp';

/** The base environment: the same for both stacks, every task. */
export const BASE_ENV = Object.freeze({
  DOTNET_gcServer: '0',
  DOTNET_CLI_TELEMETRY_OPTOUT: '1',
  DOTNET_NOLOGO: '1',
  NODE_OPTIONS: '',
  LANG: 'de_DE.UTF-8',
  LC_ALL: 'de_DE.UTF-8',
  TZ: 'UTC',
  HOME: BASE_TMP,
  TMPDIR: BASE_TMP,
  DOTNET_CLI_HOME: BASE_TMP,
});

/** The per-stack keys, computed from the pinned toolchain directories. */
const PER_STACK_KEYS = Object.freeze(['DOTNET_ROOT', 'PATH']);

/**
 * The keys of one stack only (D55, 2026-11-02): `DOTNET_GCHeapHardLimit` is ⌊0.9·L⌋ bytes in hexadecimal, for .NET (the percent form cannot express it). Node's value is the base's `NODE_OPTIONS`, which a Node
 * scoring box replaces with the old-space flag; it is a base key, so it is not in this table. `heapKeys` derives both.
 */
export const STACK_KEYS = Object.freeze({ csharp: Object.freeze(['DOTNET_GCHeapHardLimit']) });
const STACK_ONLY = Object.values(STACK_KEYS).flat();

/** Every key the box environment can hold: the one literal list the scan compares against. */
export const ALLOW_LIST = Object.freeze([...Object.keys(BASE_ENV), ...PER_STACK_KEYS, ...STACK_ONLY]);

/** The keys a box of `stack` holds (`undefined`: a box for no stack in particular, such as the probe's facts): the allow-list without the other stacks' keys. */
export const allowFor = stack => ALLOW_LIST.filter(key => !STACK_ONLY.includes(key) || (STACK_KEYS[stack] ?? []).includes(key));

const SYSTEM_PATH = Object.freeze(['/usr/bin', '/bin']);

/** The box's PATH: the pinned directories, then the system tools. `dirs` is `{ dotnetSdk, node, typescript }` (full paths). */
export const boxPath = dirs => [dirs.dotnetSdk, `${dirs.node}/bin`, `${dirs.typescript}/node_modules/.bin`, ...SYSTEM_PATH].join(':');

const MIB = 1024 * 1024;

/** The share of the case's limit L the runtime's heap may use (D55): the runtime's own memory — code, stacks, GC bookkeeping, off-heap buffers — needs the rest, or the kernel kills before the GC compacts. */
export const HEAP_SHARE_NUM = 9n;
export const HEAP_SHARE_DEN = 10n;

/** ⌊0.9·L⌋ in whole units, by integer arithmetic (a float would be a guess at the last byte). */
const heapBytes = memoryMaxBytes => Number((BigInt(memoryMaxBytes) * HEAP_SHARE_NUM) / HEAP_SHARE_DEN);

/**
 * The two per-case heap keys of D55, derived from the case's limit L in bytes and never typed by hand: .NET `DOTNET_GCHeapHardLimit` = ⌊0.9·L⌋ bytes in hexadecimal (the percent form cannot express the
 * value), Node `NODE_OPTIONS=--max-old-space-size=⌊0.9·L / 1 MiB⌋`. One pure function for both stacks. L is a whole MiB (D57) and always given: a missing or non-positive L throws for both stacks.
 * A stack that is `undefined` (a box for no stack in particular: the build, the driver, the probe's facts) gets no key.
 */
export function heapKeys(stack, memoryMaxBytes) {
  if (stack === undefined) return {};
  if (!Number.isSafeInteger(memoryMaxBytes) || memoryMaxBytes <= 0 || memoryMaxBytes % MIB !== 0) {
    throw new Error(`heapKeys: the ${stack} stack needs memoryMaxBytes, the case's limit, as a positive whole number of MiB (got ${String(memoryMaxBytes)}): the heap keys are not a guess`);
  }
  if (stack === 'csharp') return { DOTNET_GCHeapHardLimit: `0x${heapBytes(memoryMaxBytes).toString(16)}` };
  if (stack === 'node') return { NODE_OPTIONS: `--max-old-space-size=${Math.floor(heapBytes(memoryMaxBytes) / MIB)}` };
  return {};
}

const lower = key => key.toLowerCase();
const REPLACEABLE = 'TZ';

function refusedKeys(extra) {
  const base = new Map(ALLOW_LIST.map(key => [lower(key), key]));
  return Object.keys(extra).filter(key => base.has(lower(key)) && key !== REPLACEABLE).map(key => base.get(lower(key)));
}

/**
 * The box environment: the base, the per-stack keys, then `extra` (`PORT`, a scenario's `env` lines). An extra may add a key and may replace `TZ`; a base key in any other
 * case or spelling is refused naming the base key (a program reads `lc_all` as it reads `LC_ALL`).
 */
export function boxEnv({ dirs, extra = {}, stack, memoryMaxBytes }) {
  const refused = refusedKeys(extra);
  if (refused.length > 0) throw new Error(`boxEnv: ${refused.join(', ')} ${refused.length === 1 ? 'is' : 'are'} in the base environment (D15) and not an extra's to replace; only TZ may be replaced`);
  return Object.freeze({ ...BASE_ENV, DOTNET_ROOT: dirs.dotnetSdk, PATH: boxPath(dirs), ...heapKeys(stack, memoryMaxBytes), ...extra });
}

/** The keys of `env` that are not on the allow-list (the scan the box's environment and the probe record are checked with). */
export const unlistedKeys = env => Object.keys(env).filter(key => !ALLOW_LIST.includes(key));

/**
 * The driver's environment (the runner, outside the box, and the launcher it starts): the same allow-listed values, a folder of the run's own for `HOME`, `TMPDIR` and `DOTNET_CLI_HOME`
 * (whatever the runner or a build makes dies with the run folder), and `XDG_RUNTIME_DIR` — the one key that is not the program's: `systemd-run --user` finds the user manager through it.
 */
export function driverEnv({ dirs, tmp, runtimeDir = `/run/user/${process.getuid()}` }) {
  return Object.freeze({ ...boxEnv({ dirs }), HOME: tmp, TMPDIR: tmp, DOTNET_CLI_HOME: tmp, XDG_RUNTIME_DIR: runtimeDir });
}

/** The per-case keys of D55: the scoring box's alone (T-L14). `NODE_OPTIONS` is a case key only when it carries the old-space flag; the base's empty `NODE_OPTIONS` is not. */
export const CASE_KEYS = Object.freeze(['DOTNET_GCHeapHardLimit', 'NODE_OPTIONS']);
const OLD_SPACE_FLAG = /(^|\s)--max[-_]old[-_]space[-_]size\b/;

/** The case keys an environment holds, compared without regard to case. Empty for the agent box and the build box. */
export function caseKeysIn(env) {
  return Object.keys(env).filter(key => (lower(key) === lower('DOTNET_GCHeapHardLimit') && env[key] !== undefined) || (lower(key) === 'node_options' && OLD_SPACE_FLAG.test(String(env[key]))));
}

/** Throws naming the keys when `env` — the agent box's or the build box's — holds a per-case heap key (T-L14). */
export function assertNoCaseKeys(env, where) {
  const found = caseKeysIn(env);
  if (found.length > 0) throw new Error(`${where}: the per-case heap key${found.length === 1 ? '' : 's'} ${found.join(', ')} ${found.length === 1 ? 'is' : 'are'} the scoring box's alone (D55, T-L14)`);
}
