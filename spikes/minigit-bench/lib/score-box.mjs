// The scoring box as the scorers build it (E2 amendment 2026-11-19, D-9): `scoreBoxArgv` of `lib/bwrap.mjs`, UNCHANGED, with the dotnet folders this box must not show inserted as empty tmpfs before
// its first bind. Why here and not in `bwrap.mjs`: that file is part of the language comparison's pinned execution-layer identity (`spikes/lang-bench/inputs/specs.d/exec-layer.json`, with every
// module it imports), so it stays byte for byte what the frozen spec hashed; this module is outside that identity (nothing of the layer imports it; `test/exec-layer-pin.test.mjs` holds both).
//
// The decision is the agent box's, from the same functions (`lib/dotnet-hide.mjs`), taken when the box is built:
//   - no pinned SDK directory: the box runs the distribution's dotnet and shows only `dotnetPin`'s versions (`boxDotnetHide`); a box with no pin is refused;
//   - a pinned SDK directory (`dotnetDir`): it is bound read-only and put on PATH with the other toolchains, every distribution root the host has is hidden whole, and nothing is listed;
//   - a bind that is, or holds, a dotnet folder the box hides is refused (`refuseBindOverHidden`): bound after the tmpfs, it would show the folder again.
// A scorer never calls `scoreBoxArgv` itself (`test/box-builders-scan.test.mjs`): v1's steps (`score-steps.mjs`) and the pilot's judge (`spikes/lang-explore/lib/score.mjs`) come through here.
import { existsSync } from 'node:fs';
import { bind, roBind, roRoot, scoreBoxArgv, tmpfs } from './bwrap.mjs';
import { boxDotnetHide, boxWholeHidden, dirEntriesOf, refuseBindOverHidden } from './dotnet-hide.mjs';
import { bindPairsOf, hiddenPathsOf } from './sandbox.mjs';

/** The flags as the primitives spell them: read from `bwrap.mjs`, never written a second time. */
const BIND_FLAGS = Object.freeze([bind('')[0], roBind('')[0]]);
const TMPFS_FLAG = tmpfs('')[0];
/** The read-only root bind, which follows the program name: an argv of `scoreBoxArgv` opens with it. */
const ROOT_BIND = Object.freeze(roRoot());
const AFTER_ROOT = 1 + ROOT_BIND.length;

/** The toolchain directories of a scoring box, the pinned SDK directory among them, each once: what is bound read-only and what PATH is made from. */
export const scoringToolchains = ({ toolchainDirs = [], dotnetDir = null }) => Object.freeze([...new Set([...toolchainDirs, ...(dotnetDir ? [dotnetDir] : [])])]);

const firstBindOf = argv => argv.findIndex((word, i) => i >= AFTER_ROOT && BIND_FLAGS.includes(word));
const lateTmpfsOf = ({ argv, firstBind }) => argv.findIndex((word, i) => i > firstBind && word === TMPFS_FLAG);
/** The shape `scoreBoxArgv`'s argv must have, in order, each as `[holds(found), what was found instead]`. */
const SHAPE = Object.freeze([
  [({ argv }) => ROOT_BIND.every((word, i) => argv[1 + i] === word), ({ argv }) => `does not open with its program and ${ROOT_BIND.join(' ')} (it opens with ${argv.slice(0, AFTER_ROOT).join(' ')})`],
  [({ firstBind }) => firstBind >= 0, () => 'binds nothing after its root'],
  [({ late }) => late < 0, ({ argv, firstBind, late }) => `holds a tmpfs (${argv[late + 1]}) after its first bind (${argv[firstBind + 1]})`],
]);

/**
 * Where the dotnet tmpfs go in an argv `scoreBoxArgv` built: the index of its first bind after the read-only root. The shape is ASSERTED, not assumed: the argv opens with its program and the root bind, it
 * binds something after that, and every tmpfs it already holds comes before that bind. Anything else throws, naming what was found: a tmpfs put at a guessed place could land after a bind and hide it.
 */
function insertionIndex(argv) {
  const found = { argv, firstBind: firstBindOf(argv) };
  const wrong = SHAPE.find(([holds]) => !holds({ ...found, late: lateTmpfsOf(found) }));
  if (wrong) throw new Error(`the scoring box argv ${wrong[1]({ ...found, late: lateTmpfsOf(found) })}: scoreBoxArgv changed its shape, and the dotnet tmpfs have no known place`);
  return found.firstBind;
}

/**
 * The argv prefix (ending in `--`) of a scoring box: `scoreBoxArgv`'s, for the same options, with the dotnet tmpfs inserted before its first bind. `dotnetPin` is the pinned SDK version (required
 * unless `dotnetDir`, a pinned SDK directory, is given); `exists` and `listDir` are injected as in `agentBoxPrefix`; `scoreBox` is the tests' seam for the shape assertion.
 */
export function scoringBoxArgv({ dotnetDir = null, dotnetPin, exists = existsSync, listDir = dirEntriesOf, scoreBox = scoreBoxArgv, ...box }) {
  const base = scoreBox({ ...box, toolchainDirs: scoringToolchains({ toolchainDirs: box.toolchainDirs, dotnetDir }), exists });
  const at = insertionIndex(base);
  const hidden = [...boxWholeHidden({ dotnetDir }).filter(p => exists(p)).flatMap(tmpfs), ...boxDotnetHide({ dotnetDir, dotnetPin, exists, listDir }).flatMap(tmpfs)];
  const argv = Object.freeze([...base.slice(0, at), ...hidden, ...base.slice(at)]);
  refuseBindOverHidden({ hidden: hiddenPathsOf(argv), binds: bindPairsOf(argv).map(b => b.box) });
  return argv;
}
