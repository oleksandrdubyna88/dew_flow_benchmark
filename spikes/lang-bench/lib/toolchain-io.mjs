// E1.S5: the installer's only contact with the outside — the official hosts through curl, the official install script, and npm run by the
// pinned Node. Every command is exe + argv with a ceiling (`common.security`); the argv builders are pure so a test reads them without a network.
import { chmodSync, existsSync, writeFileSync } from 'node:fs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const NPM_REGISTRY = 'https://registry.npmjs.org/';
/** The manifest the TypeScript install starts from; npm adds the dependency. */
export const NPM_PROJECT_MANIFEST = `${JSON.stringify({ name: 'lang-v2-toolchain', version: '0.0.0', private: true }, null, 2)}\n`;
const TEXT_MAX_BYTES = 64 * 1024 * 1024; // npm's full packument of typescript is ~16 MB
const FETCH_MS = 120_000;
const DOWNLOAD_MS = 900_000;
const INSTALL_MS = 1_200_000;
const EXTRACT_MS = 300_000;
const SYSTEM_PATH = '/usr/bin:/bin';

/**
 * The tree hash covers the read and execute bits of every file, and an extraction leaves them as the umask allows: under 077 the same archive hashes
 * differently than under 022. Library code must not change the process-wide umask (it reaches every other caller), so each install child (tar,
 * dotnet-install.sh, npm) is started through `/bin/sh -c 'umask 022 && exec "$@"' sh <exe> <argv>`: the argv travels as arguments, never in the shell string.
 */
export const INSTALL_UMASK = 0o022;
export const underInstallUmask = (exe, argv) => ({ exe: '/bin/sh', argv: ['-c', 'umask 022 && exec "$@"', 'sh', exe, ...argv] });
const runUnderUmask = (exe, argv, options, what) => {
  const wrapped = underInstallUmask(exe, argv);
  return runChecked(wrapped.exe, wrapped.argv, options, what);
};

/** `tar -xJf` of `archive` into `dest`, the archive's top folder stripped, as umask 022 would leave it. -> ok | fail. */
export async function extractTarXz(archive, dest) {
  const r = await runUnderUmask('tar', ['-xJf', archive, '-C', dest, '--strip-components=1', '--no-same-owner'], { timeoutMs: EXTRACT_MS }, `tar -xJf ${archive}`);
  return r.ok ? ok(null) : r;
}

/** curl's argv for one HTTPS download (redirects stay on HTTPS, a failure status is a failure); `dest` omitted prints to stdout. */
export const curlArgv = ({ url, dest }) => ['-fsSL', '--proto', '=https', '--proto-redir', '=https', '--retry', '3', ...(dest ? ['-o', dest] : []), url];

/** The argv of `dotnet-install.sh`: the exact SDK version, into `installDir`, no PATH edit, the downloaded archive kept at `zipPath` so it can be checked. */
export const dotnetInstallArgv = ({ scriptPath, version, installDir, zipPath }) => [scriptPath, '--version', version, '--install-dir', installDir, '--no-path', '--keep-zip', '--zip-path', zipPath];

/**
 * `npm install` run by the pinned Node itself, the official registry, no lifecycle scripts. The user and the global npmrc are two DIFFERENT empty files
 * in `home` (`runNpmInstall` creates them): npm refuses one file loaded as both, and an operator's own .npmrc must never point it elsewhere.
 */
export function npmInstallArgv({ nodeDir, prefix, spec, cacheDir, home }) {
  return {
    exe: `${nodeDir}/bin/node`,
    argv: [`${nodeDir}/lib/node_modules/npm/bin/npm-cli.js`, 'install', '--prefix', prefix, spec, '--ignore-scripts', '--no-audit', '--no-fund', '--no-update-notifier', '--registry', NPM_REGISTRY],
    env: { PATH: `${nodeDir}/bin:${SYSTEM_PATH}`, HOME: home, npm_config_cache: cacheDir, npm_config_userconfig: `${home}/user.npmrc`, npm_config_globalconfig: `${home}/global.npmrc`, LANG: 'C.UTF-8' },
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
  extractTarXz,
  fetchText: url => runChecked('curl', curlArgv({ url }), { timeoutMs: FETCH_MS, maxBytes: TEXT_MAX_BYTES }, `curl ${url}`),
  async download(url, dest) {
    const r = await runChecked('curl', curlArgv({ url, dest }), { timeoutMs: DOWNLOAD_MS }, `curl ${url}`);
    return r.ok ? ok(dest) : r;
  },
  runDotnetInstall({ scriptPath, version, installDir, zipPath, workDir }) {
    const env = { PATH: SYSTEM_PATH, HOME: workDir, TMPDIR: workDir, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1', LANG: 'C.UTF-8' };
    return runUnderUmask('/bin/bash', dotnetInstallArgv({ scriptPath, version, installDir, zipPath }), { env, cwd: workDir, timeoutMs: INSTALL_MS }, 'dotnet-install.sh');
  },
  runNpmInstall({ nodeDir, prefix, spec, workDir }) {
    // npm names the project after its folder (a random staging name) in package-lock.json and node_modules/.package-lock.json unless a manifest is already there:
    // a fixed one keeps the installed tree, and so its recorded tree hash, the same whatever the staging folder was called.
    if (!existsSync(`${prefix}/package.json`)) {
      writeFileSync(`${prefix}/package.json`, NPM_PROJECT_MANIFEST);
      chmodSync(`${prefix}/package.json`, 0o644); // written by THIS process, so its mode must not depend on the caller's umask
    }
    for (const name of ['user.npmrc', 'global.npmrc']) writeFileSync(`${workDir}/${name}`, '');
    const { exe, argv, env } = npmInstallArgv({ nodeDir, prefix, spec, cacheDir: `${workDir}/npm-cache`, home: workDir });
    return runUnderUmask(exe, argv, { env, cwd: workDir, timeoutMs: INSTALL_MS }, 'npm install');
  },
});
