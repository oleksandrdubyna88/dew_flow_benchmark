// The owned-name helper of common.test-leftovers, bound to this repository's token: every folder a test makes comes from here,
// and a Python child gets its folder through TMPDIR. Imported from the conventions mount, never copied.
import { ownedFor } from '../../../../.agents/conventions/tools/lib/owned.mjs';

export const owned = ownedFor({ token: 'bench' });
