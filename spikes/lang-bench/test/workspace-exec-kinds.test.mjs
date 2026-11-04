// E1.S3 phase A, the failure cases and the process shape on the execution layer (the real toolchains and the registered snapshots): a package outside the set, a lower version, the offered packages an agent
// may add, the type check and the lint of the Node workspace, the read-only npm cache that would look like a missing package, T-L31 (`run.sh` ends in `exec`, so the program has no bash parent and SIGTERM
// reaches it), the same templates in the agent box, and the stability of the lock generation. The hello builds, the verdicts and the build box itself are in workspace-exec.test.mjs.
import assert from 'node:assert/strict';
import { cpSync, existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { sweepLeftovers } from '../../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { NPM_OFFLINE_ENV, npmOfflineArgv, openBox } from '../lib/cache-offline.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { statOf } from '../lib/exec-layer/proc.mjs';
import { defaultCaches, snapshotDirOf } from '../lib/workspace-box.mjs';
import { runShProblem } from '../lib/workspace-build.mjs';
import { ensureTestRoot } from './support/cache-box.mjs';
import { chain, commOf, needs, pidsIn, waitFor } from './support/exec-chain.mjs';
import { toolchains } from './support/exec-layer-host.mjs';
import { assertSnapshotsUnchanged, buildHello, exeBase, helloWorkspace, inBox, outputOf, programPidIn, snapshotRecord, snapshotsMissing, STACKS, stopStubSource, TEMPLATES } from './support/workspace-fixtures.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const REPO = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const SPIKE = path.join(REPO, 'spikes', 'lang-bench');
const MIB = 1024 * 1024;
const TIMEOUT = { timeout: 15 * 60_000 };
const read = file => readFileSync(file, 'utf8');
const NPM_INSTALL_LOCK = ['npm', 'install', '--package-lock-only', '--offline', '--ignore-scripts', '--no-audit', '--no-fund'];

before(() => { sweepLeftovers({ repo: REPO, tmp: ensureTestRoot(), token: 'bench' }); });
after(() => { if (snapshotsMissing() === null) assertSnapshotsUnchanged(); });

/** Adds `<PackageReference Include=name Version=version />` before `</Project>` of the workspace's App.csproj. */
function addReference(w, name, version) {
  const file = path.join(w, 'App.csproj');
  writeFileSync(file, read(file).replace('</Project>', `  <ItemGroup>\n    <PackageReference Include="${name}" Version="${version}" />\n  </ItemGroup>\n</Project>`));
}

// ---- a read-only npm cache would look like a missing package -----------------------------------------------------------------------------------------

test('A_read_only_npm_cache_would_look_like_a_missing_package_and_that_is_the_point', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const folder = label => {
    const dir = ownedFolder(t, label, ensureTestRoot());
    for (const name of ['package.json', '.npmrc']) cpSync(path.join(TEMPLATES, 'node', 'olympiad', name), path.join(dir, name));
    return dir;
  };
  const wrong = await inBox(folder('wrong'), 'node', NPM_INSTALL_LOCK, { seed: false });
  assert.notEqual(wrong.code, 0, 'a cache bound read-only and not copied cannot serve a packument');
  assert.match(outputOf(wrong), /ENOTCACHED/, 'the misleading error: the package IS in the cache');
  const right = await inBox(folder('right'), 'node', NPM_INSTALL_LOCK);
  assert.equal(right.code, 0, outputOf(right).slice(-2000));
});

// ---- a package outside the set, a lower version -----------------------------------------------------------------------------------------------------------

test('A_csharp_package_outside_the_set_fails_naming_it', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  addReference(w, 'Newtonsoft.Json', '13.0.3');
  assert.equal(snapshotRecord().csharp.packages.some(p => p.name === 'newtonsoft.json'), false, 'precondition: the package is not in the manifest');
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0);
  assert.match(outputOf(ran), /NU1101/);
  assert.match(outputOf(ran), /Newtonsoft\.Json/i);
});

test('A_lower_csharp_version_fails_with_NU1603', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'web' });
  const file = path.join(w, 'App.csproj');
  writeFileSync(file, read(file).replace('Authentication.JwtBearer" Version="10.0.14"', 'Authentication.JwtBearer" Version="10.0.1"'));
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0);
  assert.match(outputOf(ran), /NU1603/);
  assert.match(outputOf(ran), /JwtBearer/i);
});

