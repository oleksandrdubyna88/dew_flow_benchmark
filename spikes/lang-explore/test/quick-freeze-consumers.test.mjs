// Every consumer of the frozen bytes (F2.S2 (d), D67 (2), amendment 3): the frozen record holds every field F3.S1's pre-registration guard will read. The guard itself is built in F3.S1; here a test stub of it reads a SYNTHETIC record
// through `lib/freeze-consumers.mjs`'s table, and a field with no source fails. The held-out entries are made by the real seal's `entryOfSeal` and the memory registration by the real `registerMemoryValue`, so those two files are held
// to the fields the writers actually write; the spec rows and the `freeze` entry are the plan's text and amendment 3 (the writer is `freeze.mjs record`, the lang-bench half). Invented values only.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { entryOfSeal } from '../lib/heldout-registry.mjs';
import { FREEZE_READS, expandedReads, freezeProblems } from '../lib/freeze-consumers.mjs';
import { registerMemoryValue } from '../lib/memory-limit.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';
import { folder } from './quick-freeze-fixture.mjs';

const hex = (n, c) => c.repeat(n);
const W = hex(40, 'c');

const specRow = id => ({ path: `todo/tasks/${id}-invented.md`, sha256: hex(64, 'a'), state: 'frozen', gate: `inputs/gates/spec-${id}.md`, rIds: ['R1', 'R2'], commitSha: hex(40, 'b'), tagObjectSha: hex(40, 'd') });
const leak = commit => ({ commit, exit: 0, date: '2026-11-20' });
const sealEntry = id => entryOfSeal({ archive: `heldout-${id}/suite.enc`, archiveSha256: hex(64, '1'), treeSha256: hex(64, '2'), caseCount: 19, trapCount: 7, manifestSha256: hex(64, '3'), keySha256: hex(64, '4'), sealedOn: '2026-11-12' });

/** The memory-limit file as `register-memory` writes it after all twelve references passed: the real writer over invented package entries. */
function registeredMemory(t) {
  const file = path.join(folder(t, 'wsmem'), 'memory-limit.json');
  writeFileSync(file, JSON.stringify({ bytes: 536870912, mib: 512, decidedOn: '2026-11-07', by: 'an invented decision', note: 'invented' }));
  const packages = Object.fromEntries(FREEZE_TASKS.map(id => [id, { memoryBytes: 536870912, referenceVerdicts: { csharp: 'accepted', node: 'accepted' }, refsTreeSha256: { csharp: hex(64, '5'), node: hex(64, '6') } }]));
  assert.equal(registerMemoryValue({ packages, file, now: () => new Date('2026-11-14T10:00:00Z') }).value, 'registered');
  return JSON.parse(readFileSync(file, 'utf8'));
}

/** A complete synthetic record of the three files. */
const recordFor = t => ({
  specStates: {
    specs: Object.fromEntries(FREEZE_TASKS.map(id => [id, specRow(id)])),
    freeze: {
      tag: 'spec-frozen-B-20261120', tagObjectSha: hex(40, 'd'), commitSha: hex(40, 'b'), workspaceCommit: W, formatSha256: hex(64, 'e'), hashesLangSha256: hex(64, 'f'),
      leakcheck: { freezeCommit: leak(hex(40, 'b')), scratchWorkspace: leak(hex(40, '9')), workspaceCommit: leak(W) },
      state: 'accepted', provisionalFrom: '2026-11-20', acceptedOn: '2026-11-21', superseded: [], workspacesCheck: { commit: W, exit: 0, date: '2026-11-21' },
    },
  },
  heldoutFrozen: { tasks: Object.fromEntries(FREEZE_TASKS.map(id => [id, sealEntry(id)])) },
  memoryLimit: registeredMemory(t),
});

/** F3.S1's guard as a stub: it reads the table's fields from the record and fails naming each one it cannot read. */
const guardStub = record => { const problems = freezeProblems(record); return problems.length === 0 ? { ok: true } : { ok: false, reason: problems.join('; ') }; };

