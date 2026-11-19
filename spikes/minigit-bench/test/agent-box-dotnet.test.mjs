// The EFFECT of the dotnet hide list in the real box on this host (E2 amendment 2026-11-19, D-9; the plan gate's point 3). `box-dotnet-paths.test.mjs` shows every path sends the tmpfs; here bwrap, the
// muxer and the SDK answer: the box lists one SDK, the pin, and a program built and run in it is built by the pinned SDK and runs on the pin's runtime. Each "cannot" has its control: the same probe
// in the same box WITHOUT the computed tmpfs sees the other SDK. The hidden paths are read from `otherDotnetVersions`, never typed. Every folder is an owned one (`ownedDir`), removed with its test.
// The last test is the SCORING box (`scoreBoxArgv`): the same list, so a tree is scored by the SDK it was built with.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { scoreBoxArgv } from '../lib/bwrap.mjs';
import { makeSnapshot, makeWritable } from '../lib/cache-snapshot.mjs';
import { DISTRIBUTION_DOTNET, dirEntriesOf, otherDotnetVersions } from '../lib/dotnet-hide.mjs';
import { agentBoxPrefix } from '../lib/sandbox.mjs';
import { DOTNET_PIN, boxOptions, envFor, hostHas, newSession, probeArgv, withoutPair } from './box-helpers.mjs';
import { hostLacks, sandboxGate } from './helpers.mjs';

const owned = ownedFor({ token: 'bench' });
const BUILD_MS = 300_000;
const major = v => v.split('.')[0];

/** A test that needs bwrap and the distribution's dotnet calls this first. */
const dotnetGate = t => sandboxGate(t) && (hostHas('dotnet') || hostLacks(t, 'dotnet is not on PATH on this host'));

/** The prefix of the box as the harness builds it, and (asked for only after the box itself was probed) the same prefix with the computed dotnet tmpfs taken out again: the control. */
function boxes(layout) {
  const hidden = otherDotnetVersions({ pin: DOTNET_PIN });
  const real = agentBoxPrefix(boxOptions(layout));
  return { hidden, real, control: () => hidden.reduce((argv, p) => withoutPair(argv, '--tmpfs', p), real) };
}

const sdkLines = out => out.trim().split('\n').filter(l => l.trim() !== '');
/** The versions `dotnet --list-runtimes` prints (`<framework> <version> [<dir>]`). */
const runtimeVersions = out => sdkLines(out).map(l => l.split(' ')[1]);
/** A probe that prints, for each path, how many entries it holds: `<count> <path>` per line. */
const countScript = paths => paths.map(p => `echo "$(ls -A ${p} | wc -l) ${p}"`).join('\n');
const countsOf = out => sdkLines(out).map(l => ({ count: Number(l.split(' ')[0]), dir: l.split(' ')[1] }));
const otherSdks = hidden => hidden.filter(p => DISTRIBUTION_DOTNET.some(root => path.dirname(p) === `${root}/sdk`));
/** What a test says, in its own output, on a host with no second SDK: its control is then the box itself, and shows the pin only. */
const oneSdkOnly = `this host holds one SDK only (${DOTNET_PIN}): nothing is hidden, so the control is the same box and lists exactly the pin too; it cannot show a second SDK here`;

