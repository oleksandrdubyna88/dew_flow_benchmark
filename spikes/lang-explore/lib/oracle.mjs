// The independent oracle of an ungated draft's private material (plan research/PLAN_lang_explore_olympiad.md D-O3, T-O8). Two references and `gen.py` can share one misreading of the spec, so calibration first checks two more things:
//   (a) `brute.py`, a deliberately naive solver by another method, is run as `python3 -I brute.py <case.in>` on every held-out case; it prints the answer and exits 0, or exits 3 when the case is too big for it (that is
//       how it MARKS the cases it cannot run). Its output must equal the case's expected output (`gen.py`'s, pinned by the manifest) on every case it ran;
//   (b) `handcheck.md`, the cases the team lead derived by hand from the spec text alone, must exist and list at least five.
// Any disagreement stops calibration and names the CASE ID only: never an input, an output or a mutant (D41). Failures are values.
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { bind, chdir, devProc, hiddenPaths, isolation, roBind, roRoot, setenvAll, tmpfs } from '../../minigit-bench/lib/bwrap.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { listOf } from './idlist.mjs';

export const BRUTE_REFUSED_EXIT = 3;
export const HANDCHECK_MIN_CASES = 5;
const BRUTE_TIMEOUT_MS = 120_000;

const nonBlank = text => text.split('\n').filter(l => l.trim() !== '');
const SEPARATOR_ROW = /^\|[\s:|-]*\|?\s*$/;

const CASE_ID = /(?<![\w-])h\d{3}(?![\w-])/;

/**
 * How many cases a `handcheck.md` lists: the distinct case ids (`h` and three digits, as a whole word) named by the data rows of its tables, the first id of each row. Headings, list items and prose do not count, however
 * many ids they name (coai finding 10): a case is a row of a table, with its input and answer beside its id. The count is a guard against a forgotten file, not a proof that the cases are right: the team lead reads them.
 */
export function handcheckCases(text) {
  const rows = nonBlank(text).filter(l => l.startsWith('|') && !SEPARATOR_ROW.test(l));
  return new Set(rows.map(l => CASE_ID.exec(l)?.[0]).filter(Boolean)).size;
}

/** The sentence saying why `handcheck.md` is not enough, or null. */
export function handcheckProblem(file) {
  if (!existsSync(file)) return `handcheck.md is missing (${path.basename(path.dirname(file))}/handcheck.md): the team lead derives at least ${HANDCHECK_MIN_CASES} small cases by hand from the spec text before calibration`;
  const n = handcheckCases(readFileSync(file, 'utf8'));
  return n >= HANDCHECK_MIN_CASES ? null : `handcheck.md lists ${n} case(s); at least ${HANDCHECK_MIN_CASES} are required`;
}

/**
 * The argv prefix of the box `brute.py` runs in (coai findings 2 and 7): the whole filesystem read-only, the operator's home, /mnt, /run and /tmp replaced by empty tmpfs, the held-out folder bound read-only, one writable
 * temp directory (also HOME and TMPDIR), no network, its own pid namespace, an allow-listed environment. `brute.py` is a third coder's naive solver over private material: it never gets the network or a way to change the folder.
 */
export const bruteBoxArgv = ({ heldoutDir, tmp, operatorHome = os.homedir() }) => Object.freeze([
  'bwrap', ...roRoot(), ...devProc(), ...hiddenPaths({ operatorHome }).flatMap(tmpfs), ...roBind(heldoutDir), ...bind(tmp), ...isolation(),
  ...setenvAll({ PATH: '/usr/bin:/bin', HOME: tmp, TMPDIR: tmp, LANG: 'C.UTF-8' }), ...chdir(tmp), '--',
]);

/** What a finished `brute.py` process means for one case, as `[applies(result), status/why of the case]` in order. */
const BRUTE_OUTCOMES = Object.freeze([
  [r => !r.ok, () => ({ status: 'error', why: 'could not be started' })],
  [r => r.value.timedOut, () => ({ status: 'error', why: `did not finish within ${BRUTE_TIMEOUT_MS / 1000} s` })],
  [r => r.value.code === BRUTE_REFUSED_EXIT, () => ({ status: 'refused' })],
  [r => r.value.code !== 0, r => ({ status: 'error', why: `exited ${r.value.code}` })],
]);

