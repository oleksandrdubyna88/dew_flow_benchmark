// What F3.S1's pre-registration guard reads of the frozen record (F2.S2 (d), D67 (2), amendment 3): ONE table of the fields, each with the file it comes from and what makes it valid, so the guard and the test that holds the record
// to it read the same list. A field the table names that the record lacks, or holds in another shape, is a sentence; a field with no source fails.
//   specStates   `spikes/lang-bench/inputs/specs.d/spec-states.json`  { specs: { NN: row }, freeze: { … } }   written by `freeze.mjs record`
//   heldoutFrozen `spikes/lang-explore/inputs/heldout-frozen.json`   { tasks: { NN: entry } }                    written by the seal
//   memoryLimit  `spikes/lang-explore/inputs/memory-limit.json`      { …, "registration": the date and the twelve references }  written by `register-memory`
// The spec rows and the `freeze` entry are the plan's text (F2.S2 (d) `spec-states.json`); `freeze.workspacesCheck` is amendment 3's: the clean result of `explore.mjs workspaces --check --root` at W.
import { FREEZE_TASKS } from './workspaces-sources.mjs';

const HEX40 = /^[0-9a-f]{40}$/;
const HEX64 = /^[0-9a-f]{64}$/;
const isText = v => typeof v === 'string' && v.trim() !== '';
const isHex = re => v => typeof v === 'string' && re.test(v);
const isDate = v => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v);
const isCount = v => Number.isSafeInteger(v) && v >= 0;
const is = expected => v => v === expected;

const sha1 = isHex(HEX40);
const sha256 = isHex(HEX64);

const spec = (field, valid, what) => ({ file: 'specStates', path: `specs.{task}.${field}`, valid, what });
const freeze = (field, valid, what) => ({ file: 'specStates', path: `freeze.${field}`, valid, what });
const held = (field, valid, what) => ({ file: 'heldoutFrozen', path: `tasks.{task}.${field}`, valid, what });
const leak = name => [freeze(`leakcheck.${name}.commit`, sha1, `the commit of the clean ${name} leakcheck`), freeze(`leakcheck.${name}.exit`, is(0), `the exit of the ${name} leakcheck (0)`), freeze(`leakcheck.${name}.date`, isDate, `the date of the ${name} leakcheck`)];

/** The fields, in the order a guard reads them. `{task}` stands for each of the six tasks. */
export const FREEZE_READS = Object.freeze([
  spec('path', isText, 'the spec\'s path'), spec('sha256', sha256, 'the spec\'s hash at the tag'), spec('state', is('frozen'), 'the spec\'s state (frozen)'), spec('gate', isText, 'the spec\'s gate record'),
  spec('rIds', v => Array.isArray(v) && v.length > 0, 'the spec\'s requirement ids'), spec('commitSha', sha1, 'the commit the spec was frozen at'), spec('tagObjectSha', sha1, 'the tag object the spec was frozen under'),
  freeze('tag', isHex(/^spec-frozen-B-\d{8}$/), 'the freeze tag'), freeze('tagObjectSha', sha1, 'the tag object'), freeze('commitSha', sha1, 'the tag\'s commit (C)'), freeze('workspaceCommit', sha1, 'the workspace commit (W)'),
  freeze('formatSha256', sha256, 'the frozen FORMAT\'s hash'), freeze('hashesLangSha256', sha256, 'the hash list\'s hash'), freeze('state', is('accepted'), 'the freeze\'s state (accepted)'),
  freeze('provisionalFrom', isDate, 'the date of the push'), freeze('acceptedOn', isDate, 'the date W was accepted'), freeze('superseded', Array.isArray, 'the list of superseded tags'),
  ...leak('freezeCommit'), ...leak('scratchWorkspace'), ...leak('workspaceCommit'),
  freeze('workspacesCheck.commit', sha1, 'the commit `explore.mjs workspaces --check` passed at (W)'), freeze('workspacesCheck.exit', is(0), 'the exit of that check (0)'), freeze('workspacesCheck.date', isDate, 'the date of that check'),
  held('archiveSha256', sha256, 'the archive\'s hash'), held('treeSha256', sha256, 'the clear tree\'s hash'), held('caseCount', isCount, 'the case count'), held('trapCount', isCount, 'the trap count'),
  held('manifestSha256', sha256, 'the trap table\'s hash'),
  { file: 'memoryLimit', path: 'registration.date', valid: isDate, what: 'the memory value\'s registration date' },
  { file: 'memoryLimit', path: 'registration.references', valid: v => Array.isArray(v) && v.length === 12, what: 'the twelve references the memory value was registered with' },
]);

/** The paths with `{task}` filled in for each of the six tasks. -> `[{ file, path, valid, what }]`. */
export const expandedReads = () => FREEZE_READS.flatMap(r => (r.path.includes('{task}') ? FREEZE_TASKS.map(id => ({ ...r, path: r.path.replace('{task}', id) })) : [r]));

const valueAt = (root, path) => path.split('.').reduce((v, key) => (v === undefined || v === null ? undefined : v[key]), root);

/** The cross-field rule: the workspaces check passed at the workspace commit itself. */
const crossProblems = record => {
  const f = record.specStates?.freeze;
  return f?.workspacesCheck?.commit !== undefined && f.workspacesCheck.commit !== f.workspaceCommit ? ['specStates: freeze.workspacesCheck.commit is not freeze.workspaceCommit: the check passed at another commit than W'] : [];
};

/** The sentences for every field of the table that the record lacks or holds in another shape. `record` is `{ specStates, heldoutFrozen, memoryLimit }`. Empty when the record holds everything a guard reads. */
export function freezeProblems(record) {
  const own = expandedReads().flatMap(r => {
    const v = valueAt(record?.[r.file], r.path);
    if (v === undefined) return [`${r.file}: ${r.path} is absent (${r.what})`];
    return r.valid(v) ? [] : [`${r.file}: ${r.path} is not valid (${r.what})`];
  });
  return [...own, ...crossProblems(record)];
}
