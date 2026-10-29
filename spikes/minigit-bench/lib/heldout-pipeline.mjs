// Calibration through the E1.S1 pipeline: one subject (a reference or a mutant, as a tree holding only `minigit`) scored the way a
// session is -- build, the harness's fixed visible scripts, the held-out suite, the upstream originals -- in one phase.
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { defaultDeps } from './score-deps.mjs';
import { hundredPercent, runScoring, scoringPlan } from './score-pipeline.mjs';

/**
 * `pipelineScorer({ inputs, heldout, sessionRoot })` -> the `scoreSubject` calibrate() takes: resolves to
 * { heldout: <heldoutScore | { known: false, why }>, visible100: boolean|null }. A scoring that produced nothing is unknown, never a pass.
 */
export function pipelineScorer({ inputs, heldout, sessionRoot }) {
  return async function scoreSubject({ name, phase, treeDir }) {
    const sessionDir = path.join(sessionRoot, `${name}-${phase}`);
    mkdirSync(sessionDir, { recursive: true, mode: 0o700 });
    const plan = scoringPlan({ arm: 'bare', stack: 'node', phase, sourceDir: treeDir, sessionDir });
    if (!plan.ok) return { heldout: { known: false, why: plan.reason }, visible100: null };
    const scored = await runScoring(plan.value, defaultDeps({ inputs, heldout }));
    if (!scored.ok) return { heldout: { known: false, why: scored.reason }, visible100: null };
    return { heldout: scored.value.heldout, visible100: scored.value.visible.every(hundredPercent) };
  };
}