/** One case through `brute.py`: `{ id, status }`, status one of `ran` (output equal), `differs`, `refused` (exit 3), `error`. A reason is kept only for `error` and never holds the case's bytes. */
async function bruteOnCase({ heldout, python, run, id, tmp, operatorHome }) {
  const box = bruteBoxArgv({ heldoutDir: heldout.dir, tmp, operatorHome });
  const r = await run(box[0], [...box.slice(1), python, '-I', heldout.bruteFile, path.join(heldout.casesDir, `${id}.in`)], { timeoutMs: BRUTE_TIMEOUT_MS });
  const known = BRUTE_OUTCOMES.find(([applies]) => applies(r));
  if (known) return { id, ...known[1](r) };
  return { id, status: r.value.stdout === readFileSync(path.join(heldout.casesDir, `${id}.out`), 'utf8') ? 'ran' : 'differs' };
}

const idsWith = (results, status) => results.filter(r => r.status === status).map(r => r.id);

/** What the per-case results mean: the failure naming case ids (a disagreement first, then a crash, then a brute that ran nothing), or the counts. */
function verdictOf(results) {
  const differs = idsWith(results, 'differs');
  if (differs.length > 0) return fail(`brute.py disagrees with gen.py on case ${listOf(differs)}: the spec, gen.py or brute.py is wrong; calibration stops until it is corrected`);
  const broken = results.filter(r => r.status === 'error');
  if (broken.length > 0) return fail(`brute.py failed on case ${listOf(broken.map(r => `${r.id} (${r.why})`))}`);
  const ran = idsWith(results, 'ran').length;
  return ran > 0 ? ok({ ran, refused: idsWith(results, 'refused').length }) : fail('brute.py ran no case: it refused every one, so it checks nothing');
}

/** Both oracle files must be there and hold what they must (`brute.py` exists; `handcheck.md` lists enough cases): the sentence of the first that does not, or null. */
const filesProblem = heldout => (existsSync(heldout.bruteFile) ? handcheckProblem(heldout.handcheckFile) : `brute.py is missing (${path.basename(heldout.dir)}/brute.py): a third coder writes the naive solver before calibration`);

/** Every case through `brute.py` in its box, saying `case k of n` before each; the writable temp directory is made under `tmpRoot` and removed after. */
async function runAll({ heldout, python, run, say, tmpRoot, operatorHome }) {
  mkdirSync(tmpRoot, { recursive: true, mode: 0o700 });
  const tmp = mkdtempSync(path.join(tmpRoot, 'brute-'));
  const results = [];
  try {
    for (const [i, id] of heldout.caseIds.entries()) {
      say(`oracle: case ${i + 1} of ${heldout.caseIds.length}`);
      results.push(await bruteOnCase({ heldout, python, run, id, tmp, operatorHome }));
    }
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
  return results;
}

/**
 * The whole oracle: `brute.py` (in its box, see `bruteBoxArgv`) against `gen.py`'s outputs on every case, then `handcheck.md`. `heldout` is `loadHeldout`'s value; `python` the pinned interpreter; `tmpRoot` where the box's
 * writable temp is made; `operatorHome` the home the box hides; `run` is `runBounded` (a test passes its own). -> `ok({ ran, refused, handcheckCases })` | `fail(sentence)`.
 */
export async function checkOracle({ heldout, python, tmpRoot, operatorHome = os.homedir(), run = runBounded, say = () => {} }) {
  const missing = filesProblem(heldout);
  if (missing) return fail(missing);
  const verdict = verdictOf(await runAll({ heldout, python, run, say, tmpRoot, operatorHome }));
  if (!verdict.ok) return verdict;
  const handcheck = handcheckCases(readFileSync(heldout.handcheckFile, 'utf8'));
  say(`oracle: brute.py agrees with gen.py on ${verdict.value.ran} case(s) and refused ${verdict.value.refused}; handcheck.md lists ${handcheck} case(s)`);
  return ok({ ...verdict.value, handcheckCases: handcheck });
}
