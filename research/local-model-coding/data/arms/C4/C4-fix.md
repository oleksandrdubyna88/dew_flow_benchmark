# C4 — fixes after the review of E5.5 and E5.6 (Sonnet), branch `exp/kimi-e56-classic-4`

Written 2026-10-26. The review has four findings, all minor. No blocking and no major finding. One commit: `docs(plan): E5.5-E5.6 review fixes …`
(only `todo/PLAN_vibe_and_kimi_runtimes_E5.md`). No code and no test changed, so no test came first: every fix is a document.

| id | verdict | what was done |
|---|---|---|
| R1 | **fixed** | Added Open question 7 to the plan: the `unconfined` caveat is false for Kimi; the owner decides to split it per runtime or reword it to "can read this machine". It says the TS and C# sentences change together. The caveat itself is NOT changed (shared with codex, the owner decides — as deviation 56 says). |
| R2 | **fixed, the second way the finding allows** | Deviation 58: the version stays in `measuredWith.version`. Checked in the file: no row of any runtime (Claude, Codex, Vibe, Kimi) names its version in a `note`, so putting it in two Kimi notes would make the Kimi rows differ from the file's shape and add a second copy that can drift. The JSON and the generated TS did not change; the generator check is still up to date. |
| R3 | **partly fixed — the operator must answer** | Deviation 59 records that E5.5 and E5.6 were built on Sonnet, not on Opus (§0, §0.2), and that the fixes are by the same model (§0). The reason is not in the brief I had, so I wrote none (I will not invent an operator's reason). The deviation ends "Open: the operator confirms the change of model, or has the two stories rebuilt on Opus." |
| R4 | **not fixed — out of scope** | `research/module_tests.md` is under `research/`, which the brief puts out of scope. The finding is right (the Vibe flow has an entry at line 3927, the Kimi flows have none). Deviation 57 already lists it as not done; the plan's status line now says `research/module_tests.md` still owes the two flows. The text to add: a Kimi section like the Vibe one — `AKimiConsultantCheckTests` (S5.5.1–S5.5.4, teeth A–D) and `AKimiQuestionRowTests` (S5.6.7, teeth E–F), and NOT covered: the real `kimi`, the live question rows and the live `--check-consultant` owed to RESULTS §31, and hosts other than Windows (D18). |

No finding was wrong.

Also changed: the plan's status line (it said "not yet reviewed"; now "deviations 50–59; reviewed, the fixes are in").

## Results

- Build (`-c Debug -m:4 --disable-build-servers`, through `low-priority.mjs`): succeeded, 0 warnings, 0 errors.
- Changed test classes (`AKimiConsultantCheckTests`, `CapabilityMatrixTests`, `ConfinementPlannerTests`, `KimiConsultantTests`, `AKimiQuestionRowTests`): 93 of 93 pass.
- `plan-lifecycle.mjs`: clean.
- Generator check (`src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
- Whole MCP suite (Debug, 6m 46s): 10555 total, 10468 pass, 84 skipped, **3 fail**. Two are the known ones
  (`KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_…`). The third is new on this host and **not mine**:
  `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (a git call with a 1 ms deadline answers `recorded`, the test expects
  `unavailable: git…`; `TheRoundsNumstatsAreCountedTests.cs:259`). It failed alone, and it failed the same in a scratch worktree built at
  `b4e2ea96` (before E5.5), so it is the host's timing, not this branch. The worktree is removed. It passed in the coder's run (`C4-code.md`).
- Format: `dotnet format whitespace` and `dotnet format style --verify-no-changes --no-restore --include <the 8 changed C# files>` print nothing. The whole-slnx run was not repeated
  (it floods with `IL2026`/`IL3050` from `src_server`, files nobody here touched, as `C4-code.md` says).
- Generator check: `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check` (the script is under `src_vs_code/scripts/`, not `scripts/`): up to date.
- TS tests (`npm test`, compile + all): 6290 tests, 6270 pass, 19 skipped, 1 fail — the known `the baseline names no release that was never tagged` (no git tags in this clone).
- The session was interrupted once while the TS run was ending; the log was complete, and the tree was clean (`git status`).
