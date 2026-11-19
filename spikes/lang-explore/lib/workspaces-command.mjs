// The three freeze modes of the workspaces (F2.S2 (d), D70), all FREE: nothing here launches a session or spends.
//
//   workspaces --tag <ref> --task-repo <task repository> --out <dir>   the twelve workspaces, exactly what `quickWorkspaceFiles` gives a session for the six tasks and both stacks, the spec and FORMAT read at <ref> (a tag, or a
//                                                                      commit), into <dir>/csharp/<NN-slug>/ and <dir>/node/<NN-slug>/ and the manifest <dir>/workspaces.json; <dir> is refused inside any git worktree
//   workspaces-copy --from <dir> --into <worktree of the task repository>   the separate, reviewed copy step: a clean worktree at the commit the folders were made from, only those folders, resumable
//   workspaces --check --root <checkout>   the twelve are built again from the checkout's HEAD and compared with the committed ones; the last line of a pass is `workspaces-check ok commit <HEAD> tasks … stacks …`
//
// Exit 0 done | 1 a finding of the check | 4 a refusal, a usage error or a configuration. `io.workspaces` holds the test seams (`limit`, `hooks`).
import { ok } from '../../minigit-bench/lib/result.mjs';
import { buildWorkspaces, freezeContext } from './workspaces-build.mjs';
import { checkLine, checkWorkspaces } from './workspaces-check.mjs';
import { copyWorkspaces } from './workspaces-copy.mjs';
import { runSteps } from './workspaces-pipeline.mjs';
import { FREEZE_STACKS, FREEZE_TASKS, commitOf } from './workspaces-sources.mjs';
import { MANIFEST, manifestOf, writeWorkspaces } from './workspaces-write.mjs';

const EXIT = Object.freeze({ done: 0, finding: 1, config: 4 });

const refused = (io, reason) => { io.say(reason); return EXIT.config; };

/** The flags a mode needs and the ones it must not be given. -> a usage sentence, or null. */
function usageProblem({ values, needs, forbids, verb }) {
  const missing = needs.filter(flag => values[flag] === undefined);
  if (missing.length > 0) return `${verb} needs ${missing.map(f => `--${f}`).join(', ')}`;
  const extra = forbids.filter(flag => values[flag] !== undefined);
  return extra.length > 0 ? `${verb} does not take ${extra.map(f => `--${f}`).join(', ')} in this mode` : null;
}

const contextStep = io => () => { const ctx = freezeContext({ home: io.home, exploreFn: io.exploreFn, limit: io.workspaces?.limit ?? null }); return ctx.ok ? ok({ ctx: ctx.value }) : ctx; };
const commitStep = async ({ values }) => { const sha = await commitOf(values['task-repo'], values.tag); return sha.ok ? ok({ sha: sha.value }) : sha; };
const buildStep = async ({ ctx, values, sha }) => { const built = await buildWorkspaces({ ctx, repo: values['task-repo'], sha }); return built.ok ? ok({ built: built.value }) : built; };
const writeStep = io => ({ values, built, sha }) => {
  const manifest = manifestOf({ ref: values.tag, sha, built, tasks: FREEZE_TASKS, stacks: FREEZE_STACKS });
  const written = writeWorkspaces({ out: values.out, built, manifest, hooks: io.workspaces?.hooks ?? {}, log: io.say });
  return written.ok ? ok({ written: written.value }) : written;
};

/** `workspaces --tag --task-repo --out`. */
async function writeMode(values, io) {
  const r = await runSteps([contextStep(io), commitStep, buildStep, writeStep(io)], { values });
  if (!r.ok) return refused(io, r.reason);
  io.say(`wrote ${Object.keys(r.value.built).length} workspaces of tasks ${FREEZE_TASKS.join(',')} from ${values.tag} (commit ${r.value.sha}) into ${r.value.written}, with ${MANIFEST}`);
  return EXIT.done;
}

const checkStep = async ({ ctx, values }) => { const checked = await checkWorkspaces({ root: values.root, ctx }); return checked.ok ? ok({ checked: checked.value }) : checked; };

/** `workspaces --check --root`. */
async function checkMode(values, io) {
  const r = await runSteps([contextStep(io), checkStep], { values });
  if (!r.ok) return refused(io, r.reason);
  for (const line of r.value.checked.findings) io.say(line);
  if (r.value.checked.findings.length > 0) return EXIT.finding;
  io.say(checkLine(r.value.checked.commit));
  return EXIT.done;
}

const MODES = Object.freeze({
  write: { needs: ['tag', 'task-repo', 'out'], forbids: ['root', 'from', 'into'], run: writeMode },
  check: { needs: ['root'], forbids: ['tag', 'task-repo', 'out', 'from', 'into'], run: checkMode },
});

/** `workspaces`. */
export async function cmdWorkspaces(values, io) {
  const mode = MODES[values.check === true ? 'check' : 'write'];
  const problem = usageProblem({ values, needs: mode.needs, forbids: mode.forbids, verb: 'workspaces' });
  return problem === null ? mode.run(values, io) : refused(io, problem);
}

/** `workspaces-copy`. */
export async function cmdWorkspacesCopy(values, io) {
  const problem = usageProblem({ values, needs: ['from', 'into'], forbids: ['tag', 'task-repo', 'out', 'root', 'check'], verb: 'workspaces-copy' });
  if (problem !== null) return refused(io, problem);
  const r = await copyWorkspaces({ from: values.from, into: values.into, hooks: io.workspaces?.hooks ?? {}, log: io.say });
  if (!r.ok) return refused(io, r.reason);
  io.say(`workspaces-copy done: ${r.value.copied.length} copied, ${r.value.skipped.length} skipped (already in place and equal)`);
  return EXIT.done;
}
