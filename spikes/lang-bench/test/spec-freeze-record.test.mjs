// The freeze tools of F2.S2 (d), part two: `record` (what it verifies before it writes, what it refuses, the superseding tag) and the atomic outputs with their restart rules. Synthetic fixtures only
// (test/support/freeze-fixtures.mjs): a local bare repository stands for the GitHub remote, a squash merge is `git merge --squash`.
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { chmodSync, existsSync, fsyncSync, mkdirSync, openSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync, writeSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { readEvidence } from '../lib/freeze-evidence.mjs';
import { classifyName, formatOwner, processProbe, thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { run as classes } from '../tools/format-classes.mjs';
import { assemble } from '../lib/specs-record.mjs';
import { annotate, baseFiles, evidence, freezeAndMerge, freezeBranch, fullFreeze, LATER_TAG, makeFreezeRepo, pushTag, SCRATCH, sha256, specPath, specText, squashMerge, TAG, workspaceFiles, workspacesBranch, workspacesCheckFile, FORMAT_PATH } from './support/freeze-fixtures.mjs';
import { commit, git } from './support/push-fixtures.mjs';
import { owned } from './support/owned.mjs';

const said = result => `${result.stdout}${result.stderr}`;
const statesFile = f => path.join(f.inputs, 'specs.d', 'spec-states.json');
const readStates = f => JSON.parse(readFileSync(statesFile(f), 'utf8'));
// Every prefixed name the tool left in the fixture's own folder, any owner: read by the shipped classifier (a reader of names, not a sweep).
const leftovers = f => readdirSync(f.inputs).filter(name => classifyName(name).form !== 'none');
const KILL_CHILD = fileURLToPath(new URL('./support/freeze-kill-child.mjs', import.meta.url));
const commitDay = (repo, sha) => new Date(Number(git(repo, 'show', '-s', '--format=%ct', sha)) * 1000).toISOString().slice(0, 10);

/** `record` on the fixture with the hash list written first (`hashes --write`, unless `list` is false: `record` compares it with W's), the evidence and the workspaces check written beside it; `over` replaces the evidence's keys and `extra` adds flags. */
async function record(f, { c, w, tag = TAG, over = {}, leak = {}, extra = [], check, list = true } = {}) {
  if (list) await f.run('hashes', ['--commit', w, '--write']);
  const flags = ['--tag', tag, '--commit', w, '--evidence', evidence(f, { c, w, tag, over, leak }), '--workspaces-check', check ?? workspacesCheckFile(f, w), ...extra];
  return f.run('record', flags);
}

// ---- what it writes ----------------------------------------------------------------------------------------------------------------------------------

test('record_writes_the_accepted_freeze_entry_and_the_six_spec_rows_to_one_file_and_nothing_else', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  await f.run('hashes', ['--commit', w, '--write']);
  const before = readdirSync(f.inputs, { recursive: true }).sort();
  const result = await record(f, { c, w, list: false });
  assert.equal(result.exit, 0, said(result));
  const after = readdirSync(f.inputs, { recursive: true }).sort();
  assert.deepEqual(after.filter(name => !before.includes(name)), [path.join('specs.d', 'spec-states.json')], 'one new file, and no other file written or left');
  const states = readStates(f);
  const listed = (await f.run('hashes', ['--commit', w])).stdout;
  assert.deepEqual(Object.keys(states).sort(), ['freeze', 'specs']);
  assert.deepEqual(Object.keys(states.specs).sort(), ['04', '05', '06', '07', '08', '10']);
  const tagObject = git(f.repo, 'rev-parse', `refs/tags/${TAG}`);
  assert.equal(states.freeze.tag, TAG);
  assert.equal(states.freeze.tagObjectSha, tagObject);
  assert.equal(states.freeze.commitSha, c);
  assert.equal(states.freeze.workspaceCommit, w);
  assert.equal(states.freeze.formatSha256, f.formatSha256);
  assert.equal(states.freeze.hashesLangSha256, sha256(listed.endsWith('\n') ? listed.slice(0, -1) + '\n' : listed), 'the hash of the list `hashes` prints');
  assert.equal(states.freeze.state, 'accepted');
  assert.equal(states.freeze.provisionalFrom, '2026-12-01');
  assert.equal(states.freeze.acceptedOn, '2026-12-02', 'acceptedOn is the date of the clean leak check of W (the accepting step), not the commit\'s day');
  assert.notEqual(states.freeze.acceptedOn, commitDay(f.repo, w));
  assert.deepEqual(states.freeze.superseded, []);
  assert.deepEqual(states.freeze.workspacesCheck, { commit: w, exit: 0, date: '2026-12-02' });
  assert.deepEqual(Object.keys(states.freeze.leakcheck).sort(), ['freezeCommit', 'scratchWorkspace', 'workspaceCommit']);
  for (const [nn, slug] of [['04', '04-rule-api'], ['10', '10-dag-scheduler']]) {
    const row = states.specs[nn];
    assert.equal(row.path, specPath(slug));
    assert.equal(row.state, 'frozen');
    assert.equal(row.gate, `inputs/gates/spec-${nn}.md`);
    assert.equal(row.commitSha, c);
    assert.equal(row.tagObjectSha, tagObject);
    assert.equal(row.sha256, sha256(`${git(f.repo, 'show', `${c}:${specPath(slug)}`)}\n`));
    assert.deepEqual(row.rIds.slice(0, 2), ['R1', 'R2']);
  }
  assert.equal(assemble(f.inputs).ok, true, 'specs-record.mjs assembles it without error');
  assert.equal(existsSync(path.join(f.inputs, 'specs.json')), false, 'record writes no other file: specs.json is assembled by the one tool that writes it');
});

test('A_record_rewritten_with_the_same_inputs_has_the_same_bytes_and_one_of_another_freeze_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  assert.equal((await record(f, { c, w })).exit, 0);
  const first = readFileSync(statesFile(f));
  assert.equal((await record(f, { c, w })).exit, 0, 'a re-run is allowed');
  assert.deepEqual(readFileSync(statesFile(f)), first, 'and writes the same bytes');
  const g = makeFreezeRepo(t);
  const other = fullFreeze(g);
  writeFileSync(statesFile(g), `${JSON.stringify({ freeze: { tag: 'spec-frozen-B-20250101' } })}\n`);
  const refused = await record(g, other);
  assert.equal(refused.exit, 3, said(refused));
  assert.match(refused.stderr, /spec-frozen-B-20250101/);
});

