// The pin of the harness that scores a session, against the one a series was made under. The runner (`lang-bench/runner/`) is re-pinned when it changes, and a session's workspace holds the runner files of the day it began; a
// later scoring of that tree (`rescore`, `replay`) sees them differ from today's frozen set. Whether the runner has moved since the series was made decides how such a difference is reported (see `copy-quick.mjs`).
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { SPIKE } from './paths.mjs';

const RUNNER_PIN_FILE = path.resolve(SPIKE, '..', 'lang-bench', 'inputs', 'specs.d', 'runner.json');

/** The runner pin of this checkout: `{ version, manifestSha256 }`. */
export function runnerPinNow(file = RUNNER_PIN_FILE) {
  const { runner } = JSON.parse(readFileSync(file, 'utf8'));
  return Object.freeze({ version: runner.version, manifestSha256: runner.manifestSha256 });
}

/** Can the runner files in a session's tree be older than today's? True when the series recorded no runner pin (it was made before the pin was recorded) or recorded another one. */
export function runnerDriftPossible({ inputs, now = runnerPinNow() }) {
  const recorded = inputs?.runnerPin;
  return recorded === undefined || recorded.manifestSha256 !== now.manifestSha256;
}

const RUNNER_REPORT_FILE = path.resolve(SPIKE, '..', 'lang-bench', 'runner', 'rs_report.py');

/** What the runner's JSON result must say it is: `{ format, version }`, the result format the runner writes (read from its report module) and the pinned runner version (read from the pin): never retyped here. */
export function runnerRecordPinNow({ reportFile = RUNNER_REPORT_FILE, pinFile = RUNNER_PIN_FILE } = {}) {
  const format = /^RESULT_FORMAT\s*=\s*(\d+)\s*$/m.exec(readFileSync(reportFile, 'utf8'));
  if (format === null) throw new Error(`${reportFile} has no RESULT_FORMAT: the runner's result format cannot be read`);
  return Object.freeze({ format: Number(format[1]), version: runnerPinNow(pinFile).version });
}
