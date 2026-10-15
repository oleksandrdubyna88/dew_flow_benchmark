import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { auditRecord } from '../hooks/audit.mjs';
import { hookFilesOf, syncHooksKey, withoutUserGuard } from '../lib/adapter.mjs';
import { canaryTargets } from '../lib/canaries.mjs';
import { ownSuiteTargets } from '../lib/commands.mjs';
import { routeDiff } from '../lib/diffsplit.mjs';
import { buildClaudeArgs } from '../lib/env.mjs';
import { blockLead, drawBlocks, drawOrder } from '../lib/order.mjs';
import { ownSuiteVsReference } from '../lib/ownsuite.mjs';
import { agentTimeline, editsByAgent, inseparableGroups, launchesPerMessage, looksLikeShellWrite, parallelism, sumEdits, sweepSpans } from '../lib/parallel.mjs';
import { composePrompt, PARALLEL_SENTENCE } from '../lib/prompt.mjs';
import { firstTurnContext, ruleReadSignal, sessionStartSignal } from '../lib/resolver.mjs';
import { machineScrubber } from '../lib/scrub.mjs';
import { applyOverlaySettings, existingHooksPreserved, overlayEntries } from '../lib/settings.mjs';
import { costOfSummary, isShedPath, shedTargets, spendDecision } from '../lib/spend.mjs';
import { DEFAULT_STORY, loadStory, parseStory } from '../lib/story.mjs';
import { parseNdjson, summarize } from '../lib/summary.mjs';
import { shedCell } from '../lib/shed.mjs';
import { tamperFindings } from '../lib/scan.mjs';
import { gitlinkFromStage, resolverInstallArgs } from '../lib/tree.mjs';
import { renderGenerated } from '../lib/prepare-generated.mjs';
import { planCells, spentInRoot } from '../pilot.mjs';
import { trimFor } from '../prepare-cell.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const fx = name => fs.readFileSync(path.join(here, 'fixtures', name), 'utf8');

// ---- story config -------------------------------------------------------------------------------------------------

test('both story files load, resolve their paths, and keep the creds story on the native layout', () => {
  const creds = loadStory();
  assert.equal(creds.id, DEFAULT_STORY);
  assert.equal(creds.layout, 'native');
  assert.equal(creds.mount, '.claude/rules/shared');
  assert.equal(path.basename(creds.runMapFile), 'run-map.json');
  assert.equal(path.basename(creds.promptFile), 'prompt.md');
  assert.equal(creds.parallelHint, false);
  const coai = loadStory('coai-751');
  assert.deepEqual([coai.P, coai.S, coai.layout, coai.mount, coai.planFile, coai.srcDir], ['af7f2856ecfaa732159db7dacda3255dfbde2334', '3ebf208c55c591103140cd2f3148c4869e14ff7b', 'resolver', '.agents/conventions', 'todo/PLAN_paths_per_side.md', 'src_vs_code']);
  assert.equal(coai.repoClone, 'D:/rsd/dew_flow_connect_other_ais');
  assert.match(coai.inputsDir.replace(/\\/g, '/'), /stories\/coai-751\/inputs$/);
  assert.equal(path.basename(coai.runMapFile), 'run-map.coai-751.json');
  assert.equal(coai.parallelHint, true);
});

test('a story with a gap, a bad layout, a bad sha or an unsafe id is refused', () => {
  const ok = { repoClone: 'x', P: 'a'.repeat(40), S: 'b'.repeat(40), mount: 'm', layout: 'native', planFile: 'p', inputs: 'i', srcDir: 's', struckFrom: 'statement.md', defaultRunsRoot: 'r', prompt: 'prompt.md', canarySlug: 'slug' };
  assert.equal(parseStory('t', ok, 'D:/spike').id, 't');
  assert.throws(() => parseStory('t', { ...ok, srcDir: undefined }, 'D:/spike'), /missing srcDir/);
  assert.throws(() => parseStory('t', { ...ok, layout: 'weird' }, 'D:/spike'), /layout/);
  assert.throws(() => parseStory('t', { ...ok, P: 'abc' }, 'D:/spike'), /40-hex/);
  assert.throws(() => loadStory('../x'), /bad story id/);
  assert.throws(() => loadStory('no-such-story'), /no story/);
});

