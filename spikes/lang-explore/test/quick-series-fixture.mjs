// A rehearsal-named series of a quick task in a temp home, with one ended void session (its workspace on disk), a charge in its ledger and a fixture of held-out cases: the ground of the rescore and replay tests. Not a test file.
import { appendLedger } from '../../minigit-bench/lib/ledger.mjs';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickPlan } from '../lib/quick.mjs';
import { createSeries, ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { pinsOfTask, resolveTask } from '../lib/tasks.mjs';

export const EXPLORE = loadExplore().value;
export const PLAN = quickPlan(EXPLORE);
export const TASK = resolveTask(EXPLORE, PLAN.planTasks[0]).value;
export const LIMITS = { timeMs: 300000, memoryBytes: 536870912, buildTimeoutMs: 600000 };
export const VOID = { rid: 'a1', pair: 1, stack: 'node', phase: 'v1', attempt: 1, kind: 'pilot', label: 'paired', status: 'ran', class: 'void: contaminated', data: false, mask: '8-13', dummy: 'sk-ant-oat01-dummy-x', run: { flags: ['kept'] } };

/** A rehearsal-named series of a quick task with one ended void session (its workspace on disk), a charge in its ledger and a fixture of held-out cases. */
export function world(over = {}, { prefix = 'r' } = {}) {
  const home = tmpDir('rescore-run');
  const made = createSeries({ home, explore: EXPLORE, calibration: { limits: LIMITS }, task: TASK.id, prefix, inputsExtra: { ...(prefix === 'r' ? { rehearsal: true } : {}), taskPins: pinsOfTask(TASK), ...over } });
  const dir = made.value.dir;
  const map = JSON.parse(readFileSync(runMapFileOf(dir), 'utf8'));
  writeRunMap(runMapFileOf(dir), { ...map, sessions: { ...map.sessions, a1: VOID } });
  appendLedger(ledgerFileOf(dir), { kind: 'pilot', usd: 4.25, what: 'completed', rid: 'a1', t: '2026-11-05T10:00:00Z' });
  const layout = layoutPaths(path.join(dir, 's'), 'a1');
  mkdirSync(layout.work, { recursive: true });
  mkdirSync(path.join(dir, 'rehearsal', 'fixture', 'cases'), { recursive: true });
  return { home, dir, layout };
}