test('A_node_package_outside_the_set_fails_naming_it', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'node', kind: 'olympiad' });
  const install = await inBox(w, 'node', npmOfflineArgv(['left-pad@1.3.0']));
  assert.notEqual(install.code, 0);
  assert.match(outputOf(install), /ENOTCACHED/);
  assert.match(outputOf(install), /left-pad/);
  const edited = helloWorkspace(t, { stack: 'node', kind: 'olympiad', label: 'edit' });
  const file = path.join(edited, 'package.json');
  const json = JSON.parse(read(file));
  writeFileSync(file, `${JSON.stringify({ ...json, dependencies: { 'left-pad': '1.3.0' } }, null, 2)}\n`);
  const build = await inBox(edited, 'node', ['bash', 'build.sh']);
  assert.notEqual(build.code, 0);
  assert.match(outputOf(build), /left-pad/);
  assert.match(outputOf(build), /ENOTCACHED/);
});

// ---- an agent may add a listed package ----------------------------------------------------------------------------------------------------------------------

test('An_agent_may_add_a_listed_package_and_the_build_still_passes', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  await top.test('csharp', async t => {
    const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
    addReference(w, 'Microsoft.Extensions.Hosting', snapshotRecord().csharp.resolved.HOSTING);
    const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
    assert.equal(ran.code, 0, outputOf(ran).slice(-3000));
  });
  await top.test('node', async t => {
    const w = helloWorkspace(t, { stack: 'node', kind: 'olympiad' });
    const added = await inBox(w, 'node', npmOfflineArgv([`fastify@${snapshotRecord().node.packages.find(p => p.name === 'fastify').version}`]));
    assert.equal(added.code, 0, outputOf(added).slice(-3000));
    const ran = await inBox(w, 'node', ['bash', 'build.sh']);
    assert.equal(ran.code, 0, outputOf(ran).slice(-3000));
  });
});

// ---- the type check and the lint ---------------------------------------------------------------------------------------------------------------------------

test('The_type_check_and_the_lint_work_in_the_workspace', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const { w } = await buildHello(t, { stack: 'node', kind: 'olympiad' });
  const main = path.join(w, 'src', 'main.ts');
  const original = read(main);
  const clean = await inBox(w, 'node', ['npx', '--offline', 'eslint', 'src']);
  assert.equal(clean.code, 0, outputOf(clean).slice(-2000));
  writeFileSync(main, `${original}\nPromise.resolve(1);\n`);
  const floating = await inBox(w, 'node', ['npx', '--offline', 'eslint', 'src']);
  assert.equal(floating.code, 1, outputOf(floating).slice(-2000));
  assert.match(outputOf(floating), /@typescript-eslint\/no-floating-promises/);
  writeFileSync(main, `${original}\nconst wrong: number = 'text';\nconsole.log(wrong);\n`);
  const typed = await inBox(w, 'node', ['bash', 'build.sh']);
  assert.notEqual(typed.code, 0);
  assert.match(outputOf(typed), /TS2322/);
});

// ---- T-L31: the program is what run.sh starts --------------------------------------------------------------------------------------------------------------

/** Builds the stop stub of `stack` into the template of `kind`, starts `bash run.sh` in the layer's chain and waits for `ready` on its standard output. -> `{ c, child, w, out() }`. */
async function startStub(t, { stack, kind, runSh }) {
  const w = helloWorkspace(t, { stack, kind, source: stopStubSource(stack) });
  if (runSh !== undefined) writeFileSync(path.join(w, 'run.sh'), runSh(read(path.join(w, 'run.sh'))));
  const built = await inBox(w, stack, ['bash', 'build.sh']);
  assert.equal(built.code, 0, outputOf(built).slice(-3000));
  const env = boxEnv({ dirs: toolchains, stack, memoryMaxBytes: 512 * MIB });
  const c = await chain(t, { view: ownedFolder(t, 'view'), caseId: 'k1', memoryMax: 512 * MIB, holdSeconds: 120, extraBox: { tree: w, env } });
  const child = c.start(['bash', 'run.sh'], { stdio: ['ignore', 'pipe', 'ignore'] });
  let out = '';
  child.stdout.on('data', chunk => { out += chunk; });
  assert.ok(await waitFor(() => out.includes('ready\n') || null, 30_000), 'the stub said ready');
  return { c, child, w, out: () => out };
}

