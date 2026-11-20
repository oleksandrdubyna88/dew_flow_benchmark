// F3.S1 (b), PR 5: the generators' pure parts and the checks of the pre-registration files (scenarios 60 to 64, 66 to 68, 78 and 79f of the stories document): the contract's quotes read from a commit of `origin/main`, the registered
// prompts equal to what the engine sends, the schedule file, the two rule files against the contract, the leak check over the registered files and the engine's prices. Fixtures only: nothing here is a real held-out value.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { commit as commitAll, git } from '../../minigit-bench/test/prereg-fixtures.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickPrompts } from '../lib/init-quick.mjs';
import { buildMaterial } from '../lib/leak-material.mjs';
import { REPO } from '../lib/paths.mjs';
import { PROMPT_TEMPLATE } from '../lib/plan.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { PREREG_DIR, STAGE_PILOT } from '../lib/stage-lists.mjs';
import { registeredInputs } from '../lib/stage-prereg-inputs.mjs';
import { preregLeakFindings, registeredFilesOn } from '../lib/stage-leak.mjs';
import { NEW_LIMITS_SENTENCE, OLD_LIMITS_SENTENCE, STAGE_PROMPT_TEMPLATE, registeredPromptProblems, stagePrompts } from '../lib/stage-prompts.mjs';
import { ADJUDICATION_RULE_MARKER, GO_ROW_MARKER, contractIds, ruleFileProblems } from '../lib/stage-rules.mjs';
import * as scheduleModule from '../lib/stage-schedule.mjs';
import { maskOfSlot, pilotPlan } from '../lib/stage-schedule.mjs';
import { MAX_ATTEMPTS } from '../lib/series.mjs';
import { PROFILES } from '../lib/verdict.mjs';
import { FREEZE_STACKS, FREEZE_TASKS, folderOf, freezeTasks } from '../lib/workspaces-sources.mjs';
import { MARKER, PARENT_FILE, QUOTES, buildContract, extractQuotes, main as contractMain } from '../prereg/extract-contract.mjs';
import { buildPromptsFile, main as promptsMain, statementHashes, statementProblems } from '../prereg/write-prompts.mjs';
import { buildSchedule, main as scheduleMain } from '../prereg/write-schedule.mjs';
import { paragraphsAt, rowAt } from '../prereg/quote-blocks.mjs';

const REAL_PARENT = readFileSync(path.join(REPO, PARENT_FILE), 'utf8');
const EXPLORE = loadExplore().value;
const PROFILE = quickExplore(EXPLORE, '/home/example');
const HASHES = statementHashes(readFileSync(path.join(REPO, 'spikes/lang-bench/inputs/hashes-lang.txt'), 'utf8'));
const RECORD = { node: { packages: [{ name: 'fastify', version: '5.6.2' }] } };
const run = async (main, argv, ...rest) => { const lines = []; const code = await main(argv, line => lines.push(line), ...rest); return { code, lines }; };

/** A parent plan in a temporary repository with an origin, one block for every quote of the contract, each with its own words. -> `{ repo, commit, text }`. */
function parentFixture({ without = null, push = true } = {}) {
  const lines = QUOTES.filter(q => q.start !== without).flatMap(q => {
    const text = q.mode === 'paragraphs' ? Array.from({ length: q.count }, (_, i) => `${i === 0 ? q.start : 'paragraph'} ${q.name} part ${i + 1}`).join('\n\n') : `${q.start} the words of ${q.name}`;
    return [text, ''];
  });
  const remote = tmpDir('parent-remote');
  git(remote, 'init', '-q', '--bare');
  const repo = tmpDir('parent-repo');
  git(repo, 'init', '-q', '-b', 'main');
  git(repo, 'config', 'user.name', 't');
  git(repo, 'config', 'user.email', 't@invalid');
  git(repo, 'remote', 'add', 'origin', remote);
  mkdirSync(path.join(repo, 'todo'));
  writeFileSync(path.join(repo, PARENT_FILE), `${lines.join('\n')}\n`);
  const commit = commitAll(repo, 'the parent');
  if (push) git(repo, 'push', '-q', 'origin', 'main');
  git(repo, 'fetch', '-q', 'origin');
  return { repo, commit, text: readFileSync(path.join(repo, PARENT_FILE), 'utf8') };
}

