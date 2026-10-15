// Fixed inputs shared by every story of the policy A/B. Per-story constants (repo, P, S, mount, layout, plan, inputs,
// source folder) live in stories/<id>/story.json - see lib/story.mjs. Nothing here is read from the environment.
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const SPIKE_DIR = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const REPO_ROOT = path.resolve(SPIKE_DIR, '..', '..');
export const CONV_CLONE = 'D:/rsd/dew_flow_conventions';
export const ARM_SHAS = Object.freeze({
  A: '3df745a7ce9959c251c65dd688c16be0295e8724',
  B: 'ea3252f2bd88db1fc7e698c2c9a1a58ca7b916c0',
});
export const OVERLAY_RULES = Object.freeze(['common/subagent-models.md', 'common/planning-docs.md']);
export const NEUTRAL_CELLS = Object.freeze(['c1', 'c2', 'c3']);
export const GUARD_SOURCE = 'settings/hooks/subagent-model.mjs';
export const AUDIT_REL = '.git/audit.ndjson';
export const DEFAULT_CAP_USD = 30;
export const DEFAULT_STOP_AFTER_USD = 100;
