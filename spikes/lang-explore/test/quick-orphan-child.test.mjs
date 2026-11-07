// A killed driver leaves no pass child running (review item 2). The driver records the pass child's pid and start time under `score/child.json` when it spawns it and clears it when the child is gone; a scorer that starts (or takes
// a dead scorer's lock) kills and waits for any recorded child that still runs, before its first attempt, and says so in an event. The answer file of a child carries a per-start token, so a new child never reads an orphan's answer.
// The orphan here is a REAL process in its own group (a node that sleeps); nothing else starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { clearChild, recordChild, reapOrphanChild } from '../lib/child-record.mjs';
import { runPassChild } from '../lib/pass-run.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { harness } from './quick-passes-harness.mjs';

const alive = pid => { try { process.kill(pid, 0); return true; } catch { return false; } };

function realOrphan() {
  const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { detached: true, stdio: 'ignore' });
  child.unref();
  return child.pid;
}
const untilGone = async pid => { for (let i = 0; i < 100 && alive(pid); i += 1) await new Promise(r => setTimeout(r, 50)); };

test('A_scorer_that_starts_kills_and_waits_for_a_pass_child_a_killed_driver_left_before_its_first_attempt', async () => {
  const h = harness();
  const pid = realOrphan();
  recordChild({ work: h.work, pid, startTime: processStartTime(pid), token: 'oldtoken' });
  mkdirSync(path.join(h.work, 'child'), { recursive: true });
  writeFileSync(path.join(h.work, 'child', 'p1-a1-oldtoken.answer.json'), '{"kind":"completed","record":{}}');
  assert.equal(alive(pid), true);
  const seenAtFirstAttempt = [];
  const inner = h.a.runPass;
  h.a.runPass = async spec => { seenAtFirstAttempt.push(alive(pid)); return inner(spec); };
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  assert.equal(seenAtFirstAttempt[0], false, 'the orphan was gone before the first attempt began');
  await untilGone(pid);
  assert.equal(alive(pid), false);
  assert.equal(existsSync(path.join(h.work, 'child.json')), false, 'the pointer is cleared');
  assert.equal(existsSync(path.join(h.work, 'child')), false, "an orphan's answer is removed with its folder");
  const events = readdirSync(path.join(h.work, 'events')).filter(f => f.endsWith('-orphan-child.json'));
  assert.equal(events.length, 1);
  assert.equal(JSON.parse(readFileSync(path.join(h.work, 'events', events[0]), 'utf8')).pid, pid);
});

test('A_recorded_child_that_is_gone_or_whose_pid_was_reused_is_not_killed_and_the_pointer_is_cleared', async () => {
  const work = tmpDir('quick-orphan');
  recordChild({ work, pid: process.pid, startTime: 'not-this-process-start-time', token: 't' });
  const killed = [];
  const r = await reapOrphanChild({ work, killGroup: pid => killed.push(pid) });
  assert.deepEqual(killed, [], 'the pid is alive but it is another process: it is left alone');
  assert.equal(r.found?.killed, false);
  assert.equal(existsSync(path.join(work, 'child.json')), false);
  assert.deepEqual(await reapOrphanChild({ work }), { found: null });
  recordChild({ work, pid: 4242, startTime: null, token: 't' });
  const none = [];
  await reapOrphanChild({ work, isAlive: () => true, killGroup: pid => none.push(pid) });
  assert.deepEqual(none, [], 'a pointer with no start time names no process: a dead pid reads as alive when both start times are null');
});

test('A_child_that_will_not_die_is_an_environment_refusal_not_a_second_child_beside_it', async () => {
  const work = tmpDir('quick-orphan-stubborn');
  recordChild({ work, pid: process.pid, startTime: processStartTime(process.pid), token: 't' });
  const r = await reapOrphanChild({ work, killGroup: () => {}, waitMs: 60, pollMs: 10 });
  assert.equal(r.found.killed, false);
  assert.match(r.problem, /still running/);
  assert.equal(existsSync(path.join(work, 'child.json')), true, 'the pointer stays while the child lives');
  clearChild(work);
});

test('The_answer_file_of_a_pass_child_carries_a_token_of_its_own_start_so_a_new_child_never_reads_an_orphans_answer', async () => {
  const dir = tmpDir('quick-token');
  const spec = { n: 1, attempt: 1, cpus: '0-7', tree: path.join(dir, 'tree'), runCeilingMs: 40 };
  const outs = [];
  const { EventEmitter } = await import('node:events');
  const spawnFake = (exe, args) => {
    const { out } = JSON.parse(readFileSync(args.at(-1), 'utf8'));
    outs.push(out);
    const child = new EventEmitter();
    child.pid = 4242;
    child.stderr = new EventEmitter();
    setImmediate(() => child.emit('close', 1, null));
    return child;
  };
  mkdirSync(path.join(dir, 'child'), { recursive: true });
  writeFileSync(path.join(dir, 'child', 'p1-a1-orphantoken.answer.json'), '{"kind":"completed","record":{"cases":[]}}');
  const body = await runPassChild({ spec, context: {}, spawn: spawnFake, procApi: { read: () => 'Cpus_allowed_list:\t0-7\n' }, pollMs: 0, polls: 2 });
  await runPassChild({ spec, context: {}, spawn: spawnFake, procApi: { read: () => 'Cpus_allowed_list:\t0-7\n' }, pollMs: 0, polls: 2 });
  assert.equal(body.kind, 'environment', "the orphan's completed answer was not read");
  assert.notEqual(outs[0], outs[1], 'each start has its own answer name');
  assert.ok(outs.every(o => /p1-a1-[0-9a-f]{8,}\.answer\.json$/.test(o)), outs.join(' '));
});
