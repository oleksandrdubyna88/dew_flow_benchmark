// Fixed inputs of the quick pilot. Nothing here is read from the environment.
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const SPIKE_DIR = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const REPO_ROOT = path.resolve(SPIKE_DIR, '..', '..');
export const INPUTS_DIR = path.join(SPIKE_DIR, 'inputs');
export const CREDS_CLONE = 'D:/rsd/dew_flow_creds_for_devs';
export const CONV_CLONE = 'D:/rsd/dew_flow_conventions';
export const DEFAULT_RUNS_ROOT = 'D:/rsd/_policy-ab-runs';
export const P = '9f62e4db07a05e247b0fa25ea380cac21909b323';
export const S = '9369b4d3093c0852b9969b53ba8c1912141398db';
export const ARM_SHAS = Object.freeze({
  A: '3df745a7ce9959c251c65dd688c16be0295e8724',
  B: 'ea3252f2bd88db1fc7e698c2c9a1a58ca7b916c0',
});
export const MOUNT = '.claude/rules/shared';
export const OVERLAY_RULES = Object.freeze(['common/subagent-models.md', 'common/planning-docs.md']);
export const PLAN_FILE = 'todo/PLAN_typed_stored_secrets.md';
export const CELLS = Object.freeze({ A1: 'A', B: 'B', A2: 'A' });
export const GUARD_SOURCE = 'settings/hooks/subagent-model.mjs';
export const WEB_DIR = 'src_vs_code';
export const AUDIT_REL = '.git/policy-audit.ndjson';
