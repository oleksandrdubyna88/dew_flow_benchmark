// E1.S5: install one pinned toolchain into the private store. The order is the guarantee: the official source must agree with the pin BEFORE
// anything is downloaded; the artefact must be the published bytes; the tree is built in an owned staging folder beside its final place and
// only a verified, sealed tree is renamed into the store — a half-written directory is never registered. Nothing here uses apt or sudo.
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { dotnetOfficial, nodeOfficial, typescriptOfficial } from './toolchain-pins.mjs';
import { checkDigest, sdkVersionsIn, sealTree, treeRecord, unsealTree } from './toolchain-store.mjs';


export const TOOLCHAIN_NAMES = Object.freeze(['dotnetSdk', 'node', 'typescript']);
const EXTRACT_MS = 300_000;

const parseJson = (text, what) => {
  try {
    return ok(JSON.parse(text));
  } catch (e) {
    return fail(`${what} is not JSON (${e.message})`);
  }
};

/** What an official source lists for this pin: ok({ source, listed, pinned }) | fail when it cannot be read or does not list the pin. */
async function officialListing(name, stacks, io) {
  const a = stacks.artifacts;
  const url = { dotnetSdk: a.dotnetSdk.metadataUrl, node: a.node.shasumsUrl, typescript: a.typescript.packumentUrl }[name];
  const text = await io.fetchText(url);
  if (!text.ok) return fail(`the official source ${url} cannot be read: ${text.reason}`);
  const found = await readListing(name, stacks, text.value);
  return found.ok ? ok({ source: url, ...found.value }) : fail(`${url}: ${found.reason}`);
}

function readListing(name, stacks, text) {
  const a = stacks.artifacts;
  if (name === 'node') return Promise.resolve(nodeOfficial(text, a.node.file)).then(r => (r.ok ? ok({ listed: r.value, pinned: a.node.sha256 }) : r));
  const json = parseJson(text, 'the official source');
  if (!json.ok) return Promise.resolve(json);
  if (name === 'typescript') {
    const r = typescriptOfficial(json.value, stacks.pins.typescript);
    return Promise.resolve(r.ok ? ok({ listed: r.value.integrity, pinned: a.typescript.integrity }) : r);
  }
  const r = dotnetOfficial(json.value, { version: stacks.pins.dotnetSdk, rid: a.dotnetSdk.rid, name: a.dotnetSdk.name });
  return Promise.resolve(r.ok ? ok({ listed: r.value.sha512, pinned: a.dotnetSdk.sha512 }) : r);
}

async function crossCheck(name, stacks, io) {
  const listing = await officialListing(name, stacks, io);
  if (!listing.ok) return listing;
  const { source, listed, pinned } = listing.value;
  return listed === pinned ? ok(null) : fail(`${source} now lists ${listed} for ${name}, the pin in stacks-lang.json is ${pinned}: the pin is not moved silently`);
}

async function tarExtract(archive, dest) {
  const r = await runBounded('tar', ['-xJf', archive, '-C', dest, '--strip-components=1', '--no-same-owner'], { timeoutMs: EXTRACT_MS });
  if (!r.ok) return r;
  return r.value.code === 0 ? ok(null) : fail(`tar -xJf ${archive} exited ${r.value.code}: ${r.value.stderr.trim().slice(-300)}`);
}

async function buildNode({ stacks, io, staging, work }) {
  const a = stacks.artifacts.node;
  const archive = path.join(work, a.file);
  const got = await io.download(a.url, archive);
  if (!got.ok) return got;
  const checked = await checkDigest({ file: archive, algorithm: 'sha256', encoding: 'hex', expected: a.sha256, label: `the Node tarball ${a.file}` });
  if (!checked.ok) return checked;
  const unpacked = await tarExtract(archive, staging);
  return unpacked.ok && !existsSync(path.join(staging, 'bin', 'node')) ? fail(`the Node tarball ${a.file} holds no bin/node`) : unpacked;
}

const versionsUnder = (dir, rel) => (existsSync(path.join(dir, rel)) ? readdirSync(path.join(dir, rel)).sort() : []);

async function buildDotnet({ stacks, io, staging, work }) {
  const script = stacks.artifacts.dotnetInstallScript;
  const scriptPath = path.join(work, 'dotnet-install.sh');
  const got = await io.download(script.url, scriptPath);
  if (!got.ok) return got;
  const scriptOk = await checkDigest({ file: scriptPath, algorithm: 'sha256', encoding: 'hex', expected: script.sha256, label: 'the dotnet install script' });
  if (!scriptOk.ok) return fail(`${scriptOk.reason} — it was not run`);
  const zipPath = path.join(work, 'dotnet-sdk.tar.gz');
  const ran = await io.runDotnetInstall({ scriptPath, version: stacks.pins.dotnetSdk, installDir: staging, zipPath, workDir: work });
  if (!ran.ok) return ran;
  const archive = await checkDigest({ file: zipPath, algorithm: 'sha512', encoding: 'hex', expected: stacks.artifacts.dotnetSdk.sha512, label: `the SDK archive the script downloaded from ${stacks.artifacts.dotnetSdk.url}` });
  return archive.ok ? contentsOfSdk(stacks, staging) : archive;
}

