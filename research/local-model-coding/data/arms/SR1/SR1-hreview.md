# SR1 review — E5.5 and E5.6 (branch exp/kimi-e56-sonnet-split-haiku-review-1)

Reviewer: Haiku (fresh reviewer who also fixes). Scope: commits b4e2ea96..HEAD.
Read: AGENTS.md, .agents/PROJECT.md, ENTRY.md, resolver explain (40 rules selected),
E5 plan §0, E5.4, E5.5, E5.6, "Where the parent plan and the code disagree" incl. deviations 50–58.
Coder notes: /home/jinx/git/coai-exp/notes/SR1-t1-code.md … SR1-t6-code.md (no SR1-code.md exists).

## Findings (step 1, written before any code change)

H1 — minor — src_mcp/tests/ConfinementPlannerTests.cs (Kimi test, ~line 149)
  The spec names S5.6.3 `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`; the test is named
  `Kimi_None_StandsInAnEmptyFolder_Disk_InTheCopyOfItsRoots_NoFlagEver_AndWebIsRefused`.
  Fix: rename to the spec's name (the body already asserts every Then).

H2 — minor — src_mcp/tests/CapabilityMatrixTests.cs (`AdmissionFlagsBothKimiRows_AndRefusesItsWebNamingItsRecord`, ~line 220)
  The spec names S5.6.4 `AdmissionFlagsBothRows`; the name differs.
  Fix: rename to the spec's name.

H3 — minor — src_mcp/tests/CapabilityMatrixTests.cs:80 / spec S5.6.2 `TheVectorsCoverKimi`
  No test of that name exists. The spec's row points at the existing test `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`,
  which does cover kimi once the vectors and RUNTIMES gain it. Not a defect of the code.
  Fix: none in code; state the mapping in the report (not a rename of a shared test).

H4 — minor — src_mcp/tests/AKimiQuestionRowTests.cs `NothingIsLeftBehind` / refused arm of S5.6.7
  The spec's Then says "after each ending the row's scratch and coai-vh\<8> are gone". The scratch check runs only when
  `launched is not null`, so the "refused before the launch" arm never checks the row's scratch folder.
  Fix: not fixed here. Needs the row's scratch path from the refused run, which the test has no seam for. Would need
  a seam or a temp-folder enumeration; Path.GetTempPath is shared, so I will not enumerate it.

H5 — major (process) — todo/PLAN_vibe_and_kimi_runtimes_E5.md §53 and module_tests.md
  Red first is required for every behaviour. S5.5.1–S5.5.4, S5.6.5 and S5.6.7 went green on arrival (the plan's own
  deviation 53 says so). Their evidence is a temporary-break check, not a red run. Teeth are reported, so they are
  not vacuous, but the red-first rule was not followed.
  Fix: not fixed in code (a red run would need the code reverted to a state the coder did not keep). Re-check the
  teeth for S5.5.2 and S5.6.7 during this review if time allows, and say so in the report.

H6 — minor — src_mcp/tests/AKimiConsultantCheckTests.cs S5.5.1
  The Then says "the scratch repository and its canary unchanged". The test does not assert it directly. It is implied:
  ConsultantCheck.Judged turns a breach into Failed(TreeChanged), so state == "answered" already means no change.
  Fix: wrong on a second look — the invariant is covered by the production judgement. Not changed.

H7 — minor — src_mcp/tests/AKimiQuestionRowTests.cs `NothingIsLeftBehind`
  `if (Directory.Exists(oneShot))` makes the coai-vh check vacuous when the folder was never made. In the three arms
  the reservation runs before the refusal/launch, so the folder normally exists. Acceptable; noted.
  Fix: not changed.

Still to check (step 2 gates): build; changed classes; whole MCP suite; format check; generator --check for
runtimeCapabilities.generated.ts; TS tests; plan-lifecycle; the third failing test
`TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (deviation 58), run in isolation.

## Outcomes (step 2)

H1 — FIXED. Renamed the planner test to `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (its body unchanged).
     Commit 80f1d71f. Rename only, so no red run; the renamed class is green (ConfinementPlannerTests 39 of 39).
H2 — FIXED. Renamed to `AdmissionFlagsBothRows` (the spec's name). Same commit 80f1d71f. CapabilityMatrixTests 33 of 33 green.
H3 — NOT A CODE FIX. S5.6.2 is covered by the existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`, which now
     covers kimi. Mapping stated here; no shared test renamed.
H4 — NOT FIXED. The refused arm of S5.6.7 does not check the row's scratch folder is gone. The test has no path to that folder
     when the row is refused before the launch. A fix needs a seam; out of time for this review. Reported as remaining.
H5 — NOT RE-RUN. Red-first was not followed for S5.5.1–S5.5.4, S5.6.5, S5.6.7 (deviation 53 says so). A fresh teeth run would
     need the build and the suite to run again; not done in this window. Reported as remaining.
H6 — WRONG ON A SECOND LOOK. The scratch and canary invariant is implied by `ConsultantCheck.Judged`: a breach is
     `Failed(TreeChanged)`, so `state == answered` carries it. No change.
H7 — NOT A DEFECT. The `Directory.Exists` guard in `NothingIsLeftBehind` is needed for the refused arm, where the one-shot
     folder may not exist. No change.

Checks (this review, on the branch with H1/H2 fixed):
- Build (Debug, -m:4 --disable-build-servers): 0 warnings, 0 errors (baseline and after the fix).
- Changed classes, MCP (CoaiMcp.Tests on this Linux host; no .exe suffix): AKimiConsultantCheckTests 5/5, AKimiQuestionRowTests,
  KimiQuestionLaunchTests 5, CapabilityMatrixTests 33, ConfinementPlannerTests 39 — all green.
- TheRoundsNumstatsAreCountedTests (deviation 58's third failure): 9/9 green in isolation here. Not reproduced.
- Generators --check: runtime-capabilities up to date (21 rows, 7 runtimes × 3); feature-availability up to date.
- plan-lifecycle: clean.
- TS: typecheck clean; lint clean; npm test: 6369 pass, 1 fail — `the baseline names no release that was never tagged` (the known tag-less clone).
- dotnet format --verify-no-changes: exit 2 with 54 analyzer diagnostics IL2026/IL3050 in src_bugs and src_server. No format
  finding points at a file touched by this branch. Those projects are untouched on this branch (git diff shows no change there).
  Not a clean pass on this host; not caused by this work as far as the diff shows.

Full MCP suite (second run, full output in SR1-hreview-suite-full.txt): total 10561, failed 3, succeeded 10474, skipped 84.
  - KimiRuntimeTests.ALaunchThatNamesItsHome_… — known, pre-existing on this host.
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed — known, pre-existing (PathTooLongException).
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — 1 ms git deadline, not Kimi. It failed in
    the full run and passed in isolation (9 of 9) in this review. Flaky under load; not caused by this branch. Still open for the owner.
Commits from this review: 80f1d71f (test renames H1, H2). Nothing pushed.