const blocksOf = text => [...text.matchAll(/<!-- quote: (.+?) \(parent .+? -->\n([\s\S]*?)\n<!-- \/quote -->/g)].map(m => ({ name: m[1], lines: m[2].split('\n').map(l => (l === '>' ? '' : l.replace(/^> /, ''))) }));

// ---- 60, 61, 67: the contract ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('Every_quoted_block_of_the_contract_is_the_parents_text_at_a_commit_of_origin_main_byte_for_byte', async () => {
  const p = parentFixture();
  const built = await buildContract({ repo: p.repo, commit: p.commit, existing: '' });
  assert.equal(built.ok, true, built.reason);
  const blocks = blocksOf(built.value);
  assert.deepEqual(blocks.map(b => b.name), QUOTES.map(q => q.name), 'every quote, in the contract\'s order');
  const parentLines = p.text.split('\n');
  for (const block of blocks) {
    const start = parentLines.indexOf(block.lines[0]);
    assert.ok(start >= 0, `${block.name}: its first line is a line of the parent`);
    assert.deepEqual(parentLines.slice(start, start + block.lines.length), block.lines, `${block.name} is the parent's lines, byte for byte`);
  }
  assert.ok(built.value.includes(`at \`${p.commit}\``), 'the header names the full commit it read');
  assert.ok(built.value.endsWith(`${MARKER}\n`), 'the quoted half ends at the marker');
});

test('The_real_parent_plan_has_every_block_the_contract_quotes_and_each_has_the_shape_it_is_cut_by', () => {
  const quotes = extractQuotes(REAL_PARENT);
  assert.equal(quotes.ok, true, quotes.reason);
  const byName = Object.fromEntries(quotes.value.map(q => [q.name.replace(/^Threat (T\d+(?:, T\d+)?):.*$/, 'Threat $1'), q.lines]));
  assert.ok(byName['Threat T26'][0].startsWith('| T26 |'));
  assert.equal(byName.D24.length, 1, 'a decision is one table row');
  assert.ok(byName.D24[0].startsWith('| D24 | **Primary metric: cost per correct solution.**'));
  assert.ok(byName['Primary metric, exactly (D24)'].some(l => l.startsWith('`CPC(t, s, a)')), 'the formula paragraph is in');
  assert.ok(byName['The CPC ratio and its undefined cases'].some(l => l.startsWith('| both 0 accepted |')), 'the table of undefined cases is in');
  assert.ok(byName['The goal, narrowed'].every(l => l.startsWith('> ')));
  assert.ok(byName['Threat T6, T9'][0].startsWith('| T6, T9 |'));
  assert.equal(quotes.value.length, QUOTES.length);
});

