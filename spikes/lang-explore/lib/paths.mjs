// Where things are: this spike, v1's spike (imported, never copied), and the pilot's private area outside every git tree. Pure: the home is an argument.
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const SPIKE = fileURLToPath(new URL('..', import.meta.url));
export const V1 = path.resolve(SPIKE, '..', 'minigit-bench');
export const REPO = path.resolve(SPIKE, '..', '..');
export const INPUTS = path.join(SPIKE, 'inputs');

/** `~/rsd/lang-v2/explore`: the records, the toolchains and the held-out side, disjoint from v2's `~/rsd/lang-v2/` series directories. */
export const exploreRoot = (home = os.homedir()) => path.join(home, 'rsd', 'lang-v2', 'explore');
export const heldoutDir = (home = os.homedir()) => path.join(exploreRoot(home), 'heldout');
export const toolchainsDir = (home = os.homedir()) => path.join(exploreRoot(home), 'toolchains');
export const snapshotWorkDir = (home = os.homedir()) => path.join(exploreRoot(home), 'snapshot-work');
export const calibrationFile = (home = os.homedir()) => path.join(exploreRoot(home), 'calibration.json');
export const spinFile = (home = os.homedir()) => path.join(exploreRoot(home), 'spin.json');

/** A path given as parts under the home (the registered inputs name paths this way, never an absolute one of one machine). */
export const underHome = (parts, home = os.homedir()) => path.join(home, ...parts);
