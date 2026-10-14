import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { buildChildEnv, buildClaudeArgs, filterPath } from '../lib/env.mjs';
import { canaryTargets, isDeletable, plant, unplant } from '../lib/canaries.mjs';
import { toInvocation, resolveCompile } from '../lib/commands.mjs';
import { routeDiff, splitSections } from '../lib/diffsplit.mjs';
import { compiledTestPath, entryPath, fileStem, holdoutEntries, repoPathOf } from '../lib/holdout.mjs';
import { parseJunit, verdictOf } from '../lib/junit.mjs';
import { parseGitlink } from '../lib/git.mjs';
import { applyOverlaySettings } from '../lib/settings.mjs';
import { parseStruck, strikeTargets } from '../lib/strike.mjs';
import { parseNdjson, summarize } from '../lib/summary.mjs';
import { scrub } from '../lib/scrub.mjs';
import { auditRecord } from '../hooks/audit.mjs';
import { effortPinOutput } from '../hooks/effort.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const fx = name => fs.readFileSync(path.join(here, 'fixtures', name), 'utf8');

test('strike removes the heading suffix and the model markers, reports misses', () => {
  const statement = '**Struck**\n\n1. Suffix ` (Opus)` — original: `### E1 — Head (Opus)`.\n2. Sentence: `Opus: no judgement.`\n3. Marker: `**Model: Opus** — a type, two lines.`\n';
  const edits = parseStruck(statement);
  assert.equal(edits.length, 3);
  const plan = '### E1 — Head (Opus)\nWhy. Opus: no judgement.\nbody\n**Model: Opus** —\n      a type, two lines.\n**DoD:** x\n';
  const { text, notFound } = strikeTargets(plan, edits);
  assert.deepEqual(notFound, []);
  assert.ok(text.startsWith('### E1 — Head\n'));
  assert.ok(!/Opus/.test(text));
  assert.equal(strikeTargets('nothing', edits).notFound.length, 3);
});

test('parseStruck on the real statement (when present) yields four edits', () => {
  const file = path.join(here, '..', 'inputs', 'statement.md');
  if (!fs.existsSync(file)) return;
  assert.equal(parseStruck(fs.readFileSync(file, 'utf8')).length, 4);
});

test('settings overlay keeps project keys, adds arm B guard, refuses double wiring', () => {
  const base = { permissions: { defaultMode: 'acceptEdits' }, hooks: { PreToolUse: [{ matcher: 'Bash', hooks: [] }] } };
  const a = applyOverlaySettings(base, 'A');
  const b = applyOverlaySettings(base, 'B');
  assert.equal(a.permissions.defaultMode, 'acceptEdits');
  assert.equal(a.hooks.PreToolUse.length, 3);
  assert.equal(b.hooks.PreToolUse.length, 4);
  assert.equal(a.hooks.InstructionsLoaded.length, 1);
  assert.ok(JSON.stringify(b).includes('subagent-model.mjs') && !JSON.stringify(a).includes('subagent-model.mjs'));
  assert.equal(base.hooks.PreToolUse.length, 1, 'input not mutated');
  assert.throws(() => applyOverlaySettings(b, 'B'));
});

test('child env drops session vars, pins git, arm decides the guard variable', () => {
  const parent = { Path: 'C:\\a;C:\\gh;C:\\b', CLAUDECODE: '1', CLAUDE_CODE_SSE_PORT: '5', ANTHROPIC_MODEL: 'x', ANTHROPIC_DEFAULT_OPUS_MODEL: 'y', SUBAGENT_MODEL_GUARD: 'off', ANTHROPIC_API_KEY: 'k', GH_TOKEN: 't' };
  const hasGh = dir => dir === 'C:\\gh';
  const a = buildChildEnv(parent, { arm: 'A', emptyGitConfig: 'E', hasGh });
  const b = buildChildEnv(parent, { arm: 'B', emptyGitConfig: 'E', hasGh });
  assert.equal(a.SUBAGENT_MODEL_GUARD, 'off');
  assert.ok(!('SUBAGENT_MODEL_GUARD' in b));
  for (const e of [a, b]) {
    assert.equal(e.Path, ['C:\\a', 'C:\\b'].join(path.delimiter));
    assert.equal(e.CLAUDE_CODE_DISABLE_BACKGROUND_TASKS, '1');
    assert.equal(e.GIT_CONFIG_GLOBAL, 'E');
    assert.equal(e.GIT_CONFIG_NOSYSTEM, '1');
    assert.equal(e.ANTHROPIC_API_KEY, 'k');
    assert.ok(!('CLAUDECODE' in e) && !('CLAUDE_CODE_SSE_PORT' in e) && !('ANTHROPIC_MODEL' in e) && !('ANTHROPIC_DEFAULT_OPUS_MODEL' in e) && !('GH_TOKEN' in e));
  }
  assert.equal(filterPath(['a', 'b'].join(path.delimiter), d => d === 'a'), 'b');
});