test('A_quote_whose_block_is_missing_at_that_commit_fails_the_extraction_and_names_it', async () => {
  const p = parentFixture({ without: '| T26 |' });
  const built = await buildContract({ repo: p.repo, commit: p.commit, existing: '' });
  assert.equal(built.ok, false);
  assert.match(built.reason, /the parent plan has no block starting '\| T26 \|' \(Threat T26: the Stage 1 reading and the report's sentence\)/);
  const out = path.join(tmpDir('contract-out'), 'analysis-contract.md');
  const ran = await run(contractMain, ['--parent-commit', p.commit, '--out', out], p.repo);
  assert.equal(ran.code, 5, ran.lines.join('|'));
  assert.deepEqual(ran.lines.length, 1);
  assert.equal(await run(contractMain, ['--out', out], p.repo).then(r => r.code), 4, 'no --parent-commit is a usage error');
});

test('A_commit_that_is_not_public_history_is_refused_and_the_text_below_the_marker_is_kept', async () => {
  const p = parentFixture();
  writeFileSync(path.join(p.repo, PARENT_FILE), `${p.text}local edit\n`);
  const local = commitAll(p.repo, 'not on origin/main');
  const refused = await buildContract({ repo: p.repo, commit: local, existing: '' });
  assert.match(refused.reason, /is not an ancestor of origin\/main: a registered quote must come from public history/);
  const out = path.join(tmpDir('contract-keep'), 'analysis-contract.md');
  assert.equal((await run(contractMain, ['--parent-commit', p.commit, '--out', out], p.repo)).code, 0);
  writeFileSync(out, `${readFileSync(out, 'utf8')}\nThe pre-registration's own prose.\n`);
  const before = readFileSync(out, 'utf8');
  assert.equal((await run(contractMain, ['--parent-commit', p.commit, '--out', out], p.repo)).code, 0);
  assert.equal(readFileSync(out, 'utf8'), before, 'written again: the same bytes, the prose below the marker kept');
});

test('The_contract_states_the_claims_limit_the_outcome_classes_and_the_void_line_in_the_parents_words', () => {
  const text = extractQuotes(REAL_PARENT).value.map(q => q.lines.join('\n')).join('\n');
  for (const words of ['Neither reading can show a difference of 20 %', 'five pairs separate\nper-task medians only at about **1.5 times**', 'Outside the claim.**', 'v1\'s twelve rows (`stop: alias drift`', 'A void is **kept** in the records and re-run once', '`unscored: environment`', 'enters CPC as a void', 'The plan was written after an exploratory run of the same tasks', 'The result holds for these six daemon and web tasks only']) {
    assert.ok(text.includes(words) || text.replace(/\s+/g, ' ').includes(words.replace(/\s+/g, ' ')), `the contract quotes: ${words}`);
  }
});

test('A_row_and_a_run_of_paragraphs_are_cut_by_their_first_words_and_a_missing_start_is_named', () => {
  const text = 'a\n| T1 | x |\n| T2 | y |\n\nfirst para\nstill first\n\nformula\n\nthird\n';
  assert.deepEqual(rowAt(text, '| T1 |'), { lines: ['| T1 | x |'] }, 'one row, not the table');
  assert.deepEqual(paragraphsAt(text, 'first para', 2), { lines: ['first para', 'still first', '', 'formula'] });
  assert.deepEqual(paragraphsAt(text, 'first para', 9).lines.at(-1), 'third', 'fewer paragraphs than asked: all there are');
  assert.deepEqual(rowAt(text, '| T9 |'), { missing: '| T9 |' });
  assert.deepEqual(paragraphsAt(text, 'nope', 2), { missing: 'nope' });
});

// ---- 62: written again to the same bytes ------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_prompts_the_schedule_and_the_contract_are_written_again_to_the_same_bytes_and_a_typed_value_fails_the_check', async () => {
  const dir = tmpDir('written');
  const p = parentFixture();
  const jobs = [
    ['prompts-lang.json', promptsMain, [], '"statementSha256": "', '"statementSha256": "0'],
    ['schedule.json', scheduleMain, [], '"rounds": 5', '"rounds": 6'],
    ['analysis-contract.md', contractMain, ['--parent-commit', p.commit], 'the words of D24', 'the words of D25'],
  ];
  for (const [name, main, args, find, replace] of jobs) {
    const out = path.join(dir, name);
    const extra = main === contractMain ? [p.repo] : [];
    assert.equal((await run(main, ['--out', out, ...args], ...extra)).code, 0, name);
    const first = readFileSync(out, 'utf8');
    assert.equal((await run(main, ['--out', out, ...args], ...extra)).code, 0);
    assert.equal(readFileSync(out, 'utf8'), first, `${name}: the same bytes the second time`);
    assert.equal((await run(main, ['--out', out, '--check', ...args], ...extra)).code, 0, `${name}: --check of the written file`);
    assert.ok(first.includes(find), `${name} holds ${find}`);
    writeFileSync(out, first.replace(find, replace));
    const checked = await run(main, ['--out', out, '--check', ...args], ...extra);
    assert.equal(checked.code, 1, `${name}: a typed value fails the check`);
    assert.match(checked.lines.join('|'), /is not what the generator would write/);
    assert.equal(readFileSync(out, 'utf8'), first.replace(find, replace), `${name}: --check writes nothing`);
    assert.equal((await run(main, ['--out', path.join(dir, `absent-${name}`), '--check', ...args], ...extra)).code, 1, `${name}: an absent file fails the check`);
  }
});

test('A_failed_write_leaves_the_target_as_it_was_and_no_sibling_behind', async () => {
  const dir = tmpDir('atomic');
  const out = path.join(dir, 'missing-folder', 'schedule.json');
  const ran = await run(scheduleMain, ['--out', out]);
  assert.equal(ran.code, 5);
  assert.match(ran.lines.join('|'), /cannot write .*schedule\.json: ENOENT/);
  const target = path.join(dir, 'schedule.json');
  writeFileSync(target, 'old\n');
  assert.equal((await run(scheduleMain, ['--out', target])).code, 0);
  assert.notEqual(readFileSync(target, 'utf8'), 'old\n');
  assert.deepEqual(readdirNames(dir), ['schedule.json'], 'no sibling is left beside the written file');
});

