#!/usr/bin/env node
// The freeze tools of the language comparison v2 (F2.S2 (d)): the checks and the two writers of the one freeze, over the task repository. Each verb reads, prints and exits; only `hashes --write` and `record` write,
// one file each, through an owned temporary file and one rename.
//
//   plan --repo <task> --ref <commit> --date <yyyymmdd> [--needles <file>]
//       Prints, as one `git apply` patch, the freeze commit's edits: in each of the six specs the `Freeze tag` row set to `spec-frozen-B-<date>`, and nothing else. It refuses first (and prints nothing) for a spec whose Gate verdict is
//       still its placeholder or is gated under another FORMAT record than B's, a conformance check that is not all ok, a FORMAT that is not the one step 2a records, a task list that is not exactly the six, a date that is not today's UTC
//       date, a FORMAT class check that is not clean, a worktree that is not clean or not at --ref.                                                                                    Exit 0 a plan, 3 refused
//   check-commit --repo <task> --before <commit> --after <commit>
//       The freeze commit changes only the six `Freeze tag` rows, to one tag; any change to todo/tasks/FORMAT.md is refused, as is any other path.                                         Exit 0, 1
//   check-local-tag --repo <task> --tag <t> --commit <C> --message-file <f>
//       BEFORE the push: the tag is not on the remote, is annotated, points at exactly C, carries the intended message byte for byte, has the rows' name and date, and C is the squash-merge commit on the remote's main.   Exit 0, 1
//   check-tag --repo <task> --tag <t> --commit <C> [--message-file <f>]
//       AFTER the push: the remote's tag is annotated and peels to C, its object (fetched into a folder of its own) names the tag and C and is the local tag object's; the six specs are the gated ones with only their row set; FORMAT's
//       blob at C is the step-2a pin of `inputs/specs.d/format.json` (entry B), never a hash taken from the freeze commit.                                                              Exit 0, 1
//   check-workspace --repo <task> (--tag <t> | --base <C>) --commit <W>
//       C is an ancestor of W; every spec blob and FORMAT's blob at W equal C's; `git diff --name-only C W` lists only files of `csharp/<NN-slug>/` and `node/<NN-slug>/` of the six.       Exit 0, 1
//   hashes --repo <task> --commit <W> [--write]
//       The `inputs/hashes-lang.txt` of commit W, read from W's blobs (never the working tree): every file under csharp/, node/, runner/ and tasks/, plus FORMAT and the six specs, `sha256  path`, sorted. Printed, or with --write
//       published atomically and verified through `fetchTaskRepo`'s rules.                                                                                                                 Exit 0, 3
//   record --repo <task> --tag <t> --commit <W> --evidence <file> --workspaces-check <file> [--supersedes <old tag> --why <sentence>]...
//       Writes `inputs/specs.d/spec-states.json` (and only that file) after check-tag, check-workspace and the hash list of W all pass itself, the engine's workspaces check at W is clean (its last line, read, not called), W holds exactly the
//       twelve workspace folders and the three leak-check results are clean. `--supersedes` names an old tag that exists on the remote.                                                     Exit 0, 1 a check is red, 3 an input is refused
//
// Common: `--inputs <dir>` the bench's inputs folder (default spikes/lang-bench/inputs), `--record <file>` the FORMAT record (default <inputs>/specs.d/format.json). Exit 2 usage, 4 the environment (git, the network).
// Lowered before anything else runs (common.local-cpu-budget), as run-tests.mjs does.
await import('./lib/lower-priority.mjs');

const { runFreeze } = await import('./lib/freeze-cli.mjs');

const result = await runFreeze(process.argv.slice(2));
process.stdout.write(result.stdout);
process.stderr.write(result.stderr);
process.exitCode = result.exit;
