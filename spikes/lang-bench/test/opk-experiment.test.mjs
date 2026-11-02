// E1.S8b OP-K experiment: the pure parts. How a run ended is read from what the runner recorded (never guessed), and the read-only cgroup view of the program's OWN cgroup is a prefix
// variant built from the layer's own prefix: `--unshare-cgroup` plus a read-only bind of `prog/` at /sys/fs/cgroup, and nothing else changed (the box module is not touched).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cgroupViewPrefix, classify, extraEnvFor, nodeOptionsFor, POLICY_3C, POLICY_1C, POLICY_DECIDED_RUN, POLICY_NODE_FLAG, setNodeOptions, summarise, viewFor, withoutCgroupView } from '../lib/exec-probe/opk.mjs';

const PREFIX = ['/usr/bin/systemd-run', '--user', '--scope', '--unit=u-{case}', '/x/scope-entry.sh', '/o/{case}', '1', '9', '/usr/bin/bwrap', '--json-status-fd', '7', '--tmpfs', '/', '--proc', '/proc', '--unshare-pid', '--new-session', '--cap-drop', 'ALL', '--clearenv', '--setenv', 'A', 'b', '--'];

test('A_run_is_classified_from_the_runners_record_by_how_the_program_ended', () => {
  assert.deepEqual(classify({ status: 'PASS', reason: '', memory: { oomKill: 0 }, stderrTail: '' }), { result: 'PASS', ended: 'exit 0' });
  assert.deepEqual(classify({ status: 'FAIL', reason: 'memory limit', memory: { oomKill: 1 }, stderrTail: '' }), { result: 'memory limit', ended: 'kernel oom_kill (1)' });
  assert.deepEqual(classify({ status: 'FAIL', reason: 'exit status 134', memory: { oomKill: 0 }, stderrTail: 'Unhandled exception. System.OutOfMemoryException: Insufficient memory' }), { result: 'runtime error', ended: '.NET OutOfMemoryException (exit status 134)' });
  assert.deepEqual(classify({ status: 'FAIL', reason: 'exit status 134', memory: { oomKill: 0 }, stderrTail: 'FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory' }), { result: 'runtime error', ended: 'V8 heap-limit abort (exit status 134)' });
  assert.deepEqual(classify({ status: 'FAIL', reason: 'time limit', memory: { oomKill: 0 }, stderrTail: '' }), { result: 'runtime error', ended: 'time limit' });
  assert.equal(classify({ status: 'FAIL', reason: 'exit status 139', memory: { oomKill: 0 }, stderrTail: 'boom' }).result, 'runtime error');
});

test('A_kernel_oom_kill_wins_over_what_the_dying_runtime_printed', () => {
  assert.equal(classify({ status: 'FAIL', reason: 'memory limit', memory: { oomKill: 2 }, stderrTail: 'JavaScript heap out of memory' }).result, 'memory limit');
});

test('The_cgroup_view_prefix_adds_unshare_cgroup_and_a_read_only_bind_of_prog_and_changes_nothing_else', () => {
  const view = cgroupViewPrefix(PREFIX, '/sys/fs/cgroup/user.slice/u-{case}.scope/prog');
  const added = view.filter(arg => !PREFIX.includes(arg) || view.indexOf(arg) !== PREFIX.indexOf(arg));
  assert.equal(view.length, PREFIX.length + 4);
  assert.ok(view.includes('--unshare-cgroup'));
  const at = view.indexOf('--ro-bind');
  assert.deepEqual(view.slice(at, at + 3), ['--ro-bind', '/sys/fs/cgroup/user.slice/u-{case}.scope/prog', '/sys/fs/cgroup']);
  assert.deepEqual(view.filter((_, i) => i < at || i >= at + 3).filter(arg => arg !== '--unshare-cgroup'), PREFIX, 'every other element of the prefix is as it was, in order');
  assert.ok(added.length > 0);
  assert.equal(view.at(-1), '--', 'the prefix still ends before the program');
});

test('The_table_has_one_row_per_mode_stack_limit_and_policy_with_the_three_runs_and_their_peaks', () => {
  const runs = [1, 2, 3].map(n => ({ mode: 'medium-objects', stack: 'node', limitMiB: 256, policy: 'today', result: n === 3 ? 'memory limit' : 'PASS', ended: n === 3 ? 'kernel oom_kill (1)' : 'exit 0', peakBytes: n * 1048576 }));
  const rows = summarise(runs);
  assert.equal(rows.length, 1);
  assert.deepEqual(rows[0], { mode: 'medium-objects', stack: 'node', limitMiB: 256, policy: 'today', results: ['PASS', 'PASS', 'memory limit'], ended: ['exit 0', 'exit 0', 'kernel oom_kill (1)'], peakMiB: [1, 2, 3] });
});

test('The_3b_variant_adds_the_hexadecimal_hard_limit_percent_to_the_dotnet_environment_only_on_top_of_the_view', () => {
  assert.deepEqual(extraEnvFor('cgroup-view-own+gchl100', 'csharp'), { DOTNET_GCHeapHardLimitPercent: '0x64' });
  assert.deepEqual(extraEnvFor('cgroup-view-own+gchl100', 'node'), {}, '.NET only');
  assert.deepEqual(extraEnvFor('cgroup-view-own', 'csharp'), {});
  assert.deepEqual(extraEnvFor('today', 'csharp'), {});
  const view = cgroupViewPrefix(PREFIX, '/p/{case}');
  assert.ok(viewFor('cgroup-view-own+gchl100') && viewFor('cgroup-view-own') && !viewFor('today'), 'the view is in 3 and 3b and not in 1');
  assert.ok(view.length > PREFIX.length);
});

