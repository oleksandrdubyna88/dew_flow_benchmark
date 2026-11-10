// The three verbs of the clear held-out folder (F2.S2 (d), bench PR 4a): `heldout-open`, `heldout-close`, `heldout-check`. All FREE: nothing here launches a session or spends. Exit 0 done | 4 a refusal or a usage error.
// Every line they say names tasks, counts and states only (D41): never a case text, a case id, a trap name or a key. `io.heldout` holds the test seams (`isAlive`, `mountOf`, `unseal`, `unpackTo`, `fs`, `hooks`, `now`, `retry`).
//
//   heldout-open  --task NN [--key-file <file>] [--key-backup <file> --share-mount <dir>]   make the clear folder from the archive, held for the operator until `heldout-close`
//   heldout-close --task NN --key-backup <file> --share-mount <dir> [--key-file <file>]      remove the clear folder and its stamp, after the key's backup is verified on the share's mount
//   heldout-check --tasks NN,NN [--key-file <file>] [--key-backup <file> --share-mount <dir>]   read only: the entry, the archive, the key and the unsealed tree of each task
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { backupSettings } from './heldout-backup-config.mjs';
import { checkHeldout } from './heldout-check.mjs';
import { closeClearFolder, openClearFolder } from './heldout-lifecycle.mjs';
import { loadExplore } from './inputs.mjs';
import { isQuick } from './quick.mjs';
import { resolveTask, taskFromValues } from './tasks.mjs';

const EXIT = Object.freeze({ done: 0, config: 4 });

/**
 * The held-out context of a command, without its task: the operator's flags, the private backup file, then the test seams (`io.heldout`, `io.frozenFile`). Every entry point that may remove a stale clear folder is given it as
 * `io.heldoutArgs`. -> `ok({ frozenFile?, keyFile?, backupFile?, shareMount?, ...seams })` | `fail`.
 */
export function heldoutBase(values, io, { backup: wantsBackup = true } = {}) {
  const backup = wantsBackup ? backupSettings(values, io.home) : ok({});
  return backup.ok ? ok({ ...frozenPart(io), ...keyPart(values), ...backup.value, ...(io.heldout ?? {}) }) : backup;
}

const frozenPart = io => (io.frozenFile === undefined ? {} : { frozenFile: io.frozenFile });
const keyPart = values => (values['key-file'] === undefined ? {} : { keyFile: path.resolve(values['key-file']) });

/** The arguments of the held-out operations for one task. -> `ok(args)` | `fail`. */
function argsFor({ values, io, task }) {
  const base = heldoutBase(values, io);
  return base.ok ? ok({ home: io.home, task, ...base.value }) : base;
}

/** Does the command read a quick task's folder, so that the key's backup settings matter? The task is resolved first; an unresolved one is the command's own refusal; with no `--task` (leakcheck) every task is read, the quick ones included. */
function readsQuickTask(values, io) {
  if (values.task === undefined) return true;
  const given = taskFromValues(values, io.exploreFn);
  return given.ok && isQuick(given.value.task);
}

/**
 * A command that reads a task's held-out folder, given the held-out context in `io.heldoutArgs`. The private backup file is read, and a malformed one refuses the command (exit 4) before it starts, only for a quick task: an olympiad
 * task never reads it.
 */
export const heldoutAware = command => async (values, io) => {
  const base = heldoutBase(values, io, { backup: readsQuickTask(values, io) });
  if (!base.ok) return refused(io, base.reason);
  return command(values, { ...io, heldoutArgs: base.value });
};

const refused = (io, reason) => { io.say(reason); return EXIT.config; };

/** `ok({ explore, task })` for a quick task, else a `fail`: only the quick tasks have a sealed held-out folder. */
function quickTaskOf(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  if (!given.ok) return given;
  return isQuick(given.value.task) ? given : fail(`task ${given.value.task.id} is not a quick-run task: only the quick tasks have a sealed clear held-out folder`);
}

