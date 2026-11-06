// The build of a quick-run tree in the build box, and what its end means.
//
// The shared build box (`lang-bench/lib/workspace-box.mjs`) answers `{ code, signal, timedOut, stdout, stderr }` and cannot say whether `bash build.sh` ever started: a failed `nsenter`, a failed `bwrap`
// or a failed seed step (`cp -r`) all end as a non-zero code, the same as a build the agent broke. So the command run in the box is a wrapper that prints MARKER to standard error, then `exec`s the build:
// the marker is the box's claim that the seed step succeeded and `build.sh` is now running. The marker is a positional argument of `bash -c`, never text of the script.
//
//   timedOut, marker present         the build ran past its bound (D65, 600 s): a SUBMISSION
//   timedOut, marker absent          the box, nsenter, bwrap or the seed step hung before the build began: `environment`
//   code === null, not timedOut      killed from outside (a signal the box did not send): `environment`, a void
//   marker absent                    nsenter, bwrap or the seed step failed before the build began: `environment`
//   marker present, any exit         the build ran: exit 0 passes, any other exit is a SUBMISSION (a failed build)
//
// What this cannot tell apart: a host fault DURING a marked build (a full disk, for example) is a non-zero exit of `build.sh` and reads as a submission. The code cannot see it; the build's tail is kept
// so a person can.
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { inBuildBox } from '../../lang-bench/lib/workspace-box.mjs';

export const BUILD_MARKER = '@@bench-build-started@@';
const TAIL_BYTES = 2000;
/** The wrapper: print the marker (argument 1) to standard error, drop it, `exec` the rest. */
export const MARKED_BUILD = Object.freeze(['bash', '-c', 'printf "%s\\n" "$1" >&2; shift; exec "$@"', 'marked-build', BUILD_MARKER, 'bash', 'build.sh']);

const environment = reason => fail(reason, { kind: 'environment' });
const markerless = text => text.replace(`${BUILD_MARKER}\n`, '');

/**
 * Why the build did not run to an end of its own, or null. A timeout WITH the marker is a submission (the build ran past its bound); a timeout WITHOUT it is the box, `nsenter`, `bwrap` or the seed step hanging before the
 * build began: the harness, so `environment`.
 */
function unfinishedProblem({ code, signal, stderr, timedOut }) {
  if (code === null && !timedOut) return `the build box was ended from outside (signal ${signal}) and the build did not finish`;
  return stderr.includes(BUILD_MARKER) ? null : notBegun({ code, timedOut, stderr });
}

const notBegun = ({ code, timedOut, stderr }) => `the build did not begin (${timedOut ? 'timed out' : `exit ${code}`}): the box, nsenter, bwrap or the seed step failed or hung: ${stderr.trim().slice(-300)}`;

/** What the box's answer means: `ok({ ok, exit, timedOut, wallMs, tail })` (the build ran) or an `environment` failure (it did not, or was killed from outside). The marker never reaches the tail. */
export function buildOutcome({ code, signal = null, timedOut = false, stdout = '', stderr = '', ms = 0 }) {
  const problem = unfinishedProblem({ code, signal, stderr, timedOut });
  if (problem) return environment(problem);
  return ok({ ok: code === 0 && !timedOut, exit: code, timedOut, wallMs: ms, tail: `${stdout}\n${markerless(stderr)}`.slice(-TAIL_BYTES) });
}

/** The default build: the marked `bash build.sh` in the build box of the stack, on the copy at `tree`. `inBox` is the seam of the tests. -> `ok({ ok, exit, wallMs, timedOut, tail })` | `fail`. */
export function boxBuild({ owned, dirs, snapshots, stack, inBox = inBuildBox }) {
  const record = { csharp: snapshots.csharp?.record, node: snapshots.node?.record };
  return async ({ tree, timeoutMs }) => {
    const ran = await inBox({ owned, view: tree, dirs, stack, record, caches: path.dirname(snapshots[stack].dir), command: [...MARKED_BUILD], timeoutMs });
    return ran.ok ? buildOutcome(ran.value) : environment(`the build box did not run: ${ran.reason}`);
  };
}
