// Calibration: both references must score 100 % and every mutant must fail EXACTLY its pre-registered cases (and block exactly its
// pre-registered ones), per phase. The registry is the pre-registration; an observed set is never copied into it. A mismatch is
// resolved by fixing the case or the mutant in the clear source directory and re-sealing. This module knows the suite's SHAPE only.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { readFileOrNull } from './inputs.mjs';
import { runSuite, heldoutScore } from './heldout-run.mjs';
import { fail, ok } from './result.mjs';

const PHASES = ['v1', 'v2'];
const REFERENCES = ['a', 'b'];

function readJson(file, what) {
  const bytes = readFileOrNull(file);
  if (bytes === null) return fail(`${what} ${file} does not exist`);
  try {
    return ok(JSON.parse(bytes.toString('utf8')));
  } catch (e) {
    if (e instanceof SyntaxError) return fail(`${what} ${file} is not JSON: ${e.message}`);
    throw e;
  }
}

const idList = v => Array.isArray(v) && v.every(x => typeof x === 'string');
const validCase = c => c && typeof c.id === 'string' && typeof c.group === 'string' && typeof c.scored === 'boolean' && typeof c.spec === 'string' && idList(c.needs);
const validSets = s => s && idList(s.fail) && idList(s.blocked);

/** `registry.json` of a suite directory: { salt, cases: [{ id, group, scored, spec, needs }], mutants: [{ id, clause, expected: { v1, v2 } }] }. */
export function readRegistry(suiteDir) {
  const doc = readJson(path.join(suiteDir, 'registry.json'), 'the registry');
  if (!doc.ok) return doc;
  const r = doc.value;
  if (typeof r.salt !== 'string' || !Array.isArray(r.cases) || !Array.isArray(r.mutants)) return fail('the registry needs salt, cases and mutants');
  const badCase = r.cases.find(c => !validCase(c));
  if (badCase) return fail(`the registry has a malformed case ${JSON.stringify(badCase?.id)}`);
  const badMutant = r.mutants.find(m => typeof m.id !== 'string' || !PHASES.every(p => validSets(m.expected?.[p])));
  if (badMutant) return fail(`mutant ${badMutant.id} has no expected { fail, blocked } sets for both phases (v1, v2)`);
  return ok(r);
}

/** ok(edited text) only when `find` occurs exactly once and the edit changes something; otherwise a fail naming the mutant. */
export function applyMutant(text, { id, find, replace }) {
  if (typeof find !== 'string' || find === '' || typeof replace !== 'string') return fail(`mutant ${id}: find and replace must be strings and find non-empty`);
  const count = text.split(find).length - 1;
  if (count !== 1) return fail(`mutant ${id}: its find must occur exactly once in the reference, found ${count} times`);
  if (find === replace) return fail(`mutant ${id}: the edit changes nothing`);
  return ok(text.replace(find, () => replace));
}

const sorted = list => [...list].sort();
const diff = (expected, observed) => ({ missing: sorted(expected.filter(x => !observed.includes(x))), extra: sorted(observed.filter(x => !expected.includes(x))) });
const idsWith = (score, status) => score.cases.filter(c => c.status === status).map(c => c.id);

/** One line per (subject, phase): every missing and extra id of the failing set and of the blocked set. */
function setProblems(label, expected, score) {
  const parts = ['fail', 'blocked'].flatMap(kind => {
    const d = diff(expected[kind], idsWith(score, kind));
    return [...(d.missing.length > 0 ? [`missing ${kind}: ${d.missing.join(', ')}`] : []), ...(d.extra.length > 0 ? [`extra ${kind}: ${d.extra.join(', ')}`] : [])];
  });
  return parts.length > 0 ? [`${label}: ${parts.join('; ')}`] : [];
}

function coverageProblems(label, registry, phase, score) {
  const wanted = registry.cases.filter(c => c.scored && (phase === 'v2' || c.group === 'H1')).map(c => c.id);
  const ran = score.cases.map(c => c.id);
  const d = diff(wanted, ran);
  return [...d.extra.map(id => `${label}: case ${id} ran but is not in the registry`), ...d.missing.map(id => `${label}: registry case ${id} did not run`)];
}

function referenceProblems(name, registry, obs, requireVisible) {
  return PHASES.flatMap(phase => {
    const label = `reference ${name} ${phase}`;
    const o = obs?.[phase];
    if (!o?.heldout?.known) return [`${label}: unknown (${o?.heldout?.why ?? 'not scored'})`];
    const out = [...coverageProblems(label, registry, phase, o.heldout)];
    if (!o.heldout.primary100) out.push(`${label}: not 100 % (failed ${idsWith(o.heldout, 'fail').join(', ') || 'none'}; blocked ${idsWith(o.heldout, 'blocked').join(', ') || 'none'})`);
    if (requireVisible && o.visible100 !== true) out.push(`${label}: the harness's visible scripts were ${o.visible100 === false ? 'not all passed' : 'unknown'} (visible 100 % is required of a reference)`);
    return out;
  });
}

