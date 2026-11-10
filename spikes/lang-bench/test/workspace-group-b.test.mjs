// F2.S2 (a): every check keyed on a task's group finds the six tasks (04, 05, 06, 07, 08, 10) through the `B` entry of FORMAT's record and each olympiad task (01, 02, 03, 09) through `A`: the group rule
// (`groupProblem`), the grammar (`grammarOf`) and the command (`build-workspaces.mjs --group`). The real record and the real FORMAT at the pinned commit are read; the cases skip without LANG_V2_TASK_REPO.
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { PINNED_TASK_SHA } from '../lib/task-repo.mjs';
import { buildWorkspace, loadInputs } from '../lib/workspace-build.mjs';
import { sha256 } from './support/format-fixtures.mjs';
import { SPIKE, helloSamples, helloStatement } from './support/workspace-fixtures.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const GROUPS = Object.freeze({ A: ['01', '02', '03', '09'], B: ['04', '05', '06', '07', '08', '10'] });
const OTHER = Object.freeze({ A: 'B', B: 'A' });
const ENV = Object.freeze({ PATH: '/usr/bin:/bin', HOME: '/nonexistent', GIT_CONFIG_NOSYSTEM: '1', GIT_CONFIG_GLOBAL: '/dev/null' });

/** The task repository's checkout named by `LANG_V2_TASK_REPO`, or null when the case skipped (under BENCH_REQUIRE_SANDBOX=1 an unset variable throws). */
function realRepo(t) {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo !== undefined && repo !== '') return repo;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error('BENCH_REQUIRE_SANDBOX=1 and LANG_V2_TASK_REPO does not name the task repository checkout');
  t.skip('LANG_V2_TASK_REPO does not name the task repository checkout');
  return null;
}

/** FORMAT's bytes at the pinned commit, as the builder reads them. */
async function pinnedFormat(repo) {
  const shown = await runBounded('git', ['-C', repo, 'show', `${PINNED_TASK_SHA}:todo/tasks/FORMAT.md`], { timeoutMs: 60_000, env: ENV, bytes: true });
  assert.equal(shown.ok && shown.value.code, 0, 'FORMAT at the pinned commit is read');
  return shown.value.stdoutBytes;
}

const args = (t, over) => ({ stack: 'node', kind: 'olympiad', out: path.join(ownedFolder(t, 'out'), 'w'), statement: readFileSync(helloStatement('olympiad')), samplesDir: helloSamples('olympiad'), helloFixture: false, ...over });

test('The_workspace_builder_finds_the_six_in_B_and_builds_with_Bs_grammar', async t => {
  const repo = realRepo(t);
  if (repo === null) return;
  const formatBytes = await pinnedFormat(repo);
  const loaded = loadInputs();
  assert.equal(loaded.ok, true, loaded.reason);
  const inputs = loaded.value;
  for (const [group, tasks] of Object.entries(GROUPS)) {
    for (const task of tasks) {
      const built = buildWorkspace(args(t, { task, group, formatBytes, inputs }));
      assert.equal(built.ok, true, `${task} is built under ${group}: ${built.reason}`);
      const grammar = readFileSync(path.join(built.value.out, 'runner', 'GRAMMAR.md'));
      assert.equal(sha256(grammar), inputs.format[group].grammarSha256, `${task} takes the grammar of ${group}'s row`);
      const other = buildWorkspace(args(t, { task, group: OTHER[group], formatBytes, inputs }));
      assert.equal(other.ok, false, `${task} is refused under ${OTHER[group]}`);
      assert.match(other.reason, new RegExp(`the task ${task} belongs to group ${group}, not to group ${OTHER[group]}`));
    }
  }
});

test('The_command_builds_a_task_of_the_six_under_B_and_an_olympiad_task_under_A_and_refuses_the_other_way', async t => {
  const repo = realRepo(t);
  if (repo === null) return;
  const home = ownedFolder(t, 'home');
  const run = async (task, group) => {
    const out = path.join(ownedFolder(t, 'cmd'), 'w');
    const flags = ['--stack', 'node', '--kind', 'olympiad', '--task', task, '--group', group, '--out', out, '--statement', helloStatement('olympiad'), '--samples', helloSamples('olympiad'), '--task-repo', repo];
    const ran = await runBounded(process.execPath, [path.join(SPIKE, 'build-workspaces.mjs'), ...flags], { timeoutMs: 300_000, env: { PATH: '/usr/bin:/bin', HOME: home }, maxBytes: 1024 * 1024 });
    assert.equal(ran.ok, true, ran.reason);
    return { ...ran.value, out };
  };
  const six = await run('04', 'B');
  assert.equal(six.code, 0, six.stderr);
  assert.equal(existsSync(six.out), true);
  const olympiad = await run('01', 'A');
  assert.equal(olympiad.code, 0, olympiad.stderr);
  for (const [task, group] of [['04', 'A'], ['01', 'B']]) {
    const refused = await run(task, group);
    assert.equal(refused.code, 3, `${task} under ${group} is a refusal`);
    assert.match(refused.stderr, /refuses to write into/);
    assert.equal(existsSync(refused.out), false, 'a refusal wrote nothing');
  }
});