test('A_write_that_cannot_replace_its_target_removes_the_sibling_it_wrote_and_leaves_the_target_alone', async () => {
  const dir = tmpDir('atomic-target');
  const target = path.join(dir, 'schedule.json');
  mkdirSync(target);
  writeFileSync(path.join(target, 'held'), 'a folder the file cannot replace\n');
  const ran = await run(scheduleMain, ['--out', target]);
  assert.equal(ran.code, 5);
  assert.match(ran.lines.join('|'), /cannot write .*schedule\.json: (EISDIR|ENOTEMPTY|EEXIST)/);
  assert.deepEqual(readdirNames(dir), ['schedule.json'], 'the sibling the write made is removed when the rename fails');
  assert.deepEqual(readdirNames(target), ['held'], 'the target is as it was');
});

const readdirNames = dir => readdirSync(dir).sort();

// ---- 63, 64: the prompts ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_registered_prompt_of_a_task_and_stack_is_the_text_the_engine_sends', () => {
  assert.ok(!STAGE_PROMPT_TEMPLATE.includes(OLD_LIMITS_SENTENCE) && STAGE_PROMPT_TEMPLATE.includes(NEW_LIMITS_SENTENCE), 'the limits sentence is reworded (F3 closed point 10)');
  assert.equal(STAGE_PROMPT_TEMPLATE.replace(NEW_LIMITS_SENTENCE, OLD_LIMITS_SENTENCE), PROMPT_TEMPLATE, 'and nothing else of the template changed');
  assert.deepEqual(quickPrompts(PROFILE).template, PROMPT_TEMPLATE, 'the quick run\'s own prompts are as before when no template is passed');
  const built = buildPromptsFile({ explore: PROFILE, record: RECORD, hashes: HASHES });
  assert.equal(built.ok, true, built.reason);
  const sent = stagePrompts(PROFILE);
  assert.equal(built.value.template, sent.template);
  assert.equal(built.value.templateSha256, sent.templateSha256);
  for (const task of FREEZE_TASKS) {
    for (const stack of FREEZE_STACKS) {
      assert.equal(built.value.tasks[task][stack].text, sent.prompts[stack].text, `${task} ${stack}`);
      assert.equal(built.value.tasks[task][stack].sha256, sent.prompts[stack].sha256);
    }
  }
  assert.deepEqual(registeredPromptProblems({ registered: built.value, explore: PROFILE }), []);
  assert.equal(built.value.tasks['04'].csharp.frameworkSentence !== null && built.value.tasks['05'].csharp.frameworkSentence === null, true, 'a web task has its framework sentence, a daemon task none');
  assert.match(built.value.tasks['04'].node.frameworkSentence, /^Use Fastify \(fastify 5\.6\.2, listed in PACKAGES\.md\) for the HTTP server\.$/);
  const edited = structuredClone(built.value);
  edited.tasks['07'].node.text = edited.tasks['07'].node.text.replace('TypeScript', 'Typescript');
  assert.deepEqual(registeredPromptProblems({ registered: edited, explore: PROFILE }), ['the registered prompt of task 07 in node is not the text the engine sends']);
  assert.deepEqual(registeredPromptProblems({ registered: { ...built.value, template: PROMPT_TEMPLATE }, explore: PROFILE }), ['the registered template is not the one the engine sends']);
  assert.deepEqual(registeredPromptProblems({ registered: { ...built.value, tasks: {} }, explore: PROFILE }), ['the registered prompts name no task']);
});

