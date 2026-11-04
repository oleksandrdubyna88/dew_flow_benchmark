// Test plumbing: owner records for fixtures (a claim beside a folder, a lock file) built on the conventions' own grammar, and the pid of a process that has run and exited.
import { spawnSync } from 'node:child_process';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { formatOwner, thisOwner } from '../../../../.agents/conventions/tools/lib/owned.mjs';

/** The pid of a child that has already exited: no process has it now. */
export function exitedPid() {
  const child = spawnSync(process.execPath, ['-e', '0'], { timeout: 20_000 });
  assert.equal(child.status, 0);
  return child.pid;
}

/** The record of this process with some fields replaced (`start: undefined` drops the start time), as one line. */
export const recordOf = over => `${formatOwner({ ...thisOwner(), ...over })}\n`;

/** The claim `<dir>.owner` of a folder, written as the conventions' helper writes it. */
export const writeClaim = (dir, over = {}) => writeFileSync(`${dir}.owner`, recordOf(over));