// ---- the prompt -----------------------------------------------------------------------------------------------------

test('the parallel sentence is one paragraph after the autonomy paragraph, identical for any arm, and absent when the story opts out', () => {
  const base = 'Implement X.\n\nWork autonomously. Decide.\n\nBuild it.\n';
  const out = composePrompt(base, true);
  assert.deepEqual(out.split('\n\n'), ['Implement X.', 'Work autonomously. Decide.', PARALLEL_SENTENCE, 'Build it.\n']);
  assert.equal(composePrompt(base, false), base);
  assert.equal(composePrompt('Only.\n', true), `Only.\n\n${PARALLEL_SENTENCE}\n`);
  assert.throws(() => composePrompt(out, true), /already carries/);
  const real = fs.readFileSync(loadStory('coai-751').promptFile, 'utf8').replace(/\r\n/g, '\n');
  assert.equal(composePrompt(real, true).split(PARALLEL_SENTENCE).length, 2);
});

// ---- blocks ---------------------------------------------------------------------------------------------------------

test('blocks: n adjacent A/B pairs named c1..c2n, deterministic, led by both arms across seeds', () => {
  const plan = drawBlocks('seed-x', 3);
  assert.deepEqual(plan.map(p => p.cell), ['c1', 'c2', 'c3', 'c4', 'c5', 'c6']);
  assert.deepEqual(plan.map(p => p.block), [1, 1, 2, 2, 3, 3]);
  for (const b of [1, 2, 3]) assert.deepEqual(plan.filter(p => p.block === b).map(p => p.arm).sort(), ['A', 'B']);
  assert.deepEqual(plan, drawBlocks('seed-x', 3));
  const leads = new Set(Array.from({ length: 40 }, (_, i) => blockLead(`s${i}`, 1)));
  assert.deepEqual([...leads].sort(), ['A', 'B']);
  assert.equal(blockLead('s', 1) === blockLead('s', 2) && blockLead('s', 2) === blockLead('s', 3) && blockLead('s', 3) === blockLead('s', 4) && blockLead('s', 4) === blockLead('s', 5), false);
  assert.throws(() => drawBlocks('s', 0), /blocks must be/);
  assert.throws(() => drawBlocks('s', 5), /blocks must be/);
});

test('the lead of a block is the parity of sha256(seed:block) - a fixed vector', () => {
  const lead = blockLead('policy-series-1', 1);
  assert.ok(['A', 'B'].includes(lead));
  assert.equal(drawBlocks('policy-series-1', 2)[0].arm, lead);
  assert.equal(drawBlocks('policy-series-1', 2)[2].arm, blockLead('policy-series-1', 2));
});

test('the pilot keeps the 3-cell mode without --blocks and uses blocks with it', () => {
  assert.deepEqual(planCells('some seed'), drawOrder('some seed').map((arm, i) => ({ cell: `c${i + 1}`, arm })));
  assert.equal(planCells('some seed', 3).length, 6);
});

// ---- spend and disk ---------------------------------------------------------------------------------------------------

test('spend stop: strictly above stop-after stops, with a report line naming both numbers', () => {
  assert.equal(spendDecision(100, 100).stop, false);
  const d = spendDecision(100.01, 100);
  assert.equal(d.stop, true);
  assert.match(d.line, /STOP: spent \$100\.01.*--stop-after \$100/);
  assert.equal(costOfSummary({ usd: 4.5 }), 4.5);
  assert.equal(costOfSummary({ result: { total_cost_usd: 2 } }), 2);
  assert.equal(costOfSummary(null), 0);
});

test('spentInRoot sums every cell summary it can read', () => {
  const read = file => (file.includes('c1') ? { usd: 3 } : file.includes('c4') ? { usd: 4.25 } : null);
  assert.equal(spentInRoot('D:/r', read), 7.25);
});

