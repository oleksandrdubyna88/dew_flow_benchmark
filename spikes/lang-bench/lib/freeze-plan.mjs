// The plan of the freeze commit (F2.S2 (d), `freeze.mjs plan`): in each of the six specs the `Freeze tag` row set to `` `spec-frozen-B-<date>` `` and nothing else, printed as one unified diff that `git apply` takes. It reads the
// checkout it is run in (the worktree must be clean and at `--ref`), refuses first and prints nothing when anything is refused. Every refusal is a sentence naming the spec:
//   a Gate verdict row that is still its placeholder; a conformance check that is not all `ok` (the checker's own `run`, never a copy); a spec gated under a FORMAT record other than B's, the amended FORMAT's;
//   FORMAT at the ref that is not the FORMAT step 2a records; a task list that is not exactly the six; a `<date>` that is not today's UTC date; FORMAT's class check (`tools/format-classes.mjs --self`) not clean.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { run as conformanceRun } from './conformance.mjs';
import { selfRunBytes } from '../tools/format-classes.mjs';
import { blobsOf, commitOf, dirtyLines, FORMAT_PATH, treeAt } from './freeze-git.mjs';
import { failWith, loadPin, pinTaskProblem, specSet } from './freeze-inputs.mjs';
import { factsOf, freezePlaceholder, gateFilled, readRows, tagDate, tagOfValue, withTag } from './freeze-rows.mjs';