test('The_registered_statement_hashes_are_the_published_workspaces', () => {
  assert.equal(Object.keys(HASHES).length, 12, 'twelve STATEMENT.md hashes in hashes-lang.txt');
  const tasks = freezeTasks(PROFILE).value;
  const folders = tasks.flatMap(task => FREEZE_STACKS.map(stack => folderOf(task, stack)));
  const root = tmpDir('workspaces');
  const bytes = folder => Buffer.from(`published ${folder}\n`);
  const hashed = Object.fromEntries(folders.map(f => [f, createHashOf(bytes(f))]));
  for (const folder of folders) { mkdirSync(path.join(root, folder), { recursive: true }); writeFileSync(path.join(root, folder, 'STATEMENT.md'), bytes(folder)); }
  const read = folder => { try { return readFileSync(path.join(root, folder, 'STATEMENT.md')); } catch { return null; } };
  assert.deepEqual(statementProblems({ hashes: hashed, folders, read }), []);
  writeFileSync(path.join(root, folders[3], 'STATEMENT.md'), 'one changed byte\n');
  assert.match(statementProblems({ hashes: hashed, folders, read }).join('|'), new RegExp(`^${folders[3].replace('/', '\\/')}\\/STATEMENT\\.md is not the published one`));
  assert.deepEqual(statementProblems({ hashes: hashed, folders: [...folders, 'node/99-none'], read }).slice(-1), ['hashes-lang.txt has no STATEMENT.md hash for node/99-none']);
  assert.deepEqual(statementProblems({ hashes: { ...hashed, 'node/99-none': 'a'.repeat(64) }, folders: ['node/99-none'], read }), ['the published workspace node/99-none has no STATEMENT.md to check its hash against']);
  const partial = buildPromptsFile({ explore: PROFILE, record: RECORD, hashes: { ...HASHES, 'node/04-rule-api': undefined } });
  assert.equal(partial.ok, false);
  assert.match(partial.reason, /hashes-lang\.txt has no STATEMENT\.md hash for node\/04-rule-api/);
});
const createHashOf = bytes => createHash('sha256').update(bytes).digest('hex');

// ---- 68: the schedule file ------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_schedule_file_is_refused_without_the_pilots_tasks_and_the_A_As_task_and_stack', () => {
  const caps = { capUsd: 50, wallMs: 4_500_000, stallMs: 2_700_000 };
  assert.match(buildSchedule({ explore: { caps } }).reason, /the pilot is refused: pilot: required/);
  assert.match(buildSchedule({ explore: { caps }, pilot: { daemon: '10', web: '04' } }).reason, /^the pilot is refused: pilot\.aa/);
  assert.match(buildSchedule({ explore: { caps }, pilot: { aa: { task: '05' }, daemon: '10', web: '04' } }).reason, /^the pilot is refused: pilot\.aa\.stack/);
  assert.match(buildSchedule({ explore: { caps }, pilot: { aa: { task: '05', stack: 'node' }, web: '04' } }).reason, /pilot\.daemon/);
  assert.match(buildSchedule({ explore: { caps }, pilot: { aa: { task: '05', stack: 'node' }, daemon: '05', web: '04' } }).reason, /^the pilot is refused: pilot\./);
  const built = buildSchedule({ explore: { caps }, pilot: STAGE_PILOT });
  assert.equal(built.ok, true, built.reason);
  assert.deepEqual(built.value.pilot.aa, { task: '05', stack: 'node', twins: ['a', 'b'] }, 'the owner\'s answer of 2026-11-19: the A/A is task 05 on Node, both twins');
  assert.equal(built.value.pilot.daemon, '10');
  assert.equal(built.value.pilot.web, '04');
  assert.deepEqual(STAGE_PILOT, { aa: { task: '05', stack: 'node' }, daemon: '10', web: '04' });
});

test('The_schedule_files_prose_states_the_numbers_of_the_code_and_the_read_back_rounds_are_the_schedule_modules', () => {
  const caps = { capUsd: 50, wallMs: 4_500_000, stallMs: 2_700_000 };
  const { value } = buildSchedule({ explore: { caps }, pilot: STAGE_PILOT });
  assert.deepEqual(scheduleModule.READBACK_ROUNDS, [1, 2], 'the rule is exported once by the schedule module');
  assert.deepEqual(value.readback.rounds, [...scheduleModule.READBACK_ROUNDS], 'the file holds that very rule');
  assert.deepEqual(/in round (\d+) and in round (\d+)/.exec(value.readback.pairs).slice(1).map(Number), [...scheduleModule.READBACK_ROUNDS], 'and its prose says the same rounds');
  const plan = pilotPlan({ pilot: STAGE_PILOT }).value;
  const [aa, pair] = [plan.aa.sessions.length * MAX_ATTEMPTS, plan.pairs[0].sessions.length * MAX_ATTEMPTS];
  assert.deepEqual(/at most (\d+): the A\/A (\d+), each pair (\d+)/.exec(value.pilot.sessions).slice(1).map(Number), [aa + pair * plan.pairs.length, aa, pair], 'the pilot\'s sessions: the A/A\'s twins and each pair, with the attempts');
  assert.deepEqual([...value.masks.rule.matchAll(/(\d\d) is (\d)\b/g)].map(m => [m[1], Number(m[2])]), FREEZE_TASKS.map((task, k) => [task, k]), 'the task indices of the mask rule are the tasks\' places in the freeze');
  const counts = PROFILES.map(p => p.cpus.split('-').reduce((lo, hi) => Number(hi) - Number(lo) + 1));
  assert.deepEqual(/the odd passes run on the (\d+)-CPU profile and the even passes on the (\d+)-CPU profile/.exec(value.scoring.rule).slice(1).map(Number), counts, 'the CPU counts of the two profiles');
});

