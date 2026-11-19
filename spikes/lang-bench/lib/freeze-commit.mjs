// The freeze commit's rule (F2.S2 (d), `freeze.mjs check-commit`): between the commit before and the commit after, the six specs change in their `Freeze tag` row and nowhere else, all six to ONE tag
// `spec-frozen-B-<a real day>`; no byte of FORMAT and no other path changes. The row's earlier value is the placeholder FORMAT states, or an earlier freeze tag when a later one supersedes it (the recovery of
// (d): a new freeze commit whose only change against the previous one is the six rows).
// `checkFreezeCommit` -> ok({ tag, date, specs: [{ task, slug, path, sha256 }] }) | failWith(sentences) | an environment fail (`env: true`).
import { createHash } from 'node:crypto';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { blobsOf, changedPaths, commitOf, FORMAT_PATH, treeAt } from './freeze-git.mjs';
import { failWith, specSet } from './freeze-inputs.mjs';
import { gatedProblems } from './freeze-pins.mjs';
import { differingLines, factsOf, freezePlaceholder, readRows, tagDate, tagOfValue, withTag } from './freeze-rows.mjs';

const sha256Of = bytes => createHash('sha256').update(bytes).digest('hex');

/** The date of an earlier freeze tag a row's value names, or null when the value is not a freeze tag. */
const earlierDate = value => {
  const name = tagOfValue(value);
  return name === null ? null : tagDate(name);
};

/** The sentences about the row's earlier value: the placeholder, or an earlier freeze tag, else a problem. */
function beforeProblem({ file, value, after, facts }) {
  if (freezePlaceholder(value, facts)) return [];
  const oldDate = earlierDate(value);
  if (oldDate === null) return [`${file}: the row before the freeze commit says ${value}, which is neither the placeholder nor a freeze tag`];
  return tagDate(after) > oldDate ? [] : [`${file}: the new tag ${after} is not later than the tag ${tagOfValue(value)} it replaces: a superseding tag has a later date`];
}

/** ok({ before, after }) the rows of both sides of one spec, or a fail naming the file and what cannot be read. */
function bothRows({ file, bytes }) {
  const [before, after] = [readRows(bytes.before), readRows(bytes.after)];
  return before.ok && after.ok ? ok({ before: before.value, after: after.value }) : fail(`${file}: ${(before.ok ? after : before).reason}`);
}

/** The sentence when a line other than the Freeze tag row differs (or the line count does), else null. */
function lineProblem({ file, bytes, row }) {
  const lines = differingLines(bytes.before, bytes.after);
  if (lines === null) return `${file} line count changed: only the Freeze tag row (line ${row.no}) may change, every other byte stays`;
  const outside = lines.filter(no => no !== row.no);
  return outside.length === 0 ? null : `${file} line ${outside[0]} changed: only the Freeze tag row (line ${row.no}) may change, every other byte stays`;
}

/** The sentence when the row's new value is not `spec-frozen-B-<yyyymmdd>` of a real day, else null. */
function newTagProblem({ file, row, tag }) {
  return tag !== null && tagDate(tag) !== null ? null : `${file} line ${row.no}: the Freeze tag row says ${row.value}, not \`spec-frozen-B-<yyyymmdd>\` of a real day`;
}

/** The sentence when the new row is not the old row with only its value replaced (the cell's padding included), else null. */
function shapeProblem({ file, bytes, row, tag }) {
  const written = withTag(bytes.before, tag);
  return written.ok && written.value.equals(bytes.after) ? null : `${file} line ${row.no}: the row is not the old row with only its value replaced`;
}

const verdict = (problems, tag = null) => ({ problems, tag });

/** The first sentence about the new row of a spec: a line outside the row, or a value that is not a freeze tag, else null. */
function rowProblem({ file, bytes, row, tag }) {
  return lineProblem({ file, bytes, row }) ?? newTagProblem({ file, row, tag });
}

/** The sentences for one spec between its bytes before and after; `tag` is what its row now says (null when the row is not a tag). */
function specProblems({ file, bytes, facts }) {
  const rows = bothRows({ file, bytes });
  if (!rows.ok) return verdict([rows.reason]);
  const tag = tagOfValue(rows.value.after.value);
  const early = rowProblem({ file, bytes, row: rows.value.after, tag });
  if (early !== null) return verdict([early]);
  const shape = shapeProblem({ file, bytes, row: rows.value.after, tag });
  return verdict([...(shape === null ? [] : [shape]), ...beforeProblem({ file, value: rows.value.before.value, after: tag, facts })], tag);
}

