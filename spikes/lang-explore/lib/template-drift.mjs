// Template drift on a kept tree, judged per file by BYTES (F1.S3 (b), review round). A frozen file that is a template file (a script or a pin file) or the derived `PACKAGES.md` can differ from today's bytes because the harness moved since
// the session began or because the agent changed it. It is the harness's only when the tree's bytes equal the bytes the file had when the session began (`history`); any other difference, a deletion included, is the agent's; where the
// old bytes cannot be read the file is `undecided` and is charged to nobody (the deviations are then unknown). `templateHistory` answers "what bytes did this file have when the series began": from the map the series recorded at `init`,
// else from git at the commit its subject recorded; a series made before either existed that git cannot show is unknown, never a guess.
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { templateFilesOf } from '../../lang-bench/lib/frozen-set.mjs';
import { allWorksMatrix } from '../../lang-bench/lib/package-matrix.mjs';
import { renderPackagesMd } from '../../lang-bench/lib/packages-md.mjs';
import { REPO } from './paths.mjs';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const known = hash => ({ known: true, sha256: hash });
const unknown = why => ({ known: false, why });
const DERIVED = 'PACKAGES.md';
const INPUTS = 'spikes/lang-bench/inputs';
const COMMIT = /^[0-9a-f]{40}$/;

/** True when a change to `rel` can be the harness's move: a template file of the stack (a script, a pin file) or the derived packages list. */
export const isMovable = (stack, rel) => rel === DERIVED || templateFilesOf(stack).includes(rel);

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
 * -> `({ stack, kind, rel }) => { known: true, sha256 } | { known: false, why }`: the bytes the file had when the series began. `inputs` is the series' inputs: `templateHashes` and `packagesMdSha256` when it recorded them,
 * `subject.sha` (the commit of the harness it was made with) otherwise. `readAt(commit, path)` is the seam for git.
 */
export function templateHistory({ inputs, readAt = readAtCommit }) {
  return args => {
    const recorded = recordedFor(inputs, args);
    if (typeof recorded === 'string') return known(recorded);
    const commit = usableCommit(inputs);
    if (commit === null) return unknown(`the series recorded no bytes for ${args.rel} and no commit git could show them at`);
    return args.rel === DERIVED ? packagesMdAt({ commit, ...args, readAt }) : templateAt({ commit, ...args, readAt });
  };
}

/** The sha256 the series recorded at `init` for the file, or undefined. */
const recordedFor = (inputs, { stack, kind, rel }) => (rel === DERIVED ? inputs?.packagesMdSha256?.[stack] : inputs?.templateHashes?.[`${stack}/${kind}/${rel}`]);

/** The commit of the series' subject when it is a full hash git can be asked for, else null. */
const usableCommit = inputs => (typeof inputs?.subject?.sha === 'string' && COMMIT.test(inputs.subject.sha) ? inputs.subject.sha : null);

/**
 * The judge of one change to a frozen file: `'agent'` | `'harness'` | `{ undecided: why }`. Without a `history` every change is the agent's, as it always was. A file that is gone, or is not a regular file, is the agent's
 * (no bytes can equal the old ones); a movable file whose bytes equal the old bytes is the harness's; whose old bytes cannot be read is undecided; any other is the agent's.
 */
export const driftDecider = args => change => (isJudged(args, change) ? verdictOf(args, change) : 'agent');

/** True when the change can be the harness's: there is a history, the file is movable and the tree still had its bytes (it was not deleted or replaced by a link). */
const isJudged = ({ stack, treeSha, history }, change) => history !== undefined && isMovable(stack, change.path) && typeof treeSha?.[change.path] === 'string';

/** The verdict on a judged change: the old bytes unreadable is undecided, the tree's bytes equal to them the harness's, any other the agent's. */
function verdictOf({ stack, kind, treeSha, history }, change) {
  const old = history({ stack, kind, rel: change.path });
  if (!old.known) return { undecided: old.why };
  return treeSha[change.path] === old.sha256 ? 'harness' : 'agent';
}