test('The_class_check_reads_the_frozen_FORMAT_from_the_record_this_tool_wrote', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  assert.equal((await record(f, { c, w })).exit, 0);
  const frozenFile = path.join(f.root, 'FORMAT.frozen.md');
  writeFileSync(frozenFile, `${git(f.repo, 'show', `${c}:${FORMAT_PATH}`)}\n`);
  const rewritten = JSON.parse(readFileSync(f.recordFile, 'utf8'));
  rewritten.A.sha256 = '0'.repeat(64);
  writeFileSync(f.recordFile, JSON.stringify(rewritten));
  const result = classes(['--frozen', frozenFile, '--candidate', frozenFile, '--record', f.recordFile]);
  assert.equal(result.exit, 0, `A's sha256 no longer matters once B's record exists: ${result.lines.join('\n')}`);
});

// ---- what it verifies before it writes ---------------------------------------------------------------------------------------------------------------

test('record_refuses_while_either_post_merge_leakcheck_result_is_missing', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  const clean = key => ({ commit: key === 'scratchWorkspace' ? 'f'.repeat(40) : (key === 'freezeCommit' ? c : w), exit: 0, date: key === 'workspaceCommit' ? '2026-12-02' : '2026-12-01' });
  for (const key of ['freezeCommit', 'scratchWorkspace', 'workspaceCommit']) {
    const leakcheck = Object.fromEntries(['freezeCommit', 'scratchWorkspace', 'workspaceCommit'].filter(name => name !== key).map(name => [name, clean(name)]));
    const refused = await record(f, { c, w, over: { leakcheck } });
    assert.equal(refused.exit, 3, `${key} missing: ${said(refused)}`);
    assert.ok(refused.stderr.includes(key), refused.stderr);
    assert.equal(existsSync(statesFile(f)), false, 'nothing is written');
  }
  const dirty = { freezeCommit: clean('freezeCommit'), scratchWorkspace: { ...clean('scratchWorkspace'), exit: 1 }, workspaceCommit: clean('workspaceCommit') };
  assert.equal((await record(f, { c, w, over: { leakcheck: dirty } })).exit, 3, 'a result whose exit is not 0');
  const wrongCommit = { freezeCommit: clean('workspaceCommit'), scratchWorkspace: clean('scratchWorkspace'), workspaceCommit: clean('workspaceCommit') };
  const mismatch = await record(f, { c, w, over: { leakcheck: wrongCommit } });
  assert.equal(mismatch.exit, 3, 'the freeze commit\'s result must name C');
  assert.match(mismatch.stderr, /freezeCommit/);
  assert.equal((await record(f, { c, w, over: { state: 'provisional' } })).exit, 3, 'any state but accepted');
  assert.equal((await record(f, { c, w, over: { provisionalFrom: 'yesterday' } })).exit, 3, 'a date that is not a date');
  assert.equal(existsSync(statesFile(f)), false);
  assert.equal((await record(f, { c, w })).exit, 0, 'the control, all three clean, writes');
});

