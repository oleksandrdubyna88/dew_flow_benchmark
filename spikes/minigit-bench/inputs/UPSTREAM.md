# Where the task inputs come from, and why they are not in this repository

The minigit stack bench scores agents against two specifications and two visible test scripts. None of those files is committed
here. They live in a public fork, and this repository holds only their hashes (`hashes.txt`), this note, and the code that
fetches and verifies them (`lib/fetch-inputs.mjs`).

| | |
|---|---|
| The fork | `oleksandrdubyna88/minigit-bench` (https://github.com/oleksandrdubyna88/minigit-bench), branch `inputs` |
| Pinned `inputs` commit | `df00cac8dae3f7bdc64a8cd5a5b7f777fec055c3` (the same value as `PINNED_INPUTS_SHA` in `lib/fetch-inputs.mjs`; a test keeps them equal) |
| Upstream | `mame/ai-coding-lang-bench` at `7f6ffdc`, byte-identical on these four files to `banteg/minigit-bench` at `83644e0` |
| Local cache | `~/.cache/minigit-bench/inputs/<sha>/`, outside every repository; `fetchInputs` clones into it once and checks every file against `hashes.txt` |

## What the fork's `inputs` branch holds

- `upstream/` -- the four upstream files (`SPEC-v1.txt`, `SPEC-v2.txt`, `test-v1.sh`, `test-v2.sh`), unchanged.
- The repository root -- the two specifications again, unchanged, and the two **fixed** visible test scripts the agents run and the
  scorer runs. Each fixed script begins with a header naming the upstream commit, the upstream file's sha256 and its edits.
- Removed from the branch: the upstream benchmark harness, plot and report scripts, results, figures and `CLAUDE.md`.

`hashes.txt` lists six files: the four under `upstream/`, and the two fixed scripts. The specifications at the root are byte copies
of the ones under `upstream/`, so the scorer compares a session's `SPEC-v*.txt` against the `upstream/` hash.

## The three edits to the visible scripts, in words

1. **The build fails loudly.** Upstream hides a failing `make` or `build.sh` (`2>/dev/null || true`), so a tree that does not build
   is scored by tests that all fail for an unstated reason. The fixed script prints `BUILD FAILED`, `PASSED: 0`, `FAILED: 1` and
   exits 1, and shows the build's own error output.
2. **A crash counts as failures.** Upstream starts with `set -e`, so one crashing command ends the script before any summary and
   the run reads as "no tests". The fixed script drops `set -e`, carries the number of tests it should run (11 for v1, 30 for v2;
   a test checks both against the count of test headers in the upstream scripts), and before the summary counts every test that
   did not run as a failure.
3. **The parent of a commit is checked (v2 only).** Upstream's checkout test (Test 20) and reset test (Test 24) call `pass` in both
   branches of their parent check, so a commit with the wrong parent passes. The fixed script calls `fail` naming the expected
   parent in the inner branch of each.

## The licence finding, and the operator's decision

The specifications and the two scripts were written by Yusuke Endoh (`mame/ai-coding-lang-bench`). Neither that repository nor
`banteg/minigit-bench` carries a licence file: checked on 2026-10-29 with `gh api repos/<owner>/<repo>/license`, in the clones,
and in their history. Without a licence the files are all-rights-reserved by default.

The operator's decision (2026-10-29, recorded as D18 in `todo/PLAN_minigit_stack_bench.md`): the files live **only inside the
GitHub fork**, relying on GitHub's Terms of Service section D.5, which lets public repositories be viewed and forked on GitHub.
The fixes to the scripts are kept in the same fork, on the `inputs` branch. This repository is not a fork, so it holds no upstream
byte: not as a fixture, not in a test, not in a comment. A test walks the spike and fails if any distinctive upstream line appears
in it. The terms grant viewing and forking; keeping the fixed scripts in the fork is the operator's call, and this note is its record.

## Changing a pinned input

Never edit a pinned file in place. Add a commit to the fork's `inputs` branch, then in one commit here update the hashes in
`hashes.txt`, the pinned sha in `lib/fetch-inputs.mjs` and the table above.
