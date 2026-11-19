// Inside the boxes (plan T-X4 and D38): the locale reaches both boxes for both stacks (`LANG` / `LC_ALL` `de_DE.UTF-8`, .NET `CurrentCulture` `de-DE`, Node's `Intl` locale `de...`), and the process
// `run.sh` starts is `dotnet` or `node`, not `bash`. The workspaces are the pilot's templates with a four-line program; the spec text is a stand-in with one readable sample (the workspace refuses a spec whose samples table it cannot read), so no task repository is read.
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { scoreBoxArgv } from '../../minigit-bench/lib/bwrap.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { agentBoxPrefix, agentEnv, dummyToken, sessionLayout } from '../../minigit-bench/lib/sandbox.mjs';
import { nodeDir, probeArgv } from '../../minigit-bench/test/box-helpers.mjs';
import { hostLacks, sandboxGate, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickExplore, quickPaths } from '../lib/quick.mjs';
import { PLACE, boxArgvs, judgeDotnet } from '../lib/score.mjs';
import { fillWorkspace, workspaceFiles } from '../lib/workspace.mjs';

const EXPLORE = loadExplore().value;
const NO_SPEC = { spec: '# Task 01 — x\n\n## 6. Visible samples\n\n| Id | Standard input | Expected standard output |\n|---|---|---|\n| `s01` | `1⏎` | `1⏎` |\n', format: '# F\n\n## 3. Visible samples\n\nx\n\n## 6. Worked examples\n' };
const CS_PROGRAM = 'using System.Globalization;\nConsole.Out.Write($"{CultureInfo.CurrentCulture.Name}|{Environment.GetEnvironmentVariable("LANG")}|{Environment.GetEnvironmentVariable("LC_ALL")}\\n");\n';
const NODE_PROGRAM = "process.stdout.write(`${new Intl.DateTimeFormat().resolvedOptions().locale}|${process.env.LANG}|${process.env.LC_ALL}\\n`);\n";
const SOURCES = { csharp: ['src/Program.cs', CS_PROGRAM], node: ['src/main.ts', NODE_PROGRAM] };

/** A workspace of the stack with the four-line program, in a fresh directory. */
function workspace(stack, work) {
  const files = workspaceFiles({ explore: EXPLORE, stack, sources: NO_SPEC, typesNode: '0.0.0' });
  fillWorkspace({ files, work });
  const [rel, text] = SOURCES[stack];
  mkdirSync(path.dirname(path.join(work, rel)), { recursive: true });
  writeFileSync(path.join(work, rel), text);
}

async function haveDotnet(t) {
  return existsSync('/usr/bin/dotnet') || hostLacks(t, '/usr/bin/dotnet is missing');
}

const EXPECT = { csharp: 'de-DE|de_DE.UTF-8|de_DE.UTF-8', node: /^de(-DE)?\|de_DE\.UTF-8\|de_DE\.UTF-8$/ };
const check = (stack, out) => (stack === 'csharp' ? assert.equal(out.trim(), EXPECT.csharp) : assert.match(out.trim(), EXPECT.node));

/** The commands that build and run the program in the box, per stack (the node program is run directly: the type check is not what is probed). */
const COMMANDS = { csharp: 'bash build.sh > /dev/null && bash run.sh', node: 'bash run.sh' };

test('The_agent_box_gives_both_stacks_the_locale_of_the_plan', async t => {
  if (!sandboxGate(t) || !await haveDotnet(t)) return;
  for (const stack of ['csharp', 'node']) {
    const made = sessionLayout(tmpDir('locale'), 'a1');
    assert.equal(made.ok, true, made.reason);
    const layout = made.value;
    workspace(stack, layout.work);
    const env = agentEnv({ layout, dummyToken: dummyToken('x'), cellId: 'cell-a1', nodeDir, locale: EXPLORE.locale, extra: EXPLORE.envExtra });
    const r = await probeArgv(agentBoxPrefix({ layout, arm: 'bare', toolchains: [], claudeBinary: process.execPath, nodeDir, dotnetPin: EXPLORE.dotnet.sdk }), COMMANDS[stack], env, { timeoutMs: 180_000 });
    check(stack, r.out);
  }
});

test('The_scoring_box_gives_both_stacks_the_locale_too', async t => {
  if (!sandboxGate(t) || !await haveDotnet(t)) return;
  for (const stack of ['csharp', 'node']) {
    const base = tmpDir('locale-score');
    const dirs = Object.fromEntries(['copy', 'home', 'tmp'].map(n => [n, path.join(base, n)]));
    for (const d of Object.values(dirs)) mkdirSync(d, { recursive: true });
    workspace(stack, dirs.copy);
    const argv = scoreBoxArgv({ copyDir: dirs.copy, home: dirs.home, tmp: dirs.tmp, toolchainDirs: [nodeDir], cwd: dirs.copy, locale: EXPLORE.locale });
    const r = await probeArgv(argv, COMMANDS[stack], undefined, { timeoutMs: 180_000 });
    check(stack, r.out);
  }
});