test('record_refuses_an_empty_W_and_a_W_missing_one_workspace', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  pushTag(f);
  const empty = commit(f.repo, 'a descendant of C with no change', {});
  git(f.repo, 'push', '-q', 'origin', 'main');
  const refusedEmpty = await record(f, { c, w: empty });
  assert.equal(refusedEmpty.exit, 1, said(refusedEmpty));
  assert.match(refusedEmpty.stderr, /twelve/);
  assert.equal(existsSync(statesFile(f)), false);
  const g = makeFreezeRepo(t);
  const merged = freezeAndMerge(g);
  annotate(g, { at: merged.c });
  pushTag(g);
  const files = workspaceFiles();
  for (const key of Object.keys(files).filter(name => name.startsWith('node/10-dag-scheduler/'))) delete files[key];
  const missing = squashMerge(g, workspacesBranch(g, { from: merged.c, files }), 'T2 without one workspace');
  const refusedMissing = await record(g, { c: merged.c, w: missing });
  assert.equal(refusedMissing.exit, 1, said(refusedMissing));
  assert.match(refusedMissing.stderr, /node\/10-dag-scheduler/);
  assert.equal(existsSync(statesFile(g)), false);
});

test('record_refuses_a_workspaces_check_that_is_missing_names_another_commit_or_ends_in_another_line', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  const bad = [
    ['an absent file', path.join(f.root, 'no-such-check.txt')],
    ['another commit', workspacesCheckFile(f, w, `workspaces-check ok commit ${c} tasks 04,05,06,07,08,10 stacks csharp,node`)],
    ['another task list', workspacesCheckFile(f, w, `workspaces-check ok commit ${w} tasks 04,05,06,07,08 stacks csharp,node`)],
    ['a finding line last', workspacesCheckFile(f, w, 'csharp/04-rule-api/STATEMENT.md: bytes differ')],
  ];
  for (const [what, check] of bad) {
    const refused = await record(f, { c, w, check });
    assert.equal(refused.exit, 3, `${what}: ${said(refused)}`);
    assert.match(refused.stderr, /workspaces-check/);
  }
  assert.equal(existsSync(statesFile(f)), false);
});

test('record_runs_check_tag_and_check_workspace_itself_and_writes_nothing_when_either_is_red', async t => {
  const lightweight = makeFreezeRepo(t);
  const merged = freezeAndMerge(lightweight);
  git(lightweight.repo, 'tag', TAG, merged.c);
  pushTag(lightweight);
  const w1 = squashMerge(lightweight, workspacesBranch(lightweight, { from: merged.c }), 'T2');
  const tagRed = await record(lightweight, { c: merged.c, w: w1 });
  assert.equal(tagRed.exit, 1, said(tagRed));
  assert.match(tagRed.stderr, /check-tag/);
  assert.equal(existsSync(statesFile(lightweight)), false);
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  pushTag(f);
  const w = squashMerge(f, workspacesBranch(f, { from: c, files: workspaceFiles({ 'docs/stray.md': 'a path outside the twelve folders\n' }) }), 'T2');
  const workspaceRed = await record(f, { c, w });
  assert.equal(workspaceRed.exit, 1, said(workspaceRed));
  assert.match(workspaceRed.stderr, /check-workspace/);
  assert.ok(workspaceRed.stderr.includes('docs/stray.md'));
  assert.equal(existsSync(statesFile(f)), false);
});

// ---- the superseding tag -----------------------------------------------------------------------------------------------------------------------------

/** The failure after the push and its recovery in the fixture: tag one pushed and T2 merged with a stray path (so the checks are red), a revert, a new freeze commit under the later tag, its tag pushed and its workspaces merged. */
function failureAndRecovery(f) {
  const first = fullFreeze(f, { files: workspaceFiles({ 'docs/stray.md': 'the planted cause\n' }) });
  git(f.repo, 'revert', '--no-edit', first.w);
  git(f.repo, 'push', '-q', 'origin', 'main');
  freezeBranch(f, { tag: LATER_TAG, from: 'main' });
  const c2 = squashMerge(f, `freeze/${LATER_TAG}`, 'T1 again');
  annotate(f, { tag: LATER_TAG, at: c2 });
  pushTag(f, LATER_TAG);
  const w2 = squashMerge(f, workspacesBranch(f, { tag: LATER_TAG, from: c2 }), 'T2 again');
  return { first, c2, w2 };
}

