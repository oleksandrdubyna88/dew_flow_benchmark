// P1 and P6 as the tool can read them (F2.S1): the corrected spec passes the conformance checker (`spikes/lang-bench/lib/conformance.mjs`, called in this process; the checker lives in lang-bench and needs the task repository, so the
// command runs it only when it is given one, and `register` needs it), the header's `Gate verdict` row carries a verdict and a date, and the gate record `spikes/lang-bench/inputs/gates/spec-NN.md` exists. The bias question's review (P6) is
// the operator's through the coai document gate: the tool reads that its record exists and says so. Sentences name the checker's first line, never a case.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { run } from '../../lang-bench/lib/conformance.mjs';
import { INPUTS } from './paths.mjs';

const BENCH_GATES = path.join(INPUTS, '..', '..', 'lang-bench', 'inputs', 'gates');
export const gateRecordOf = task => path.join(BENCH_GATES, `spec-${task.id}.md`);
const VERDICTS = ['proceed', 'good_enough', 'continue_anyway'];
const VERDICT_ROW = new RegExp(`^\\| Gate verdict \\| \`(${VERDICTS.join('|')})\` · (\\d{4}-\\d{2}-\\d{2}) \\|$`, 'm');
const PLACEHOLDER_ROW = /^\| Gate verdict \|/m;

/** The sentence refusing a checked spec that is not the pinned one (the checker's exit 0 is then about another text), or null. A missing pin is a refusal too. */
function pinProblem({ file, specSha256 }) {
  if (typeof specSha256 !== 'string') return 'no spec pin was given: the checker is not run over a text that is not known to be the pinned one';
  const have = existsSync(file) ? createHash('sha256').update(readFileSync(file)).digest('hex') : null;
  return have === specSha256 ? null : `the checked spec is not the pinned spec (sha256 ${have?.slice(0, 12) ?? 'none: the file is not there'}, the pin says ${specSha256.slice(0, 12)}): the conformance check would be about another text`;
}

/** The checker over a task repository: `{ exit, firstLine }`, the first `FAIL` line (or the first line the checker said) when it is not 0. `record` names the FORMAT record (tests); the checker's own default is the committed one. */
export async function runConformance({ repo, slug, format, needles, record, specSha256 }) {
  const moved = pinProblem({ file: path.join(repo, 'todo', 'tasks', `${slug}.md`), specSha256 });
  if (moved !== null) return { exit: 4, firstLine: moved, pinned: false };
  const argv = ['--repo', repo, '--task', slug, '--format', format, ...(needles ? ['--needles', needles] : []), ...(record ? ['--record', record] : [])];
  const r = await run(argv);
  const said = [...r.lines, ...(r.errors ?? [])];
  return { exit: r.exit, pinned: true, firstLine: said.find(l => l.startsWith('FAIL')) ?? said[0] ?? '(the checker said nothing)' };
}

/** The reviewer count a gate record states (`N reviewers`), or `null`: counts only. */
const reviewersOf = text => { const m = /(\d+)\s+reviewers?/i.exec(text); return m === null ? null : Number(m[1]); };

const missingRow = specText => (PLACEHOLDER_ROW.test(specText) ? 'the Gate verdict row of the header is still its placeholder: the spec is not gated' : 'the spec has no Gate verdict row');

/** Each way P1 is red, as `[holds(a), the item]`, in order. `a` = `{ specText, conformance, row, recordFile }`. */
const RED = Object.freeze([
  [a => a.conformance === null || a.conformance === undefined, () => ({ ok: false, notRun: true, sentence: 'the conformance check was not run by this command (give --task-repo): the spec is not known to conform' })],
  [a => a.conformance.exit !== 0, a => ({ ok: false, sentence: `the conformance check failed (exit ${a.conformance.exit}): ${a.conformance.firstLine}` })],
  [a => a.row === null, a => ({ ok: false, sentence: missingRow(a.specText) })],
  [a => !existsSync(a.recordFile), a => ({ ok: false, sentence: `the gate record ${path.basename(a.recordFile)} is not there: the verdict stands on a record` })],
]);

/** `{ ok, sentence, gate }` for P1: the checker's answer (`conformance`, or `null` when it was not run), the header's verdict row and the gate record's existence. */
export function gateItem({ specText, conformance, task, recordFile = gateRecordOf(task) }) {
  const a = { specText, conformance, recordFile, row: VERDICT_ROW.exec(specText) };
  const red = RED.find(([holds]) => holds(a));
  return red ? red[1](a) : { ok: true, sentence: 'the spec conforms and is gated', gate: { verdict: a.row[1], date: a.row[2], reviewers: reviewersOf(readFileSync(recordFile, 'utf8')) } };
}
