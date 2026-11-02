// E1.S8b: the layer's runs through the REAL runner (E1.S8a part ii-a) and the real prefix — the scope, the box, the holder's namespaces. `runCases` makes the run folder, the holder and the
// views, certifies the memory read, starts the driver under `nsenter` OUTSIDE the scope and hands the runner the prefix, `--cgroup-of prog/` and `--case-view`; whatever happens, one
// cleanup ends it. The runner is not changed by this story: where the spec and the runner did not fit, `launchPrefix` says so.
import assert from 'node:assert/strict';
import { readFileSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { cleanup } from '../lib/exec-layer/cleanup.mjs';
import { launchPrefix, openRun, runCases, runnerArgv } from '../lib/exec-layer/run.mjs';
import { progTemplate } from '../lib/exec-layer/scope.mjs';
import { needs, runnerDir, toolchains } from './support/exec-layer-host.mjs';
import { layerOptions, MIB } from './support/exec-tree.mjs';

const caseOf = (result, id) => result.record.cases.find(entry => entry.id === id);

test('A_case_runs_through_the_real_prefix_and_its_memory_is_read_from_prog_and_certified', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { ids: ['s01', 's02'] }));
  assert.equal(result.exitCode, 0, result.stderr);
  assert.deepEqual(result.record.cases.map(entry => [entry.id, entry.status]), [['s01', 'PASS'], ['s02', 'PASS']]);
  const memory = caseOf(result, 's01').lifecycle.memory;
  assert.equal(memory.known, true, 'memory.peak and memory.events were read from prog/');
  assert.ok(memory.peakBytes > 0 && memory.oomKill === 0);
  assert.equal(result.memoryCertified, true);
  assert.equal(result.record.launch.failed, false);
  assert.ok(result.record.emptyProgram.launchMs > 0, 'the empty-program control was timed through the same prefix');
  assert.equal(result.cleanup.clean, true, JSON.stringify(result.cleanup.left));
});

test('Every_case_and_the_empty_program_control_get_a_fresh_scope_of_their_own', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { ids: ['s01', 's02', 's03'] }));
  const units = result.cleanup.removed.filter(entry => entry.kind === 'scope').map(entry => entry.name.replace(/^.*-lsc-[A-Za-z0-9]+-/, '')).sort();
  assert.deepEqual(units, ['certify', 'empty-program', 's01', 's02', 's03'], 'one scope per case, one for the control, one for the certification launch');
});

test('The_runner_gets_a_prefix_that_is_already_inside_the_namespaces_and_names_prog_and_the_views_by_case', async t => {
  if (!needs(t)) return;
  const seen = [];
  const result = await runCases({ ...layerOptions(t), observe: run => seen.push(launchPrefix(run)) });
  assert.equal(result.exitCode, 0, result.stderr);
  const prefix = seen[0];
  assert.ok(!prefix.some(arg => path.basename(arg) === 'nsenter'), 'the runner IS the driver, already in the holder\'s namespaces: a second nsenter -U from there fails with EINVAL');
  assert.ok(prefix.some(arg => arg.includes('{case}') && arg.startsWith('--unit=')), 'the unit name holds {case}');
  assert.equal(path.basename(prefix.find(arg => path.basename(arg) === 'bwrap')), 'bwrap');
  assert.ok(prefix.includes('--json-status-fd') && prefix[prefix.indexOf('--json-status-fd') + 1] === '7');
  assert.ok(!prefix.includes('--die-with-parent') && !prefix.includes('--unshare-net'));
});

test('The_drivers_command_is_nsenter_then_python_and_a_scope_appears_only_inside_the_prefix_it_hands_over', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t));
  assert.ok(opened.ok, opened.reason);
  t.after(() => cleanup(opened.value));
  const argv = runnerArgv(opened.value, { cgroupOf: progTemplate(opened.value.unitPrefix), jsonOut: '/x/record.json' });
  assert.equal(path.basename(argv[0]), 'nsenter');
  assert.deepEqual(argv.slice(1, 6), ['-t', String(opened.value.holder.pid), '-U', '-n', '--preserve-credentials']);
  assert.equal(path.basename(argv[6]), 'python3');
  assert.equal(argv[7], '-I', 'python isolated: no user site, no PYTHON* variable');
  const outsideThePrefix = argv.filter((arg, i) => argv[i - 1] !== '--launch-prefix');
  assert.deepEqual(outsideThePrefix.filter(arg => ['systemd-run', 'scope-entry.sh'].includes(path.basename(arg))), [], 'the driver itself is not started through a scope: the only scope in its command is inside the prefix it hands to the runner');
  assert.ok(argv.some(arg => arg.startsWith('[') && arg.includes('systemd-run')), 'and that prefix does hold the scope (the check above is not looking at nothing)');
  assert.equal(argv[argv.indexOf('--cgroup-of') + 1].endsWith('{case}.scope/prog'), true, '--cgroup-of names prog/ of the case\'s scope');
});