test('A_failure_after_the_push_leads_to_a_superseding_tag_on_a_new_freeze_commit_and_the_record_names_both', async t => {
  const f = makeFreezeRepo(t);
  const first = fullFreeze(f, { files: workspaceFiles({ 'docs/stray.md': 'the planted cause\n' }) });
  const provisional = await record(f, { c: first.c, w: first.w });
  assert.equal(provisional.exit, 1, 'T2\'s checks are red: the freeze stays provisional');
  assert.equal(existsSync(statesFile(f)), false, 'record never writes a provisional state');
  assert.ok(git(f.repo, 'ls-remote', '--tags', 'origin').includes(`refs/tags/${TAG}`), 'the first tag stays on the remote');
  const g = makeFreezeRepo(t);
  const { first: pushed, c2, w2 } = failureAndRecovery(g);
  const why = 'the first workspace commit held a stray path, found by check-workspace and reverted';
  const accepted = await record(g, { c: c2, w: w2, tag: LATER_TAG, extra: ['--supersedes', TAG, '--why', why] });
  assert.equal(accepted.exit, 0, said(accepted));
  const states = readStates(g);
  assert.equal(states.freeze.tag, LATER_TAG, 'freeze.tag is the superseding tag');
  assert.equal(states.freeze.state, 'accepted');
  assert.equal(states.freeze.commitSha, c2);
  assert.deepEqual(states.freeze.superseded, [{ tag: TAG, tagObjectSha: git(g.repo, 'rev-parse', `refs/tags/${TAG}`), why }], 'the first tag, its object and the sentence why');
  assert.notEqual(pushed.c, c2);
  assert.equal(states.specs['04'].tagObjectSha, git(g.repo, 'rev-parse', `refs/tags/${LATER_TAG}`));
});

test('The_record_names_both_tags_and_freeze_tag_is_the_superseding_one', async t => {
  const f = makeFreezeRepo(t);
  const { c2, w2 } = failureAndRecovery(f);
  assert.equal((await record(f, { c: c2, w: w2, tag: LATER_TAG, extra: ['--supersedes', TAG, '--why', 'a cause was fixed'] })).exit, 0);
  const states = readStates(f);
  assert.deepEqual([states.freeze.tag, states.freeze.superseded.map(entry => entry.tag)], [LATER_TAG, [TAG]]);
  const text = readFileSync(statesFile(f), 'utf8');
  assert.equal(text.includes(TAG), true);
});

test('record_supersedes_accepts_an_old_tag_that_exists_on_the_remote_and_refuses_one_that_does_not', async t => {
  const f = makeFreezeRepo(t);
  const { c2, w2 } = failureAndRecovery(f);
  const run = extra => record(f, { c: c2, w: w2, tag: LATER_TAG, extra });
  const none = await run(['--supersedes', 'spec-frozen-B-20261130', '--why', 'a tag nobody pushed']);
  assert.equal(none.exit, 3, said(none));
  assert.match(none.stderr, /spec-frozen-B-20261130.*remote/);
  git(f.repo, 'tag', '-a', 'spec-frozen-B-20261130', c2, '-m', 'local only');
  assert.equal((await run(['--supersedes', 'spec-frozen-B-20261130', '--why', 'a local tag never pushed'])).exit, 3, 'a tag that exists only locally');
  assert.equal((await run(['--supersedes', LATER_TAG, '--why', 'the tag itself'])).exit, 3, 'a tag cannot supersede itself');
  assert.equal((await run(['--supersedes', TAG])).exit, 3, 'a --supersedes without --why');
  assert.equal(existsSync(statesFile(f)), false);
  assert.equal((await run(['--supersedes', TAG, '--why', 'a cause was fixed'])).exit, 0, 'the old tag on the remote passes');
});

test('record_refuses_a_superseded_tag_that_is_not_older_than_the_new_one', async t => {
  const f = makeFreezeRepo(t);
  const { c2, w2 } = failureAndRecovery(f);
  git(f.repo, 'tag', '-a', 'spec-frozen-B-20261203', c2, '-m', 'later than the new tag');
  git(f.repo, 'push', '-q', 'origin', 'refs/tags/spec-frozen-B-20261203');
  const refused = await record(f, { c: c2, w: w2, tag: LATER_TAG, extra: ['--supersedes', 'spec-frozen-B-20261203', '--why', 'a later tag cannot be replaced by an earlier one'] });
  assert.equal(refused.exit, 3, said(refused));
  assert.match(refused.stderr, /later|older/);
});

// ---- atomic outputs, restarts ------------------------------------------------------------------------------------------------------------------------

/** A freeze verb in a child that kills itself before its rename. `flags` are the verb's flags after the verb. */
function killedBeforeRename(f, verb, flags) {
  return spawnSync(process.execPath, [KILL_CHILD, 'before-rename', verb, '--repo', f.repo, '--inputs', f.inputs, '--pins', f.pins, ...flags], { encoding: 'utf8', timeout: 120_000, killSignal: 'SIGKILL' });
}