function contentsOfSdk(stacks, dir) {
  const sdks = sdkVersionsIn(dir);
  if (sdks.length !== 1 || sdks[0] !== stacks.pins.dotnetSdk) return fail(`the SDK directory must hold only ${stacks.pins.dotnetSdk}; it holds ${sdks.join(', ') || 'no SDK'}`);
  const runtimes = versionsUnder(dir, 'shared/Microsoft.NETCore.App');
  return runtimes.length === 1 && runtimes[0] === stacks.pins.dotnetRuntime ? ok(null) : fail(`the SDK bundles runtime ${runtimes.join(', ') || '(none)'}; the pin is ${stacks.pins.dotnetRuntime}`);
}

async function buildTypescript({ stacks, io, staging, work, storeRoot }) {
  const nodeDir = path.join(storeRoot, stacks.directories.node);
  if (!existsSync(path.join(nodeDir, 'bin', 'node'))) return fail(`TypeScript is installed with the pinned Node, which is not in the store yet: install ${stacks.directories.node} first`);
  const ran = await io.runNpmInstall({ nodeDir, prefix: staging, spec: `typescript@${stacks.pins.typescript}`, workDir: work });
  if (!ran.ok) return ran;
  return lockIntegrity(stacks, staging);
}

function lockIntegrity(stacks, dir) {
  let lock;
  try {
    lock = JSON.parse(readFileSync(path.join(dir, 'node_modules', '.package-lock.json'), 'utf8'));
  } catch (e) {
    return fail(`npm left no readable node_modules/.package-lock.json (${e.code ?? e.message}), so the installed bytes cannot be checked`);
  }
  const entry = lock.packages?.['node_modules/typescript'];
  const pinned = stacks.artifacts.typescript.integrity;
  if (entry?.integrity !== pinned) return fail(`npm installed typescript with integrity ${entry?.integrity ?? '(none)'}, the pinned dist.integrity is ${pinned}`);
  return entry.version === stacks.pins.typescript ? ok(null) : fail(`npm installed typescript ${entry.version}, the pin is ${stacks.pins.typescript}`);
}

const BUILDERS = Object.freeze({ dotnetSdk: buildDotnet, node: buildNode, typescript: buildTypescript });

function discard(dir) {
  try {
    unsealTree(dir);
    rmSync(dir, { recursive: true, force: true });
  } catch (e) {
    process.stderr.write(`could not remove the staging folder ${dir}: ${e.message}\n`);
  }
}

/**
 * `installToolchain({ name, stacks, storeRoot, io, newFolder })` -> ok({ name, dir, state: 'installed' | 'present', treeSha256 }) | fail(reason).
 * `newFolder(what, parent)` makes an owned folder (removed by its owner); `io` is lib/toolchain-io.mjs's `realIo` or a test's fixture.
 * A directory already in the store is left exactly as it is: `present`, `treeSha256: null` (not examined — `verifyRecordedTrees` examines).
 */
export async function installToolchain({ name, stacks, storeRoot, io, newFolder }) {
  if (!TOOLCHAIN_NAMES.includes(name)) return fail(`'${name}' is not a toolchain; the legal names are ${TOOLCHAIN_NAMES.join(', ')}`);
  const final = path.join(storeRoot, stacks.directories[name]);
  if (existsSync(final)) return ok({ name, dir: final, state: 'present', treeSha256: null });
  const agreed = await crossCheck(name, stacks, io);
  if (!agreed.ok) return agreed;
  mkdirSync(storeRoot, { recursive: true, mode: 0o755 });
  const staging = newFolder(`stage-${name}`, storeRoot);
  const work = newFolder(`dl-${name}`, os.tmpdir());
  const built = await BUILDERS[name]({ stacks, io, staging, work, storeRoot });
  if (!built.ok) {
    discard(staging);
    return built;
  }
  sealTree(staging);
  const treeSha256 = treeRecord(staging);
  try {
    renameSync(staging, final);
  } catch (e) {
    discard(staging);
    return fail(`could not move the verified ${name} tree into ${final}: ${e.code ?? e.message}`);
  }
  return ok({ name, dir: final, state: 'installed', treeSha256 });
}
