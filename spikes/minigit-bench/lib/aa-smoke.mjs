// The committed smoke fixture's B-T1 for `aa-verdicts` (E3b.S4: G-4's subagent half is replaced by it): E2.S2's bare-session smoke on the pinned CLI, its verdict recomputed here from the fixture's own files with the
// registered price table and policy, never read from a typed line. `null` when it cannot be read: a replacement that cannot be shown replaces nothing.
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { smokeEvidence } from './aa-facts.mjs';
import { loadSmoke } from './smoke-load.mjs';
import { computeSmokeVerdicts } from './smoke-verdicts.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const FIXTURE = path.join(SPIKE, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');

/** `{ ok, cli, fixture, observed }` of B-T1 over the committed fixture, or null. */
export function smokeFixture({ fixtureDir = FIXTURE } = {}) {
  const policy = crosscheckPolicyFrom(SPIKE, {});
  if (!policy.ok) return null;
  return smokeEvidence({ fixtureDir, compute: dir => { const loaded = loadSmoke(dir); if (!loaded.ok) throw new Error(loaded.reason); return computeSmokeVerdicts(loaded.value, policy.value); } });
}