test('shedding names exact paths under the runs root and refuses anything else', () => {
  const root = path.resolve('D:/runs');
  const targets = shedTargets(root, 'c2', { srcDir: 'src_vs_code', mount: '.agents/conventions' });
  assert.deepEqual(targets.map(t => path.relative(root, t).replace(/\\/g, '/')), ['c2/score', 'c2/tree/node_modules', 'c2/tree/src_vs_code/node_modules', 'c2/tree/.agents/conventions/node_modules']);
  assert.ok(targets.every(t => isShedPath(t, root)));
  for (const bad of [root, path.join(root, 'c2'), path.join(root, 'c2', 'tree'), path.join(root, 'c2', 'summary.json'), path.resolve('D:/other/c2/score'), path.join(root, '..', 'score'), path.join(root, 'score')]) assert.equal(isShedPath(bad, root), false, bad);
});

test('shedCell removes only the targets that exist, through the injected remover, and keeps the records', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-shed-'));
  try {
    const cell = path.join(root, 'c1');
    fs.mkdirSync(path.join(cell, 'score', '_own'), { recursive: true });
    fs.mkdirSync(path.join(cell, 'tree', 'src_vs_code', 'node_modules', 'x'), { recursive: true });
    fs.writeFileSync(path.join(cell, 'score.json'), '{}');
    fs.writeFileSync(path.join(cell, 'summary.json'), '{}');
    const { removed, refused } = shedCell(root, 'c1', { srcDir: 'src_vs_code' });
    assert.equal(removed.length, 2);
    assert.deepEqual(refused, []);
    assert.ok(!fs.existsSync(path.join(cell, 'score')) && !fs.existsSync(path.join(cell, 'tree', 'src_vs_code', 'node_modules')));
    assert.ok(fs.existsSync(path.join(cell, 'score.json')) && fs.existsSync(path.join(cell, 'summary.json')) && fs.existsSync(path.join(cell, 'tree')));
    assert.deepEqual(shedCell(root, 'c1', { srcDir: 'src_vs_code' }).removed, []);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

// ---- parallel children, from a fixture -----------------------------------------------------------------------------------

const events = () => parseNdjson(fx('parallel.stream.ndjson'));
const audit = () => parseNdjson(fx('parallel.audit.ndjson'));

test('launches per assistant message: three children in one message, one in the next', () => {
  const tl = agentTimeline(events());
  assert.equal(tl.length, 4);
  const l = launchesPerMessage(tl);
  assert.deepEqual([l.maxPerMessage, l.messagesWithLaunches, l.perMessage], [3, 2, [3, 1]]);
  assert.deepEqual(tl.map(t => t.agentId), ['agA', 'agB', 'agC', 'agD']);
});

test('spans: concurrency, overlap and the union - never a sum of child time', () => {
  const p = parallelism(events(), { wallMs: 330000 });
  assert.equal(p.maxConcurrent, 3);
  assert.equal(p.overlapMs, 119900);
  assert.equal(p.childUnionMs, 210000);
  assert.equal(p.wallMs, 330000);
  assert.ok(!('childSumMs' in p));
  assert.deepEqual(sweepSpans([[0, 10], [10, 20]]), { maxConcurrent: 1, overlapMs: 0, unionMs: 20 });
  assert.deepEqual(sweepSpans([[0, 10], [5, 15]]), { maxConcurrent: 2, overlapMs: 5, unionMs: 15 });
});

test('per-child cost is inseparable exactly when two children share a model in overlapping time', () => {
  const tl = agentTimeline(events());
  const groups = inseparableGroups(tl);
  assert.deepEqual([...groups.keys()].sort(), ['ta', 'tb']);
  const p = parallelism(events());
  const byId = Object.fromEntries(p.perChild.map(c => [c.id, c]));
  assert.equal(byId.ta.cost.inseparable, true);
  assert.equal(byId.ta.cost.usd, null);
  assert.deepEqual(byId.ta.cost.group, ['ta', 'tb']);
  assert.ok(byId.ta.cost.groupUsdEstimate > 0);
  assert.equal(byId.tc.cost.inseparable, false);
  assert.ok(byId.tc.cost.usd > 0);
  assert.equal(byId.td.cost.inseparable, false);
});

test('a child on the same model in a LATER, non-overlapping turn is separable; an unfinished child has no span', () => {
  const ev = [
    { type: 'assistant', timestamp: '2026-10-15T10:00:00Z', message: { id: 'a', content: [{ type: 'tool_use', id: 'x', name: 'Agent', input: { model: 'sonnet' } }] } },
    { type: 'user', timestamp: '2026-10-15T10:01:00Z', tool_use_result: { agentId: 'g1', resolvedModel: 'claude-sonnet-x' }, message: { content: [{ type: 'tool_result', tool_use_id: 'x', content: 'ok' }] } },
    { type: 'assistant', timestamp: '2026-10-15T10:02:00Z', message: { id: 'b', content: [{ type: 'tool_use', id: 'y', name: 'Agent', input: { model: 'sonnet' } }] } },
  ];
  assert.equal(inseparableGroups(agentTimeline(ev)).size, 0);
  const p = parallelism(ev);
  assert.equal(p.timed, false);
  assert.equal(p.maxConcurrent, null);
  assert.equal(p.perChild[1].spanMs, null);
});

test('child edits are counted per agent: edit tools (with lines) and shell writes (calls only)', () => {
  const by = editsByAgent(audit(), agentTimeline(events()));
  assert.deepEqual(by.main.editTools, { calls: 1, files: ['main.ts'], approxLines: 4 });
  assert.deepEqual(by.agA.editTools, { calls: 2, files: ['a.ts', 'a2.ts'], approxLines: 12 });
  assert.equal(by.agA.call, 'ta');
  assert.equal(by.agA.description, 'coder A');
  assert.equal(by.agB.editTools.calls, 0);
  assert.equal(by.agB.shellWriteCalls, 1);
  assert.equal(by.agB.toolCalls, 2);
  assert.equal(by.agC.shellWriteCalls, 0);
  const sums = sumEdits(by);
  assert.deepEqual([sums.main.approxLines, sums.children.calls, sums.children.approxLines, sums.children.shellWriteCalls], [4, 2, 12, 1]);
});

test('the summary carries per-agent child edits, the parallel record and the whole-cell wall from the envelope', () => {
  const s = summarize({ events: events(), audit: audit(), cellJson: {}, treeDir: 'D:/runs/c1/tree', run: { wallMs: 331000 } });
  assert.equal(s.edits.children.calls, 2, 'the quick pilot read 0 here');
  assert.equal(s.edits.children.approxLines, 12);
  assert.equal(s.edits.main.approxLines, 4);
  assert.equal(s.edits.byAgent.agB.shellWriteCalls, 1);
  assert.equal(s.parallel.launch.maxPerMessage, 3);
  assert.deepEqual(s.wall, { cellMs: 330000, launcherMs: 331000 });
  assert.equal(s.parallel.wallMs, 330000);
  assert.equal(s.usd, 3.25);
  assert.equal(s.resolver, undefined);
});

test('shell-write detection: in-place edits, redirects and writers count; reads and stderr merges do not', () => {
  for (const c of ['sed -i s/a/b/ f', 'echo x > f.ts', 'cat <<EOF >> f.ts', 'python -c "open(\'f\',\'w\').write(1)"', 'Set-Content f x', 'git apply p.diff', 'rm -f f', 'node -e "fs.writeFileSync(1)"', 'x | tee out.txt']) assert.equal(looksLikeShellWrite(c), true, c);
  for (const c of ['node --test out/x.js 2>&1 | tail', 'ls -la', 'grep -n x f', 'node -e "a=>b"', 'cmd 2>/dev/null', 'git status']) assert.equal(looksLikeShellWrite(c), false, c);
});

test('the audit hook summarises MultiEdit and NotebookEdit lines too', () => {
  const m = auditRecord({ hook_event_name: 'PreToolUse', tool_name: 'MultiEdit', tool_input: { file_path: 'f', edits: [{ old_string: 'a', new_string: 'x\ny' }, { old_string: 'b\nc', new_string: 'z' }] } }, 'T', () => '');
  assert.deepEqual(m.input, { file_path: 'f', added: 3, removed: 3 });
  assert.deepEqual(auditRecord({ hook_event_name: 'PreToolUse', tool_name: 'NotebookEdit', tool_input: { notebook_path: 'n', new_source: 'a\nb' } }, 'T', () => '').input, { file_path: 'n', added: 2, removed: 0 });
});

// ---- the resolver layout --------------------------------------------------------------------------------------------

test('the first main message gives the context the arm cost at session start', () => {
  const c = firstTurnContext(events());
  assert.deepEqual([c.cacheCreation, c.cacheRead, c.input, c.total], [40000, 1000, 2, 41002]);
  assert.equal(firstTurnContext([]).present, false);
  assert.equal(firstTurnContext([{ type: 'assistant', parent_tool_use_id: 'c', message: { usage: { input_tokens: 1 } } }]).present, false);
});

test('rule reached: the SessionStart hook output and a Read of the rule file are two separate signals', () => {
  const ev = [
    { type: 'system', subtype: 'hook_started', hook_event: 'SessionStart', hook_name: 'SessionStart:startup' },
    { type: 'system', subtype: 'hook_response', hook_event: 'SessionStart', hook_name: 'SessionStart:startup', output: 'rules: common.subagent-models\n', exit_code: 0 },
  ];
  const s = sessionStartSignal(ev);
  assert.deepEqual([s.hookEventsPresent, s.mentionsRule, s.incomplete, s.responses], [true, true, false, 1]);
  assert.equal(sessionStartSignal([]).hookEventsPresent, false);
  assert.equal(sessionStartSignal([{ type: 'system', subtype: 'hook_response', hook_name: 'SessionStart:startup', output: 'Instruction loading is INCOMPLETE — x' }]).incomplete, true);
  assert.equal(sessionStartSignal([{ type: 'system', subtype: 'hook_response', hook_name: 'PreToolUse:Bash', output: 'subagent-models' }]).mentionsRule, false);
  const calls = [
    { agent: 'main', tool: 'Read', input: { file_path: 'D:\\runs\\c1\\tree\\.agents\\conventions\\common\\subagent-models.md' } },
    { agent: 'ag1', tool: 'Bash', input: { command: 'node .agents/conventions/tools/rules.mjs explain --repo . --task implement' } },
    { agent: 'ag1', tool: 'Bash', input: { command: 'cat .agents/conventions/common/subagent-models.md' } },
  ];
  const r = ruleReadSignal(calls, '.agents/conventions');
  assert.deepEqual([r.readOfRule, r.readAgents, r.shellMentionsOfRule, r.resolverRuns], [true, ['main'], 1, 1]);
  assert.equal(ruleReadSignal([], '.agents/conventions').readOfRule, false);
});

test('a resolver-layout summary adds the resolver record with both signals and the loaded context', () => {
  const story = { layout: 'resolver', mount: '.agents/conventions' };
  const s = summarize({ events: events(), audit: audit(), cellJson: { overlay: {} }, treeDir: 'D:/runs/c1/tree', story });
  assert.equal(s.resolver.mount, '.agents/conventions');
  assert.equal(s.resolver.context.cacheCreation, 40000);
  assert.equal(s.resolver.ruleReached, false);
  assert.equal(s.resolver.sessionStart.hookEventsPresent, false);
});

test('the resolver tree is not trimmed, the native one is', () => {
  assert.equal(trimFor('D:/never-touched', { layout: 'resolver', mount: 'm' }).skipped, true);
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-trim-'));
  try {
    fs.mkdirSync(path.join(dir, 'm', 'common'), { recursive: true });
    fs.mkdirSync(path.join(dir, 'm', 'research'), { recursive: true });
    fs.writeFileSync(path.join(dir, 'm', 'common', 'subagent-models.md'), 'x');
    fs.writeFileSync(path.join(dir, 'm', 'research', 'r.md'), 'y');
    assert.deepEqual(trimFor(dir, { layout: 'native', mount: 'm' }), { files: 1, bytes: 1 });
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('adapter sync: reference hooks key replaces the project one, other keys stay, the user-guard installer is removed everywhere', () => {
  assert.deepEqual(hookFilesOf(['subagent-model.mjs', 'lib\\user-guard-install.mjs', 'x.test.mjs', 'README.md']), ['lib/user-guard-install.mjs', 'subagent-model.mjs']);
  const reference = { permissions: { allow: ['x'] }, hooks: { SessionStart: [{ hooks: [{ type: 'command', command: 'node', args: ['${CLAUDE_PROJECT_DIR}/.claude/hooks/load-instructions.mjs'] }, { type: 'command', command: 'node', args: ['${CLAUDE_PROJECT_DIR}/.claude/hooks/ensure-user-guard.mjs'], timeout: 5 }] }], PreToolUse: [{ matcher: 'Agent|Task', hooks: [{ type: 'command', command: 'node', args: ['${CLAUDE_PROJECT_DIR}/.claude/hooks/subagent-model.mjs'] }] }] } };
  const project = { permissions: { defaultMode: 'acceptEdits' }, hooks: { PreToolUse: [{ matcher: 'Bash', hooks: [] }] } };
  const synced = syncHooksKey(project, reference);
  assert.deepEqual(synced.permissions, project.permissions);
  assert.deepEqual(synced.hooks, reference.hooks);
  assert.notEqual(synced.hooks, reference.hooks, 'a copy, not the reference object');
  const { settings, removed } = withoutUserGuard(synced);
  assert.equal(removed, true);
  assert.deepEqual(settings.hooks.SessionStart[0].hooks.map(h => h.args[0].split('/').pop()), ['load-instructions.mjs']);
  assert.equal(settings.hooks.PreToolUse.length, 1);
  assert.equal(withoutUserGuard(settings).removed, false);
  assert.equal(withoutUserGuard({ hooks: { SessionStart: [{ hooks: [{ command: 'node', args: ['ensure-user-guard.mjs'] }] }] } }).settings.hooks.SessionStart.length, 0);
});

test('the guard wired by the arm\'s own release is not double-wired; ours land beside it and the adapter entries stay first', () => {
  const adapter = { hooks: { PreToolUse: [{ matcher: 'Bash|PowerShell', hooks: [{ type: 'command', command: 'node', args: ['b.mjs'] }] }, { matcher: 'Agent|Task', hooks: [{ type: 'command', command: 'node', args: ['${CLAUDE_PROJECT_DIR}/.claude/hooks/subagent-model.mjs'] }] }] } };
  assert.throws(() => applyOverlaySettings(adapter, 'B'), /already wire/);
  const b = applyOverlaySettings(adapter, 'B', { guardWired: true });
  assert.deepEqual(b.hooks.PreToolUse.map(e => e.matcher), ['Bash|PowerShell', 'Agent|Task', '*', 'Edit|Write|MultiEdit|NotebookEdit']);
  assert.equal(existingHooksPreserved(adapter, b), true);
  const a = applyOverlaySettings({ hooks: { PreToolUse: adapter.hooks.PreToolUse.slice(0, 1) } }, 'A', { guardWired: true });
  assert.deepEqual(a.hooks.PreToolUse.map(e => e.matcher), ['Bash|PowerShell', '*']);
  assert.equal(existingHooksPreserved(adapter, { hooks: { PreToolUse: [adapter.hooks.PreToolUse[1]] } }), false);
  assert.equal(overlayEntries('B').PreToolUse.length, 3);
  assert.equal(overlayEntries('B', { guardWired: true }).PreToolUse.length, 2);
});

test('git helpers: the gitlink in a stage listing, the resolver install argv', () => {
  assert.equal(gitlinkFromStage(`160000 ${'a'.repeat(40)} 0\t.agents/conventions\n`), 'a'.repeat(40));
  assert.equal(gitlinkFromStage(`100644 ${'a'.repeat(40)} 0\tx\n`), '');
  assert.equal(gitlinkFromStage(`160000 ${'a'.repeat(40)} 1\t.agents/conventions\n`), '');
  assert.deepEqual(resolverInstallArgs(), ['ci', '--ignore-scripts', '--no-audit', '--no-fund']);
});

// ---- the argv, routing, scanning, scoring -------------------------------------------------------------------------

test('hook events join the argv only on request; the tools flag stays last', () => {
  const plain = buildClaudeArgs({ settingsPath: 'S', mcpPath: 'M', cap: 30 });
  const hooked = buildClaudeArgs({ settingsPath: 'S', mcpPath: 'M', cap: 30, hookEvents: true });
  assert.ok(!plain.includes('--include-hook-events'));
  assert.deepEqual(hooked.filter(a => !plain.includes(a)), ['--include-hook-events']);
  assert.equal(hooked.at(-2), '--tools');
});

test('the resolver layout excludes its mount from the candidate diff; the native one does not', () => {
  const diff = ['diff --git a/src_vs_code/a.ts b/src_vs_code/a.ts', '+x', 'diff --git a/.agents/conventions/tools/x.mjs b/.agents/conventions/tools/x.mjs', '+y'].join('\n');
  assert.deepEqual(routeDiff(diff).candidateFiles, ['src_vs_code/a.ts', '.agents/conventions/tools/x.mjs']);
  assert.deepEqual(routeDiff(diff, ['.agents']).candidateFiles, ['src_vs_code/a.ts']);
});

test('tamper findings follow the story mount', () => {
  const t = tamperFindings(['.agents/conventions/common/subagent-models.md', '.claude/rules/shared/common/subagent-models.md'], [], '.agents/conventions');
  assert.deepEqual(t.excluded, ['.agents/conventions/common/subagent-models.md']);
  assert.deepEqual(tamperFindings(['.claude/rules/shared/common/planning-docs.md'], []).excluded, ['.claude/rules/shared/common/planning-docs.md']);
});

test('canary targets follow the story slug; the scrubber labels the story clone', () => {
  const t = canaryTargets('C:/home', () => 'r', () => 't', 'D--rsd-dew-flow-connect-other-ais');
  assert.ok(t[0].dir.replace(/\\/g, '/').endsWith('projects/D--rsd-dew-flow-connect-other-ais'));
  assert.ok(canaryTargets('C:/home')[0].dir.endsWith('D--rsd-dew-flow-creds-for-devs'));
  const s = machineScrubber({ runsRoot: 'D:/rsd/_s2', creds: 'D:/rsd/dew_flow_connect_other_ais', label: '<repo>' });
  assert.equal(s('see D:/rsd/dew_flow_connect_other_ais/x and D:/rsd/_s2/c1'), 'see <repo>/x and <runs>/c1');
});

test('own-suite targets come from testAll when it is a node --test; the reference failing set is the baseline', () => {
  assert.deepEqual(ownSuiteTargets({ commands: { testAll: { exe: 'node', args: ['--test', 'out/test/*.test.js'] } } }), ['out/test/*.test.js']);
  assert.deepEqual(ownSuiteTargets({ commands: { testAll: { exe: 'node', args: ['scripts/run-tests.mjs'] } } }), ['out/test/*.test.js']);
  assert.deepEqual(ownSuiteTargets({}), ['out/test/*.test.js']);
  const ref = { tests: 100, failedCases: ['a', 'b', 'c'] };
  assert.deepEqual(ownSuiteVsReference({ tests: 100, failedCases: ['a', 'c'] }, ref), { verdict: 'pass', newFailures: [], referenceFailed: 3, referenceTests: 100, candidateTests: 100 });
  assert.deepEqual(ownSuiteVsReference({ tests: 101, failedCases: ['a', 'z'] }, ref).newFailures, ['z']);
  assert.equal(ownSuiteVsReference({ tests: 101, failedCases: ['z'] }, ref).verdict, 'fail');
  assert.equal(ownSuiteVsReference({ tests: 0 }, ref).verdict, 'no-report');
  assert.equal(ownSuiteVsReference({ tests: 5, failedCases: [] }, null).verdict, 'no-reference');
});

test('the generated modules are rendered exactly as the story\'s prepare-gate writes them', () => {
  const g = renderGenerated({ gate: 'G`x`', document: 'D', caller: 'C', consultant: 'K', feature: 'F', question: 'Q' });
  assert.deepEqual(Object.keys(g), ['gateRule.ts', 'consultantRule.ts', 'featureRule.ts', 'questionRule.ts']);
  assert.equal(g['gateRule.ts'], '// Generated from pinned conventions; do not edit.\nexport const GATE_RULE = "G`x`";\nexport const DOCUMENT_RULE = "D";\nexport const CALLER_RULE = "C";\n');
  assert.equal(g['questionRule.ts'], '// Generated from pinned conventions; do not edit.\nexport const QUESTION_RULE = "Q";\n');
});
