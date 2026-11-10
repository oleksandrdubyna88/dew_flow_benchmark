// The steps of a package run (F2.S1): the suite and the gate (cheap, no scoring), the two references (ten passes each), the F-LOAD measurement (the load cases' twins, once per reference) and the witnesses (one pass or ten each). Each step
// returns `{ item }` (the record's item: `ok` and counts) or `{ halted: sentence }` (nothing was judged: the run stops resumably). A finished step is written once to the run folder (`step-<name>.json`) and a resumed run reads it instead of
// running it again; the scoring folders of a step stay in the run folder (the attempt files), the assembled trees are owned temporary folders that go with the run. Every build goes through `buildAndRestore`, every pass through `runPassChild`.
import path from 'node:path';
import { gateItem } from './package-gate.mjs';
import { maxOf } from './package-max.mjs';
import { STACKS } from './package-manifest.mjs';
import { onePass, wrapPass } from './package-pass.mjs';
import { assembleReference } from './package-quick.mjs';
import { loadRule, stageTwins, validateReference } from './package-refs.mjs';
import { readStep, recordS, writeStep } from './package-record.mjs';
import { largestReplyBody, suiteProblems } from './package-suite.mjs';
import { assembleWitness, runWitness } from './package-witness.mjs';

const slim = rows => ({ ...rows, passes: rows.passes.map(({ cases, ...row }) => row) });

const maybe = (holds, part) => (holds ? part : {});

/** The folders S is counted over: the held-out cases and the visible samples when given. */
const foldersOf = (pkg, samplesDir) => (samplesDir === null ? [pkg.casesDir] : [pkg.casesDir, samplesDir]);

/** The gate item of the record: the verdict, and the verdict and date (or that the checker was not run). */
function gateOf(gate) {
  return { ok: gate.ok, sentence: gate.sentence, ...maybe(gate.gate !== undefined, { gate: gate.gate }), ...maybe(gate.notRun === true, { notRun: true }) };
}

/** The suite checks, S and the gate: `{ item }`. S is counted from the held-out cases and the visible samples (`samplesDir`) and recorded apart from registration (`package-NN/s.json`, once per suite hash), and only for a suite with no problem. `conformance` is the checker's answer or `null`. */
export function suiteStep({ pkg, home, task, conformance, now, recordFile, samplesDir = null }) {
  const problems = suiteProblems(pkg);
  const sBytes = largestReplyBody(foldersOf(pkg, samplesDir));
  if (problems.length === 0) recordS({ home, task, treeSha256: pkg.held.treeSha256, sBytes, now });
  const gate = gateItem({ specText: pkg.specText, conformance, task, ...maybe(recordFile !== undefined, { recordFile }) });
  return { item: { suite: { ok: problems.length === 0, problems, sBytes, counts: { cases: pkg.held.caseIds.length, problems: problems.length } }, gate: gateOf(gate) } };
}

const treeOf = (c, stack) => assembleReference({ templates: c.world.templates, stack, kind: c.task.kind, refDir: c.pkg.refs[stack].dir, scope: c.scope, parent: c.parent });

const unscored = sentence => ({ ok: false, halted: false, verdict: null, sentence });

/** One reference scored. -> `{ ok, halted, verdict, sentence, rows }`. */
async function scoreReference(c, stack) {
  if (c.pkg.refs[stack].files.length === 0) return unscored(`the ${stack} reference is not there (refs-NN/${stack}/ holds no file)`);
  const tree = treeOf(c, stack);
  if (!tree.ok) return unscored(tree.reason);
  return validateReference({ world: c.world, pkg: c.pkg, stack, source: tree.value.dir, work: path.join(c.run.dir, `refs-${stack}`), seriesDir: c.seriesDir, limits: c.limits, memoryBytes: c.memoryBytes, say: c.say, signal: c.signal, now: c.now, heartbeatMs: c.heartbeatMs });
}

/** One reference scored, or read back from its step file when the run is a resume. */
async function oneReference(c, stack) {
  const done = readStep(c.run, `ref-${stack}`);
  if (done !== null) return done;
  const r = await scoreReference(c, stack);
  if (!r.halted) writeStep(c.run, `ref-${stack}`, r);
  return r;
}

/** The two references: `{ item, results }` | `{ halted }`. */
export async function refsStep(c) {
  const results = {};
  for (const stack of STACKS) {
    results[stack] = await oneReference(c, stack);
    if (results[stack].halted) return { halted: results[stack].sentence };
  }
  const item = { ok: STACKS.every(s => results[s].ok), ...Object.fromEntries(STACKS.map(s => [s, { ok: results[s].ok, verdict: results[s].verdict, sentence: results[s].sentence, ...(results[s].rows ? { rows: slim(results[s].rows) } : {}) }])) };
  return { item, results };
}

