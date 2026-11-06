// What the end of a quick-run build means (lib/build-quick.mjs): a box that never started the build is a void (`environment`), a killed box is a void, a timeout and a failed marked build are submissions, and the marker
// stays out of the kept tail. The box is a fake: it answers what `inBuildBox` answers, and nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { BUILD_MARKER, MARKED_BUILD, boxBuild, buildOutcome } from '../lib/build-quick.mjs';

const answer = over => ({ code: 0, signal: null, timedOut: false, stdout: '', stderr: `${BUILD_MARKER}\n`, ms: 77, ...over });
const SNAPSHOTS = { csharp: { dir: '/c/cs-snap', record: {} }, node: { dir: '/c/node-snap', record: {} } };
const buildWith = fakeBox => boxBuild({ owned: {}, dirs: {}, snapshots: SNAPSHOTS, stack: 'node', inBox: fakeBox });
const run = fakeBox => buildWith(fakeBox)({ tree: '/t', timeoutMs: 1000 });

test('A_box_that_fails_before_the_build_begins_has_no_marker_and_is_an_environment_failure_not_a_failed_build', async () => {
  for (const [name, stderr] of [['nsenter', 'nsenter: cannot open /proc/9/ns/user: No such file or directory\n'], ['bwrap', 'bwrap: Can\'t mkdir /x: Permission denied\n'], ['seed', 'cp: error writing: No space left on device\n']]) {
    const r = await run(async () => ok(answer({ code: 1, stderr })));
    assert.equal(r.ok, false, name);
    assert.equal(r.kind, 'environment', name);
    assert.match(r.reason, /did not begin/, name);
  }
});

test('A_box_ended_from_outside_is_an_environment_failure_even_when_the_marker_was_printed', async () => {
  const r = await run(async () => ok(answer({ code: null, signal: 'SIGKILL' })));
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /ended from outside \(signal SIGKILL\)/);
});

test('A_marked_build_past_its_bound_is_a_submission', async () => {
  const r = await run(async () => ok(answer({ code: null, signal: 'SIGKILL', timedOut: true, stderr: `${BUILD_MARKER}\nstill compiling\n` })));
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual([r.value.ok, r.value.timedOut], [false, true]);
  assert.equal(r.value.tail.includes(BUILD_MARKER), false);
});

test('A_timeout_with_no_marker_is_the_box_hanging_before_the_build_began_and_an_environment_failure', async () => {
  for (const stderr of ['', 'nsenter: waiting for the holder\n']) {
    const r = await run(async () => ok(answer({ code: null, signal: 'SIGKILL', timedOut: true, stderr })));
    assert.equal(r.ok, false);
    assert.equal(r.kind, 'environment');
    assert.match(r.reason, /did not begin \(timed out\)/);
  }
});

test('A_marked_build_that_exits_non_zero_is_a_failed_build_a_submission_with_its_tail', async () => {
  const r = await run(async () => ok(answer({ code: 2, stdout: 'compiling\n', stderr: `${BUILD_MARKER}\nerror CS1002\n` })));
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual([r.value.ok, r.value.exit, r.value.timedOut, r.value.wallMs], [false, 2, false, 77]);
  assert.match(r.value.tail, /error CS1002/);
});

test('A_marked_build_that_exits_zero_passes', async () => {
  const r = await run(async () => ok(answer({})));
  assert.deepEqual([r.ok, r.value.ok, r.value.exit], [true, true, 0]);
});

test('The_marker_never_reaches_the_kept_tail', async () => {
  const r = await run(async () => ok(answer({ code: 1, stdout: 'out\n', stderr: `${BUILD_MARKER}\nboom\n` })));
  assert.equal(r.value.tail.includes(BUILD_MARKER), false);
  assert.equal(r.value.tail.includes('boom'), true);
  assert.equal(buildOutcome(answer({})).value.tail.includes('@@'), false);
});

test('The_marker_is_a_positional_argument_of_the_wrapper_and_the_command_ends_in_the_frozen_build', async () => {
  let seen = null;
  await run(async args => { seen = args; return ok(answer({})); });
  assert.deepEqual(seen.command.slice(0, 2), ['bash', '-c']);
  assert.equal(seen.command[2].includes(BUILD_MARKER), false, 'the marker is not text of the script');
  assert.equal(seen.command.includes(BUILD_MARKER), true);
  assert.deepEqual(seen.command.slice(-2), ['bash', 'build.sh']);
  assert.deepEqual(seen.command, [...MARKED_BUILD]);
  assert.equal(seen.view, '/t');
  assert.equal(seen.timeoutMs, 1000);
  assert.equal(seen.caches, '/c');
});

test('A_box_that_could_not_be_started_at_all_is_an_environment_failure', async () => {
  const r = await run(async () => fail('could not start nsenter: ENOENT'));
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /the build box did not run: could not start nsenter/);
});