test('Without_the_locale_seam_the_same_programs_see_the_C_locale_so_the_probe_can_tell_the_difference', async t => {
  if (!sandboxGate(t)) return;
  const base = tmpDir('locale-control');
  const dirs = Object.fromEntries(['copy', 'home', 'tmp'].map(n => [n, path.join(base, n)]));
  for (const d of Object.values(dirs)) mkdirSync(d, { recursive: true });
  workspace('node', dirs.copy);
  const r = await probeArgv(scoreBoxArgv({ copyDir: dirs.copy, home: dirs.home, tmp: dirs.tmp, toolchainDirs: [nodeDir], cwd: dirs.copy }), COMMANDS.node, undefined, { timeoutMs: 60_000 });
  assert.match(r.out.trim(), /\|C\.UTF-8\|undefined$/);
});

/** Start `bash run.sh` in `cwd`; resolve with `/proc/<pid>/comm` once it is no longer `bash`. */
async function commOfRunSh(cwd, env) {
  const child = spawn('bash', ['run.sh'], { cwd, env, stdio: ['pipe', 'ignore', 'ignore'] });
  try {
    for (let i = 0; i < 80; i += 1) {
      await new Promise(r => setTimeout(r, 100));
      const comm = readFileSync(`/proc/${child.pid}/comm`, 'utf8').trim();
      if (comm !== 'bash') return comm;
    }
    return 'bash';
  } finally {
    child.kill('SIGKILL');
  }
}

test('The_process_run_sh_starts_is_the_program_itself_dotnet_or_node_and_not_bash', async t => {
  if (!await haveDotnet(t)) return;
  const wait = '\nawait Task.Delay(8000);\n';
  const home = tmpDir('d38');
  const env = { PATH: `${nodeDir}/bin:/usr/bin:/bin`, HOME: home, DOTNET_CLI_HOME: home, NUGET_PACKAGES: path.join(home, 'nuget'), DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1' };
  const cs = path.join(tmpDir('d38-cs'), 'w');
  workspace('csharp', cs);
  writeFileSync(path.join(cs, 'src', 'Program.cs'), wait);
  const built = await runBounded('bash', ['build.sh'], { cwd: cs, env, timeoutMs: 180_000 });
  assert.equal(built.value.code, 0, built.value.stdout + built.value.stderr);
  assert.equal(await commOfRunSh(cs, env), 'dotnet');
  const nd = path.join(tmpDir('d38-node'), 'w');
  workspace('node', nd);
  writeFileSync(path.join(nd, 'src', 'main.ts'), 'setTimeout(() => {}, 8000);\n');
  assert.match(await commOfRunSh(nd, env), /^node/, 'Node names its main thread node-MainThread');
});

test('The_judges_box_for_the_quick_profile_lists_exactly_the_profiles_sdk_from_its_pinned_directory', async t => {
  if (!sandboxGate(t)) return;
  const explore = quickExplore(EXPLORE, realHome);
  const { dotnetDir } = quickPaths(EXPLORE, realHome);
  const pin = EXPLORE.quick.dotnet.sdk;
  if (!existsSync(path.join(dotnetDir, 'sdk', pin))) return hostLacks(t, `the quick profile's pinned SDK directory ${dotnetDir} (SDK ${pin}) is not on this host`);
  const root = ownedFor({ token: 'bench' }).ownedDir(t, 'judge');
  const dirs = { [PLACE.tree]: path.join(root, 'tree'), [PLACE.home]: path.join(root, 'home'), [PLACE.tmp]: path.join(root, 'tmp') };
  for (const d of Object.values(dirs)) mkdirSync(d);
  // the judge's own boxes for this profile (boxArgvs, with what judgeConfig hands it); the judge fills the three placeholders per case, here the test does
  const { buildBox } = boxArgvs({ toolchainDirs: [nodeDir], tsDir: '/ts', locale: explore.locale, envExtra: explore.envExtra, ...judgeDotnet({ explore, toolchains: { dotnetDir } }), operatorHome: realHome }); // the REAL home is hidden, as in a run (this file's own HOME is a fake one): the directory shows only because it is bound
  const box = buildBox.map(word => Object.entries(dirs).reduce((text, [place, dir]) => text.replaceAll(place, dir), word));
  const r = await probeArgv(box, 'dotnet --list-sdks; echo "root=$DOTNET_ROOT"; ls /usr/lib/dotnet 2>/dev/null | wc -l', undefined, { timeoutMs: 60_000 });
  t.diagnostic(`judge box, quick profile: ${JSON.stringify(r.out.trim().split('\n'))}`);
  assert.deepEqual(r.out.trim().split('\n'), [`${pin} [${dotnetDir}/sdk]`, `root=${dotnetDir}`, '0'], 'one SDK, the profile\'s, from its pinned directory; DOTNET_ROOT is that directory; the distribution\'s dotnet is an empty folder');
  // control: the same profile WITHOUT its pinned directory would get a box that hides every SDK of the host, which is why the judge refuses it
  assert.throws(() => judgeDotnet({ explore, toolchains: {} }), /the quick profile pins SDK/);
});