test('The_schedule_file_holds_the_30_masks_the_rule_gives_the_two_profiles_the_counts_and_the_caps', () => {
  const caps = { capUsd: 50, wallMs: 4_500_000, stallMs: 2_700_000 };
  const { value } = buildSchedule({ explore: { caps }, pilot: STAGE_PILOT });
  assert.equal(value.masks.cells.length, 30);
  for (const cell of value.masks.cells) {
    assert.equal(cell.csharp, maskOfSlot({ round: cell.round, task: cell.task, stack: 'csharp' }).value);
    assert.equal(cell.node, maskOfSlot({ round: cell.round, task: cell.task, stack: 'node' }).value);
    assert.notEqual(cell.csharp, cell.node, 'the stacks take the two masks');
  }
  assert.equal(value.masks.cells.filter(c => c.csharp === '0-5').length, 15, 'C# is on mask A in 15 of the 30 cells');
  assert.deepEqual([value.rounds, value.slots, value.dataSessions, value.scoring.passes, value.attempts.perCell], [5, 30, 60, 10, 2]);
  assert.deepEqual(value.scoring.profiles, [{ name: '2cpu', cpus: '6-7' }, { name: '8cpu', cpus: '0-7' }]);
  assert.deepEqual(value.caps, caps);
  assert.deepEqual(value.readback.rounds, [1, 2]);
  assert.match(value.attempts.rerun, /once, alone, as its own attempt, before the next slot/);
});

// ---- 79f: the two rule files against the contract ---------------------------------------------------------------------------------------------------------------------------------------------

const ROW = id => ({ id, askedOf: 'the twins', evidence: 'score/passes', mode: 'computed', threshold: 'every pass', whenMissing: 'NO GO', notExercised: false, stopsWhenRefuted: id === 'G-2' });
const CONTRACT = ['text', '<!-- go-row: G-1 -->', '| G-1 | row |', '<!-- go-row: G-2 -->', '<!-- adjudication-rule: packets-sessions -->', '<!-- adjudication-rule: exclusion -->'].join('\n');

const GOOD = { pilotRules: { rows: [ROW('G-1'), ROW('G-2')] }, adjudicationRules: { rules: [{ id: 'packets-sessions', text: 'x' }, { id: 'exclusion', text: 'y' }] } };
const problems = over => ruleFileProblems({ contractText: CONTRACT, ...GOOD, ...over });

test('The_two_rule_files_hold_every_row_and_rule_the_contract_names_and_no_other', () => {
  assert.deepEqual(contractIds(CONTRACT), { rows: ['G-1', 'G-2'], rules: ['packets-sessions', 'exclusion'] });
  assert.ok(GO_ROW_MARKER.test('<!-- go-row: G-16 -->') && !GO_ROW_MARKER.test('<!-- go-row: g-1 -->') && ADJUDICATION_RULE_MARKER.test('<!-- adjudication-rule: a-b -->'));
  assert.deepEqual(problems({}), []);
  assert.deepEqual(problems({ contractText: undefined, pilotRules: { rows: [] }, adjudicationRules: { rules: [] } }), [], 'a contract that names nothing needs files that hold nothing');
  assert.deepEqual(problems({ pilotRules: { rows: [{ ...ROW('G-1'), notExercised: false }, ROW('G-2')] } }), [], 'false is an answer, not an open rule');
});

test('A_row_the_contract_names_and_the_file_lacks_is_refused_by_its_id', () => {
  assert.deepEqual(problems({ pilotRules: { rows: [ROW('G-1')] } }), ['row G-2 is named by the contract and the file lacks it']);
  assert.deepEqual(problems({ adjudicationRules: { rules: [{ id: 'exclusion', text: 'y' }] } }), ['rule packets-sessions is named by the contract and the file lacks it']);
});

