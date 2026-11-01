// E1.S5: the installer's guarantees, run against local fixtures through the `io` seam (nothing here touches the network): an artefact is kept only
// when it is the published bytes, an official source that disagrees with the pin stops the install before a download, the install script runs
// only when it is the recorded one, and a half-written directory is never registered in the store.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { installToolchain } from '../lib/toolchain-install.mjs';
import { curlArgv, dotnetInstallArgv, npmInstallArgv } from '../lib/toolchain-io.mjs';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { sealTree, unsealTree, writablePaths } from '../lib/toolchain-store.mjs';
import { spawnSync } from 'node:child_process';
import { makeTarXz, ownedApi, ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const real = JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/stacks-lang.json', import.meta.url)), 'utf8'));
const hash = (algorithm, text, encoding = 'hex') => createHash(algorithm).update(text).digest(encoding);

/** A store root, fixture artefacts whose published checksums are written into a copy of the real pins, and an `io` that serves them and counts calls. */
function rig(t, { nodeFiles, scriptText = '#!/bin/bash\n:\n', archiveText = 'sdk archive bytes', npmIntegrityLie = false } = {}) {
  const storeRoot = ownedFolder(t, 'store');
  const serve = ownedFolder(t, 'serve');
  const nodeArchive = makeTarXz(t, path.join(serve, 'node.tar.xz'), nodeFiles ?? { 'node-v26.13.0-linux-x64/bin/node': '#!node\n', 'node-v26.13.0-linux-x64/README.md': 'r\n' });
  const stacks = structuredClone(real);
  delete stacks.treeSha256; // the committed hashes are those of the real toolchains; a fixture tree has its own
  stacks.artifacts.node.sha256 = hash('sha256', readFileSync(nodeArchive));
  stacks.artifacts.dotnetSdk.sha512 = hash('sha512', archiveText);
  stacks.artifacts.dotnetInstallScript.sha256 = hash('sha256', scriptText);
  const integrity = `sha512-${hash('sha512', 'ts tarball', 'base64')}`;
  stacks.artifacts.typescript.integrity = integrity;
  const text = new Map([
    [stacks.artifacts.dotnetSdk.metadataUrl, JSON.stringify({ releases: [{ sdk: { version: '10.0.403', files: [{ rid: 'linux-x64', name: stacks.artifacts.dotnetSdk.name, url: stacks.artifacts.dotnetSdk.url, hash: stacks.artifacts.dotnetSdk.sha512 }] } }] })],
    [stacks.artifacts.node.shasumsUrl, `${stacks.artifacts.node.sha256}  ${stacks.artifacts.node.file}\n`],
    [stacks.artifacts.typescript.packumentUrl, JSON.stringify({ versions: { '6.0.3': { dist: { tarball: stacks.artifacts.typescript.tarball, integrity } } } })],
  ]);
  const calls = { downloads: [], scripts: [], npm: [] };
  const io = {
    fetchText: async url => (text.has(url) ? { ok: true, value: text.get(url) } : { ok: false, reason: `no fixture for ${url}` }),
    download: async (url, dest) => {
      calls.downloads.push(url);
      if (url === stacks.artifacts.node.url) writeFileSync(dest, readFileSync(nodeArchive));
      else if (url === stacks.artifacts.dotnetInstallScript.url) writeFileSync(dest, scriptText);
      else return { ok: false, reason: `no fixture for ${url}` };
      return { ok: true, value: dest };
    },
    runDotnetInstall: async ({ scriptPath, installDir, zipPath, version }) => {
      calls.scripts.push({ scriptPath, version });
      writeFileSync(zipPath, archiveText);
      writeTree(installDir, { dotnet: 'host', [`sdk/${version}/dotnet.dll`]: 'sdk', 'shared/Microsoft.NETCore.App/10.0.14/a.dll': 'rt' });
      return { ok: true, value: null };
    },
    runNpmInstall: async ({ prefix }) => {
      calls.npm.push(prefix);
      writeTree(prefix, { 'node_modules/typescript/bin/tsc': '#!node\n', 'node_modules/.package-lock.json': JSON.stringify({ packages: { 'node_modules/typescript': { version: '6.0.3', integrity: npmIntegrityLie ? 'sha512-liar' : integrity } } }) });
      return { ok: true, value: null };
    },
  };
  const newFolder = (what, parent) => ownedFolder(t, what, parent);
  return { stacks, storeRoot, io, calls, newFolder, run: name => installToolchain({ name, stacks, storeRoot, io, newFolder }) };
}

/** Unseal a store before the owned-folder cleanup removes it. */
const storeEntries = root => readdirSync(root).filter(name => !name.endsWith('.owner')); // the owned helper's claim files sit beside a folder until the test ends
const unsealStore = storeRoot => { for (const name of storeEntries(storeRoot)) unsealTree(path.join(storeRoot, name)); };

test('The_fixture_pins_are_valid_pins', t => {
  assert.equal(loadStacksLang(rig(t).stacks).ok, true);
});

test('Node_is_installed_from_the_verified_tarball_sealed_and_its_tree_hash_returned', async t => {
  const r = rig(t);
  const done = await r.run('node');
  try {
    assert.equal(done.ok, true, JSON.stringify(done));
    const dir = path.join(r.storeRoot, r.stacks.directories.node);
    assert.equal(done.value.dir, dir);
    assert.equal(done.value.state, 'installed');
    assert.match(done.value.treeSha256, /^[0-9a-f]{64}$/);
    assert.equal(readFileSync(path.join(dir, 'bin', 'node'), 'utf8'), '#!node\n', 'the archive root folder is stripped');
    assert.deepEqual(writablePaths(dir), []);
    assert.deepEqual(storeEntries(r.storeRoot), [r.stacks.directories.node], 'no staging folder is left in the store');
  } finally { unsealStore(r.storeRoot); }
});

test('A_download_that_is_not_the_published_bytes_is_refused_and_nothing_is_kept', async t => {
  const r = rig(t);
  r.stacks.artifacts.node.sha256 = 'e'.repeat(64);
  r.io.fetchText = (orig => async url => (url === r.stacks.artifacts.node.shasumsUrl ? { ok: true, value: `${'e'.repeat(64)}  ${r.stacks.artifacts.node.file}\n` } : orig(url)))(r.io.fetchText);
  const done = await r.run('node');
  assert.equal(done.ok, false);
  assert.match(done.reason, /sha256.*e{64}/s, 'names the published digest');
  assert.deepEqual(storeEntries(r.storeRoot), [], 'the store holds nothing: a half-written directory is never registered');
});

test('An_official_source_that_lists_another_checksum_than_the_pin_stops_the_install_before_any_download', async t => {
  const r = rig(t);
  r.io.fetchText = async () => ({ ok: true, value: `${'9'.repeat(64)}  ${r.stacks.artifacts.node.file}\n` });
  const done = await r.run('node');
  assert.equal(done.ok, false);
  assert.match(done.reason, /9{64}.*pin|pin.*9{64}/s);
  assert.deepEqual(r.calls.downloads, [], 'nothing was downloaded');
});

test('An_official_source_that_cannot_be_read_stops_the_install_instead_of_trusting_the_pin_alone', async t => {
  const r = rig(t);
  r.io.fetchText = async () => ({ ok: false, reason: 'offline' });
  const done = await r.run('node');
  assert.equal(done.ok, false);
  assert.match(done.reason, /official.*offline|offline/s);
  assert.deepEqual(r.calls.downloads, []);
});

test('A_toolchain_already_in_the_store_is_left_alone_not_reinstalled', async t => {
  const r = rig(t);
  const dir = writeTree(path.join(r.storeRoot, r.stacks.directories.node), { 'bin/node': 'existing' });
  const done = await r.run('node');
  assert.equal(done.ok, true, JSON.stringify(done));
  assert.equal(done.value.state, 'present');
  assert.deepEqual(r.calls.downloads, []);
  assert.equal(readFileSync(path.join(dir, 'bin', 'node'), 'utf8'), 'existing');
});

test('The_SDK_is_installed_by_the_recorded_install_script_and_the_archive_it_downloaded_is_checked', async t => {
  const r = rig(t);
  const done = await r.run('dotnetSdk');
  try {
    assert.equal(done.ok, true, JSON.stringify(done));
    assert.deepEqual(r.calls.scripts.map(s => s.version), ['10.0.403']);
    assert.deepEqual(readdirSync(path.join(done.value.dir, 'sdk')), ['10.0.403']);
    assert.deepEqual(writablePaths(done.value.dir), []);
  } finally { unsealStore(r.storeRoot); }
});

test('An_install_script_that_is_not_the_recorded_one_is_never_run', async t => {
  const r = rig(t);
  r.stacks.artifacts.dotnetInstallScript.sha256 = 'c'.repeat(64);
  const done = await r.run('dotnetSdk');
  assert.equal(done.ok, false);
  assert.match(done.reason, /install script.*c{64}|c{64}.*install script/s);
  assert.deepEqual(r.calls.scripts, [], 'the script did not run');
});

test('An_SDK_archive_that_is_not_the_published_bytes_is_refused_after_the_script_and_nothing_is_kept', async t => {
  const r = rig(t);
  r.io.runDotnetInstall = async ({ installDir, zipPath }) => { writeFileSync(zipPath, 'tampered'); writeTree(installDir, { 'sdk/10.0.403/dotnet.dll': 'x' }); return { ok: true, value: null }; };
  const done = await r.run('dotnetSdk');
  assert.equal(done.ok, false);
  assert.match(done.reason, /sha512/);
  assert.deepEqual(storeEntries(r.storeRoot), []);
});

test('An_SDK_install_that_leaves_a_second_SDK_in_the_directory_is_refused_and_not_registered', async t => {
  const r = rig(t);
  r.io.runDotnetInstall = async ({ installDir, zipPath }) => { writeFileSync(zipPath, 'sdk archive bytes'); writeTree(installDir, { 'sdk/10.0.403/a': 'x', 'sdk/10.0.114/a': 'y' }); return { ok: true, value: null }; };
  const done = await r.run('dotnetSdk');
  assert.equal(done.ok, false);
  assert.match(done.reason, /10\.0\.114/);
  assert.deepEqual(storeEntries(r.storeRoot), []);
});

test('TypeScript_is_installed_by_the_pinned_node_and_the_integrity_in_the_lock_is_the_pinned_one', async t => {
  const r = rig(t);
  const noNode = await r.run('typescript');
  assert.equal(noNode.ok, false, 'TypeScript is installed with the pinned Node, which must be in the store first');
  assert.match(noNode.reason, /node-v26\.13\.0-linux-x64/);
  assert.deepEqual(r.calls.npm, []);
  const node = await r.run('node');
  assert.equal(node.ok, true, JSON.stringify(node));
  const done = await r.run('typescript');
  try {
    assert.equal(done.ok, true, JSON.stringify(done));
    assert.equal(statSync(path.join(done.value.dir, 'node_modules', 'typescript', 'bin', 'tsc')).isFile(), true);
  } finally { unsealStore(r.storeRoot); }
});

test('A_TypeScript_install_whose_lock_integrity_is_not_the_pinned_one_is_refused_and_not_registered', async t => {
  const r = rig(t, { npmIntegrityLie: true });
  const node = await r.run('node');
  assert.equal(node.ok, true, JSON.stringify(node));
  const done = await r.run('typescript');
  try {
    assert.equal(done.ok, false);
    assert.match(done.reason, /integrity.*sha512-liar/s);
    assert.deepEqual(storeEntries(r.storeRoot), [r.stacks.directories.node]);
  } finally { unsealStore(r.storeRoot); }
});

test('A_name_that_is_not_a_toolchain_is_refused_naming_the_legal_ones', async t => {
  const done = await rig(t).run('python');
  assert.equal(done.ok, false);
  assert.match(done.reason, /dotnetSdk, node, typescript/);
});

test('The_real_install_commands_are_argv_with_the_pinned_version_and_reach_only_the_official_hosts', () => {
  const dotnet = dotnetInstallArgv({ scriptPath: '/w/dotnet-install.sh', version: '10.0.403', installDir: '/s/dotnet', zipPath: '/w/sdk.tar.gz' });
  assert.deepEqual(dotnet, ['/w/dotnet-install.sh', '--version', '10.0.403', '--install-dir', '/s/dotnet', '--no-path', '--keep-zip', '--zip-path', '/w/sdk.tar.gz']);
  const npm = npmInstallArgv({ nodeDir: '/s/node', prefix: '/s/ts', spec: 'typescript@6.0.3', cacheDir: '/w/npm-cache', home: '/w' });
  assert.deepEqual(npm.argv, ['/s/node/lib/node_modules/npm/bin/npm-cli.js', 'install', '--prefix', '/s/ts', 'typescript@6.0.3', '--ignore-scripts', '--no-audit', '--no-fund', '--no-update-notifier', '--registry', 'https://registry.npmjs.org/']);
  assert.equal(npm.exe, '/s/node/bin/node', 'the pinned Node runs npm, not the host one');
  assert.ok(npm.env.PATH.startsWith('/s/node/bin:'), npm.env.PATH);
  assert.equal(npm.env.npm_config_userconfig, '/w/user.npmrc', 'an empty file of ours: no user .npmrc can point npm at another registry');
  assert.equal(npm.env.npm_config_globalconfig, '/w/global.npmrc');
  assert.notEqual(npm.env.npm_config_userconfig, npm.env.npm_config_globalconfig, 'npm stops with "double-loading config" when one file is both the user and the global config');
  assert.deepEqual(curlArgv({ url: 'https://nodejs.org/x', dest: '/w/x' }).slice(0, 4), ['-fsSL', '--proto', '=https', '--proto-redir']);
});

/** Runs `fn` with the process umask set to `mask`, then puts the old one back. */
async function underUmask(mask, fn) {
  const before = process.umask(mask);
  try {
    return await fn();
  } finally {
    process.umask(before);
  }
}

test('An_extraction_under_a_restrictive_umask_gives_the_same_tree_hash_as_under_the_usual_one', async t => {
  const outer = process.umask();
  const usual = await underUmask(0o022, async () => { const r = rig(t); const done = await r.run('node'); return { r, done }; });
  const strict = await underUmask(0o022, async () => rig(t)); // the fixture archive is made under the usual umask in both cases
  const tight = await underUmask(0o077, async () => { const done = await strict.run('node'); return { ...done, umaskAfter: process.umask() }; });
  try {
    assert.equal(usual.done.ok, true, JSON.stringify(usual.done));
    assert.equal(tight.ok, true, JSON.stringify(tight));
    assert.equal(tight.value.treeSha256, usual.done.value.treeSha256, 'the tree hash is that of the files as the archive holds them, not as this user\'s umask left them');
    assert.equal(tight.umaskAfter, 0o077, 'the caller\'s umask is put back by the installer itself');
    assert.equal(process.umask(), outer);

  } finally { unsealStore(usual.r.storeRoot); unsealStore(strict.storeRoot); }
});

test('A_tree_that_differs_from_the_recorded_hash_is_discarded_and_never_registered', async t => {
  const first = rig(t);
  const reference = await first.run('node');
  try {
    assert.equal(reference.ok, true, JSON.stringify(reference));
  } finally { unsealStore(first.storeRoot); }
  const wrong = rig(t);
  wrong.stacks.treeSha256 = { node: 'f'.repeat(64) };
  const refused = await wrong.run('node');
  assert.equal(refused.ok, false);
  assert.match(refused.reason, new RegExp(`tree hash ${reference.value.treeSha256}.*recorded.*f{64}`, 's'), 'names the hash found and the hash recorded');
  assert.deepEqual(storeEntries(wrong.storeRoot), [], 'the tree was discarded before it was registered');
  const right = rig(t);
  right.stacks.treeSha256 = { node: reference.value.treeSha256 };
  const accepted = await right.run('node');
  try {
    assert.equal(accepted.ok, true, JSON.stringify(accepted));
  } finally { unsealStore(right.storeRoot); }
});

/** A staging folder in `storeRoot` named as the installer names them, sealed, with a claim beside it written for `pid`. */
function staleStaging(storeRoot, id, { pid, claim = true } = {}) {
  const dir = path.join(storeRoot, `${ownedApi.ownedPrefix('stage')}node-${id}`);
  writeTree(dir, { 'bin/node': 'half written\n' });
  if (claim) writeFileSync(`${dir}.owner`, `${ownedApi.formatOwner({ ...ownedApi.thisOwner(), pid, start: 1 })}\n`);
  sealTree(dir);
  return dir;
}

const deadPid = () => spawnSync('true').pid;

test('A_staging_folder_left_by_an_interrupted_install_is_unsealed_removed_and_named_when_its_owner_is_gone', async t => {
  if (!ownedApi) return t.skip('the conventions mount is absent: no owner records to read');
  const r = rig(t);
  mkdirSync(r.storeRoot, { recursive: true });
  const stale = staleStaging(r.storeRoot, 'dead01', { pid: deadPid() });
  const live = staleStaging(r.storeRoot, 'live01', { pid: process.pid });
  const unmarked = staleStaging(r.storeRoot, 'none01', { claim: false });
  const foreign = path.join(r.storeRoot, 'testsToDelete-u99999-bench-stage-node-other1');
  writeTree(foreign, { a: 'b' });
  const notStaging = path.join(r.storeRoot, `${ownedApi.ownedPrefix('something-else')}x1`); // an owned folder of ours with a dead owner, but not an installer's staging folder
  writeTree(notStaging, { a: 'b' });
  writeFileSync(`${notStaging}.owner`, `${ownedApi.formatOwner({ ...ownedApi.thisOwner(), pid: deadPid(), start: 1 })}\n`);
  const notes = [];
  const done = await installToolchain({ name: 'node', stacks: r.stacks, storeRoot: r.storeRoot, io: r.io, newFolder: r.newFolder, owned: ownedApi, log: line => notes.push(line) });
  try {
    assert.equal(done.ok, true, JSON.stringify(done));
    assert.deepEqual(done.value.swept, [stale]);
    assert.equal(existsSync(stale), false, 'the dead owner\'s sealed folder is gone');
    assert.equal(existsSync(`${stale}.owner`), false, 'and its claim');
    assert.ok(notes.some(line => line.includes(stale)), `each removal is named: ${notes.join(' | ')}`);
    assert.ok(existsSync(live) && existsSync(unmarked) && existsSync(foreign) && existsSync(notStaging), 'a live owner\'s, an unmarked, another user\'s and a not-staging folder are left alone');
  } finally {
    for (const dir of [live, unmarked]) unsealTree(dir);
    unsealStore(r.storeRoot);
  }
});

test('The_first_install_into_a_store_root_that_does_not_exist_yet_has_nothing_to_collect', async t => {
  if (!ownedApi) return t.skip('the conventions mount is absent: no owner records to read');
  const r = rig(t);
  const root = path.join(r.storeRoot, 'not-yet');
  const done = await installToolchain({ name: 'node', stacks: r.stacks, storeRoot: root, io: r.io, newFolder: r.newFolder, owned: ownedApi });
  try {
    assert.equal(done.ok, true, JSON.stringify(done));
    assert.deepEqual(done.value.swept, []);
  } finally { unsealStore(root); }
});