/** The arguments of `heldout-open` and `heldout-close`: a quick task, the flags and the private file. -> `ok(args)` | `fail`. */
function prepared(values, io) {
  const given = quickTaskOf(values, io);
  return given.ok ? argsFor({ values, io, task: given.value.task }) : given;
}

const takeoverLine = o => `took over the lock of pid ${o.pid} (started ${o.startTime}), whose owner is gone`;
const closeFlags = args => `--key-backup ${args.backupFile ?? '<file>'} --share-mount ${args.shareMount ?? '<dir>'}`;
const openLine = (args, state) => `task ${args.task.id}: the clear folder is ${state === 'opened' ? 'open, made from the archive' : 'already open, used as it is'}; it is held until \`explore.mjs heldout-close --task ${args.task.id} ${closeFlags(args)}\``;

/** `heldout-open --task NN`. */
export async function cmdHeldoutOpen(values, io) {
  const args = prepared(values, io);
  const r = args.ok ? await openClearFolder(args.value) : args;
  if (!r.ok) return refused(io, r.reason);
  if (r.value.takeover) io.say(takeoverLine(r.value.takeover));
  io.say(openLine(args.value, r.value.state));
  return EXIT.done;
}

/** `heldout-close --task NN`. */
export async function cmdHeldoutClose(values, io) {
  const args = prepared(values, io);
  const r = args.ok ? await closeClearFolder(args.value) : args;
  if (!r.ok) return refused(io, r.reason);
  io.say(`task ${args.value.task.id}: ${r.value.state === 'closed' ? 'the clear folder is removed; the archive and the entry are kept' : 'there is no clear folder: nothing to remove'}`);
  return EXIT.done;
}

/** The legal quick tasks named by `--tasks`: `ok([task])` | `fail` naming the first that is not one. */
function quickTasks(explore, ids) {
  const named = ids.map(id => resolveTask(explore, id.trim()));
  const bad = named.find(t => !t.ok || !isQuick(t.value));
  return bad === undefined ? ok(named.map(t => t.value)) : fail(bad.ok ? `task ${bad.value.id} is not a quick-run task` : bad.reason);
}

/** The sentence refusing a `--tasks` list that is empty or has an empty entry (a doubled, leading or trailing comma), or null: said before any task is resolved. */
function tasksSyntaxProblem(tasks) {
  if (typeof tasks !== 'string' || tasks.trim() === '') return 'heldout-check needs --tasks NN,NN';
  return tasks.split(',').some(id => id.trim() === '') ? `--tasks is NN,NN: ${JSON.stringify(tasks)} has an empty entry (a doubled, leading or trailing comma)` : null;
}

/** The tasks of `--tasks NN,NN`: `ok([task])` | `fail` naming the syntax or the legal ids. */
function tasksOf(values, io) {
  const syntax = tasksSyntaxProblem(values.tasks);
  if (syntax !== null) return fail(syntax);
  const explore = (io.exploreFn ?? loadExplore)();
  return explore.ok ? quickTasks(explore.value, values.tasks.split(',')) : explore;
}

/** Check one task and say the line (or the refusal). -> true when it passed. */
async function checkOne({ values, io, task }) {
  const args = argsFor({ values, io, task });
  const r = args.ok ? await checkHeldout(args.value) : args;
  io.say(r.ok ? r.value.line : `task ${task.id}: REFUSED: ${r.reason}`);
  return r.ok;
}

/** `heldout-check --tasks NN,NN`: every task is checked and said, whatever the others gave; exit 4 when any refused. */
export async function cmdHeldoutCheck(values, io) {
  const tasks = tasksOf(values, io);
  if (!tasks.ok) return refused(io, tasks.reason);
  const passed = [];
  for (const task of tasks.value) passed.push(await checkOne({ values, io, task }));
  return passed.every(Boolean) ? EXIT.done : EXIT.config;
}