test('A_record_killed_before_its_rename_leaves_only_an_owned_temporary_file_that_the_restart_removes_and_never_reads', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  await f.run('hashes', ['--commit', w, '--write']);
  const flags = ['--tag', TAG, '--commit', w, '--evidence', evidence(f, { c, w }), '--workspaces-check', workspacesCheckFile(f, w)];
  const killed = killedBeforeRename(f, 'record', flags);
  assert.equal(killed.signal, 'SIGKILL', killed.stderr);
  assert.equal(existsSync(statesFile(f)), false, 'no record: the rename never happened');
  const left = leftovers(f);
  assert.equal(left.filter(name => !name.endsWith('.owner')).length, 1, `one owned temporary file: ${left.join(', ')}`);
  assert.equal(left.filter(name => name.endsWith('.owner')).length, 1, 'and its claim beside it');
  assert.equal(assemble(f.inputs).ok, true, 'specs-record never sees a temporary file in specs.d');
  const tmp = path.join(f.inputs, left.find(name => !name.endsWith('.owner')));
  writeFileSync(tmp, '{"freeze": {"state": "accepted", "tag": "half a recor');
  const restarted = await record(f, { c, w });
  assert.equal(restarted.exit, 0, said(restarted));
  assert.deepEqual(leftovers(f), [], 'the restart removed the dead owner\'s file and its claim, and left none of its own');
  assert.equal(readStates(f).freeze.tag, TAG, 'the record is whole: the truncated temporary file was never read');
});

test('A_hash_list_killed_before_its_rename_leaves_the_old_list_whole', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const target = path.join(f.inputs, 'hashes-lang.txt');
  const old = `${'0'.repeat(64)}  csharp/.gitkeep\n`;
  writeFileSync(target, old);
  const killed = killedBeforeRename(f, 'hashes', ['--commit', w, '--write']);
  assert.equal(killed.signal, 'SIGKILL', killed.stderr);
  assert.equal(readFileSync(target, 'utf8'), old, 'the old list is whole, never half of the new one');
  assert.equal(leftovers(f).filter(name => !name.endsWith('.owner')).length, 1, 'only an owned temporary file is left');
  const restarted = await f.run('hashes', ['--commit', w, '--write']);
  assert.equal(restarted.exit, 0, said(restarted));
  assert.deepEqual(leftovers(f), []);
  assert.equal(readFileSync(target, 'utf8'), (await f.run('hashes', ['--commit', w])).stdout, 'the written list is the printed one');
});

test('A_restart_leaves_a_temporary_file_whose_owner_still_runs_and_names_it', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const sleeper = spawn(process.execPath, ['-e', 'setTimeout(() => {}, 60000)'], { stdio: 'ignore' });
  t.after(() => sleeper.kill('SIGKILL'));
  const name = `${owned.ownedName('hashes')}.tmp`;
  const alive = path.join(f.inputs, name);
  writeFileSync(alive, 'a half list of a run that is still going\n');
  writeFileSync(`${alive}.owner`, `${formatOwner({ ...thisOwner(), pid: sleeper.pid, start: processProbe(sleeper.pid).start })}\n`);
  const result = await f.run('hashes', ['--commit', w, '--write']);
  assert.equal(result.exit, 0, said(result));
  assert.ok(said(result).includes(name), 'the restart names the file it left');
  assert.equal(existsSync(alive), true, 'a file whose owner still runs is left');
  assert.equal(existsSync(`${alive}.owner`), true);
  assert.equal(existsSync(path.join(f.inputs, 'hashes-lang.txt')), true, 'and the write went on');
  assert.deepEqual(leftovers(f).filter(entry => !entry.startsWith(name)), [], 'nothing else of the tool is left behind');
});

test('A_rerun_after_an_interruption_writes_the_same_bytes', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  await f.run('hashes', ['--commit', w, '--write']);
  const flags = ['--tag', TAG, '--commit', w, '--evidence', evidence(f, { c, w }), '--workspaces-check', workspacesCheckFile(f, w)];
  assert.equal(killedBeforeRename(f, 'record', flags).signal, 'SIGKILL');
  assert.equal((await record(f, { c, w })).exit, 0);
  const afterInterruption = readFileSync(statesFile(f));
  rmSync(statesFile(f));
  assert.equal((await record(f, { c, w })).exit, 0, 'a clean run, nothing interrupted');
  assert.deepEqual(readFileSync(statesFile(f)), afterInterruption, 'the same bytes after the interruption and after a clean run');
  const list = path.join(f.inputs, 'hashes-lang.txt');
  assert.equal(killedBeforeRename(f, 'hashes', ['--commit', w, '--write']).signal, 'SIGKILL');
  assert.equal((await f.run('hashes', ['--commit', w, '--write'])).exit, 0);
  const listAfterInterruption = readFileSync(list);
  rmSync(list);
  assert.equal((await f.run('hashes', ['--commit', w, '--write'])).exit, 0);
  assert.deepEqual(readFileSync(list), listAfterInterruption);
  assert.deepEqual(leftovers(f), []);
});

// ---- a record already in the file ------------------------------------------------------------------------------------------------------------------

