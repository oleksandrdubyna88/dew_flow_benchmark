// Template drift on a kept tree, judged per file by BYTES (F1.S3 (b), review round). A frozen file that is a template file (a script or a pin file) or the derived `PACKAGES.md` can differ from today's bytes because the harness moved since
// the session began or because the agent changed it. It is the harness's only when the tree's bytes equal the bytes the file had when the session began (`history`); any other difference, a deletion included, is the agent's; where the
// old bytes cannot be read the file is `undecided` and is charged to nobody (the deviations are then unknown). `templateHistory` answers "what bytes did this file have when the series began": from the map the series recorded at `init`,
// else from git at the commit its subject recorded; a series made before either existed that git cannot show is unknown, never a guess.
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { templateFilesOf } from '../../lang-bench/lib/frozen-set.mjs';
import { allWorksMatrix } from '../../lang-bench/lib/package-matrix.mjs';
import { renderPackagesMd } from '../../lang-bench/lib/packages-md.mjs';
import { GENERATED_FILES } from './generated-drift.mjs';
import { REPO } from './paths.mjs';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const known = hash => ({ known: true, sha256: hash });
const unknown = why => ({ known: false, why });
const DERIVED = 'PACKAGES.md';
const INPUTS = 'spikes/lang-bench/inputs';
const COMMIT = /^[0-9a-f]{40}$/;
const ABSENT = Object.freeze({ known: true, absent: true });

/** True when a change to `rel` can be the harness's move: a template file of the stack (a script, a pin file), the derived packages list, or a file generated from the one memory value (`generated-drift.mjs`). */
export const isMovable = (stack, rel) => rel === DERIVED || GENERATED_FILES.includes(rel) || templateFilesOf(stack).includes(rel);

/** The bytes of a file at a commit of this repository (git as exe + argv, a ceiling), or null when git cannot show it. */
export function readAtCommit(commit, rel) {
  const r = spawnSync('git', ['-C', REPO, 'show', `${commit}:${rel}`], { timeout: 30_000, maxBuffer: 64 * 1024 * 1024, env: { PATH: process.env.PATH ?? '/usr/bin:/bin', LANG: 'C.UTF-8', GIT_TERMINAL_PROMPT: '0' } });
  return r.status === 0 ? r.stdout : null;
}

const jsonOf = bytes => { try { return bytes === null ? null : JSON.parse(bytes.toString('utf8')); } catch { return null; } };

/** The matrix at the commit; before the matrix existed every package was offered in every kind. */
const matrixAt = ({ commit, recipe, readAt }) => jsonOf(readAt(commit, `${INPUTS}/package-matrix.json`)) ?? allWorksMatrix(recipe);

/** `PACKAGES.md` as the series' own record and recipe render it, at its commit. */
function packagesMdAt({ commit, stack, kind, readAt }) {
  const [record, recipe] = [jsonOf(readAt(commit, `${INPUTS}/cache-snapshots-lang.json`)), jsonOf(readAt(commit, `${INPUTS}/cache-recipe-lang.json`))];
  if (record === null || recipe === null) return unknown(`the record and the recipe of commit ${commit} cannot be read, so ${DERIVED} cannot be rendered as the series saw it`);
  const rendered = renderPackagesMd({ stack, kind, record, recipe, matrix: matrixAt({ commit, recipe, readAt }) });
  return rendered.ok ? known(sha256(rendered.value)) : unknown(`${DERIVED} of commit ${commit} cannot be rendered: ${rendered.reason}`);
}

/** The bytes of a template file at the commit. */
function templateAt({ commit, stack, kind, rel, readAt }) {
  const bytes = readAt(commit, `spikes/lang-bench/templates/${stack}/${kind}/${rel}`);
  return bytes === null ? unknown(`git cannot show ${stack}/${kind}/${rel} at commit ${commit}`) : known(sha256(bytes));
}

/**
 * -> `({ stack, kind, rel }) => { known: true, sha256 } | { known: true, absent: true } | { known: false, why }`: the bytes the file had when the series began (`absent`: it did not exist). `inputs` is the series' inputs:
 * `templateHashes`, `packagesMdSha256` and `generatedSha256` when it recorded them (a recorded `null` is a file the workspace did not have), `subject.sha` (the commit of the harness it was made with) otherwise. `readAt(commit, path)`
 * is the seam for git; `legacy` (`generated-drift.mjs`) makes the generated files again as they were at that commit.
 */
export function templateHistory({ inputs, readAt = readAtCommit, legacy }) {
  return args => {
    const recorded = recordedFor(inputs, args);
    if (recorded !== undefined) return recorded === null ? ABSENT : known(recorded);
    const commit = usableCommit(inputs);
    if (commit === null) return unknown(`the series recorded no bytes for ${args.rel} and no commit git could show them at`);
    return unrecorded({ commit, args, readAt, legacy });
  };
}

/** The old bytes of a file the series recorded nothing for, from git at its commit (the generated files: made again by `legacy`). */
function unrecorded({ commit, args, readAt, legacy }) {
  if (args.rel === DERIVED) return packagesMdAt({ commit, ...args, readAt });
  if (!GENERATED_FILES.includes(args.rel)) return templateAt({ commit, ...args, readAt });
  return legacy === undefined ? unknown(`the series recorded no bytes for ${args.rel} and nothing can make them again`) : legacy({ commit, ...args, readAt });
}

/** What the series recorded at `init` for the file: a sha256, `null` (the workspace had no such file), or undefined (nothing recorded). */
function recordedFor(inputs, { stack, kind, rel }) {
  if (rel === DERIVED) return inputs?.packagesMdSha256?.[stack];
  return GENERATED_FILES.includes(rel) ? inputs?.generatedSha256?.[stack]?.[rel] : inputs?.templateHashes?.[`${stack}/${kind}/${rel}`];
}

/** The commit of the series' subject when it is a full hash git can be asked for, else null. */
const usableCommit = inputs => (typeof inputs?.subject?.sha === 'string' && COMMIT.test(inputs.subject.sha) ? inputs.subject.sha : null);

/**
 * The judge of one change to a frozen file: `'agent'` | `'harness'` | `{ undecided: why }`. Without a `history` every change is the agent's, as it always was. A file that is gone, or is not a regular file, is the agent's
 * (no bytes can equal the old ones) unless the file did not exist when the series began: then its absence is the harness's change. A movable file whose bytes equal the old bytes is the harness's; whose old bytes cannot be read is
 * undecided; any other is the agent's.
 */
export const driftDecider = args => change => (isJudged(args, change) ? verdictOf(args, change) : 'agent');

/** True when the change can be the harness's: there is a history and the file is one the harness can move. */
const isJudged = ({ stack, history }, change) => history !== undefined && isMovable(stack, change.path);

const isAbsence = old => old.known && old.absent === true;
const bytesOf = (treeSha, rel) => treeSha?.[rel];

/** The verdict on a judged change: a file that did not exist is the harness's change when it is missing now; else the tree must still have its bytes. */
function verdictOf({ stack, kind, treeSha, history }, change) {
  const old = history({ stack, kind, rel: change.path });
  if (isAbsence(old)) return change.state === 'absent' ? 'harness' : 'agent';
  return typeof bytesOf(treeSha, change.path) === 'string' ? byBytes(old, bytesOf(treeSha, change.path)) : 'agent';
}

/** The old bytes unreadable is undecided, the tree's bytes equal to them the harness's, any other the agent's. */
const byBytes = (old, here) => (old.known ? (here === old.sha256 ? 'harness' : 'agent') : { undecided: old.why });
