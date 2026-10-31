// The read-back of what a session was launched with (`common.measurement` rule 1): the plan beside the session without any secret, compared with the registered prompt, the CLI's own transcript and the
// box's environment keys.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { loadPrompts, planForSession } from '../lib/plan.mjs';
import { firstUserMessage, planRecord, readbackOf, recordPlan } from '../lib/readback.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';

const EXPLORE = loadExplore().value;
const PROMPTS = loadPrompts().prompts;
const sha = t => createHash('sha256').update(t).digest('hex');

/** A session dir with a plan.json (from the real plan function), an argv.json and optionally a transcript. */
function session({ stack = 'node', transcript = null, envKeys = ['HOME', 'LANG', 'LC_ALL', 'PATH'], over = {} } = {}) {
  const dir = tmpDir('rb');
  const layout = { ...layoutPaths(dir, 'abc123'), dir };
  const ctx = { claudeBinary: '/x/claude', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/ts', token: 'REAL-TOKEN-VALUE', prices: {}, home: '/h', harnessSources: {}, caps: EXPLORE.caps, envExtra: EXPLORE.envExtra, locale: EXPLORE.locale, prompts: loadPrompts() };
  const plan = { ...planForSession({ ctx, item: { session: { stack }, layout } }), ...over };
  recordPlan(plan);
  writeFileSync(path.join(dir, 'argv.json'), JSON.stringify({ envKeys }));
  if (transcript !== null) {
    mkdirSync(path.join(dir, 'transcripts'), { recursive: true });
    writeFileSync(path.join(dir, 'transcripts', 't.jsonl'), `${JSON.stringify({ type: 'system' })}\n${JSON.stringify({ type: 'user', message: { role: 'user', content: transcript } })}\n`);
  }
  return { dir, plan };
}

test('The_plan_record_is_beside_the_session_and_holds_no_token_no_credential_and_no_price', () => {
  const { dir, plan } = session();
  const text = readFileSync(path.join(dir, 'plan.json'), 'utf8');
  assert.ok(!text.includes('REAL-TOKEN-VALUE') && !text.includes(plan.dummy), 'neither the token nor the dummy credential');
  const rec = JSON.parse(text);
  assert.deepEqual([rec.promptSha256, rec.allow, rec.capUsd, rec.locale], [PROMPTS.node.sha256, [], 50, 'de_DE.UTF-8']);
  assert.deepEqual(Object.keys(planRecord(plan)).sort(), Object.keys(rec).sort());
  assert.ok(!('token' in rec) && !('dummy' in rec) && !('prices' in rec));
});

test('A_session_launched_as_planned_reads_back_ok_and_a_stub_without_a_transcript_is_not_applicable', () => {
  const { dir } = session();
  const rb = readbackOf({ sessionDir: dir, registered: PROMPTS.node });
  assert.equal(rb.ok, true);
  assert.deepEqual(rb.checks.map(c => c.ok), [true, null, true, true]);
  const real = session({ transcript: [{ type: 'text', text: `${PROMPTS.node.text}\n` }] });
  const seen = readbackOf({ sessionDir: real.dir, registered: PROMPTS.node });
  assert.deepEqual(seen.checks.map(c => c.ok), [true, true, true, true], 'the CLI\'s own record of the prompt agrees (trailing whitespace ignored)');
  assert.equal(seen.transcriptPromptSha256, PROMPTS.node.sha256);
  assert.equal(firstUserMessage(real.dir), `${PROMPTS.node.text}\n`);
  assert.equal(firstUserMessage(dir), null);
});

test('Each_departure_is_named_a_different_prompt_a_missing_locale_key_a_transcript_that_differs_an_allowed_host', () => {
  const failing = rb => rb.checks.filter(c => c.ok === false).map(c => c.id);
  const wrong = session({ over: { prompt: 'another prompt' } });
  assert.deepEqual(failing(readbackOf({ sessionDir: wrong.dir, registered: PROMPTS.node })), ['the planned prompt is the registered one']);
  const noLc = session({ envKeys: ['HOME', 'LANG'] });
  assert.deepEqual(failing(readbackOf({ sessionDir: noLc.dir, registered: PROMPTS.node })), ['LANG and LC_ALL are in the box environment']);
  const drift = session({ transcript: 'what the CLI really received' });
  assert.deepEqual(failing(readbackOf({ sessionDir: drift.dir, registered: PROMPTS.node })), ['the prompt the CLI recorded is the registered one']);
  const open = session({ over: { allow: ['registry.npmjs.org'] } });
  assert.deepEqual(failing(readbackOf({ sessionDir: open.dir, registered: PROMPTS.node })), ['no host is allowed']);
  assert.equal(readbackOf({ sessionDir: open.dir, registered: PROMPTS.node }).ok, false);
  assert.equal(sha('x').length, 64);
});