test('A_record_already_in_the_file_for_the_tag_is_rewritten_only_with_identical_bytes_and_any_difference_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  assert.equal((await record(f, { c, w })).exit, 0);
  const first = readFileSync(statesFile(f));
  assert.equal((await record(f, { c, w })).exit, 0, 'identical inputs: the same bytes are written again');
  assert.deepEqual(readFileSync(statesFile(f)), first);
  const differentEvidence = await record(f, { c, w, over: { workspacesCheck: { commit: w, exit: 0, date: '2026-12-09' } } });
  assert.equal(differentEvidence.exit, 3, said(differentEvidence));
  assert.match(differentEvidence.stderr, /already holds.*different|differs from/);
  assert.deepEqual(readFileSync(statesFile(f)), first, 'a refusal leaves the record as it was');
  const w2 = commit(f.repo, 'a later workspace file', { 'node/04-rule-api/more.txt': 'later\n' });
  git(f.repo, 'push', '-q', 'origin', 'main');
  const differentW = await record(f, { c, w: w2 });
  assert.notEqual(differentW.exit, 0, `a later commit as W under the same tag: ${said(differentW)}`);
  assert.deepEqual(readFileSync(statesFile(f)), first);
  assert.equal(leftovers(f).length, 0, 'no temporary file is left by a refusal');
});

// ---- a short write is never published ----------------------------------------------------------------------------------------------------------------

/** A `writeSync` that writes at most `chunk` bytes per call and says so honestly (a legal short write). `lie` makes it report the whole length while writing only the chunk (a full disk that does not say so). */
const shortWrite = ({ chunk, lie = false }) => (fd, data, offset = 0, length = undefined) => {
  const bytes = typeof data === 'string' ? Buffer.from(data) : data;
  const wanted = length ?? bytes.length - offset;
  const written = writeSync(fd, bytes, offset, Math.min(wanted, chunk));
  return lie ? wanted : written;
};

test('A_writer_that_is_given_short_chunks_loops_until_every_byte_is_written_and_publishes_the_whole_list', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const whole = (await f.run('hashes', ['--commit', w])).stdout;
  const result = await f.run('hashes', ['--commit', w, '--write'], { io: { writeSync: shortWrite({ chunk: 7 }) } });
  assert.equal(result.exit, 0, said(result));
  assert.equal(readFileSync(path.join(f.inputs, 'hashes-lang.txt'), 'utf8'), whole, 'all bytes, though each call wrote seven');
  assert.deepEqual(leftovers(f), []);
});

test('A_short_write_that_is_not_reported_is_caught_by_reading_the_temporary_file_back_and_nothing_is_published', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  const old = `${'0'.repeat(64)}  csharp/.gitkeep\n`;
  writeFileSync(path.join(f.inputs, 'hashes-lang.txt'), old);
  const lied = await f.run('hashes', ['--commit', w, '--write'], { io: { writeSync: shortWrite({ chunk: 7, lie: true }) } });
  assert.notEqual(lied.exit, 0, 'the command fails');
  assert.match(said(lied), /read back|holds \d+ bytes/);
  assert.equal(readFileSync(path.join(f.inputs, 'hashes-lang.txt'), 'utf8'), old, 'the old list is whole, no truncated file was published');
  assert.deepEqual(leftovers(f), [], 'and no temporary file is left');
  await f.run('hashes', ['--commit', w, '--write']);
  const flags = ['--tag', TAG, '--commit', w, '--evidence', evidence(f, { c, w }), '--workspaces-check', workspacesCheckFile(f, w)];
  const recorded = await f.run('record', flags, { io: { writeSync: shortWrite({ chunk: 7, lie: true }) } });
  assert.notEqual(recorded.exit, 0);
  assert.equal(existsSync(statesFile(f)), false, 'no truncated record');
});

test('record_refuses_a_gate_record_missing_from_the_inputs_naming_each_spec', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  rmSync(path.join(f.inputs, 'gates', 'spec-05.md'));
  rmSync(path.join(f.inputs, 'gates', 'spec-10.md'));
  const refused = await record(f, { c, w });
  assert.equal(refused.exit, 3, said(refused));
  assert.ok(refused.stderr.includes('05-key-value-server') && refused.stderr.includes('10-dag-scheduler'), refused.stderr);
  assert.equal(existsSync(statesFile(f)), false);
});

// ---- the review's findings of PR 4 (B1, the minors) --------------------------------------------------------------------------------------------------