test('A_program_in_the_run_is_in_the_holders_namespace_and_its_stderr_holds_only_its_own_line', async t => {
  if (!needs(t)) return;
  const nets = [];
  const result = await runCases({ ...layerOptions(t, { runSh: '#!/bin/bash\nreadlink /proc/self/ns/net >&2\nexec cat\n' }), observe: run => nets.push(run.holderNet) });
  assert.equal(result.exitCode, 0, result.stderr);
  assert.match(nets[0], /^net:\[\d+\]$/);
  assert.notEqual(nets[0], readlinkSync('/proc/self/ns/net'), 'the holder\'s namespace is not the host\'s');
  assert.equal(caseOf(result, 's01').lifecycle.stderr.tail.trim(), nets[0], 'the program is in the holder\'s namespace; and nothing of the launcher (no "Running as unit") is in its recorded stderr');
});

test('An_oom_killed_child_is_memory_limit_whatever_the_exit_status_through_the_runner', async t => {
  if (!needs(t)) return;
  const alloc = 'b = bytearray(300 * 1024 * 1024)\nfor i in range(0, len(b), 4096):\n    b[i] = 1\n';
  const options = layerOptions(t, { memoryMaxBytes: 64 * MIB, runSh: '#!/bin/bash\npython3 -I child.py &\nwait\ncat\nexit 0\n', files: { 'child.py': alloc } });
  const result = await runCases(options);
  const entry = caseOf(result, 's01');
  assert.equal(entry.status, 'FAIL');
  assert.equal(entry.reason, 'memory limit', 'the parent printed the right output and exited 0; the OOM-killed child decides');
  assert.ok(entry.lifecycle.memory.oomKill >= 1 && entry.lifecycle.memory.known, 'oom_kill is read back from prog/memory.events');
  assert.equal(result.cleanup.clean, true, JSON.stringify(result.cleanup.left));
});

test('A_lingering_child_is_a_leftover_with_cgroup_procs_as_the_evidence_and_nothing_is_left_after_the_kill', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { runSh: '#!/bin/bash\nsleep 300 &\nexec cat\n' }));
  const entry = caseOf(result, 's01');
  assert.deepEqual(entry.lifecycle.leftover, { known: true, found: true }, 'the child outlived the program and cgroup.procs listed it before any kill');
  assert.match(entry.lifecycle.leftoverEvidence, /cgroup\.procs/);
  assert.match(result.stdout, /leftover: process still running after the exit \(killed\)/);
  assert.equal(result.cleanup.clean, true, 'after the kill nothing is left');
});

test('A_failed_launch_prefix_is_launch_failed_and_never_a_program_verdict', async t => {
  if (!needs(t)) return;
  const missing = { ...toolchains, node: path.join(toolchains.node, 'no-such-directory') };
  const result = await runCases(layerOptions(t, { dirs: missing }));
  assert.equal(result.exitCode, 3, 'exit 3: the environment');
  assert.equal(result.launchFailed, true);
  assert.match(result.stderr, /launch failed: .*Can't find source path .*no-such-directory/s, 'the launcher\'s own words are the evidence');
  assert.equal(result.record, null, 'no case verdict was reached: the runner never started');
  assert.equal(result.cleanup.clean, true);
  // The refuted shape: without the layer's launch check the runner alone cannot tell. bwrap writes child-pid BEFORE its mounts, so the missing bind reaches the runner as a program that exited 1.
  const blind = await runCases(layerOptions(t, { dirs: missing, deps: { launch: async () => ({ ok: true, value: { code: 0, stderr: '', timedOut: false } }) } }));
  assert.equal(blind.record.cases[0].reason, 'exit status 1', 'a program verdict for a prefix that never ran a program — the very thing this layer must never report');
});

const UNREADABLE = () => ({ peakBytes: null, events: null, procs: null });

test('A_data_run_refuses_to_start_when_memory_peak_cannot_be_read', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { data: true, deps: { read: UNREADABLE } }));
  assert.equal(result.exitCode, 3, 'exit 3');
  assert.equal(result.halt, 'memory-not-certifiable', 'the named halt');
  assert.equal(result.record, null, 'no case was started: nothing was judged');
  assert.match(result.stderr, /memory-not-certifiable: prog\/memory\.peak cannot be read/);
  assert.equal(result.cleanup.clean, true);
});

test('A_run_that_is_not_a_data_run_proceeds_with_memory_bound_not_certified', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { data: false, deps: { read: UNREADABLE } }));
  assert.equal(result.exitCode, 0, result.stderr);
  assert.equal(result.halt, null);
  assert.equal(result.memoryCertified, false, 'memory bound not certified');
  assert.equal(result.record.cases[0].status, 'PASS');
});

test('A_data_run_on_a_host_that_reads_memory_peak_is_certified_and_proceeds', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { data: true }));
  assert.equal(result.exitCode, 0, result.stderr);
  assert.equal(result.memoryCertified, true);
  assert.equal(result.halt, null);
});

test('T_L31_through_the_runner_is_not_applicable_until_the_runner_runs_a_signal_TERM_step', () => {
  const source = readFileSync(path.join(runnerDir, 'run_samples.py'), 'utf8');
  assert.match(source, /running a \{plan\.kind\} sample is part ii of this story and is not built in this runner/, 'the runner still refuses the daemon and web kinds: with them goes `signal TERM`, and T-L31 through the runner (9) is NOT APPLICABLE until E1.S8a part ii-b — when ii-b lands this test goes red and (9) must be written');
});
