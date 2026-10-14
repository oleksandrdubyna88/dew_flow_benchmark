import assert from 'node:assert/strict';
import test from 'node:test';
import { buildChildEnv, buildClaudeArgs } from '../lib/env.mjs';
import { findOutsideAccesses } from '../lib/leak.mjs';
import { drawOrder } from '../lib/order.mjs';
import { costFromMessages } from '../lib/pricing.mjs';
import { scanTokens, tamperFindings } from '../lib/scan.mjs';
import { remainingModels, withholdModels } from '../lib/strike.mjs';
import { attributeDiagnostics, parseTscDiagnostics } from '../lib/tsc.mjs';
import { gitDirFor } from '../hooks/audit.mjs';
import { initInfo, summarize } from '../lib/summary.mjs';
import { selectRemovals } from '../lib/trim.mjs';

const ctx = { treeRoot: 'D:/rsd/_q1/c1/tree', home: 'C:\\Users\\me' };
const call = (tool, input, agent = 'main') => ({ agent, tool, input });
const outside = calls => findOutsideAccesses(calls, ctx);

test('withholdModels replaces whole words case-insensitively and counts', () => {
  const r = withholdModels('T1, T2 (Opus). stories on opus, security on Fable; SONNET and haiku; Opusx stays; "Opus-only".');
  assert.equal(r.count, 6);
  assert.ok(r.text.includes('Opusx'));
  assert.deepEqual(remainingModels(r.text), []);
  assert.deepEqual(remainingModels('an Opus here'), ['Opus']);
});

test('leak: file tools, git-bash paths, home forms, dot-dot, allow-list', () => {
  assert.equal(outside([call('Read', { file_path: 'D:\\rsd\\dew_flow_creds_for_devs\\x' })]).length, 1);
  assert.equal(outside([call('Glob', { pattern: '*.ts', path: '/d/rsd/other' })]).length, 1);
  assert.equal(outside([call('Read', { file_path: 'D:/rsd/_q1/c1/tree/src/a.ts' })]).length, 0);
  assert.equal(outside([call('Edit', { file_path: 'src/a.ts' })]).length, 0);
  assert.equal(outside([call('Write', { file_path: '../../c2/tree/x' })]).length, 1);
  assert.equal(outside([call('Bash', { command: 'cat /c/Users/me/.claude/x' })]).length, 1);
  assert.equal(outside([call('Bash', { command: 'cat $HOME/.claude/x' })]).length, 1);
  assert.equal(outside([call('Bash', { command: 'ls ~/.claude' })]).length, 1);
  assert.equal(outside([call('PowerShell', { command: 'gc $env:USERPROFILE\\.claude\\a' })]).length, 1);
  assert.equal(outside([call('PowerShell', { command: 'gc %USERPROFILE%\\a' })]).length, 1);
  assert.equal(outside([call('Bash', { command: 'cat ../../c2/tree/x' })]).length, 1);
  assert.equal(outside([call('Bash', { command: 'cat ../README.md' })]).length, 1);
  assert.equal(outside([call('Bash', { command: 'cd src_vs_code && node --test out/x.js' })]).length, 0);
  assert.equal(outside([call('Bash', { command: '"C:\\Program Files\\nodejs\\node.exe" a.js' })]).length, 0);
  assert.equal(outside([call('Bash', { command: 'ls D:\\rsd\\_q1\\c1\\tree\\src' })]).length, 0);
  assert.equal(outside([call('NotebookEdit', { file_path: 'D:\\x\\n.ipynb' })]).length, 1);
});

test('scanTokens finds identifiers whole-word and phrases whitespace-collapsed', () => {
  const sources = [{ source: 'diff', text: 'const x = SlotSource; // a   harness over\n nothing passes' }, { source: 'diff2', text: 'MySlotSourceX' }];
  const r = scanTokens(sources, { identifiers: ['SlotSource'], phrases: ['a harness over nothing passes'] });
  assert.equal(r.count, 2);
  assert.deepEqual(r.hits.map(h => h.token).sort(), ['SlotSource', 'a harness over nothing passes']);
  assert.equal(scanTokens(sources, { canary: ['abcd1234'] }).count, 0);
  assert.equal(scanTokens([{ source: 's', text: 'x abcd1234 y' }], { canary: ['abcd1234'] }).count, 1);
});

test('tamperFindings flags instrument files and audit-log mentions', () => {
  const t = tamperFindings(['.claude/settings.json', '.claude/hooks/audit.mjs', '.claude/rules/shared/common/subagent-models.md', 'todo/P.md', 'src/a.ts'], [call('Bash', { command: 'rm .git/audit.ndjson' })]);
  assert.deepEqual(t.excluded.sort(), ['.claude/hooks/audit.mjs', '.claude/rules/shared/common/subagent-models.md', '.claude/settings.json']);
  assert.equal(t.calls.length, 1);
});