test('record_refuses_a_W_that_is_not_on_the_remotes_main_or_whose_parent_is_not_C_and_passes_the_squash_W', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  pushTag(f);
  const branch = workspacesBranch(f, { from: c });
  const head = git(f.repo, 'rev-parse', branch);
  const unmerged = await record(f, { c, w: head });
  assert.equal(unmerged.exit, 1, `the T2 branch head, on no remote ref: ${said(unmerged)}`);
  assert.match(unmerged.stderr, /remote's main/);
  assert.equal(existsSync(statesFile(f)), false);
  const w = squashMerge(f, branch, 'T2');
  const later = commit(f.repo, 'another pull request after W', { 'docs/after.md': 'x\n' });
  git(f.repo, 'push', '-q', 'origin', 'main');
  const notChild = await record(f, { c, w: later });
  assert.equal(notChild.exit, 1, `a commit above W: ${said(notChild)}`);
  assert.match(notChild.stderr, /parent/);
  assert.equal((await record(f, { c, w })).exit, 0, 'the squash W, on main with C as its parent, passes');
});

test('record_refuses_a_hash_list_on_disk_that_is_not_the_list_of_W_and_names_hashes_write', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  const absent = await record(f, { c, w, list: false });
  assert.equal(absent.exit, 1, said(absent));
  assert.match(absent.stderr, /hashes --write/);
  writeFileSync(path.join(f.inputs, 'hashes-lang.txt'), `${'0'.repeat(64)}  csharp/.gitkeep\n`);
  const stale = await record(f, { c, w, list: false });
  assert.equal(stale.exit, 1, said(stale));
  assert.match(stale.stderr, /hashes --write/);
  assert.equal(existsSync(statesFile(f)), false);
  assert.equal((await record(f, { c, w })).exit, 0, 'the list written for W: the control');
});

test('record_cross_checks_the_evidence_commits_and_the_order_of_its_dates', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  const done = (commitSha, date) => ({ commit: commitSha, exit: 0, date });
  const cases = [
    ['a scratch W″ that is C', { leak: { scratchWorkspace: done(c, '2026-12-01') } }, /scratchWorkspace/],
    ['a scratch W″ that is W', { leak: { scratchWorkspace: done(w, '2026-12-01') } }, /scratchWorkspace/],
    ['the scratch check dated before the check of C', { leak: { freezeCommit: done(c, '2026-12-01'), scratchWorkspace: done(SCRATCH, '2026-11-30') } }, /scratchWorkspace.*freezeCommit|freezeCommit.*scratchWorkspace/],
    ['the push before the scratch check', { leak: { scratchWorkspace: done(SCRATCH, '2026-12-01') }, over: { provisionalFrom: '2026-11-30' } }, /provisionalFrom/],
    ['the push after the check of W', { over: { provisionalFrom: '2026-12-03' } }, /provisionalFrom/],
    ['the push before the tag\'s own date', { over: { provisionalFrom: '2026-11-30' }, leak: { freezeCommit: done(c, '2026-11-29'), scratchWorkspace: done(SCRATCH, '2026-11-29') } }, /tag/],
  ];
  for (const [what, options, pattern] of cases) {
    const refused = await record(f, { c, w, ...options });
    assert.equal(refused.exit, 3, `${what}: ${said(refused)}`);
    assert.match(refused.stderr, pattern, what);
  }
  assert.equal(existsSync(statesFile(f)), false);
  assert.equal((await record(f, { c, w })).exit, 0, 'the control: the dates in the order of the procedure');
});

test('record_refuses_a_spec_with_no_requirement_ids_and_a_tag_superseded_twice', async t => {
  const noIds = baseFiles({ specs: Object.fromEntries(['04', '05', '06', '07', '08', '10'].map(nn => [nn, specText(nn).replaceAll('**R', '**Q')])) });
  const f = makeFreezeRepo(t, { files: noIds });
  const { c, w } = fullFreeze(f);
  const empty = await record(f, { c, w });
  assert.equal(empty.exit, 3, said(empty));
  assert.match(empty.stderr, /requirement ids/);
  assert.equal(existsSync(statesFile(f)), false);
  const g = makeFreezeRepo(t);
  const { c2, w2 } = failureAndRecovery(g);
  const twice = await record(g, { c: c2, w: w2, tag: LATER_TAG, extra: ['--supersedes', TAG, '--why', 'one', '--supersedes', TAG, '--why', 'two'] });
  assert.equal(twice.exit, 3, said(twice));
  assert.match(twice.stderr, /twice|more than once/);
});

test('The_writer_syncs_the_folder_after_the_rename_so_the_new_name_survives_a_power_loss', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const events = [];
  const dirFds = new Set();
  const io = {
    renameSync: (from, to) => { events.push('rename'); return renameSync(from, to); },
    openSync: (file, ...rest) => { const fd = openSync(file, ...rest); if (file === f.inputs) { dirFds.add(fd); events.push('open-dir'); } return fd; },
    fsyncSync: fd => { events.push(dirFds.has(fd) ? 'fsync-dir' : 'fsync-file'); return fsyncSync(fd); },
  };
  const result = await f.run('hashes', ['--commit', w, '--write'], { io });
  assert.equal(result.exit, 0, said(result));
  assert.ok(events.indexOf('fsync-dir') > events.indexOf('rename') && events.indexOf('rename') > events.indexOf('fsync-file'), `the file is fsynced, renamed, then its folder is fsynced: ${events.join(' ')}`);
});