/** The load scenarios without their load steps run once on a reference (one pass on the 2-CPU profile through the same seams). -> `{ halted, sentence }` | `{ ok, wallMs, sentence }`. */
async function runTwins(c, stack, loadCases) {
  const tree = treeOf(c, stack);
  if (!tree.ok) return { ok: false, sentence: tree.reason };
  const work = path.join(c.run.dir, `twin-${stack}`);
  const dir = path.join(work, 'twin-cases');
  stageTwins({ pkg: c.pkg, dir });
  const seams = c.world.passSeams({ stack, source: tree.value.dir, limits: c.limits, sets: [{ name: 'heldout', dir }], traps: [], say: c.say, seriesDir: c.seriesDir });
  const ran = wrapPass({ runPass: seams.runPass, label: `twin ${stack}`, say: c.say, memoryBytes: c.memoryBytes, heartbeatMs: c.heartbeatMs });
  const r = await onePass({ seams, ran, work, now: c.now });
  if (r.kind !== 'completed') return { halted: true, sentence: `the twin of the load cases on the ${stack} reference was not measured (${r.kind}): ${r.reason ?? r.tail ?? ''}` };
  const wall = maxOf(r.cases.filter(x => loadCases.includes(x.id)).map(x => x.wallMs).filter(Number.isFinite));
  return { ok: wall !== null, wallMs: wall, sentence: wall === null ? 'no twin case was judged' : 'measured' };
}

/** The twin wall of a reference, run once or read back from its step file. */
async function twinWall(c, stack, loadCases) {
  const done = readStep(c.run, `twin-${stack}`);
  if (done !== null) return done;
  const body = await runTwins(c, stack, loadCases);
  if (!body.halted) writeStep(c.run, `twin-${stack}`, body);
  return body;
}

/** F-LOAD: the twins once per reference, then the measured rule over every pass of both references. `refs` is `refsStep`'s results. -> `{ item }` | `{ halted }`. */
export async function loadStep(c, refs) {
  const load = c.pkg.manifest.load;
  if (load === undefined) return { item: { ok: true, none: true, sentence: 'the manifest names no load case: there is nothing to measure' } };
  const twins = await bothTwins(c, load.cases);
  return twins.halted ? { halted: twins.sentence } : { item: ruleItem({ refs, twins, load }) };
}

/** The twin walls of both references: `{ csharp, node }` | `{ halted, sentence }`. */
async function bothTwins(c, loadCases) {
  const twins = {};
  for (const stack of STACKS) {
    twins[stack] = await twinWall(c, stack, loadCases);
    if (twins[stack].halted) return twins[stack];
  }
  return twins;
}

/** The measured rule over every completed pass of both references and the two twins: the item. */
function ruleItem({ refs, twins, load }) {
  const passes = STACKS.flatMap(stack => refs[stack].rows.passes.filter(p => p.completed).map(p => ({ stack, profile: p.profile, n: p.n, cases: p.cases })));
  const walls = STACKS.map(s => twins[s].wallMs).filter(Number.isFinite);
  const rule = loadRule({ passes, loadCases: load.cases, twinWallMs: walls.length === STACKS.length ? maxOf(walls) : null, durationS: load.durationS });
  return { ok: rule.ok, slowestWallMs: rule.slowestWallMs, twinWallMs: rule.twinWallMs, durationS: rule.durationS, problems: rule.problems };
}

/** One witness run (or read back from its step file). -> its result `{ caught, reason, sentence, record }` | `{ halted, sentence }`. */
async function oneWitness(c, id, ordinal) {
  const done = readStep(c.run, `witness-${id}`);
  if (done !== null) return done;
  const w = c.pkg.manifest.witnesses[id];
  const tree = assembleWitness({ templates: c.world.templates, stack: w.stack, kind: c.task.kind, refDir: c.pkg.refs[w.stack].dir, overlayDir: path.join(c.pkg.mutantsDir, id), scope: c.scope, parent: c.parent });
  if (!tree.ok) return { caught: false, reason: 'refused overlay', sentence: `witness ${ordinal} is not caught: ${tree.reason}`, record: { ordinal, stack: w.stack, kind: w.kind, caught: false, reason: 'refused overlay' } };
  const r = await runWitness({ world: c.world, pkg: c.pkg, id, ordinal, source: tree.value.dir, work: path.join(c.run.dir, `witness-${id}`), seriesDir: c.seriesDir, limits: c.limits, memoryBytes: c.memoryBytes, say: c.say, signal: c.signal, now: c.now, heartbeatMs: c.heartbeatMs });
  if (!r.halted) writeStep(c.run, `witness-${id}`, r);
  return r;
}

/** Every witness of the manifest, in id order. -> `{ item }` | `{ halted }`. The item counts the witnesses caught and keeps the cost (walls and sizes) of the runs. */
export async function witnessesStep(c) {
  const ids = Object.keys(c.pkg.manifest.witnesses ?? {}).sort();
  const results = [];
  for (const [i, id] of ids.entries()) {
    const r = await oneWitness(c, id, i + 1);
    if (r.halted) return { halted: r.sentence };
    results.push({ id, r });
  }
  const caught = results.filter(({ r }) => r.caught).length;
  const wall = results.reduce((s, { r }) => s + (r.record?.cost?.wallMs ?? 0), 0);
  return { item: { ok: results.length > 0 && caught === results.length, witnesses: ids.length, caught, notCaught: results.filter(({ r }) => !r.caught).map(({ r }) => r.sentence), cost: { wallMs: wall, retainedBytes: results.reduce((s, { r }) => s + (r.record?.cost?.retainedBytes ?? 0), 0) } } };
}