test('The_process_run_sh_starts_is_the_program_with_no_bash_parent_and_SIGTERM_reaches_it', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    for (const kind of ['olympiad', 'daemon', 'web']) {
      await top.test(`${stack} ${kind}`, async t => {
        const { c, child, out } = await startStub(t, { stack, kind });
        const pid = programPidIn(c.prog);
        assert.ok(pid, `a dotnet or node process is in prog/: ${(pidsIn(c.prog) ?? []).map(p => commOf(p))}`);
        assert.match(exeBase(pid), /^(dotnet|node)$/, 'its executable is the pinned toolchain\'s');
        assert.deepEqual((pidsIn(c.prog) ?? []).filter(p => commOf(p) === 'bash'), [], 'no bash is left in prog/: run.sh became the program');
        assert.equal(commOf(statOf(pid).ppid), 'bwrap', 'the program\'s parent is the box\'s bwrap');
        process.kill(pid, 'SIGTERM');
        assert.ok(await waitFor(() => out().includes('TERM\n') || null, 5000), 'the program logged TERM');
        const ended = await Promise.race([child.exited, new Promise(resolve => setTimeout(() => resolve('not ended within 10 s'), 10_000))]);
        assert.deepEqual(ended, { code: 0, signal: null });
      });
    }
  }
});

test('A_run_sh_without_exec_leaves_a_bash_parent_that_eats_SIGTERM_and_that_is_the_point', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    await top.test(stack, async t => {
      const { c, w, out } = await startStub(t, { stack, kind: 'daemon', runSh: text => text.replace('exec ', '') });
      assert.match(runShProblem(read(path.join(w, 'run.sh'))), /exec/, 'the check names this run.sh');
      const pid = programPidIn(c.prog);
      const bash = (pidsIn(c.prog) ?? []).find(p => commOf(p) === 'bash');
      assert.ok(pid && bash, 'prog/ holds a bash and the program');
      assert.equal(statOf(pid).ppid, bash, 'the program\'s parent is that bash');
      process.kill(bash, 'SIGTERM');
      await new Promise(resolve => setTimeout(resolve, 2000));
      assert.equal(out().includes('TERM'), false, 'the program never saw the signal');
      assert.ok(statOf(pid) !== null && (pidsIn(c.prog) ?? []).includes(pid), 'and it is still running in prog/ (the chain\'s hook ends it)');
    });
  }
});

// ---- the same templates in the agent box -------------------------------------------------------------------------------------------------------------------

test('The_same_templates_build_in_the_agent_box', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    await top.test(stack, async t => {
      const w = helloWorkspace(t, { stack, kind: 'olympiad' });
      const record = snapshotRecord();
      const snap = snapshotDirOf({ stack, record, caches: defaultCaches() }).value;
      const opened = await openBox({ stack, snapshot: { dir: snap, parts: record[stack].parts }, root: ownedFolder(t, 'box', ensureTestRoot()), rid: 'a1', dirs: toolchains });
      assert.equal(opened.ok, true, opened.reason);
      const box = opened.value;
      try {
        cpSync(w, box.layout.work, { recursive: true });
        const ran = await box.run(['bash', 'build.sh'], { env: stack === 'node' ? NPM_OFFLINE_ENV : {} });
        assert.equal(ran.ok && ran.value.code, 0, ran.ok ? ran.value.output.slice(-3000) : ran.reason);
        assert.deepEqual(box.egress().allowed, {}, 'no registry was reached');
        assert.deepEqual(box.egress().refused, {}, 'and none was asked');
      } finally {
        await box.close();
      }
    });
  }
});

// ---- the lock generation is stable ---------------------------------------------------------------------------------------------------------------------------

test('The_lock_generation_is_stable_and_leaves_no_folder_behind', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const work = path.join(os.homedir(), '.cache', 'lang-v2', 'w');
  const listing = () => (existsSync(work) ? readdirSync(work) : []).filter(name => name.includes('-lock-')).sort();
  const existing = listing();
  const ran = await runBounded(process.execPath, [path.join(SPIKE, 'build-workspaces.mjs'), '--make-locks'], { timeoutMs: 300_000, env: { PATH: '/usr/sbin:/usr/bin:/bin', HOME: os.homedir() } });
  assert.equal(ran.ok, true, ran.reason);
  assert.equal(ran.value.code, 0, ran.value.stderr);
  assert.deepEqual(ran.value.stdout.trim().split('\n'), ['olympiad: unchanged', 'daemon: unchanged', 'web: unchanged'], 'the committed locks are what a fresh generation gives');
  assert.deepEqual(listing(), existing, 'the owned folders of the generation are removed');
});