test('claude argv carries the pinned flags and no prompt', () => {
  const args = buildClaudeArgs({ settingsPath: 'S', mcpPath: 'M', cap: 40 });
  assert.deepEqual(args.slice(0, 2), ['-p', '--model']);
  assert.ok(args.includes('--no-session-persistence') && args.includes('--strict-mcp-config'));
  assert.equal(args[args.indexOf('--max-budget-usd') + 1], '40');
  assert.equal(args[args.indexOf('--setting-sources') + 1], 'project');
});

test('diff routing separates overlay, todo and docs from code', () => {
  const diff = ['diff --git a/src/a.ts b/src/a.ts', '+x', 'diff --git a/.claude/settings.json b/.claude/settings.json', '+y', 'diff --git a/todo/P.md b/todo/P.md', '+z', 'diff --git a/README.md b/README.md', '+w'].join('\n');
  assert.equal(splitSections(diff).length, 4);
  const r = routeDiff(diff);
  assert.deepEqual(r.candidateFiles, ['src/a.ts']);
  assert.equal(r.excludedFiles.length, 3);
});

test('junit fixture from a real node --test run parses', () => {
  const p = parseJunit(fx('junit-sample.xml'));
  assert.deepEqual([p.tests, p.passed, p.failed, p.skipped], [3, 1, 1, 1]);
  assert.deepEqual(p.cases.map(c => c.status), ['pass', 'fail', 'skipped']);
  assert.equal(verdictOf(p, 1), 'fail');
  assert.equal(verdictOf({ tests: 2, failed: 0 }, 0), 'pass');
  assert.equal(verdictOf(parseJunit(''), 1), 'no-report');
});

test('summary over the synthetic stream and audit log', () => {
  const cellJson = { cell: 'A1', arm: 'A', overlay: { '.claude/rules/shared/common/subagent-models.md': 'abc' } };
  const s = summarize({ events: parseNdjson(fx('stream.ndjson')), audit: parseNdjson(fx('audit.ndjson')), cellJson, treeDir: 'D:/runs/A1/tree', canaryTokens: ['CANARY-zzz'], texts: { stream: 'x CANARY-zzz y' } });
  assert.equal(s.result.total_cost_usd, 1.5);
  assert.equal(s.agentCalls.length, 3);
  assert.deepEqual(s.agentCalls.map(c => c.denied), [null, 'model-guard', 'effort-pin']);
  assert.deepEqual(s.agentCalls[0].resolvedModels, ['claude-sonnet-x']);
  assert.deepEqual(s.childrenPerModel, { 'claude-sonnet-x': 1 });
  assert.deepEqual(s.denials, { modelGuard: 1, effortPin: 1, other: 0 });
  assert.deepEqual(s.effortNotHigh, [{ agent: 'ag1', levels: ['high', 'medium'] }]);
  assert.ok(s.instructions.ruleLoaded && s.instructions.shaMatchesOverlay);
  assert.deepEqual([s.edits.main.approxLines, s.edits.children.approxLines], [4, 10]);
  assert.equal(s.leakSuspects.length, 1);
  assert.match(s.leakSuspects[0].reasons[0], /outside/);
  assert.equal(s.canary.count, 1);
});

test('summary leak check flags gh in the stream when no audit exists', () => {
  const s = summarize({ events: parseNdjson(fx('stream.ndjson')), audit: [], cellJson: {}, treeDir: '' });
  assert.equal(s.leakSuspects.length, 1);
  assert.match(s.leakSuspects[0].reasons.join(), /network/);
  assert.equal(s.mainSessionEffortRecorded, false);
});

test('audit record summarises inputs and hashes instruction files', () => {
  const r = auditRecord({ hook_event_name: 'PreToolUse', tool_name: 'Write', agent_id: 'a', effort: { level: 'high' }, tool_input: { file_path: 'f', content: 'a\nb' } }, 'T', () => 'h');
  assert.deepEqual(r.input, { file_path: 'f', added: 2, removed: 0 });
  const l = auditRecord({ hook_event_name: 'InstructionsLoaded', file_path: 'p', load_reason: 'session_start' }, 'T', () => 'h');
  assert.equal(l.sha256, 'h');
  assert.equal(auditRecord({ hook_event_name: 'PreToolUse', tool_name: 'Agent', tool_input: { model: 'sonnet', effort: 'high' } }, 'T', () => '').input.model, 'sonnet');
});

