// The defect witnesses of a package (F2.S1, P5): one isolated witness per trap and stack, an OVERLAY on its stack's reference (the files it replaces or adds, never a whole tree, never a frozen file, never equal to the reference). A
// witness must build (one that does not is not caught), its stack's reference must pass (P3, checked by the run before witnesses start), and the cases registered for its trap must reject it.
//   deterministic  ONE pass on the 2-CPU profile; caught when it fails every registered case and no other case.
//   race           TEN passes, five per profile, like a session (`scorePasses`, the pass driver, called as it is); judged only on its FAILING passes: each failing pass fails at least one registered case and none outside them, and at least one
//                  pass fails; the clean passes are allowed and kept. A pass's verdict is read from its per-pass case sets, never from `verdict.json` (which is a session's).
// A timeout or a hang of a case counts as that case failing; an extra failure (a case beyond the table, a hang outside the trap) is a mapping to fix and the witness is NOT caught, with the actual set kept in the record. Builds go through
// `buildAndRestore` (inside `commitBuild`), passes through `runPassChild`, cases through the runner, which kills each program's cgroup: no spawn path is written here, so a witness that hangs or leaves a grandchild leaves no process behind.
import { cpSync, mkdirSync, readFileSync, existsSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { isFrozen } from '../../lang-bench/lib/frozen-set.mjs';
import { readBuildRecord } from './build-commit.mjs';
import { dirBytes } from './measure.mjs';
import { maxOf } from './package-max.mjs';
import { onePass, wrapPass } from './package-pass.mjs';
import { registeredOf } from './package-manifest.mjs';
import { assembleReference, listFolder } from './package-quick.mjs';
import { scorePasses } from './passes.mjs';
import { removeArea } from './score.mjs';
import { readAttempts } from './score-files.mjs';
import { runnerRecordPinNow } from './harness-pin.mjs';
import { BENCH_TEMPLATES } from './workspace-quick.mjs';

const sameBytes = (a, b) => a !== null && b !== null && a.equals(b);
const bytesOrNull = file => (existsSync(file) ? readFileSync(file) : null);

/** The bytes the reference tree has at `rel`: the reference's own file, else the template's, else null (the overlay adds the file). */
const referenceBytes = ({ refDir, template, rel }) => bytesOrNull(path.join(refDir, rel)) ?? bytesOrNull(path.join(template, rel));

/** Each way an overlay is not one, as `[holds(a), the sentence]`, in order. `a` = `{ overlay, overlayDir, stack, template, refDir }`. */
const NOT_AN_OVERLAY = Object.freeze([
  [a => a.overlay.problems.length > 0, a => `the overlay holds ${a.overlay.problems[0]}`],
  [a => a.overlay.files.length === 0, () => 'the overlay is empty: the witness changes nothing'],
  [a => listFolder(a.template).files.every(rel => a.overlay.files.includes(rel)), () => 'the folder holds every file of the template: it is a whole tree, not an overlay'],
  [a => a.overlay.files.some(rel => isFrozen(rel, a.stack)), a => `the overlay holds the frozen file ${a.overlay.files.find(rel => isFrozen(rel, a.stack))}: a witness never replaces or adds a frozen file`],
  [a => a.overlay.files.every(rel => sameBytes(bytesOrNull(path.join(a.overlayDir, rel)), referenceBytes({ refDir: a.refDir, template: a.template, rel }))), () => 'the overlay equals the reference byte for byte: the witness changes nothing'],
]);

/** The sentence refusing an overlay that is not one, or null: a link, nothing in it, the whole template, a frozen file, or only bytes the reference already has. */
const overlayProblem = a => NOT_AN_OVERLAY.find(([holds]) => holds(a))?.[1](a) ?? null;

/**
 * The witness tree: the reference assembled on its template (`assembleReference`), then the overlay's files over it, in an owned temporary folder of `scope`. An overlay that is empty, a whole tree, holds a link or a frozen file, or only
 * what the reference has, is refused naming the reason, and nothing is assembled. -> `ok({ dir, files })` | `fail`.
 */
export function assembleWitness({ templates = BENCH_TEMPLATES, stack, kind = 'daemon', refDir, overlayDir, scope, parent }) {
  const overlay = listFolder(overlayDir);
  const refused = overlayProblem({ overlay, overlayDir, stack, template: path.join(templates, stack, kind), refDir });
  if (refused) return fail(refused);
  const tree = assembleReference({ templates, stack, kind, refDir, scope, parent });
  if (!tree.ok) return tree;
  for (const rel of overlay.files) { mkdirSync(path.dirname(path.join(tree.value.dir, rel)), { recursive: true }); cpSync(path.join(overlayDir, rel), path.join(tree.value.dir, rel)); }
  return ok({ dir: tree.value.dir, files: listFolder(tree.value.dir).files });
}

// ---- the verdict ------------------------------------------------------------------------------------------------------------------------------------

const HANG = /time|deadline|hang|stop|bound/i;
const failingCases = p => p.cases.filter(c => c.verdict !== 'pass');
const idsOf = cases => cases.map(c => c.id).sort();
const unique = list => [...new Set(list)].sort();

/** The counts of a judgement: `registered`, `failedRegistered`, `missing`, `beyond`, `hangsBeyond`, `failingPasses`, `cleanPasses` (counts only). */
function countsOf({ registered, passes }) {
  const failing = passes.filter(p => failingCases(p).length > 0);
  const failed = unique(failing.flatMap(p => idsOf(failingCases(p))));
  const beyond = failed.filter(id => !registered.includes(id));
  const hangsBeyond = unique(failing.flatMap(p => failingCases(p).filter(c => !registered.includes(c.id) && HANG.test(c.reason ?? '')).map(c => c.id)));
  const failedRegistered = failed.filter(id => registered.includes(id)).length;
  return { registered: registered.length, failedRegistered, missing: registered.length - failedRegistered, beyond: beyond.length, hangsBeyond: hangsBeyond.length, failingPasses: failing.length, cleanPasses: passes.length - failing.length };
}

const beyondSentence = c => (c.hangsBeyond > 0
  ? `a hang outside the trap: ${c.hangsBeyond} case(s) beyond the table ended by a timeout (registered ${c.registered}, failed ${c.failedRegistered}, beyond ${c.beyond}): a mapping to fix, never allowed silently`
  : `${c.beyond === 1 ? 'one case' : `${c.beyond} cases`} beyond the table failed (registered ${c.registered}, failed ${c.failedRegistered}, beyond ${c.beyond}): a mapping to fix, never allowed silently`);

/** Each way a witness is not caught, as `[holds({ kind, counts }), reason code, sentence(counts)]`, in order. */
const NOT_CAUGHT = Object.freeze([
  [({ counts }) => counts.beyond > 0, 'beyond the table', beyondSentence],
  [({ kind, counts }) => kind === 'race' && counts.failingPasses === 0, 'no catching case', c => `a trap with no catching case: ${c.cleanPasses} pass(es) and none failed a registered case`],
  [({ kind, counts }) => kind === 'deterministic' && counts.missing > 0, 'registered case passed', c => `registered ${c.registered}, failed ${c.failedRegistered}, missing ${c.missing}`],
]);

/** `[reason code, sentence]` for a witness that is not caught, or null when it is caught. */
function notCaught(a) {
  const hit = NOT_CAUGHT.find(([holds]) => holds(a));
  return hit ? [hit[1], hit[2](a.counts)] : null;
}

/**
 * The verdict of a witness from its passes (`[{ n, profile, cases: [{ id, verdict, reason }] }]`, the completed ones): the two rules of P5. -> `{ caught, reason, sentence, counts, failingSets }`; `failingSets` is the actual failing set of every
 * pass (the record keeps it). Deterministic: the one pass's failing set equals the registered set. Race: every failing pass fails registered cases only, and at least one fails.
 */
export function witnessVerdict({ kind, registered, passes }) {
  const used = kind === 'deterministic' ? passes.slice(0, 1) : passes;
  const counts = countsOf({ registered, passes: used });
  const refused = notCaught({ kind, counts });
  const failingSets = used.map(p => idsOf(failingCases(p)));
  return refused === null ? { caught: true, reason: 'caught', sentence: `caught: registered ${counts.registered}, failed ${counts.failedRegistered}, ${counts.failingPasses} failing pass(es) of ${used.length}`, counts, failingSets } : { caught: false, reason: refused[0], sentence: refused[1], counts, failingSets };
}

// ---- the run ----------------------------------------------------------------------------------------------------------------------------------------

const halt = (sentence, detail = sentence) => ({ halted: true, sentence, detail });
const buildMsOf = work => (existsSync(path.join(work, 'build.json')) ? readBuildRecord(work).value?.wallMs ?? null : null);

/** The one pass of a deterministic witness: `{ passes }` | `{ buildFailed }` | `{ halt }`. The built tree is measured and removed (it is reproducible). */
async function deterministicPasses({ seams, ran, work, now }) {
  const r = await onePass({ seams, ran, work, now });
  const treeBytes = dirBytes(path.join(work, 'tree'));
  ['tree', 'cases', 'child'].forEach(d => removeArea(path.join(work, d)));
  if (r.kind === 'build-failed') return { buildFailed: true };
  if (r.kind !== 'completed') return { halt: r.reason, detail: r.detail };
  return { passes: [{ n: 1, profile: '2cpu', cases: r.cases, ms: r.ms }], treeBytes };
}

/** The ten passes of a race witness through the driver: `{ passes }` | `{ buildFailed }` | `{ halt }`; a pass with no completed attempt is a halt (environment), never a verdict. */
async function racePasses({ seams, ran, work, seriesDir, signal, now }) {
  const answer = await scorePasses({ ...seams, runPass: ran, work, seriesDir, signal, ...(now === undefined ? {} : { now }) });
  if (answer.kind !== 'scored') return { halt: `the scoring ended ${answer.kind}: nothing was judged`, detail: answer.reason };
  return answer.verdict.build?.ok === false ? { buildFailed: true } : completedPasses({ work, ids: answer.score.explore.cases.map(c => c.id).sort() });
}

/** The completed attempt of each of the ten passes: `{ passes }`, or a halt when a pass has none. */
function completedPasses({ work, ids }) {
  const read = readAttempts({ work, expectedIds: ids, pinnedRunner: runnerRecordPinNow() });
  const done = read.map(p => [p, p.attempts.find(a => a.kind === 'completed')]);
  const open = done.filter(([, a]) => a === undefined).length;
  return open > 0 ? { halt: `${open} pass(es) have no completed attempt: environment, never a verdict` } : { passes: done.map(([p, a]) => ({ n: p.n, profile: p.profile, cases: a.cases, ms: a.ms })) };
}

const peakOf = cases => maxOf(cases.map(c => c.peakBytes).filter(Number.isFinite)) ?? 0;

/** The private record of a witness: every pass's failing set and wall (the cost), counts and ids of the host. */
function recordOf({ a, witness, registered, verdict, ran, work }) {
  const walls = ran.passes.map(p => p.ms ?? 0);
  return {
    id: a.id, ordinal: a.ordinal, stack: witness.stack, kind: witness.kind, trap: witness.trap, registered, caught: verdict.caught, reason: verdict.reason, sentence: verdict.sentence, counts: verdict.counts,
    passes: ran.passes.map(p => ({ n: p.n, profile: p.profile, wallMs: p.ms ?? null, failing: idsOf(failingCases(p)), verdicts: Object.fromEntries(failingCases(p).map(c => [c.id, c.verdict])), peakBytes: peakOf(p.cases) })),
    cost: { wallMs: walls.reduce((s, w) => s + w, 0), buildMs: buildMsOf(work), treeBytes: ran.treeBytes ?? null, retainedBytes: dirBytes(work) },
  };
}

/**
 * Run one witness: `a` = `{ world, pkg, id, ordinal, source, work, seriesDir, limits, memoryBytes, say, signal, now?, heartbeatMs? }`; `source` is the assembled witness tree (`assembleWitness`). -> `{ caught, reason, sentence, record }`
 * | `{ halted: true, sentence }` (nothing was judged: an environment halt, the mask, the read-back limit). A witness that does not build is `{ caught: false, reason: 'does not build' }` with a record, whatever the cases would have said.
 */
export async function runWitness(a) {
  const { world, pkg, id } = a;
  const witness = pkg.manifest.witnesses[id];
  const registered = registeredOf(pkg.manifest, witness);
  const seams = world.passSeams({ stack: witness.stack, source: a.source, limits: a.limits, sets: [{ name: 'heldout', dir: pkg.casesDir }], traps: pkg.held.traps, say: a.say, seriesDir: a.seriesDir });
  const ran = wrapPass({ runPass: seams.runPass, label: `witness ${a.ordinal}`, say: a.say ?? (() => {}), memoryBytes: a.memoryBytes, ...(a.heartbeatMs === undefined ? {} : { heartbeatMs: a.heartbeatMs }) });
  const got = await (witness.kind === 'deterministic' ? deterministicPasses : racePasses)({ seams, ran, work: a.work, seriesDir: a.seriesDir, signal: a.signal, now: a.now });
  return got.halt === undefined ? judged({ a, witness, registered, got }) : halt(`witness ${a.ordinal} was not judged: ${got.halt}`, got.detail ?? got.halt);
}

const DOES_NOT_BUILD = (registered) => ({ caught: false, reason: 'does not build', sentence: 'not caught: the witness does not build, so it proves nothing', counts: { registered: registered.length }, failingSets: [] });

/** The witness's verdict and record from what the passes gave. */
function judged({ a, witness, registered, got }) {
  const verdict = got.buildFailed ? DOES_NOT_BUILD(registered) : witnessVerdict({ kind: witness.kind, registered, passes: got.passes });
  return { caught: verdict.caught, reason: verdict.reason, sentence: verdict.sentence, record: recordOf({ a, witness, registered, verdict, ran: { passes: got.passes ?? [], treeBytes: got.treeBytes }, work: a.work }) };
}