function mutantProblems(m, registry, obs) {
  return PHASES.flatMap(phase => {
    const label = `mutant ${m.id} ${phase}`;
    const o = obs?.[phase];
    if (!o?.heldout?.known) return [`${label}: unknown (${o?.heldout?.why ?? 'not scored'})`];
    return [...coverageProblems(label, registry, phase, o.heldout), ...setProblems(label, m.expected[phase], o.heldout)];
  });
}

/** Pure. observations = { references: { a: { v1: { heldout, visible100 }, v2 }, b }, mutants: { M01: { v1, v2 } } } -> { problems, flags }. */
export function compareCalibration(registry, observations, { requireVisible = true } = {}) {
  const refs = Object.fromEntries(Object.entries(observations.references).map(([name, obs]) => [name, referenceProblems(name, registry, obs, requireVisible)]));
  const muts = Object.fromEntries(registry.mutants.filter(m => observations.mutants[m.id]).map(m => [m.id, mutantProblems(m, registry, observations.mutants[m.id])]));
  return { problems: [...Object.values(refs).flat(), ...Object.values(muts).flat()], refs, muts };
}

function summarize(registry, observations, compared, treeProblems) {
  const refEntry = name => Object.fromEntries(PHASES.map(p => {
    const o = observations.references[name]?.[p];
    return [p, { heldout100: o?.heldout?.known === true && o.heldout.primary100 === true, visible100: o?.visible100 ?? null }];
  }));
  const mutEntry = id => Object.fromEntries(PHASES.map(p => {
    const mine = (compared.muts[id] ?? []).filter(x => x.includes(` ${p}:`));
    return [p, { exact: !treeProblems[id] && observations.mutants[id] !== undefined && mine.length === 0 }];
  }));
  return {
    format: 1,
    n1: registry.cases.filter(c => c.scored && c.group === 'H1').length,
    n2: registry.cases.filter(c => c.scored && c.group === 'H2').length,
    m: registry.mutants.length,
    references: Object.fromEntries(REFERENCES.map(n => [n, refEntry(n)])),
    mutants: Object.fromEntries(registry.mutants.map(m => [m.id, mutEntry(m.id)])),
  };
}

function writeTree(root, name, text) {
  const dir = path.join(root, name);
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, 'minigit'), text, { mode: 0o755 });
  return dir;
}

/**
 * calibrate({ suiteDir, workRoot, scoreSubject, requireVisible }) -> { ok, problems, calibration }.
 * `scoreSubject({ name, kind, phase, treeDir })` resolves to { heldout: <heldoutScore>, visible100: boolean|null } and is injected: the
 * real one scores through the E1.S1 pipeline (heldout-pipeline.mjs), the tests' one runs the runner directly.
 */
export async function calibrate({ suiteDir, workRoot, scoreSubject, requireVisible = true }) {
  const registry = readRegistry(suiteDir);
  if (!registry.ok) return { ok: false, problems: [registry.reason], calibration: null };
  const refText = name => readFileSync(path.join(suiteDir, 'refs', name, 'minigit'), 'utf8');
  const subjects = REFERENCES.map(name => ({ kind: 'reference', name, text: ok(refText(name)) }));
  const treeProblems = {};
  for (const m of registry.value.mutants) {
    const file = readJson(path.join(suiteDir, 'mutants', `${m.id}.json`), 'the mutant file');
    const edited = file.ok ? applyMutant(refText('a'), file.value) : file;
    if (!edited.ok) treeProblems[m.id] = `mutant ${m.id}: ${edited.reason}`;
    else subjects.push({ kind: 'mutant', name: m.id, text: edited });
  }
  const observations = { references: {}, mutants: {} };
  for (const s of subjects) {
    const treeDir = writeTree(workRoot, s.name, s.text.value);
    const byPhase = {};
    for (const phase of PHASES) byPhase[phase] = await scoreSubject({ name: s.name, kind: s.kind, phase, treeDir });
    observations[s.kind === 'reference' ? 'references' : 'mutants'][s.name] = byPhase;
  }
  const compared = compareCalibration(registry.value, observations, { requireVisible });
  const problems = [...Object.values(treeProblems), ...compared.problems];
  const calibration = summarize(registry.value, observations, compared, treeProblems);
  return { ok: problems.length === 0, problems, calibration: { ...calibration, ok: problems.length === 0 } };
}

/** The test scorer: runs the suite's runner directly (no box, no visible scripts). */
export function directScorer({ suiteDir, workRoot, timeoutS = 10 }) {
  return async function score({ name, phase, treeDir }) {
    const workDir = path.join(workRoot, `${name}-${phase}`);
    const ran = await runSuite({ suiteDir, binary: path.join(treeDir, 'minigit'), phase, box: [], workDir, outPath: path.join(workRoot, `${name}-${phase}.json`), timeoutS });
    return { heldout: ran.ok ? heldoutScore(ran.value) : { known: false, why: ran.reason }, visible100: null };
  };
}