test('tsc diagnostics are parsed and attributed to the hold-out file only', () => {
  const out = 'src/test/slotTable.test.ts(12,5): error TS2339: Property bundleKey does not exist.\nsrc/other.ts(1,1): error TS2322: x\nnoise\n';
  const d = parseTscDiagnostics(out);
  assert.equal(d.length, 2);
  assert.deepEqual(d[0], { file: 'src/test/slotTable.test.ts', line: 12, code: 'TS2339', message: 'Property bundleKey does not exist.' });
  const a = attributeDiagnostics(d, 'src/test/slotTable.test.ts', [d[1]]);
  assert.equal(a.holdout.length, 1);
  assert.equal(a.otherNew.length, 0);
  assert.equal(attributeDiagnostics(d, 'src/test/zzz.test.ts', []).otherNew.length, 2);
});

test('child env: deny families, allow back three, arm A adds guard off', () => {
  const parent = { CLAUDE_FOO: 1, CLAUDE_CODE_GIT_BASH_PATH: 'g', CLAUDE_CODE_USE_POWERSHELL_TOOL: '1', CLAUDE_CONFIG_DIR: 'c', ANTHROPIC_BASE_URL: 'u', MCP_TIMEOUT: '1', OTEL_X: '1', TRACEPARENT: 't', AI_AGENT: 'a', GITHUB_TOKEN: 'g', ANTHROPIC_API_KEY: 'k', HOME: 'h' };
  const e = buildChildEnv(parent, { arm: 'B', emptyGitConfig: 'E', hasGh: () => false });
  for (const k of ['CLAUDE_FOO', 'ANTHROPIC_BASE_URL', 'MCP_TIMEOUT', 'OTEL_X', 'TRACEPARENT', 'AI_AGENT', 'GITHUB_TOKEN']) assert.ok(!(k in e), k);
  for (const k of ['CLAUDE_CODE_GIT_BASH_PATH', 'CLAUDE_CODE_USE_POWERSHELL_TOOL', 'CLAUDE_CONFIG_DIR', 'HOME', 'ANTHROPIC_API_KEY']) assert.ok(k in e, k);
  assert.ok(!('SUBAGENT_MODEL_GUARD' in e));
  assert.equal(buildChildEnv(parent, { arm: 'A', emptyGitConfig: 'E', hasGh: () => false }).SUBAGENT_MODEL_GUARD, 'off');
  assert.ok(buildClaudeArgs({ settingsPath: 'S', mcpPath: 'M', cap: 1 }).includes('--forward-subagent-text'));
});

test('drawOrder is deterministic, holds two A and one B, and reaches every position', () => {
  assert.deepEqual(drawOrder('seed-1'), drawOrder('seed-1'));
  const seen = new Set();
  for (let i = 0; i < 60; i++) {
    const o = drawOrder(`s${i}`);
    assert.deepEqual([...o].sort(), ['A', 'A', 'B']);
    seen.add(o.indexOf('B'));
  }
  assert.equal(seen.size, 3);
});

test('gitDirFor follows a gitdir file and returns a plain .git dir', () => {
  const fsFile = { statSync: () => ({ isFile: () => true }), readFileSync: () => 'gitdir: ../real/.git/worktrees/x\n' };
  assert.match(gitDirFor('D:/t', fsFile).replace(/\\/g, '/'), /real\/\.git\/worktrees\/x$/);
  assert.match(gitDirFor('D:/t', { statSync: () => ({ isFile: () => false }) }).replace(/\\/g, '/'), /t\/\.git$/);
});

test('cost falls back to per-message usage when there is no result envelope', () => {
  const ev = [
    { type: 'assistant', message: { id: 'm1', model: 'claude-opus-x', usage: { input_tokens: 1000, output_tokens: 100 } } },
    { type: 'assistant', message: { id: 'm1', model: 'claude-opus-x', usage: { input_tokens: 1000, output_tokens: 200 } } },
    { type: 'assistant', message: { id: 'm2', model: 'claude-sonnet-x', usage: { input_tokens: 10, output_tokens: 10, cache_read_input_tokens: 5 } } },
  ];
  const c = costFromMessages(ev);
  assert.equal(c.messages, 2);
  assert.equal(c.tokensByModel['claude-opus-x'].output, 200);
  assert.ok(c.usd > 0);
  const s = summarize({ events: ev, audit: [], cellJson: {}, treeDir: '' });
  assert.equal(s.costSource, 'messages');
  assert.equal(summarize({ events: [{ type: 'result', total_cost_usd: 2 }], audit: [], cellJson: {}, treeDir: '' }).costSource, 'envelope');
});

test('system/init is captured with the cwd hashed out', () => {
  const i = initInfo([{ type: 'system', subtype: 'init', cwd: 'D:/secret', model: 'm', tools: ['Bash'], agents: ['a'], skills: ['s'], plugins: [{ name: 'p' }], mcp_servers: [] }]);
  assert.equal(i.cwd, undefined);
  assert.match(i.cwdSha256, /^[0-9a-f]{64}$/);
  assert.deepEqual([i.model, i.tools, i.agents, i.skills], ['m', ['Bash'], ['a'], ['s']]);
});