const CONTEXT = 3;
const FIRST_LINE = /^ok format sha256 ([0-9a-f]{64}) \(record (\S+?),/;
const sha256Of = bytes => createHash('sha256').update(bytes).digest('hex');

/** Today's UTC date as `yyyymmdd`. */
export const utcDay = now => now().toISOString().slice(0, 10).replaceAll('-', '');

/** The sentences about `--date`: a form, a real day, and today's UTC day. */
function dateProblems({ date, now }) {
  if (!/^\d{8}$/.test(date)) return [`--date ${date} is not yyyymmdd`];
  return tagDate(`spec-frozen-B-${date}`) !== null && date === utcDay(now) ? [] : [`--date ${date} is not today's UTC date ${utcDay(now)}: the freeze tag is dated the day it is made`];
}

/** The sentences about the checkout: clean, and at the commit `--ref` names. */
async function checkoutProblems({ repo, ref }) {
  const [head, wanted, dirty] = [await commitOf(repo, 'HEAD'), await commitOf(repo, ref), await dirtyLines(repo)];
  const bad = [head, wanted, dirty].find(entry => !entry.ok);
  if (bad !== undefined) return bad;
  const where = head.value === wanted.value ? [] : [`the worktree's HEAD ${head.value} is not --ref ${wanted.value}: plan reads the checkout, so check ${ref} out first`];
  const clean = dirty.value.length === 0 ? [] : [`the worktree is not clean (${dirty.value.length} entries, the first: ${dirty.value[0]})`];
  return ok({ problems: [...where, ...clean], commit: wanted.value });
}

/** The sentence when the conformance check's first line names a FORMAT record other than B's, or a FORMAT other than the pin. */
function recordProblem({ file, first, pin }) {
  const found = FIRST_LINE.exec(first ?? '');
  if (found === null) return [`${file}: the conformance check printed no format line (${first ?? 'nothing'})`];
  if (found[2] !== 'B') return [`${file} is gated under FORMAT record ${found[2]}, not record B of the amended FORMAT (sha256 ${pin.sha256})`];
  return found[1] === pin.sha256 ? [] : [`${file} is gated under a FORMAT of sha256 ${found[1]}, not the amended FORMAT's ${pin.sha256}`];
}

/** The sentences from the checker's own result for one spec: any line that is not `ok`, or an exit other than 0. */
function conformanceProblems({ spec, result }) {
  const lines = [...result.lines.filter(line => !line.startsWith('ok ')), ...(result.errors ?? [])].slice(0, 3);
  return lines.length > 0 || result.exit !== 0 ? [`${spec.path}: the conformance check is not all ok (exit ${result.exit}): ${lines.join(' | ') || 'no line'}`] : [];
}

/** The sentences about one spec's header: the Gate verdict filled, the Freeze tag row the placeholder or an earlier tag. */
function headerProblems({ spec, bytes, facts, date }) {
  const read = readRows(bytes);
  if (!read.ok) return [`${spec.path}: ${read.reason}`];
  const gate = read.value.rows.find(row => row.label === 'Gate verdict');
  const open = gate === undefined || !gateFilled(gate.value, facts) ? [`${spec.path}: the Gate verdict row is still its placeholder (${gate?.value ?? 'absent'}): the spec was not gated`] : [];
  const row = read.value.value;
  const old = tagOfValue(row);
  const frozen = freezePlaceholder(row, facts) || (old !== null && tagDate(old) !== null && tagDate(old) < date);
  return [...open, ...(frozen ? [] : [`${spec.path}: the Freeze tag row says ${row}: not the placeholder, and not an earlier freeze tag than spec-frozen-B-${date}`])];
}

/** One unified diff hunk (3 lines of context) for line `no` of `before`, replaced by its text in `after`; the path is the repository's. */
function unifiedDiff({ file, before, after, no }) {
  const [old, now] = [before.toString('utf8').split('\n'), after.toString('utf8').split('\n')];
  const last = old.at(-1) === '' ? old.length - 1 : old.length;
  const [from, to] = [Math.max(1, no - CONTEXT), Math.min(last, no + CONTEXT)];
  const body = [];
  for (let line = from; line <= to; line += 1) {
    if (line === no) body.push(`-${old[line - 1]}`, `+${now[line - 1]}`);
    else body.push(` ${old[line - 1]}`);
  }
  const count = to - from + 1;
  return `diff --git a/${file} b/${file}\n--- a/${file}\n+++ b/${file}\n@@ -${from},${count} +${from},${count} @@\n${body.join('\n')}\n`;
}

/** The sentence when a spec's bytes do not end with a line feed (a plan could not be applied to it by `git apply`), else an empty list. */
const endProblem = (spec, bytes) => (bytes.at(-1) === 0x0a ? [] : [`${spec.path}: the file does not end with a line feed`]);

/** The checks that read the repository: the pin, the task set, the six specs and FORMAT at the ref. */
async function loadInputs({ repo, commit, record }) {
  const pin = loadPin(record);
  const tree = await treeAt(repo, commit);
  if (!tree.ok) return tree;
  const specs = specSet(tree.value);
  const paths = specs.ok ? [FORMAT_PATH, ...specs.value.map(spec => spec.path)] : [FORMAT_PATH];
  const blobs = await blobsOf(repo, tree.value, paths);
  return ok({ pin, specs, blobs });
}

/** The class check of FORMAT's bytes: the sentence when it is not clean. */
const classProblems = bytes => {
  const checked = selfRunBytes(bytes);
  return checked.exit === 0 ? [] : [`FORMAT's class check (tools/format-classes.mjs --self) is not clean: ${checked.lines.slice(0, 2).join(' | ')}`];
};

/** The sentences about FORMAT itself at the ref: the pin, the class check, and the facts the header rows are judged by. */
async function formatProblems({ pin, blobs }) {
  if (!blobs.ok) return { problems: [blobs.reason], facts: null };
  const bytes = blobs.value.get(FORMAT_PATH);
  const actual = sha256Of(bytes);
  const pinned = !pin.ok ? [pin.reason] : (actual === pin.value.sha256 ? [] : [`FORMAT at the ref has sha256 ${actual}, which is not the FORMAT build step 2a records (${pin.value.sha256} in the record's entry B)`]);
  const facts = factsOf(bytes);
  return { problems: [...pinned, ...classProblems(bytes), ...(facts.ok ? [] : [`FORMAT's facts cannot be read: ${facts.reason}`])], facts: facts.ok ? facts.value : null };
}

/** The checker's own `run` on one spec of the checkout, in the checker's own arguments. */
function conformanceOf({ spec, context, seams }) {
  const needles = seams.needles === undefined ? [] : ['--needles', seams.needles];
  return seams.conformance(['--repo', context.repo, '--task', spec.slug, '--format', path.join(context.repo, FORMAT_PATH), '--record', context.record, ...needles]);
}

/** The sentences about one spec: its end, its header, and the checker's result (which also names the FORMAT record it is gated under). */
async function specProblems({ spec, context, facts, seams }) {
  const bytes = context.blobs.value.get(spec.path);
  const header = facts === null ? [] : headerProblems({ spec, bytes, facts, date: context.date });
  if (!context.pin.ok) return [...endProblem(spec, bytes), ...header];
  const result = await conformanceOf({ spec, context, seams });
  return [...endProblem(spec, bytes), ...header, ...recordProblem({ file: spec.path, first: result.lines[0], pin: context.pin.value }), ...conformanceProblems({ spec, result })];
}

/** The sentences about each of the six specs, in the six's order. */
async function specsProblems({ context, facts, seams }) {
  const lists = [];
  for (const spec of context.specs) lists.push(await specProblems({ spec, context, facts, seams }));
  return lists.flat();
}

/**
 * The plan: ok({ patch, tag }) when nothing is refused (`patch` is one unified diff over the six specs), else failWith(sentences) before any edit is made. `seams`: `now` (a clock), `conformance` (the checker's `run`), `needles`.
 */
export async function planFreeze({ repo, ref, date, record, seams = {} }) {
  const options = { now: seams.now ?? (() => new Date()), conformance: seams.conformance ?? conformanceRun, needles: seams.needles };
  const where = await checkoutProblems({ repo, ref });
  if (!where.ok) return where;
  const loaded = await loadInputs({ repo, commit: where.value.commit, record });
  if (!loaded.ok) return loaded;
  const { pin, specs, blobs } = loaded.value;
  const format = await formatProblems({ pin, blobs });
  const list = [...(pin.ok && pinTaskProblem(pin.value) !== null ? [pinTaskProblem(pin.value)] : []), ...(specs.ok ? [] : specs.problems)];
  const each = specs.ok && blobs.ok ? await specsProblems({ context: { specs: specs.value, blobs, pin, repo, record, date }, facts: format.facts, seams: options }) : [];
  const problems = [...dateProblems({ date, now: options.now }), ...where.value.problems, ...list, ...format.problems, ...each];
  if (problems.length > 0) return failWith(problems);
  const edits = specs.value.map(spec => ({ spec, after: withTag(blobs.value.get(spec.path), `spec-frozen-B-${date}`) }));
  const broken = edits.find(edit => !edit.after.ok);
  if (broken !== undefined) return failWith([`${broken.spec.path}: ${broken.after.reason}`]);
  const patch = edits.map(({ spec, after }) => unifiedDiff({ file: spec.path, before: blobs.value.get(spec.path), after: after.value, no: readRows(blobs.value.get(spec.path)).value.no })).join('');
  return ok({ patch, tag: `spec-frozen-B-${date}` });
}
