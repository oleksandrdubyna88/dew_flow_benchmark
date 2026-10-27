# Q1 — polish of E5.5 and E5.6 (CODER, Sonnet)

Branch `exp/kimi-e56-skeleton-1`. Never pushed. No `kimi` or `vibe` was run.

## State at the start

- HEAD `3e447710`: the skeleton and the two fills (tests, prod) were in. The build was RED: 11 syntax errors in `KimiStandIn.cs`
  (the fill declared a nested class inside a method), then 9 more errors in the filled test bodies (see below).
- Prod fills: both were right as they were.

## Per region

| Region | Verdict |
|---|---|
| `s56-planner-arm` (`ConfinementPlanner.Kimi`) | kept; only restyled to the expression body and the `web` comment that `Vibe` has |
| `s566-noflags` (`KimiConsultant.MustCarryNoPlanFlags`) | kept as-is |
| `s56-standin` (`KimiStandIn.Of`) | rewritten: the nested class was inside the method body (does not compile); the `Scopes` class moved to class level, the body is the same expression |
| `s55-settings` | fixed: layout only (the fill's indent broke the format) |
| `s55-check` | fixed: `AnanswerAsync` typo → `AnswerAsync` |
| `s55-played` | fixed: failing arm checked before reading `CHECK.md`; layout |
| `s55-arg` | kept (a blank line added) |
| `s551` | fixed: `Regex` had no `using`; the `--home` pattern now starts at the real `<root>\coai-vh`; canary asserted `not-attempted` (was `NotBe("read")`); the check's state and messages added |
| `s552` | rewritten: a nullable `Path.Combine` error, a vacuous `NotBeNullOrEmpty`, no assertion on the exit-70 arm; now asserts the state per arm, the one-shot folder gone, recorder silent, person's folder whole. `_ = turnExit` removed |
| `s553` | rewritten: `cts` was never declared, and it expected an exception; a cancelled check returns a record with `failureKind` `cancelled`, so it asserts that, then the folder, recorder and person |
| `s554` | fixed: asserts state `answered`; layout; messages |
| `s567-init` | fixed: `src` and `docs` folders were never made before the files |
| `s567-service` | fixed: row's `ExecutablePath` empty (the shim comes from the seams, as in the E5.4 service tests); layout |
| `s567-ask` | kept |
| `s567` | rewritten: `TheRow` did not exist; the body asserted on `coai-question-*` in the machine's temp (would fail on any parallel leftover) and read `Directory.GetParent` of the cwd in a way that was always true; now one flow for the three endings, per-launch checks of the scratch (parent of `--cwd`) and one-shot folder (parent of `--home`), `coai-vh` empty, recorder silent, person whole. Added helpers `TheRow` and `ArgumentAfter` |
| `s561` | fixed: the web row's version (`2.1.1`) asserted too |
| `s564`, `s563`, `s565`, `s566` (planner/matrix tests) | kept as-is |
| `s568` (TS) | kept as-is |

All `QWEN-FILL` markers and the skeleton's instruction comments are gone (grep over `src_mcp`, `src_vs_code/src`, `todo`: none).

## Red first (details are deviations 53–56 in the plan)

- Production + data at `b4e2ea96`: S5.6.1 *Expected row not to be null*; S5.6.4 *Expected type to be Admitted but found Refused*;
  S5.6.3/S5.6.5/S5.6.6 *Expected type to be Planned but found Refused* (matrix first); S5.6.7 the row `blocked` with *'kimi' is not a runtime …*
  (its refused arm: `blocked` instead of `refused`).
- Table back, vectors + planner + `KimiConsultant` at `b4e2ea96`: S5.6.2 red (vectors lack the three `kimi` pairs); S5.6.3 *Expected disk.Cwd to be
  Snapshot but found Scratch* (as the table); S5.6.5 working-directory mismatch; S5.6.6 *Expected a ArgumentException … no exception was thrown* (as the table);
  S5.6.7 refused arm *answered* instead of *refused*, answered arm: the prompt lacks the copy.
- S5.5.1–S5.5.4 are green on the old code (E5.3 g and E5.4 are in it): red shown by two mutations (see plan 53). S5.5.1 (copy + home shape) was not mutated.
- S5.6.8 (TS): with the old generated table the test and its neighbours fail (`the words are the file's words` on the runtimes list, the three `kimi` vectors, and S5.6.8).

## Results

- The 12 scenarios: green (`AKimiConsultantCheckTests` 5, `AKimiQuestionRowTests` 3, `CapabilityMatrixTests` 36, `ConfinementPlannerTests` 34 — all pass).
- Whole MCP suite: 10556 total, 10469 passed, 84 skipped (Windows-only), **3 failed**:
  the two known ones (`KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_…`) and
  `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, which passes alone (10 of 10) — it hands git a one-millisecond timeout, so it is load-sensitive in a full run; not related to this change.
- Format: `dotnet format --verify-no-changes --no-restore` reports no whitespace/style diff; it prints AOT analyzer errors IL2026/IL3050 for `src_server` (untouched code, not mine).
- Generator: `generate-runtime-capabilities.mjs` wrote the file (no diff against HEAD) and `--check` is "up to date" (21 rows, 7 runtimes × 3).
- Extension `npm test`: 6290 tests, 1 fail — `the baseline names no release that was never tagged` (known; no git tags). `capabilityAdmission.test.js`: 31 of 31 pass.
- `plan-lifecycle.mjs`: clean.

## What remains

- Review (Opus), the live checks (J2) and M-K17: not mine.
- The cells' `resultRef` section numbers (deviation 49) follow RESULTS §31 once J2 is recorded.
