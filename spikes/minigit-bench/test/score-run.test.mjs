// Running the scorer for a finished session (E3b.S1 step 6): bounded as a whole, retried only on an environment failure, never a reason to void.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { scoreSession } from '../lib/score-run.mjs';
import { tmpDir } from './helpers.mjs';

/** A stub scorer: a node script that records each call (and its pid) in `calls.txt`, then behaves as `mode` says. */
function stubScorer(dir, body) {
  const file = path.join(dir, 'stub-scorer.mjs');
  writeFileSync(file, `import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';\nimport path from 'node:path';\nconst arg = n => process.argv[process.argv.indexOf('--' + n) + 1];\nappendFileSync(path.join(path.dirname(import.meta.filename), 'calls.txt'), process.pid + ' ' + process.argv.slice(2).join(' ') + '\\n');\n${body}\n`);
  return file;
}
const base = dir => ({ session: path.join(dir, 's'), source: path.join(dir, 's', 'box', 'work'), stack: 'csharp', phase: 'v1', toolchains: ['/ts', '/node'], nugetCache: '/n', npmCache: '/p' });
const calls = dir => readFileSync(path.join(dir, 'calls.txt'), 'utf8').split('\n').filter(Boolean);

test('A_scorer_that_exits_zero_is_scored_and_its_score_is_read_back', async () => {
  const dir = tmpDir('scorer');
  const scorer = stubScorer(dir, "mkdirSync(path.join(arg('session'), 'score'), { recursive: true }); writeFileSync(path.join(arg('session'), 'score', 'score.json'), JSON.stringify({ verdict: { accepted: { known: true, value: true } } })); console.log('score written');");
  const r = await scoreSession({ ...base(dir), scorer, outerTimeoutMs: 20_000, sleep: async () => { throw new Error('no retry expected'); } });
  assert.equal(r.kind, 'scored');
  assert.equal(r.score.verdict.accepted.value, true);
  const line = calls(dir)[0];
  for (const needle of ['--arm bare', '--stack csharp', '--phase v1', '--toolchain /ts', '--toolchain /node', '--nuget-cache /n', '--npm-cache /p', '--source']) assert.ok(line.includes(needle), `${needle} in: ${line}`);
});

test('A_scorer_that_hangs_is_killed_at_the_outer_limit', async () => {
  const dir = tmpDir('scorer');
  const scorer = stubScorer(dir, 'setInterval(() => {}, 1000);');
  let sleeps = 0;
  const r = await scoreSession({ ...base(dir), scorer, outerTimeoutMs: 500, sleep: async () => { sleeps += 1; } });
  assert.equal(r.kind, 'scorer-timeout');
  assert.equal(sleeps, 0, 'never retried: a hang repeats');
  assert.equal(calls(dir).length, 1);
  const pid = Number(calls(dir)[0].split(' ')[0]);
  await new Promise(res => setTimeout(res, 200));
  assert.throws(() => process.kill(pid, 0), /ESRCH/, 'no scorer process is left');
});

test('A_scorer_environment_failure_halts_resumably_and_never_voids', async () => {
  const dir = tmpDir('scorer');
  const scorer = stubScorer(dir, "console.log('environment: bwrap is not on PATH'); process.exit(3);");
  const waits = [];
  const r = await scoreSession({ ...base(dir), scorer, outerTimeoutMs: 20_000, sleep: async ms => { waits.push(ms); } });
  assert.equal(r.kind, 'environment');
  assert.deepEqual(waits, [60_000, 60_000, 60_000], 'three retries, 60 s apart');
  assert.equal(calls(dir).length, 4, 'the first try and three retries');
  assert.match(r.reason, /bwrap is not on PATH/);
});

test('An_environment_failure_that_clears_on_a_retry_is_scored', async () => {
  const dir = tmpDir('scorer');
  const scorer = stubScorer(dir, "import('node:fs').then(fs => { const n = fs.readFileSync(path.join(path.dirname(import.meta.filename), 'calls.txt'), 'utf8').split('\\n').filter(Boolean).length; if (n < 2) { console.log('environment: busy'); process.exit(3); } mkdirSync(path.join(arg('session'), 'score'), { recursive: true }); writeFileSync(path.join(arg('session'), 'score', 'score.json'), '{}'); });");
  const r = await scoreSession({ ...base(dir), scorer, outerTimeoutMs: 20_000, sleep: async () => {} });
  assert.equal(r.kind, 'scored');
  assert.equal(calls(dir).length, 2);
});

test('A_scorer_that_fails_is_a_failure_not_a_retry_and_not_a_void', async () => {
  for (const code of [4, 5]) {
    const dir = tmpDir('scorer');
    const scorer = stubScorer(dir, `console.log('scorer failed: boom'); process.exit(${code});`);
    const r = await scoreSession({ ...base(dir), scorer, outerTimeoutMs: 20_000, sleep: async () => { throw new Error('no retry'); } });
    assert.equal(r.kind, 'failed', `exit ${code}`);
    assert.equal(calls(dir).length, 1);
    assert.match(r.reason, /boom/);
  }
});

test('A_zero_exit_without_a_score_file_is_a_failure', async () => {
  const dir = tmpDir('scorer');
  const scorer = stubScorer(dir, "console.log('all fine');");
  const r = await scoreSession({ ...base(dir), scorer, outerTimeoutMs: 20_000, sleep: async () => {} });
  assert.equal(r.kind, 'failed');
  assert.match(r.reason, /score\.json/);
  assert.equal(existsSync(path.join(dir, 's', 'score', 'score.json')), false);
});