test('effort pin denies non-high only', () => {
  assert.equal(effortPinOutput({ tool_input: {} }), '');
  assert.equal(effortPinOutput({ tool_input: { effort: 'high' } }), '');
  assert.equal(effortPinOutput({ tool_input: { effort: null } }), '');
  const out = JSON.parse(effortPinOutput({ tool_input: { effort: 'xhigh' } }));
  assert.equal(out.hookSpecificOutput.permissionDecision, 'deny');
  assert.ok(out.hookSpecificOutput.permissionDecisionReason.startsWith('effort-pin:'));
});

test('hooks run as processes: audit appends under its own .git, pin denies', () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-hook-'));
  try {
    fs.mkdirSync(path.join(tmp, '.git'));
    fs.mkdirSync(path.join(tmp, '.claude', 'hooks'), { recursive: true });
    for (const n of ['audit.mjs', 'effort.mjs']) fs.copyFileSync(path.join(here, '..', 'hooks', n), path.join(tmp, '.claude', 'hooks', n));
    const input = JSON.stringify({ hook_event_name: 'PreToolUse', tool_name: 'Bash', tool_input: { command: 'ls' }, effort: { level: 'high' } });
    const a = spawnSync(process.execPath, [path.join(tmp, '.claude', 'hooks', 'audit.mjs')], { input, cwd: os.tmpdir() });
    assert.equal(a.status, 0);
    assert.equal(a.stdout.length, 0);
    const line = JSON.parse(fs.readFileSync(path.join(tmp, '.git', 'audit.ndjson'), 'utf8').trim());
    assert.equal(line.input.command, 'ls');
    const p = spawnSync(process.execPath, [path.join(tmp, '.claude', 'hooks', 'effort.mjs')], { input: JSON.stringify({ tool_input: { effort: 'low' } }) });
    assert.match(p.stdout.toString(), /"permissionDecision":"deny"/);
    const bad = spawnSync(process.execPath, [path.join(tmp, '.claude', 'hooks', 'audit.mjs')], { input: 'not json' });
    assert.equal(bad.status, 0);
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
});

test('canaries: manifest first, skip missing folders, unplant removes exactly the manifest', () => {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-home-'));
  try {
    const dir = path.join(home, '.claude', 'projects', 'D--rsd-dew-flow-creds-for-devs');
    fs.mkdirSync(dir, { recursive: true });
    const bystander = path.join(dir, 'keep.md');
    fs.writeFileSync(bystander, 'keep');
    const manifest = path.join(home, 'canaries.json');
    const rows = plant(manifest, home);
    assert.deepEqual(rows.map(r => r.status), ['planted', 'skipped-no-folder', 'skipped-no-folder']);
    assert.equal(JSON.parse(fs.readFileSync(manifest, 'utf8')).planted.length, 3);
    const res = unplant(manifest, home);
    assert.deepEqual(res.map(r => r.status), ['removed', 'absent', 'absent']);
    assert.ok(fs.existsSync(bystander));
    assert.equal(isDeletable(bystander, home), false);
    assert.equal(isDeletable(path.join(home, 'elsewhere', 'policy-ab-canary-1.md'), home), false);
    assert.equal(canaryTargets(home).length, 3);
  } finally {
    fs.rmSync(home, { recursive: true, force: true });
  }
});

test('holdout, commands, gitlink, scrub helpers', () => {
  const entries = holdoutEntries({ entries: [{ kind: 'test', path: 'src_vs_code/src/test/a.test.ts' }, { kind: 'doc', path: 'x.md' }, { file: 'src/test/b.test.ts' }] });
  assert.equal(entries.length, 2);
  assert.equal(compiledTestPath('src_vs_code/src/test/a.test.ts'), 'out/test/a.test.js');
  assert.equal(repoPathOf('src/test/b.test.ts'), 'src_vs_code/src/test/b.test.ts');
  assert.equal(fileStem(entryPath(entries[0])), 'a.test');
  assert.deepEqual(resolveCompile({ compile: ['node', 'x.js'] }), { exe: process.execPath, args: ['x.js'] });
  assert.ok(toInvocation(['npm', 'run', 'compile']).args.includes('compile'));
  assert.equal(parseGitlink('160000 commit 8355757833a1273c02b1aeaa6794ddddb6669056\t.claude/rules/shared\n'), '8355757833a1273c02b1aeaa6794ddddb6669056');
  assert.throws(() => parseGitlink('100644 blob abc\tx'));
  assert.deepEqual(scrub({ a: ['D:/r/x'] }, [['D:/r', '<r>']]), { a: ['<r>/x'] });
});