test('The_real_box_lists_exactly_the_pinned_sdk_and_the_same_box_without_the_hide_list_lists_the_others_too', async t => {
  if (!dotnetGate(t)) return;
  const layout = newSession({ root: owned.ownedDir(t, 'sdk') });
  const { hidden, real, control } = boxes(layout);
  const inside = await probeArgv(real, 'dotnet --list-sdks', envFor(layout));
  t.diagnostic(`box: dotnet --list-sdks -> ${JSON.stringify(sdkLines(inside.out))}`);
  assert.equal(sdkLines(inside.out).length, 1, `exactly one SDK: ${inside.out}`);
  assert.ok(sdkLines(inside.out)[0].startsWith(`${DOTNET_PIN} [`), `the one SDK is the pin ${DOTNET_PIN}: ${inside.out}`);
  const runtimes = await probeArgv(real, 'dotnet --list-runtimes', envFor(layout));
  t.diagnostic(`box: dotnet --list-runtimes -> ${JSON.stringify(sdkLines(runtimes.out))}`);
  assert.ok(runtimeVersions(runtimes.out).length > 0 && runtimeVersions(runtimes.out).every(v => major(v) === major(DOTNET_PIN)), `every runtime the box lists is of the pin's major: ${runtimes.out}`);
  // no skip on a host with one SDK (this machine before 2026-11-18): it lacks nothing, and everything above holds there. Only what the CONTROL shows depends on the host, and it is computed, not
  // assumed: with nothing to hide the control is the same box and lists exactly the pin too (the lines below then compare 1 with 1, and the diagnostic says so)
  if (otherSdks(hidden).length === 0) t.diagnostic(oneSdkOnly);
  const emptied = countsOf((await probeArgv(real, countScript(hidden), envFor(layout))).out);
  assert.deepEqual(emptied, hidden.map(dir => ({ count: 0, dir })), 'every folder of the list is an empty folder in the box');
  const open = await probeArgv(control(), `dotnet --list-sdks\necho RUNTIMES\ndotnet --list-runtimes\necho COUNTS\n${countScript(hidden)}`, envFor(layout));
  const [sdks, rest] = open.out.split('RUNTIMES\n');
  const [openRuntimes, counts] = rest.split('COUNTS\n');
  t.diagnostic(`control (no dotnet tmpfs): dotnet --list-sdks -> ${JSON.stringify(sdkLines(sdks))}`);
  assert.equal(sdkLines(sdks).length, 1 + otherSdks(hidden).length, `control: without the hide list the same box lists every SDK of the host: ${sdks}`);
  for (const p of otherSdks(hidden)) assert.ok(sdkLines(sdks).some(l => l.startsWith(`${path.basename(p)} [`)), `control: ${path.basename(p)} is listed`);
  // only where the list hides a runtime: a second SDK of the pin's own major brings none of another major
  if (hidden.some(p => DISTRIBUTION_DOTNET.some(root => p.startsWith(`${root}/shared/`)))) assert.ok(runtimeVersions(openRuntimes).some(v => major(v) !== major(DOTNET_PIN)), `control: the same box lists a runtime of another major: ${openRuntimes}`);
  assert.ok(countsOf(counts).every(c => c.count > 0), `control: every one of those folders holds something on the host: ${counts}`);
});

const PROJECT = `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
`;
const PROGRAM = 'System.Console.WriteLine("framework=" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);\n';
/** No package source at all: the restore reads the packs the SDK ships and nothing else (the box has no network either). */
const NUGET_CONFIG = '<?xml version="1.0" encoding="utf-8"?>\n<configuration>\n  <packageSources>\n    <clear />\n  </packageSources>\n</configuration>\n';

/** A console project with no package reference, in the box's work folder. */
function writeProject(layout) {
  const dir = path.join(layout.work, 'app');
  mkdirSync(dir);
  for (const [name, text] of [['app.csproj', PROJECT], ['Program.cs', PROGRAM], ['nuget.config', NUGET_CONFIG]]) writeFileSync(path.join(dir, name), text);
  return dir;
}

/** The pin's runtime on this host: the one `Microsoft.NETCore.App` folder of the pin's major (read from the disk, so a patch upgrade of the runtime does not need this test edited). */
function pinRuntime(t) {
  const versions = DISTRIBUTION_DOTNET.map(root => `${root}/shared/Microsoft.NETCore.App`).filter(existsSync).flatMap(dirEntriesOf).map(e => e.name).filter(v => major(v) === major(DOTNET_PIN));
  assert.equal(versions.length, 1, `this host holds exactly one runtime of the pin's major ${major(DOTNET_PIN)}: ${versions.join(', ')}`);
  t.diagnostic(`host: the runtime of the pin's major is ${versions[0]}`);
  return versions[0];
}

const section = (text, title) => (new RegExp(`^${title}:\\n((?:[ \\t]+.*\\n?)+)`, 'm').exec(text)?.[0] ?? `(no ${title} section in the output)`).trimEnd();
/** The highest of the versions, compared number by number. */
const newestOf = versions => [...versions].sort((a, b) => a.localeCompare(b, undefined, { numeric: true })).at(-1);
const lineOf = (out, key) => out.split('\n').find(l => l.startsWith(`${key}=`))?.slice(key.length + 1) ?? null;

