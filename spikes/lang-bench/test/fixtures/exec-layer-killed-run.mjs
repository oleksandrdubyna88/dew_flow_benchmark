// Started by exec-layer-cleanup.test.mjs and killed with SIGKILL mid-case: a layer run whose program sleeps. Nothing here cleans up after itself; that is the point.
import { owned } from '../support/owned.mjs';
import { toolchains } from '../support/exec-layer-host.mjs';
import { runCases } from '../../lib/exec-layer/run.mjs';

const { parent, tree, samplesDir } = JSON.parse(process.argv[2]);
await runCases({ owned, parent, tree, samplesDir, caseIds: ['s01'], dirs: toolchains, memoryMaxBytes: 256 * 1024 * 1024, caseWallMs: 600_000, runCeilingMs: 900_000, minSweepAgeMs: 0 });