/** The sentences for paths that changed but are not the six specs: FORMAT is named first, as its own rule. */
function pathProblems(changed, specs) {
  const own = new Set(specs.map(spec => spec.path));
  return changed.filter(file => !own.has(file)).map(file => (file === FORMAT_PATH ? `${FORMAT_PATH} changed: the freeze commit never changes FORMAT, not by one byte` : `${file} changed: the freeze commit changes only the Freeze tag row of the six specs`));
}

/** One tag for the six rows: a sentence when they differ. */
function oneTagProblem(tags) {
  const names = [...new Set(tags.filter(Boolean))];
  return names.length > 1 ? [`the six rows name different tags (${names.join(', ')}): one freeze has one tag`] : [];
}

/** The trees and the specs of both sides, or the first failure. */
async function sides({ repo, before, after }) {
  const [b, a] = [await commitOf(repo, before), await commitOf(repo, after)];
  const bad = [b, a].find(entry => !entry.ok);
  if (bad !== undefined) return bad;
  const [bt, at, changed] = [await treeAt(repo, b.value), await treeAt(repo, a.value), await changedPaths(repo, b.value, a.value)];
  const broken = [bt, at, changed].find(entry => !entry.ok);
  return broken ?? ok({ tree: { before: bt.value, after: at.value }, changed: changed.value });
}

/** ok({ tree, changed, specs }): both sides' trees, the changed paths and the six specs of the commit after. */
async function readSides({ repo, before, after }) {
  const read = await sides({ repo, before, after });
  if (!read.ok) return read;
  const specs = specSet(read.value.tree.after);
  return specs.ok ? ok({ ...read.value, specs: specs.value }) : specs;
}

/** ok({ old, now, facts }): the blobs of FORMAT and the six specs before, the specs after, and FORMAT's facts read from the FORMAT before. */
async function readBlobs({ repo, tree, specs }) {
  const paths = specs.map(spec => spec.path);
  const [old, now] = [await blobsOf(repo, tree.before, [FORMAT_PATH, ...paths]), await blobsOf(repo, tree.after, paths)];
  const unread = [old, now].find(entry => !entry.ok);
  if (unread !== undefined) return failWith([unread.reason]);
  const facts = factsOf(old.value.get(FORMAT_PATH));
  return facts.ok ? ok({ old: old.value, now: now.value, facts: facts.value }) : failWith([`FORMAT before the freeze commit: ${facts.reason}`]);
}

/** The result of each spec's own rule: changed, so its row is judged; unchanged, so its row was not set. */
function specResults({ specs, changed, blobs }) {
  return specs.map(spec => (changed.includes(spec.path)
    ? specProblems({ file: spec.path, bytes: { before: blobs.old.get(spec.path), after: blobs.now.get(spec.path) }, facts: blobs.facts })
    : verdict([`${spec.path}: unchanged, so its Freeze tag row was not set`])));
}

/** The sentences for a spec whose file mode changed (the bytes may be the same). */
function modeProblems({ specs, tree }) {
  return specs.filter(spec => tree.before.get(spec.path)?.mode !== tree.after.get(spec.path)?.mode)
    .map(spec => `${spec.path}: its mode changed from ${tree.before.get(spec.path)?.mode} to ${tree.after.get(spec.path)?.mode}: the freeze commit changes only the Freeze tag row`);
}

/** Every sentence of the freeze commit's rule, and the per-spec results they came from. */
function freezeFindings({ read, blobs, pins }) {
  const { specs, changed, tree } = read;
  const results = specResults({ specs, changed, blobs });
  const gated = specs.flatMap(spec => gatedProblems({ spec, bytes: blobs.old.get(spec.path), facts: blobs.facts, pins }));
  const problems = [...pathProblems(changed, specs), ...results.flatMap(result => result.problems), ...oneTagProblem(results.map(result => result.tag)), ...modeProblems({ specs, tree }), ...gated];
  return { problems, tag: results[0].tag };
}

/** `check-commit`: the rule above, for the commits `before` and `after` of the repository. `facts` are FORMAT's, read at `before`; `pins` are the gated hashes of the registered packages (`freeze-pins.mjs`: each package's `specSha256`), not the engine's pins, which hold the frozen hashes after the freeze. */
export async function checkFreezeCommit({ repo, before, after, pins }) {
  const read = await readSides({ repo, before, after });
  if (!read.ok) return read;
  const blobs = await readBlobs({ repo, tree: read.value.tree, specs: read.value.specs });
  if (!blobs.ok) return blobs;
  const { problems, tag } = freezeFindings({ read: read.value, blobs: blobs.value, pins });
  return problems.length > 0 ? failWith(problems) : ok({ tag, date: tagDate(tag), specs: read.value.specs.map(spec => ({ ...spec, sha256: sha256Of(blobs.value.now.get(spec.path)) })) });
}