test('A_dotnet_native_out_of_memory_abort_is_told_from_a_managed_exception_and_from_a_kernel_kill', () => {
  const abort = classify({ status: 'FAIL', reason: 'exit status 134', memory: { oomKill: 0 }, stderrTail: 'Out of memory.' });
  assert.deepEqual(abort, { result: 'runtime error', ended: '.NET runtime abort "Out of memory." (exit status 134), not a kernel oom_kill' });
  assert.match(classify({ status: 'FAIL', reason: 'exit status 134', memory: { oomKill: 0 }, stderrTail: 'Unhandled exception. System.OutOfMemoryException' }).ended, /OutOfMemoryException/);
  assert.equal(classify({ status: 'FAIL', reason: 'memory limit', memory: { oomKill: 1 }, stderrTail: 'Out of memory.' }).result, 'memory limit');
});

test('The_3c_variant_sets_the_hard_limit_to_the_cases_limit_in_hexadecimal_bytes_for_dotnet_only_on_top_of_the_view', () => {
  assert.deepEqual(extraEnvFor(POLICY_3C, 'csharp', 256), { DOTNET_GCHeapHardLimit: '0x10000000' });
  assert.deepEqual(extraEnvFor(POLICY_3C, 'csharp', 512), { DOTNET_GCHeapHardLimit: '0x20000000' });
  assert.deepEqual(extraEnvFor(POLICY_3C, 'node', 256), {}, '.NET only');
  assert.equal(viewFor(POLICY_3C), true);
});

test('The_node_flag_variant_sets_NODE_OPTIONS_in_the_prefix_for_node_only_and_the_decided_policy_changes_nothing', () => {
  const prefix = ['bwrap', '--clearenv', '--setenv', 'LANG', 'x', '--setenv', 'NODE_OPTIONS', '', '--setenv', 'TZ', 'UTC', '--'];
  assert.deepEqual(setNodeOptions(prefix, '--max-old-space-size=256'), ['bwrap', '--clearenv', '--setenv', 'LANG', 'x', '--setenv', 'NODE_OPTIONS', '--max-old-space-size=256', '--setenv', 'TZ', 'UTC', '--']);
  assert.throws(() => setNodeOptions(['bwrap', '--'], 'x'), /NODE_OPTIONS/);
  assert.equal(viewFor(POLICY_DECIDED_RUN), false, 'the layer\'s own box already has the view: nothing is added');
  assert.equal(viewFor(POLICY_NODE_FLAG), false);
  assert.deepEqual(extraEnvFor(POLICY_DECIDED_RUN, 'csharp', 256), {});
  assert.equal(nodeOptionsFor(POLICY_NODE_FLAG, 'node', 512), '--max-old-space-size=512');
  assert.equal(nodeOptionsFor(POLICY_NODE_FLAG, 'csharp', 512), null, 'Node only');
  assert.equal(nodeOptionsFor(POLICY_DECIDED_RUN, 'node', 512), null);
});

test('A_run_the_runner_judged_memory_limit_through_a_runtime_abort_is_not_labelled_a_kernel_kill', () => {
  assert.deepEqual(classify({ status: 'FAIL', reason: 'memory limit', memory: { oomKill: 0, runtimeAbort: 'v8' }, stderrTail: '' }), { result: 'memory limit', ended: 'V8 heap-limit abort (exit 134, runtimeAbort v8)' });
  assert.deepEqual(classify({ status: 'FAIL', reason: 'memory limit', memory: { oomKill: 0, runtimeAbort: 'dotnet' }, stderrTail: '' }), { result: 'memory limit', ended: '.NET runtime abort (exit 134, runtimeAbort dotnet)' });
  assert.equal(classify({ status: 'FAIL', reason: 'memory limit', memory: { oomKill: 1, runtimeAbort: null }, stderrTail: '' }).ended, 'kernel oom_kill (1)');
});

test('The_1c_variant_takes_the_cgroup_view_out_of_the_layers_prefix_and_changes_nothing_else', () => {
  const real = ['bwrap', '--tmpfs', '/', '--ro-bind', '/usr', '/usr', '--ro-bind', '/sys/fs/cgroup/x/prog', '/sys/fs/cgroup', '--unshare-cgroup', '--unshare-user', '--disable-userns', '--unshare-pid', '--clearenv', '--', 'x'];
  const bare = withoutCgroupView(real);
  assert.deepEqual(bare, ['bwrap', '--tmpfs', '/', '--ro-bind', '/usr', '/usr', '--unshare-user', '--disable-userns', '--unshare-pid', '--clearenv', '--', 'x'], 'the view\'s bind and --unshare-cgroup go; every other element stays, in order');
  assert.throws(() => withoutCgroupView(['bwrap', '--']), /no cgroup view/);
  assert.equal(viewFor(POLICY_1C), false, 'no view is ADDED');
  assert.deepEqual(extraEnvFor(POLICY_1C, 'csharp', 256), {}, 'the hard limit comes from the layer\'s own .NET environment (stack)');
  assert.equal(nodeOptionsFor(POLICY_1C, 'node', 256), null, 'Node gets no flag');
});
