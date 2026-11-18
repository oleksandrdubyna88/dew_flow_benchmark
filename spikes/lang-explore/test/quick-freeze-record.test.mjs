// The one contract between the two halves of F2.S2 (d): the record that `freeze.mjs record` (lang-bench) writes must hold every `specStates` field that F3.S1's pre-registration guard reads, as this half's table
// (`lib/freeze-consumers.mjs`) states them. lang-bench imports nothing of lang-explore, so the check lives here: it runs lang-bench's real `record` on lang-bench's synthetic task-repository fixture (invented specs, a local bare
// repository, no real file, no network) and hands the file it wrote to `freezeProblems`. The held-out and memory files are not part of this contract and are left out of the comparison.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { evidence, fullFreeze, makeFreezeRepo, TAG, workspacesCheckFile } from '../../lang-bench/test/support/freeze-fixtures.mjs';
import { freezeProblems } from '../lib/freeze-consumers.mjs';

/** The sentences about `specStates` only: the record is read as the guard reads it, the two other files being another writer's. */
const specStatesProblems = specStates => freezeProblems({ specStates }).filter(problem => problem.startsWith('specStates:'));

test('A_record_written_by_freeze_record_holds_every_field_the_pre_registration_guard_reads_of_it', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  const written = await f.run('record', ['--tag', TAG, '--commit', w, '--evidence', evidence(f, { c, w }), '--workspaces-check', workspacesCheckFile(f, w)]);
  assert.equal(written.exit, 0, `${written.stdout}${written.stderr}`);
  const specStates = JSON.parse(readFileSync(path.join(f.inputs, 'specs.d', 'spec-states.json'), 'utf8'));
  assert.deepEqual(specStatesProblems(specStates), [], 'the writer and the consumers\' table agree');
  assert.deepEqual(Object.keys(specStates.freeze.leakcheck.freezeCommit).sort(), ['commit', 'date', 'exit'], 'each leak-check result is { commit, exit, date }');
  assert.equal(specStates.freeze.workspacesCheck.commit, specStates.freeze.workspaceCommit);
  const broken = structuredClone(specStates);
  delete broken.freeze.leakcheck.scratchWorkspace.date;
  assert.ok(specStatesProblems(broken).some(problem => problem.includes('leakcheck.scratchWorkspace.date')), 'the table does read the field: a record without it is named');
});
