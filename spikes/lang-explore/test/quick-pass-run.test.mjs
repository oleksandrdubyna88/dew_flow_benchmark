// The pass child from the driver's side and from its own (sections 2.3 and the wall bound of the plan round): the child runs under `taskset -c <profile>`, its mask is read back right after the spawn and again by the child
// itself, the whole child has a deadline after which its process group is killed and the attempt is an environment one, and a child that answers nothing is an environment attempt too. Fakes: no process starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { childMain } from '../lib/pass-child.mjs';
import { PASS_CHILD_MARGIN_MS, passChildDeadlineMs, runPassChild } from '../lib/pass-run.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { bodyFor, harness } from './quick-passes-harness.mjs';

const status = list => `Name:\tnode\nCpus_allowed_list:\t${list}\nMems_allowed_list:\t0\n`;

/** A fake child process: `pid`, a stderr stream and a `close` event. */
class FakeChild extends EventEmitter {
  constructor(pid) { super(); this.pid = pid; this.stderr = new EventEmitter(); this.stdout = new EventEmitter(); }
  end(code, signal = null) { this.emit('close', code, signal); }
}

/** Seams for `runPassChild`: `answer(spec)` -> the body the child writes to its answer file (or null: it answers nothing); `maskOf(wanted)` -> the list the fake /proc shows; `silent` -> the child never ends by itself. */
function seams({ answer = () => null, maskOf = w => w, silent = false } = {}) {
  const seen = { spawned: [], killed: [] };
  let child;
  const spawn = (exe, args) => {
    child = new FakeChild(4242);
    seen.spawned.push([exe, ...args]);
    const argsFile = args.at(-1);
    if (!silent) setImmediate(() => {
      const { out, spec } = JSON.parse(readFileSync(argsFile, 'utf8'));
      const body = answer(spec);
      if (body !== null) writeFileSync(out, JSON.stringify(body));
      child.end(body === null ? 1 : 0);
    });
    return child;
  };
  return {
    seen, spawn, procApi: { read: () => status(maskOf(seen.spawned.at(-1)[2])) }, pollMs: 0, polls: 3,
    killGroup: pid => { seen.killed.push(pid); setImmediate(() => child.end(null, 'SIGKILL')); },
  };
}

const specOf = (over = {}) => ({ n: 2, attempt: 1, profile: '8cpu', cpus: '0-7', tree: path.join(tmpDir('quick-pass-run'), 'tree'), samplesDir: '/x/cases', caseIds: ['h001'], caseWallMs: 1, holdSeconds: 61, runCeilingMs: 40, limits: {}, ...over });
const CONTEXT = Object.freeze({ stack: 'node', kind: 'daemon', dirs: { dotnetSdk: '/d', node: '/n', typescript: '/t' }, caseWallMs: 1, runCeilingMs: 40 });
const run = (spec, s, extra = {}) => runPassChild({ spec, context: CONTEXT, ...s, ...extra });