// ---- coai code round 4 -------------------------------------------------------------------------------------------------------------------------------

test('The_writer_syncs_the_targets_folder_and_the_staging_folder_after_the_rename_for_record_too', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  await f.run('hashes', ['--commit', w, '--write']);
  const synced = [];
  const fdToDir = new Map();
  const io = {
    openSync: (file, ...rest) => { const fd = openSync(file, ...rest); if (rest[0] === 'r') fdToDir.set(fd, file); return fd; },
    fsyncSync: fd => { if (fdToDir.has(fd)) synced.push(fdToDir.get(fd)); return fsyncSync(fd); },
  };
  const flags = ['--tag', TAG, '--commit', w, '--evidence', evidence(f, { c, w }), '--workspaces-check', workspacesCheckFile(f, w)];
  const result = await f.run('record', flags, { io });
  assert.equal(result.exit, 0, said(result));
  assert.ok(synced.includes(path.join(f.inputs, 'specs.d')), `the folder of the target (specs.d) was fsynced: ${synced.join(', ')}`);
  assert.ok(synced.includes(f.inputs), `and the staging folder (inputs): ${synced.join(', ')}`);
});

test('Every_date_of_the_evidence_must_be_a_real_calendar_day_and_a_leap_day_passes', t => {
  const dir = path.join(owned.ownedDir(t, 'dates'));
  const file = path.join(dir, 'evidence.json');
  const [c, w] = ['c'.repeat(40), 'd'.repeat(40)];
  const body = (day, over = {}) => ({ provisionalFrom: day, workspacesCheck: { commit: w, exit: 0, date: day }, leakcheck: { freezeCommit: { commit: c, exit: 0, date: day }, scratchWorkspace: { commit: 'e'.repeat(40), exit: 0, date: day }, workspaceCommit: { commit: w, exit: 0, date: day } }, ...over });
  const read = (value, tag = TAG) => { writeFileSync(file, JSON.stringify(value)); return readEvidence({ file, commit: w, freeze: c, tag }); };
  assert.equal(read(body('2024-02-29'), 'spec-frozen-B-20240229').ok, true, 'a leap day is a real day');
  const later = '2027-01-01';
  for (const bad of ['2026-12-99', '2026-12-32']) {
    const pushed = read(body(later, { provisionalFrom: bad }));
    assert.equal(pushed.ok, false, `provisionalFrom ${bad}`);
    assert.match(pushed.reason, /provisionalFrom is not/, `${bad} is no calendar day, whatever its order`);
  }
  for (const bad of ['2026-02-30', '2025-02-29', '2026-00-10']) {
    const dated = read(body('2026-12-01', { leakcheck: { freezeCommit: { commit: c, exit: 0, date: bad }, scratchWorkspace: { commit: 'e'.repeat(40), exit: 0, date: '2026-12-01' }, workspaceCommit: { commit: w, exit: 0, date: '2026-12-01' } } }));
    assert.equal(dated.ok, false, `a result dated ${bad}`);
    assert.match(dated.reason, /freezeCommit\.date is not/, `${bad} is no calendar day, whatever its order`);
  }
});

test('An_unreadable_message_file_is_one_sentence_and_exit_4_for_both_tag_checks', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const intended = annotate(f, { at: c });
  pushTag(f);
  const folder = path.join(f.root, 'a-folder');
  mkdirSync(folder);
  const locked = path.join(f.root, 'locked.message');
  writeFileSync(locked, 'x\n');
  chmodSync(locked, 0o000);
  t.after(() => { try { chmodSync(locked, 0o600); } catch { /* the folder is already gone */ } });
  for (const [what, file] of [['missing', path.join(f.root, 'absent.message')], ['a directory', folder], ['no permission', locked]]) {
    for (const verb of ['check-tag', 'check-local-tag']) {
      const flags = verb === 'check-tag' ? ['--tag', TAG, '--commit', c, '--message-file', file] : ['--tag', TAG, '--commit', c, '--message-file', file];
      const result = await f.run(verb, flags);
      assert.equal(result.exit, 4, `${verb} with ${what}: ${said(result)}`);
      assert.match(result.stderr, /^freeze: [a-z-]+: the --message-file .* cannot be read \((ENOENT|EISDIR|EACCES)\)\n$/, `${verb} with ${what}: one sentence`);
    }
  }
  assert.equal((await f.run('check-tag', ['--tag', TAG, '--commit', c, '--message-file', intended])).exit, 0, 'the control: the intended file');
});
