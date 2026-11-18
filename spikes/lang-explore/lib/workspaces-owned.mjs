// The owner-rule helper of the freeze writers, bound to the harness's token (`common.test-leftovers`): one constant, so the staging folders of `workspaces` and `workspaces-copy` are named, claimed and swept one way.
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { OWNED_TOKEN } from './score-quick.mjs';

export const owned = ownedFor({ token: OWNED_TOKEN });
