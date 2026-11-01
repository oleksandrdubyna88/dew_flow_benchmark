// E1.S5: the installer's only contact with the outside — the official hosts through curl, the official install script, and npm run by the
// pinned Node. Every command is exe + argv with a ceiling (`common.security`); the argv builders are pure so a test reads them without a network.
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const NPM_REGISTRY = 'https://registry.npmjs.org/';
const TEXT_MAX_BYTES = 64 * 1024 * 1024; // npm's full packument of typescript is ~16 MB
const FETCH_MS = 120_000;
const DOWNLOAD_MS = 900_000;
const INSTALL_MS = 1_200_000;
const SYSTEM_PATH = '/usr/bin:/bin';

/** curl's argv for one HTTPS download (redirects stay on HTTPS, a failure status is a failure); `dest` omitted prints to stdout. */
export const curlArgv = ({ url, dest }) => ['-fsSL', '--proto', '=https', '--proto-redir', '=https', '--retry', '3', ...(dest ? ['-o', dest] : []), url];

/** The argv of `dotnet-install.sh`: the exact SDK version, into `installDir`, no PATH edit, the downloaded archive kept at `zipPath` so it can be checked. */
export const dotnetInstallArgv = ({ scriptPath, version, installDir, zipPath }) => [scriptPath, '--version', version, '--install-dir', installDir, '--no-path', '--keep-zip', '--zip-path', zipPath];

/** `npm install` run by the pinned Node itself, with no user or global npmrc, the official registry, no lifecycle scripts. */
export function npmInstallArgv({ nodeDir, prefix, spec, cacheDir = '/dev/null', home = '/dev/null' }) {
  return {
    exe: `${nodeDir}/bin/node`,
    argv: [`${nodeDir}/lib/node_modules/npm/bin/npm-cli.js`, 'install', '--prefix', prefix, spec, '--ignore-scripts', '--no-audit', '--no-fund', '--no-update-notifier', '--registry', NPM_REGISTRY],
    env: { PATH: `${nodeDir}/bin:${SYSTEM_PATH}`, HOME: home, npm_config_cache: cacheDir, npm_config_userconfig: '/dev/null', npm_config_globalconfig: '/dev/null', LANG: 'C.UTF-8' },
  };
}

async function runChecked(exe, argv, options, what) {
  const r = await runBounded(exe, argv, options);
  if (!r.ok) return r;
  const { code, stdout, stderr, timedOut } = r.value;
  if (timedOut) return fail(`${what} timed out after ${options.timeoutMs / 1000} s`);
  if (code !== 0) return fail(`${what} exited ${code}: ${stderr.trim().slice(-400)}`);
  return ok(stdout);
}

/** The real `io` of the installer: `fetchText(url)`, `download(url, dest)`, `runDotnetInstall(opts)`, `runNpmInstall(opts)`, each -> ok | fail. */
export const realIo = Object.freeze({
  fetchText: url => runChecked('curl', curlArgv({ url }), { timeoutMs: FETCH_MS, maxBytes: TEXT_MAX_BYTES }, `curl ${url}`),
  async download(url, dest) {
    const r = await runChecked('curl', curlArgv({ url, dest }), { timeoutMs: DOWNLOAD_MS }, `curl ${url}`);
    return r.ok ? ok(dest) : r;
  },
  runDotnetInstall({ scriptPath, version, installDir, zipPath, workDir }) {
    const env = { PATH: SYSTEM_PATH, HOME: workDir, TMPDIR: workDir, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1', LANG: 'C.UTF-8' };
    return runChecked('/bin/bash', dotnetInstallArgv({ scriptPath, version, installDir, zipPath }), { env, cwd: workDir, timeoutMs: INSTALL_MS }, 'dotnet-install.sh');
  },
  runNpmInstall({ nodeDir, prefix, spec, workDir }) {
    const { exe, argv, env } = npmInstallArgv({ nodeDir, prefix, spec, cacheDir: `${workDir}/npm-cache`, home: workDir });
    return runChecked(exe, argv, { env, cwd: workDir, timeoutMs: INSTALL_MS }, 'npm install');
  },
});
