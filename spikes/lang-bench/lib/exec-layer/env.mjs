// E1.S8b: the box's environment — ONE allow-list (parent D15, D35). `--clearenv` then exactly these keys, never a key of the host (V11).
//
// The base is the same for both stacks: the .NET settings of D15, `NODE_OPTIONS` empty (the memory limit is the cgroup's, no flag from the harness), the declared culture
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

/** Every key the box environment holds: the one literal list the scan compares against. */
export const ALLOW_LIST = Object.freeze([...Object.keys(BASE_ENV), ...PER_STACK_KEYS]);

const SYSTEM_PATH = Object.freeze(['/usr/bin', '/bin']);

/** The box's PATH: the pinned directories, then the system tools. `dirs` is `{ dotnetSdk, node, typescript }` (full paths). */
export const boxPath = dirs => [dirs.dotnetSdk, `${dirs.node}/bin`, `${dirs.typescript}/node_modules/.bin`, ...SYSTEM_PATH].join(':');

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
export function boxEnv({ dirs, extra = {} }) {
  const refused = refusedKeys(extra);
  if (refused.length > 0) throw new Error(`boxEnv: ${refused.join(', ')} ${refused.length === 1 ? 'is' : 'are'} in the base environment (D15) and not an extra's to replace; only TZ may be replaced`);
  return Object.freeze({ ...BASE_ENV, DOTNET_ROOT: dirs.dotnetSdk, PATH: boxPath(dirs), ...extra });
}

/** The keys of `env` that are not on the allow-list (the scan the box's environment and the probe record are checked with). */
export const unlistedKeys = env => Object.keys(env).filter(key => !ALLOW_LIST.includes(key));
