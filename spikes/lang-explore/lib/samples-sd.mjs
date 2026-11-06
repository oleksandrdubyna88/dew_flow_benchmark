// The visible samples of a daemon or web spec, read from its section 6 (FORMAT 3.4): for each `### <id>` sub-section, the fenced block after the line `samples/<id>.in` is the scenario file and the one after
// `samples/<id>.out` the expected transcript, each holding the file's text exactly (the file's final line feed is the line feed before the closing fence). The reader refuses what it cannot read: no guess, no partial list.
// Pure: text in, `ok([{ id, input, output }])` | `fail` out.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const ID = /^s\d\d$/;
const FENCE = /^```/;

/** The lines of section 6 (`## 6. ...` up to the next `## `), or `[]`. */
function section6(spec) {
  const lines = spec.split('\n');
  const from = lines.findIndex(l => /^## 6\. /.test(l));
  if (from < 0) return [];
  const to = lines.findIndex((l, i) => i > from && /^## /.test(l));
  return lines.slice(from + 1, to < 0 ? lines.length : to);
}

/** The sub-sections `### <id>` of section 6 as `{ id, lines }`. */
function subsections(lines) {
  const out = [];
  for (const line of lines) {
    const m = /^### (.+)$/.exec(line);
    if (m) out.push({ id: m[1].trim(), lines: [] });
    else if (out.length > 0) out.at(-1).lines.push(line);
  }
  return out;
}

const labelOf = line => /^`(samples\/[^`]+)`\s*$/.exec(line)?.[1] ?? null;
const closeOf = (lines, from) => lines.findIndex((x, j) => j > from && FENCE.test(x));

/** The fenced blocks of a sub-section, each with the label line (`samples/<id>.in`) that came before it: `[{ label, text }]` | a `fail` for a fence that never closes. */
function blocksOf(lines) {
  const blocks = [];
  let label = null;
  for (let i = 0; i < lines.length; i += 1) {
    if (!FENCE.test(lines[i])) { label = labelOf(lines[i]) ?? label; continue; }
    const close = closeOf(lines, i);
    if (close < 0) return fail('a fenced block is never closed');
    blocks.push({ label, text: lines.slice(i + 1, close).map(x => `${x}\n`).join('') });
    i = close;
    label = null;
  }
  return ok(blocks);
}

const countProblem = (id, blocks) => (blocks.length === 2 ? null : `sample ${id} must show two fenced blocks (the .in and the .out); it shows ${blocks.length}`);
const headProblem = (id, [a, b]) => (a.label === `samples/${id}.in` && b.label === `samples/${id}.out` ? null : `sample ${id}: the blocks must be headed by \`samples/${id}.in\` and \`samples/${id}.out\``);

/** Why a sample's blocks are not its two headed blocks, or null. */
const blockProblem = (id, blocks) => countProblem(id, blocks) ?? headProblem(id, blocks);

function sampleOf(sub) {
  if (!ID.test(sub.id)) return fail(`the sub-section '${sub.id}' is not a sample id (s01, s02, ...)`);
  const blocks = blocksOf(sub.lines);
  if (!blocks.ok) return fail(`sample ${sub.id}: ${blocks.reason}`);
  const problem = blockProblem(sub.id, blocks.value);
  return problem ? fail(problem) : ok({ id: sub.id, input: blocks.value[0].text, output: blocks.value[1].text });
}

function samplesOf(subs) {
  const samples = [];
  for (const sub of subs) {
    const s = sampleOf(sub);
    if (!s.ok) return s;
    samples.push(s.value);
  }
  return ok(samples);
}

/** The visible samples of a daemon or web spec: `ok([{ id, input, output }])`, ids `s01`, `s02`, ... in order | `fail`. */
export function parseScenarioSamples(spec) {
  const subs = subsections(section6(spec));
  if (subs.length === 0) return fail('no `### sNN` sample sub-section in section 6 of the spec');
  const read = samplesOf(subs);
  if (!read.ok) return read;
  const samples = read.value;
  const wrong = samples.find((s, i) => s.id !== `s${String(i + 1).padStart(2, '0')}`);
  return wrong ? fail(`the sample ids must be s01, s02, ... in order (found ${samples.map(s => s.id).join(', ')})`) : ok(samples);
}
