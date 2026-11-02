// T-L53 (parent D59): a probe record is never overwritten. The default name is `exec-probe-<date>[-<n>].json`: the first of the day has no suffix, the next one `-2`, then `-3`. A record is
// evidence of one run on one layer identity; a second run on the same day is a second record.
import { existsSync, mkdirSync, statSync, unlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Thrown when a record's path is taken: the file is not touched. */
export class RecordExists extends Error {
  constructor(file) {
    super(`${file} already exists: a probe record is never overwritten (T-L53); name a new file or take the default, which numbers a second run of the day`);
    this.name = 'RecordExists';
    this.file = file;
  }
}

/** The name of the `n`-th record of `date`: `exec-probe-<date>.json` for 1, `exec-probe-<date>-<n>.json` after. */
export const recordFileName = (date, n) => (n === 1 ? `exec-probe-${date}.json` : `exec-probe-${date}-${n}.json`);

/** The first free default path of `date` in `inputsDir` (`exists` is the seam of the tests). */
export function nextRecordPath({ inputsDir, date, exists = existsSync }) {
  if (!DATE.test(date)) throw new Error(`nextRecordPath: the date must be YYYY-MM-DD, got ${JSON.stringify(date)}`);
  let n = 1;
  while (exists(path.join(inputsDir, recordFileName(date, n)))) n += 1;
  return path.join(inputsDir, recordFileName(date, n));
}

/** How many names `reserveNextRecord` tries before it gives up (V3): a bound, so a folder full of records ends with an error and never a hang. */
export const RESERVE_ATTEMPTS = 1000;

/**
 * Reserve `file` BEFORE the probe starts (P1): an empty placeholder created exclusively (`wx`: O_CREAT|O_EXCL), so a second probe pointed at the same path is refused at once — it never runs the full workload
 * toward a path another probe holds. The folder is made first (V2). Throws `RecordExists` when the path is taken, and an error with `exitCode` 4 naming the path when it cannot be created at all.
 */
export function reserveRecord(file) {
  try {
    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, '', { flag: 'wx' });
  } catch (error) {
    if (error.code === 'EEXIST' && existsSync(file)) throw new RecordExists(file);
    throw Object.assign(new Error(`the record path ${file} cannot be created (${error.code ?? error.message}): a probe needs a folder it can write`), { exitCode: 4 });
  }
  return { file };
}

/** Reserve the first free default path of `date` in `inputsDir` (made if missing): the next `-n` name, created at once, so two default probes take two names. Its own index rises on every refusal; `reserve` is the test seam. */
export function reserveNextRecord({ inputsDir, date, reserve = reserveRecord }) {
  for (let n = 1; n <= RESERVE_ATTEMPTS; n += 1) {
    try {
      return reserve(path.join(inputsDir, recordFileName(date, n)));
    } catch (error) {
      if (!(error instanceof RecordExists)) throw error;
    }
  }
  throw Object.assign(new Error(`no free record name for ${date} in ${inputsDir} after ${RESERVE_ATTEMPTS} attempts (exec-probe-${date}[-<n>].json are all taken)`), { exitCode: 4 });
}

/** Release a reservation after a failure: an EMPTY placeholder is removed, so a failed probe leaves no empty record; a file with content (a written record) is never touched. */
export function releaseRecord(held) {
  try {
    if (statSync(held.file).size === 0) unlinkSync(held.file);
  } catch {
    // already gone: nothing is held
  }
}
