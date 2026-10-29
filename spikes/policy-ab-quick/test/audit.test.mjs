import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { auditRecord } from '../hooks/audit.mjs';

const hook = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'hooks', 'audit.mjs');
const sha = () => null;
const bash = (event, extra = {}) => ({ hook_event_name: event, tool_name: 'Bash', tool_use_id: 'toolu_01', session_id: 'sess-1',
  tool_input: { command: 'dotnet build', run_in_background: true }, ...extra });
const tmp = () => fs.mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-pab-audit-'));
const runHook = (args, input, cwd = os.tmpdir()) => spawnSync(process.execPath, [hook, ...args], { input, cwd });

test('The_audit_record_carries_what_the_partition_pairs_by', () => {
  const pre = auditRecord(bash('PreToolUse'), '2026-10-29T10:00:00.000Z', sha);
  const post = auditRecord(bash('PostToolUse', { duration_ms: 812 }), '2026-10-29T10:00:00.812Z', sha);
  assert.equal(pre.tool_use_id, 'toolu_01');
  assert.equal(post.tool_use_id, 'toolu_01');
  assert.equal(pre.session_id, 'sess-1');
  assert.equal(post.duration_ms, 812);
  assert.equal(pre.duration_ms, null);
  assert.equal(pre.input.run_in_background, true);
  assert.equal(pre.input.command, 'dotnet build');
});

test('An_absent_duration_or_flag_is_null_never_zero', () => {
  const r = auditRecord({ hook_event_name: 'PostToolUse', tool_name: 'Bash', tool_input: { command: 'ls' } }, 'T', sha);
  assert.equal(r.duration_ms, null);
  assert.equal(r.input.run_in_background, null);
  assert.equal(r.tool_use_id, null);
  assert.equal(r.session_id, null);
  const odd = auditRecord({ hook_event_name: 'PostToolUse', tool_name: 'Bash', duration_ms: 'soon', tool_use_id: 7, tool_input: { command: 'ls', run_in_background: 'yes' } }, 'T', sha);
  assert.equal(odd.duration_ms, null);
  assert.equal(odd.tool_use_id, null);
  assert.equal(odd.input.run_in_background, null);
});

test('The_audit_writes_where_it_is_told', () => {
  const dir = tmp();
  try {
    const out = path.join(dir, 'a.ndjson');
    const r = runHook(['--out', out], JSON.stringify(bash('PreToolUse')));
    assert.equal(r.status, 0);
    assert.equal(r.stdout.length, 0);
    const lines = fs.readFileSync(out, 'utf8').trim().split('\n');
    assert.equal(lines.length, 1);
    assert.equal(JSON.parse(lines[0]).tool_use_id, 'toolu_01');
    assert.equal(fs.existsSync(path.join(dir, '.git')), false, 'nothing is written under any .git');
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('Without_out_the_record_goes_to_the_trees_own_git_dir_as_before', () => {
  const dir = tmp();
  try {
    fs.mkdirSync(path.join(dir, '.git'));
    fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
    const copy = path.join(dir, '.claude', 'hooks', 'audit.mjs');
    fs.copyFileSync(hook, copy);
    const r = spawnSync(process.execPath, [copy], { input: JSON.stringify(bash('PreToolUse')) });
    assert.equal(r.status, 0);
    assert.equal(JSON.parse(fs.readFileSync(path.join(dir, '.git', 'audit.ndjson'), 'utf8')).tool_use_id, 'toolu_01');
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('A_broken_payload_or_argument_still_exits_zero_and_writes_no_line', () => {
  const dir = tmp();
  try {
    const out = path.join(dir, 'a.ndjson');
    const broken = runHook(['--out', out], 'not json');
    assert.equal(broken.status, 0);
    const unknown = runHook(['--out', out, '--nope'], JSON.stringify(bash('PreToolUse')));
    assert.equal(unknown.status, 0, 'a strict parse failure is still observe-only');
    assert.match(unknown.stderr.toString(), /audit/);
    assert.equal(fs.existsSync(out), false);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});
