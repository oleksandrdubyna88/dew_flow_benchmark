// E1.S8b, D55: the memory policy of the series, as the layer knows it. The policy is ONE identity (parent §6.1): `inputs/specs.d/memory-policy.json` is part of the layer's identity (`identity.mjs`),
// the probe reads it (T-L43), and a data run refuses a policy other than the pinned one (`memory-view-mismatch`, exit 3): the keys `env.mjs` derives are this policy's, and a layer run under another
// policy would be a run under a rule nobody decided for this series.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/** The decided policy (D55, 2026-11-02): each program sees its own cgroup read-only; .NET gets `DOTNET_GCHeapHardLimit` = ⌊0.9·L⌋; Node gets `--max-old-space-size` = ⌊0.9·L / 1 MiB⌋. */
export const MEMORY_POLICY = 'cgroup-view-own+gchl-90+node-old-space-90';
export const HALT_POLICY = 'memory-view-mismatch';
export const POLICY_FILE = fileURLToPath(new URL('../../inputs/specs.d/memory-policy.json', import.meta.url));

/** `{ matches, found }`: the policy the file names (`null` when it is missing, malformed or undecided) and whether it is the pinned one. */
export function policyVerdict(file = POLICY_FILE) {
  try {
    const found = JSON.parse(readFileSync(file, 'utf8')).policy ?? null;
    return { matches: found === MEMORY_POLICY, found };
  } catch {
    return { matches: false, found: null };
  }
}