test('unobserved children = Agent calls that ran minus distinct agent ids; new network verbs flagged', () => {
  const ev = [
    { type: 'assistant', parent_tool_use_id: null, message: { model: 'o', content: [{ type: 'tool_use', id: 't1', name: 'Agent', input: { model: 'sonnet' } }, { type: 'tool_use', id: 't2', name: 'Agent', input: { model: 'sonnet' } }] } },
    { type: 'user', parent_tool_use_id: null, message: { content: [{ type: 'tool_result', tool_use_id: 't1', content: 'ok' }, { type: 'tool_result', tool_use_id: 't2', content: 'ok' }] } },
  ];
  const audit = [{ event: 'PreToolUse', agent_id: 'a1', tool_name: 'Bash', effort: { level: 'high' }, input: { command: 'npm install left-pad && npx x && git push && wget y' } }];
  const s = summarize({ events: ev, audit, cellJson: {}, treeDir: 'D:/rsd/_q1/c1/tree' });
  assert.equal(s.unobservedChildren, 1);
  assert.equal(s.leakSuspects.length, 1);
  assert.ok(s.leakSuspects[0].reasons.length >= 4);
});

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { preparePlanText, validate } from '../prepare-cell.mjs';
import { holdoutVerdict } from '../score-cell.mjs';
import { planCells } from '../pilot.mjs';
import { mergeRunMap } from '../lib/runmap.mjs';
import { assertFresh } from '../run-cell.mjs';

test('preparePlanText strikes, withholds every model name, and refuses leftovers or missed strikes', () => {
  const statement = '**Struck**\n\n1. Marker: `**Model: Opus** — x.`\n';
  const plan = 'T1, T2 (Opus)\n**Model: Opus** — x.\nstories on Opus, security on Fable\n';
  const r = preparePlanText(plan, statement);
  assert.equal(r.modelWordsWithheld, 3);
  assert.deepEqual(remainingModels(r.text), []);
  assert.throws(() => preparePlanText('nothing struck here Opus', statement), /not found/);
});

test('cell and arm names are validated', () => {
  assert.doesNotThrow(() => validate({ cell: 'c2', arm: 'B' }));
  assert.throws(() => validate({ cell: 'A1', arm: 'A' }));
  assert.throws(() => validate({ cell: 'c1', arm: 'X' }));
});

test('hold-out verdict: only diagnostics in the hold-out file (or no emit) make a compile-error', () => {
  assert.equal(holdoutVerdict({ holdoutDiagnostics: 0, emitted: true, testVerdict: 'pass' }), 'pass');
  assert.equal(holdoutVerdict({ holdoutDiagnostics: 0, emitted: true, testVerdict: 'fail' }), 'fail');
  assert.equal(holdoutVerdict({ holdoutDiagnostics: 2, emitted: true, testVerdict: 'pass' }), 'compile-error');
  assert.equal(holdoutVerdict({ holdoutDiagnostics: 0, emitted: false, testVerdict: undefined }), 'compile-error');
});

test('pilot plan uses neutral names and one B', () => {
  const plan = planCells('some seed');
  assert.deepEqual(plan.map(p => p.cell), ['c1', 'c2', 'c3']);
  assert.equal(plan.filter(p => p.arm === 'B').length, 1);
});

test('run map merges per cell and keeps the seed', () => {
  const a = mergeRunMap({ seed: 's', cells: { c1: { arm: 'A' } } }, { cells: { c1: { armSha: 'x' }, c2: { arm: 'B' } } });
  assert.deepEqual(a, { seed: 's', cells: { c1: { arm: 'A', armSha: 'x' }, c2: { arm: 'B' } } });
});

test('a cell with a stream already on disk is refused', async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-fresh-'));
  try {
    fs.writeFileSync(path.join(dir, 'stream.ndjson'), '');
    await assert.rejects(assertFresh(dir, path.join(dir, 'tree'), dir), /already has a stream/);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('mount trim keeps only rule directories', () => {
  const files = ['common/a.md', 'common/sub/b.md', 'csharp/c.md', 'typescript/d.md', 'rust/e.md', 'research/x.md', 'todo/y.md', 'README.md', 'settings/hooks/h.mjs', 'AGENTS.md', 'commonx/z.md'];
  assert.deepEqual(selectRemovals(files).sort(), ['AGENTS.md', 'README.md', 'commonx/z.md', 'research/x.md', 'settings/hooks/h.mjs', 'todo/y.md']);
});

test('agent calls report the effort as requested', () => {
  const ev = [{ type: 'assistant', parent_tool_use_id: null, message: { model: 'o', content: [{ type: 'tool_use', id: 't', name: 'Agent', input: { model: 'sonnet', effort: 'xhigh' } }] } }];
  assert.equal(summarize({ events: ev, audit: [], cellJson: {}, treeDir: '' }).agentCalls[0].effortRequested, 'xhigh');
});
