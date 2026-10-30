// The pure planning of a bare session (E3b.S1): the rids, the CPU masks, which stack is launched and scored first, the prompt, the files of a
// workspace and the plan `launchSession` takes. Nothing here reads a file but the registered prompts, and nothing a model can see names the arm
// or the stack: a path holds a series id and a rid, `BENCH_CELL` is `cell-<rid>`.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { EGRESS_HOSTS } from './egress-hosts.mjs';

/** The registered caps of a bare session (parent 4.2, 8): $6, 60 min wall, 45 min stall. */
export const BARE_CAPS = Object.freeze({ capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 });
export const BARE_STACKS = Object.freeze(['csharp', 'node']);
const PHASES = Object.freeze(['v1', 'v2']);
const MASKS = Object.freeze(['0-5', '8-13']);

const hash = text => createHash('sha256').update(text).digest();
const hex6 = text => hash(text).toString('hex').slice(0, 6);
const bitOf = text => hash(text).readBigUInt64BE(0) % 2n === 1n;

/** The rid of a data session: `sha256('<seed>:bare:p<pair>:<stack>:<phase>:a<attempt>')[0:6]` (registered; unique across the 24 sessions and their re-runs). */
export const ridOf = ({ seed, pair, stack, phase, attempt }) => hex6(`${seed}:bare:p${pair}:${stack}:${phase}:a${attempt}`);

/** The rid of an A/A, pilot or rehearsal session, from a random value the caller drew (these have no seed: the seed is revealed after them). */
export const pilotRid = rand => hex6(`pilot:${rand}`);

/** `s-<yyyymmdd>-<4 hex>`: the series directory under `~/rsd/minigit/`. */
export const seriesId = ({ date, rand }) => `s-${date.toISOString().slice(0, 10).replaceAll('-', '')}-${rand}`;

/** The CPU mask of a stack in a pair: the two stacks hold `0-5` and `8-13`; they swap every pair; the seed decides pair 1. Core 3 (CPUs 6-7) is the harness's. */
export function maskFor({ seed, pair, stack }) {
  const csharpHigh = bitOf(`${seed}:bare:mask`) !== ((pair - 1) % 2 === 1);
  const high = stack === 'csharp' ? csharpHigh : !csharpHigh;
  return MASKS[high ? 1 : 0];
}

/** Which stack is launched first, and scored first, in a phase of a pair: a seeded bit, so neither stack always leads. */
export const firstStack = ({ seed, pair, phase }) => BARE_STACKS[bitOf(`${seed}:bare:p${pair}:${phase}:first`) ? 1 : 0];

const REGISTERED_PROMPTS = new URL('../inputs/prompts-bare.json', import.meta.url);

/** The registered prompt bytes of a stack and phase (`inputs/prompts-bare.json`, derived from parent 4.3 by `prereg/extract-prompts.mjs`). */
export function promptFor({ stack, phase, registered = JSON.parse(readFileSync(REGISTERED_PROMPTS, 'utf8')) }) {
  if (!BARE_STACKS.includes(stack)) throw new Error(`stack '${stack}': the legal stacks are ${BARE_STACKS.join(', ')}`);
  if (!PHASES.includes(phase)) throw new Error(`phase '${phase}': the legal phases are ${PHASES.join(', ')}`);
  return registered.prompts[stack][phase].text;
}

/** The pinned input files a workspace of this phase holds, and nothing else (v2 keeps the v1 inputs: the agent's tree already holds them). */
export const workspaceFiles = phase => (phase === 'v1' ? ['SPEC-v1.txt', 'test-v1.sh'] : ['SPEC-v1.txt', 'test-v1.sh', 'SPEC-v2.txt', 'test-v2.sh']);

/**
 * The plan `launchSession` takes for one bare session (E2.S2's entry point); every value an input of it, none read from the environment. `over` is the test seam (a stub upstream,
 * short ticks) and is spread LAST, so a real run passes none.
 */
export function bareSessionPlan({ layout, stack, phase, claudeBinary, cliVersion, nodeDir, tsDir, token, dummy, prices, operatorHome, harnessSources, caps = BARE_CAPS, envExtra = {}, over = {} }) {
  return {
    layout, arm: 'bare', cellId: `cell-${layout.rid}`, prompt: promptFor({ stack, phase }), ...caps,
    claudeBinary, cliVersion, nodeDir, tsDir, toolchains: [tsDir], token, dummy, prices, allow: EGRESS_HOSTS.bare, operatorHome, harnessSources, envExtra, ...over,
  };
}
