// The identity of the submission contract a scoring was made under (F1.S3 (b); parent D65). A sha256 over sorted `path<TAB>sha256` lines of the modules that decide what is copied, restored and built (the clean copy, the marked
// build, the frozen list, the build box, the offline settings, the excluded-folder list, the package matrix), every file under `spikes/lang-bench/templates/`, and the tree hashes of the two registered snapshots. It moves with exactly those and with nothing else: a fix to the
// scorer (`score-quick.mjs`) or a change to the statement's sentences does not move it. The scoring puts it into `score.json` (`explore.contractIdentity`) and the record reads it from there, so a record finished later still names the
// identity it was scored under. The templates' identity alone (`spikes/lang-bench/lib/templates-identity.mjs`, re-exported here) is what the package matrix is measured against, and a series records the bytes of every template file at `init` (`harness-pin.mjs`).
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { TEMPLATES_FOLDER } from '../../lang-bench/lib/templates-identity.mjs';
import { REPO } from './paths.mjs';

export { TEMPLATES_FOLDER, templatesIdentity } from '../../lang-bench/lib/templates-identity.mjs';

/** The files of the contract, from the repository root: the copy, the marked build, the frozen list, the build box, the offline settings, the excluded-folder list and the package matrix. */
export const CONTRACT_MODULES = Object.freeze([
  'spikes/lang-explore/lib/copy-quick.mjs', 'spikes/lang-explore/lib/build-quick.mjs', 'spikes/lang-bench/lib/frozen-set.mjs', 'spikes/lang-bench/lib/workspace-box.mjs', 'spikes/lang-bench/lib/cache-offline.mjs',
  // the folders a copy leaves out (the scorer's own list), and the offered set per kind (the package matrix)
  'spikes/minigit-bench/lib/score-deps.mjs', 'spikes/lang-bench/inputs/package-matrix.json',
]);
export const SNAPSHOT_RECORD = 'spikes/lang-bench/inputs/cache-snapshots-lang.json';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const lineOf = (rel, bytes) => `${rel}\t${sha256(bytes)}`;
const digest = lines => sha256([...lines].sort().join('\n'));

/** `path<TAB>sha256` of every regular file under the templates folder, paths from the repository root with `/`. */
function templateLines(root) {
  const base = path.join(root, TEMPLATES_FOLDER);
  return readdirSync(base, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => {
    const abs = path.join(e.parentPath, e.name);
    return lineOf(`${TEMPLATES_FOLDER}/${path.relative(base, abs).split(path.sep).join('/')}`, readFileSync(abs));
  });
}

/** The tree hash of each registered snapshot as a line: `snapshot/<stack><TAB><treeSha256>`. A stack the record lacks is a throw: a contract with no snapshot has no identity. */
function snapshotLines(root) {
  const record = JSON.parse(readFileSync(path.join(root, SNAPSHOT_RECORD), 'utf8'));
  return ['csharp', 'node'].map(stack => {
    const tree = record[stack]?.treeSha256;
    if (typeof tree !== 'string') throw new Error(`${SNAPSHOT_RECORD} has no registered ${stack} snapshot: the contract has no identity`);
    return `snapshot/${stack}\t${tree}`;
  });
}

/** The identity of the whole contract. */
export function contractIdentity({ root = REPO } = {}) {
  const modules = CONTRACT_MODULES.map(rel => lineOf(rel, readFileSync(path.join(root, rel))));
  return digest([...modules, ...templateLines(root), ...snapshotLines(root)]);
}