test('A_pass_whose_mask_cannot_be_applied_is_refused_before_the_build_recorded_as_a_halt_named_affinity_not_applied_and_never_scored', async () => {
  const s = seams({ maskOf: () => '6' });
  const body = await run(specOf({ cpus: '6-7', profile: '2cpu', n: 1 }), s);
  assert.equal(body.halt, 'affinity');
  assert.equal(body.kind, 'environment');
  assert.match(body.reason, /^affinity-not-applied: wanted 6-7, observed 6/);
  assert.deepEqual(s.seen.killed, [4242], 'the child is killed');
  const h = harness({ a: { runPass: spec => run({ ...spec, tree: path.join(path.dirname(spec.tree), 'tree') }, seams({ maskOf: () => '6' })) } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /^affinity-not-applied: /);
  assert.equal(existsSync(path.join(h.work, 'passes')), false, 'no attempt file');
  assert.ok(readdirSync(path.join(h.work, 'events')).some(f => f.endsWith('-affinity.json')));
});

test('A_child_started_under_a_profile_reads_back_that_profile_in_any_spelling_and_its_answer_is_returned_whole', async () => {
  const s = seams({ maskOf: () => '0,1,2,3,4,5,6,7', answer: () => ({ kind: 'completed', record: { cases: [] }, runner: '0.x', memoryCertified: true, observedCpus: '0-7' }) });
  const body = await run(specOf(), s);
  assert.equal(body.kind, 'completed');
  assert.equal(body.observedCpus, '0-7');
  assert.deepEqual(s.seen.spawned[0].slice(0, 3), ['taskset', '-c', '0-7'], 'the child runs under taskset with the profile');
  assert.equal(s.seen.killed.length, 0);
});

test('A_child_that_ends_without_an_answer_is_an_environment_attempt_naming_how_it_ended', async () => {
  const body = await run(specOf(), seams());
  assert.equal(body.kind, 'environment');
  assert.match(body.reason, /without an answer.*exit 1/);
  assert.equal(body.halt, undefined);
});

test('The_pass_child_has_a_wall_bound_a_child_that_never_answers_is_killed_with_its_group_recorded_as_a_pass_child_timeout_and_counts_toward_the_three', async () => {
  assert.equal(passChildDeadlineMs({ runCeilingMs: 7_200_000 }), 7_200_000 + PASS_CHILD_MARGIN_MS);
  assert.ok(PASS_CHILD_MARGIN_MS > 0);
  const s = seams({ silent: true });
  const body = await run(specOf(), s, { deadlineMs: 30 });
  assert.equal(body.kind, 'environment');
  assert.match(body.reason, /^pass-child timeout/);
  assert.deepEqual(s.seen.killed, [4242]);
  const h = harness({ a: { runPass: spec => run(spec, seams({ silent: true }), { deadlineMs: 20 }) } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'void', 'every pass used its three attempts and none failed functionally');
  assert.equal(readdirSync(path.join(h.work, 'passes')).length, 30);
  assert.match(JSON.parse(readFileSync(path.join(h.work, 'passes', 'p01-2cpu-a3.json'), 'utf8')).reason, /pass-child timeout/);
});

// ---- the child's own side ---------------------------------------------------------------------------------------------------------------------------

function childFiles(spec) {
  const dir = tmpDir('quick-pass-child');
  const argsFile = path.join(dir, 'args.json');
  const out = path.join(dir, 'answer.json');
  writeFileSync(argsFile, JSON.stringify({ spec, context: CONTEXT, out }));
  return { argsFile, out };
}
const write = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(`${file}.part`, text); renameSync(`${file}.part`, file); };

test('The_pass_child_reads_its_own_affinity_at_start_and_another_mask_is_an_environment_answer_naming_both_masks', async () => {
  const { argsFile, out } = childFiles(specOf({ cpus: '6-7' }));
  let ran = 0;
  const code = await childMain({ argv: [argsFile], readStatus: () => status('0-7'), runPassFn: async () => { ran += 1; }, write });
  const body = JSON.parse(readFileSync(out, 'utf8'));
  assert.equal(code, 3);
  assert.equal(body.kind, 'environment');
  assert.match(body.reason, /^affinity-not-applied: wanted 6-7, observed 0-7/);
  assert.equal(ran, 0, 'no case is run');
});

test('The_pass_child_runs_one_pass_when_its_mask_is_the_profile_and_writes_a_completed_answer_with_the_runners_record', async () => {
  const { argsFile, out } = childFiles(specOf({ cpus: '6-7' }));
  const record = { runner: '0.x', cases: [] };
  const ranFn = async () => ({ ok: true, value: { cases: [], record, ran: { memoryCertified: true } } });
  const code = await childMain({ argv: [argsFile], readStatus: () => status('6,7'), runPassFn: ranFn, write });
  assert.equal(code, 0);
  assert.deepEqual(JSON.parse(readFileSync(out, 'utf8')), { kind: 'completed', record, runner: '0.x', memoryCertified: true, observedCpus: '6,7' });
  const failing = childFiles(specOf({ cpus: '6-7' }));
  const bad = await childMain({ argv: [failing.argsFile], readStatus: () => status('6-7'), runPassFn: async () => ({ ok: false, reason: 'the layer halted: invented', kind: 'environment' }), write });
  assert.equal(bad, 3);
  assert.equal(JSON.parse(readFileSync(failing.out, 'utf8')).kind, 'environment');
  assert.equal(await childMain({ argv: [], readStatus: () => '', runPassFn: async () => {}, write }), 4, 'a usage error is exit 4');
});