test('A_row_or_a_rule_in_the_file_that_the_contract_does_not_name_is_refused_by_its_id', () => {
  assert.deepEqual(problems({ adjudicationRules: { rules: [...GOOD.adjudicationRules.rules, { id: 'harness-class', text: 'z' }] } }), ['rule harness-class is in the file and the contract does not name it']);
  assert.deepEqual(problems({ pilotRules: { rows: [...GOOD.pilotRules.rows, ROW('G-9')] } }), ['row G-9 is in the file and the contract does not name it']);
});

test('A_row_given_twice_is_refused_and_a_row_that_leaves_a_key_open_is_named_with_its_keys', () => {
  assert.deepEqual(problems({ pilotRules: { rows: [ROW('G-1'), ROW('G-2'), ROW('G-2')] } }), ['row G-2 is in the file twice']);
  assert.deepEqual(problems({ pilotRules: { rows: [ROW('G-1'), { ...ROW('G-2'), threshold: undefined, whenMissing: '' }] } }), ['row G-2 leaves open: threshold, whenMissing']);
  assert.deepEqual(problems({ pilotRules: { rows: [ROW('G-1'), { ...ROW('G-2'), stopsWhenRefuted: null }] } }), ['row G-2 leaves open: stopsWhenRefuted'], 'a null answer is an open one');
  assert.deepEqual(problems({ adjudicationRules: { rules: [{ id: 'packets-sessions', text: '' }, { id: 'exclusion', text: 'y' }] } }), ['rule packets-sessions leaves open: text']);
});

test('A_file_with_no_list_is_refused_naming_the_file', () => {
  assert.deepEqual(ruleFileProblems({ contractText: CONTRACT, pilotRules: {}, adjudicationRules: null }), ['pilot-rules.json holds no `rows` list', 'adjudication-rules.json holds no `rules` list']);
});

// ---- 66: no registered file holds a case line, a trap name or a reference line -----------------------------------------------------------------------------------------------------------------

function materialOfFixture() {
  const dir = tmpDir('held');
  const casesDir = path.join(dir, 'cases');
  mkdirSync(casesDir);
  mkdirSync(path.join(dir, 'refs', 'csharp', 'src'), { recursive: true });
  writeFileSync(path.join(casesDir, 'a.out'), 'xyzzy 11 22 33 44 55 66\n');
  writeFileSync(path.join(dir, 'refs', 'csharp', 'src', 'Program.cs'), 'long frobnicate = quuxify(input, 7, extra);\n');
  return buildMaterial({ tasks: [{ id: '01', heldout: { dir, casesDir }, shinglesText: 'the trap named quuxify scatters frobnicate' }] }).value;
}

test('No_registered_file_holds_a_case_id_a_trap_name_or_a_case_line', () => {
  const root = tmpDir('prereg-root');
  const put = (rel, text) => { mkdirSync(path.dirname(path.join(root, rel)), { recursive: true }); writeFileSync(path.join(root, rel), text); };
  put(`${PREREG_DIR}/statement.md`, 'what is run: six tasks, bare\n');
  put(`${PREREG_DIR}/prices-2026-11-19.json`, '{}\n');
  put('spikes/lang-bench/inputs/specs.d/runner.json', '{}\n');
  put('docs/outside.md', 'xyzzy 11 22 33 44 55 66\n');
  const material = materialOfFixture();
  assert.deepEqual(registeredFilesOn(root), [`${PREREG_DIR}/statement.md`, `${PREREG_DIR}/prices-2026-11-19.json`, 'spikes/lang-bench/inputs/specs.d/runner.json'], 'the plain entries in the list\'s order, a folder entry\'s files in name order');
  const clean = preregLeakFindings({ root, material });
  assert.deepEqual([clean.findings, clean.incomplete], [[], []], 'a clean folder; the file outside the registered list is not scanned');
  put(`${PREREG_DIR}/analysis-contract.md`, 'a result: xyzzy 11 22 33 44 55 66 and more\n');
  put('spikes/lang-bench/inputs/specs.d/format.json', '{"note":"long frobnicate = quuxify(input, 7, extra); copied"}\n');
  put(`${PREREG_DIR}/prediction.md`, 'we expect the trap named quuxify scatters frobnicate to cost more\n');
  const dirty = preregLeakFindings({ root, material });
  assert.deepEqual(dirty.findings.map(f => f.file).sort(), [`${PREREG_DIR}/analysis-contract.md`, `${PREREG_DIR}/prediction.md`, 'spikes/lang-bench/inputs/specs.d/format.json'].sort(), 'a case line, a reference line and a trap-note run are each found in the file that holds them');
  assert.ok(dirty.findings.every(f => f.raws === undefined), 'the public form carries a place and a tier, never the private text');
});