test('A_net10_console_project_built_and_run_in_the_real_box_is_built_by_the_pinned_sdk_and_runs_on_the_pins_runtime', { timeout: BUILD_MS + 120_000 }, async t => {
  if (!dotnetGate(t)) return;
  const layout = newSession({ root: owned.ownedDir(t, 'build') });
  const { hidden, real, control } = boxes(layout);
  const app = writeProject(layout);
  // HOME, DOTNET_CLI_HOME and NUGET_PACKAGES are the session's own folders (agentEnv); telemetry and the logo are off there too; the first-run experience is switched off here
  const env = envFor(layout, { extra: { DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1' } });
  assert.ok([env.HOME, env.DOTNET_CLI_HOME, env.NUGET_PACKAGES].every(p => p.startsWith(`${layout.dir}/`)), 'the home and the package folder are inside this test\'s own folder');
  assert.deepEqual([env.DOTNET_CLI_TELEMETRY_OPTOUT, env.DOTNET_NOLOGO], ['1', '1']);
  const script = [
    `cd ${app}`,
    'echo "version=$(dotnet --version)"',
    'dotnet build -c Release -m:1 -nr:false -p:UseSharedCompilation=false -v:q > build.log 2>&1; echo "build=$?"',
    'tail -n 25 build.log | sed "s/^/log: /"',
    'dotnet bin/Release/net10.0/app.dll',
    'echo INFO-BEGIN; dotnet --info; echo INFO-END',
  ].join('\n');
  const ran = await probeArgv(real, script, env, { timeoutMs: BUILD_MS });
  const info = ran.out.slice(ran.out.indexOf('INFO-BEGIN'), ran.out.indexOf('INFO-END'));
  // the one difference from before SDK 11 came: the muxer and host/fxr are the newest on the host and resolve to the pinned SDK (recorded here, in the test's own output)
  t.diagnostic(`box: dotnet --version -> ${lineOf(ran.out, 'version')}`);
  t.diagnostic(`box: the program printed ${lineOf(ran.out, 'framework')}`);
  t.diagnostic(`box: dotnet --info, the muxer/fxr fact:\n${section(info, 'Host')}`);
  t.diagnostic(`box: dotnet --info, SDKs and runtimes:\n${section(info, '\\.NET SDKs installed')}\n${section(info, '\\.NET runtimes installed')}`);
  assert.equal(lineOf(ran.out, 'version'), DOTNET_PIN, `dotnet --version in the project is the pin: ${ran.out.slice(0, 400)}`);
  assert.equal(lineOf(ran.out, 'build'), '0', `the build succeeded with local packs only:\n${ran.out}`);
  assert.equal(lineOf(ran.out, 'framework'), `.NET ${pinRuntime(t)}`, `the program runs on the pin's runtime:\n${ran.out}`);
  assert.equal(major(lineOf(ran.out, 'framework').replace('.NET ', '')), major(DOTNET_PIN), 'the runtime is of the pin\'s major');
  if (otherSdks(hidden).length === 0) t.diagnostic(oneSdkOnly); // no skip: the control below then answers the pin as well, which is what `newestOf` of one SDK is
  const open = await probeArgv(control(), `cd ${app}\necho "version=$(dotnet --version)"`, env);
  t.diagnostic(`control (no dotnet tmpfs): dotnet --version -> ${lineOf(open.out, 'version')}`);
  // with no global.json the muxer gives the newest SDK it sees: on this host that is not the pin, which is what the hide list prevents (were the pin the newest, the two lines would agree and say so)
  const newest = newestOf([DOTNET_PIN, ...otherSdks(hidden).map(p => path.basename(p))]);
  assert.equal(lineOf(open.out, 'version'), newest, 'control: without the hide list the same project is given the newest SDK of the host');
  if (newest === DOTNET_PIN) t.diagnostic('the pin is the newest SDK of this host: the control cannot show another SDK being chosen');
});

test('A_snapshot_made_with_no_box_options_runs_its_steps_in_a_box_that_lists_only_v1s_pinned_sdk', async t => {
  if (!dotnetGate(t)) return;
  let out = null;
  t.after(() => { if (out !== null) makeWritable(out); }); // registered BEFORE the owned folders: hooks run first-registered-first, and a snapshot is read-only until this ran
  out = owned.ownedDir(t, 'snapo');
  // the recipe's own pin mechanism: the step prints every SDK the box lists, one per line, and the recipe pins that output to the one pinned SDK
  const step = { argv: ['sh', '-c', 'mkdir -p "$NUGET_PACKAGES" && dotnet --list-sdks | cut -d" " -f1'], expectStdout: DOTNET_PIN };
  const recipe = { hosts: 'bare', stepTimeoutMs: 60_000, stacks: { csharp: { steps: [step], capture: { nuget: 'box/nuget' } } } };
  const made = await makeSnapshot({ stack: 'csharp', recipe, root: owned.ownedDir(t, 'snapw'), out });
  assert.equal(made.ok, true, `the snapshot's box lists the pin only: ${made.reason}`);
  assert.equal(made.value.steps[0].stdout, DOTNET_PIN);
});

test('The_real_scoring_box_lists_exactly_the_pinned_sdk_and_gives_a_folder_with_no_global_json_the_pin_and_without_the_hide_list_the_newest_sdk', async t => {
  if (!dotnetGate(t)) return;
  const root = owned.ownedDir(t, 'score');
  const dirs = { copyDir: path.join(root, 'copy'), home: path.join(root, 'home'), tmp: path.join(root, 'tmp') };
  for (const d of Object.values(dirs)) mkdirSync(d);
  assert.equal(existsSync(path.join(dirs.copyDir, 'global.json')), false, 'nothing in the folder pins an SDK: what dotnet --version says is what the box shows');
  const hidden = otherDotnetVersions({ pin: DOTNET_PIN });
  const real = scoreBoxArgv({ ...dirs, cwd: dirs.copyDir, dotnetPin: DOTNET_PIN });
  const script = 'dotnet --list-sdks\necho "version=$(dotnet --version)"';
  const listed = out => sdkLines(out).filter(l => !l.startsWith('version='));
  const inside = await probeArgv(real, script); // the scoring box carries its own environment (--clearenv, --setenv), none is given here
  t.diagnostic(`scoring box: dotnet --list-sdks -> ${JSON.stringify(listed(inside.out))}; dotnet --version -> ${lineOf(inside.out, 'version')}`);
  assert.equal(listed(inside.out).length, 1, `exactly one SDK: ${inside.out}`);
  assert.ok(listed(inside.out)[0].startsWith(`${DOTNET_PIN} [`), `the one SDK is the pin ${DOTNET_PIN}: ${inside.out}`);
  assert.equal(lineOf(inside.out, 'version'), DOTNET_PIN, 'a tree with no global.json is built by the pinned SDK');
  if (otherSdks(hidden).length === 0) t.diagnostic(oneSdkOnly); // no skip: the control below is then the same box, one SDK listed and the pin answered
  const open = await probeArgv(hidden.reduce((argv, p) => withoutPair(argv, '--tmpfs', p), real), script);
  t.diagnostic(`control (no dotnet tmpfs): dotnet --list-sdks -> ${JSON.stringify(listed(open.out))}; dotnet --version -> ${lineOf(open.out, 'version')}`);
  assert.equal(listed(open.out).length, 1 + otherSdks(hidden).length, `control: without the hide list the scoring box lists every SDK of the host: ${open.out}`);
  assert.equal(lineOf(open.out, 'version'), newestOf([DOTNET_PIN, ...otherSdks(hidden).map(p => path.basename(p))]), 'control: and gives the same folder the newest of them');
});

test('The_real_listing_reports_a_version_folder_that_is_a_link_and_the_list_refuses_it_naming_the_path', t => {
  // no box: this is the listing's own effect. The fixture tests inject `{ name, link: true }`; here the disk says it, over a root of this test's own
  const root = owned.ownedDir(t, 'link');
  for (const dir of [`sdk/${DOTNET_PIN}`, 'elsewhere/11.0.100']) mkdirSync(path.join(root, dir), { recursive: true });
  const link = path.join(root, 'sdk', '11.0.100');
  symlinkSync(path.join(root, 'elsewhere', '11.0.100'), link);
  assert.deepEqual(dirEntriesOf(path.join(root, 'sdk')).sort((a, b) => a.name.localeCompare(b.name)), [{ name: DOTNET_PIN, link: false }, { name: '11.0.100', link: true }]);
  assert.throws(() => otherDotnetVersions({ pin: DOTNET_PIN, roots: [root] }), e => e.message.startsWith(`${link} is a symbolic link`), 'the real listing, the real refusal');
  rmSync(link);
  mkdirSync(link);
  assert.deepEqual(otherDotnetVersions({ pin: DOTNET_PIN, roots: [root] }), [link], 'control: the same name as a folder is listed to be hidden');
});
