import assert from 'node:assert/strict';
import { cpSync, existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { runBounded } from '../lib/run.mjs';
import { parseCli } from '../score-session.mjs';
import { sandboxGate, spikeDir, stubTrees, tmpDir } from './helpers.mjs';

const cli = fileURLToPath(new URL('../score-session.mjs', import.meta.url));
const good = ['--session', '/s', '--source', '/src', '--arm', 'bare', '--stack', 'node', '--phase', 'v1', '--toolchain', '/t'];

async function runCli(args) {
  const r = await runBounded(process.execPath, [cli, ...args], { cwd: spikeDir, timeoutMs: 120_000 });
  assert.equal(r.ok, true, r.reason);
  return r.value;
}

test('A_usage_error_exits_4_and_names_what_is_wrong', async () => {
  assert.equal(parseCli(good).ok, true);
  assert.match(parseCli(good.slice(2)).reason, /--session/);
  assert.match(parseCli(['--bogus']).reason, /bogus/);
  assert.match(parseCli(good.slice(0, -2)).reason, /toolchain/);
  const r = await runCli(good.slice(0, 4));
  assert.equal(r.code, 4);
  assert.match(r.stdout, /usage error/);
  const bad = await runCli(good.map(a => (a === 'v1' ? 'v9' : a)));
  assert.equal(bad.code, 4);
});

test('A_missing_toolchain_exits_3_as_an_environment_problem', async () => {
  const r = await runCli(good.map(a => (a === '/t' ? '/no/such/toolchain' : a)));
  assert.equal(r.code, 3);
  assert.match(r.stdout, /\/no\/such\/toolchain/);
});

test('One_command_scores_a_session_and_writes_score_json', async t => {
  if (!sandboxGate(t)) return;
  const session = tmpDir('cli-session');
  const source = tmpDir('cli-source');
  cpSync(path.join(stubTrees, 'silent'), source, { recursive: true });
  const repo = process.env.BENCH_INPUTS_REPO;
  const r = await runCli(['--session', session, '--source', source, '--arm', 'bare', '--stack', 'node', '--phase', 'v1',
    '--toolchain', path.dirname(path.dirname(process.execPath)), ...(repo ? ['--inputs-repo', repo] : [])]);
  assert.equal(r.code, 0, r.stdout + r.stderr);
  assert.match(r.stdout, /^build: ok/m);
  assert.match(r.stdout, /^visible: test-v1\.sh \d+\/11/m);
  assert.match(r.stdout, /^heldout: unknown/m);
  const score = JSON.parse(readFileSync(path.join(session, 'score', 'score.json'), 'utf8'));
  assert.equal(score.format, 1);
  assert.deepEqual(score.accepted, { known: true, value: false }, 'a silent binary fails the visible tests');
  assert.equal(score.heldout.known, false);
  assert.equal(existsSync(path.join(session, 'score', 'copy')), false);
});