/** What the plan and amendment 3 say the guard reads, as paths of the three files; the table must hold each. */
const REQUIRED = [
  'specStates:specs.04.sha256', 'specStates:specs.10.state', 'specStates:freeze.tag', 'specStates:freeze.commitSha', 'specStates:freeze.workspaceCommit', 'specStates:freeze.hashesLangSha256',
  'heldoutFrozen:tasks.05.archiveSha256', 'heldoutFrozen:tasks.05.treeSha256', 'heldoutFrozen:tasks.05.caseCount', 'heldoutFrozen:tasks.05.trapCount', 'heldoutFrozen:tasks.05.manifestSha256',
  'memoryLimit:registration.date', 'memoryLimit:registration.references',
  'specStates:freeze.workspacesCheck.commit', 'specStates:freeze.workspacesCheck.exit', 'specStates:freeze.workspacesCheck.date',
];

const without = (record, file, dotted) => {
  const copy = structuredClone(record);
  const keys = dotted.split('.');
  const last = keys.pop();
  delete keys.reduce((v, k) => v[k], copy[file])[last];
  return copy;
};

test('The_frozen_record_holds_every_field_the_pre_registration_guard_will_read', t => {
  const record = recordFor(t);
  assert.deepEqual(guardStub(record), { ok: true }, freezeProblems(record).join('\n'));
  const named = new Set(expandedReads().map(r => `${r.file}:${r.path}`));
  for (const want of REQUIRED) assert.equal(named.has(want), true, `the table names ${want}`);
  assert.ok(FREEZE_READS.some(r => r.path === 'freeze.workspacesCheck.commit'), 'amendment 3: the workspaces check is a field the guard reads');
});

test('A_field_with_no_source_fails_the_guard_naming_it', t => {
  const record = recordFor(t);
  for (const r of expandedReads()) {
    const result = guardStub(without(record, r.file, r.path));
    assert.equal(result.ok, false, `${r.file}:${r.path} removed: the guard fails`);
    assert.match(result.reason, new RegExp(`${r.file}: ${r.path.replaceAll('.', '\\.')} is absent`), `${r.path} is named`);
  }
});

test('A_field_in_another_shape_or_a_check_that_passed_at_another_commit_fails_the_guard', t => {
  const record = recordFor(t);
  const bad = (file, mutate) => { const copy = structuredClone(record); mutate(copy[file]); return guardStub(copy); };
  assert.equal(bad('specStates', s => { s.freeze.workspacesCheck.exit = 1; }).ok, false, 'a check that exited 1');
  assert.equal(bad('specStates', s => { s.freeze.workspacesCheck.commit = hex(40, '7'); }).ok, false, 'a check at another commit than W');
  assert.equal(bad('specStates', s => { s.freeze.state = 'provisional'; }).ok, false, 'a freeze that is not accepted');
  assert.equal(bad('specStates', s => { s.specs['06'].state = 'gated'; }).ok, false, 'a spec that is not frozen');
  assert.equal(bad('heldoutFrozen', h => { h.tasks['07'].caseCount = '19'; }).ok, false, 'a count that is text');
  assert.equal(bad('memoryLimit', m => { m.registration.references.pop(); }).ok, false, 'a registration with eleven references');
});

test('The_held_out_entry_and_the_memory_registration_the_real_writers_make_hold_the_fields_the_table_names', t => {
  const record = recordFor(t);
  const entry = record.heldoutFrozen.tasks['04'];
  for (const field of ['archiveSha256', 'treeSha256', 'caseCount', 'trapCount', 'manifestSha256']) assert.equal(Object.hasOwn(entry, field), true, `the seal's entry holds ${field}`);
  assert.deepEqual(Object.keys(record.memoryLimit.registration).sort(), ['date', 'references'], 'the registration is { date, references }');
  assert.equal(record.memoryLimit.registration.references.length, 12);
});