// ---- 78: the engine prices a Stage 1 session from the registered price file --------------------------------------------------------------------------------------------------------------------

test('The_engine_prices_a_stage_session_from_the_registered_price_file', () => {
  const root = tmpDir('prices-root');
  mkdirSync(path.join(root, PREREG_DIR), { recursive: true });
  assert.match(registeredInputs(root).reason, /must hold exactly one prices-<yyyy-mm-dd>\.json \(found 0\)/);
  const row = { modelId: 'claude-opus-5-5', input: 3, output: 17, cacheRead: 0.3, cacheWrite5m: 3.75, cacheWrite1h: 6 };
  writeFileSync(path.join(root, PREREG_DIR, 'prices-2026-11-19.json'), `${JSON.stringify({ source: 'https://example.invalid/pricing', readOn: '2026-11-19', models: [row], cliRates: [], notes: [] })}\n`);
  writeFileSync(path.join(root, PREREG_DIR, 'crosscheck-policy.json'), `${JSON.stringify({ t1: 'stop', t2: 'otel-incomplete', t3: 'stop', t5: 'investigate', t3OutputSource: 'transcript', join: 'request_id', decidedOn: '2026-11-19', reason: 'registered' })}\n`);
  const found = registeredInputs(root);
  assert.equal(found.ok, true, found.reason);
  const loaded = crosscheckPolicyFrom(path.join(REPO, 'spikes', 'minigit-bench'), found.value);
  assert.equal(loaded.ok, true, loaded.reason);
  assert.deepEqual({ ...loaded.value.table.models.get('claude-opus-5-5') }, row, 'the rates of the registered file, not v1\'s');
  const v1 = crosscheckPolicyFrom(path.join(REPO, 'spikes', 'minigit-bench'), {});
  assert.notEqual(v1.value.table.models.get('claude-opus-5-5').input, row.input, 'v1\'s own file prices it otherwise: the registered path is what makes the difference');
  writeFileSync(path.join(root, PREREG_DIR, 'prices-2026-11-20.json'), '{}\n');
  assert.match(registeredInputs(root).reason, /found 2: prices-2026-11-19\.json, prices-2026-11-20\.json/, 'one day is chosen once');
});

// ---- rule fixes of the Opus review (PR 5, V11) ---------------------------------------------------------------------------------------------------------------------------------------------------

test('A_missing_statement_hashes_file_is_a_refusal_naming_it_and_never_a_throw', async () => {
  const dir = tmpDir('no-hashes');
  const out = path.join(dir, 'prompts-lang.json');
  const ran = await run(promptsMain, ['--out', out], { hashesFile: path.join(dir, 'absent-hashes.txt') });
  assert.equal(ran.code, 5);
  assert.match(ran.lines[0], /absent-hashes\.txt cannot be read: ENOENT/);
  assert.equal(existsSync(out), false, 'nothing was written');
});

test('No_module_of_the_pre_registration_reassigns_a_variable', () => {
  const files = [...readdirSync(path.join(REPO, 'spikes/lang-explore/prereg')).filter(f => f.endsWith('.mjs')).map(f => `spikes/lang-explore/prereg/${f}`),
    ...['stage-prereg', 'stage-prereg-guard', 'stage-prereg-inputs', 'stage-prereg-commands', 'stage-branch', 'stage-seed', 'stage-leak', 'stage-slices'].map(f => `spikes/lang-explore/lib/${f}.mjs`), 'spikes/minigit-bench/lib/seed.mjs'];
  const lets = files.flatMap(file => readFileSync(path.join(REPO, file), 'utf8').split('\n').map((line, i) => [file, i + 1, line]).filter(([, , line]) => /^\s*(let|var)\s/.test(line)).map(([f, n]) => `${f}:${n}`));
  assert.deepEqual(lets, [], 'common.coding-style: no mutation, so no variable that is assigned twice');
});
