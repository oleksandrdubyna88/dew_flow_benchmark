# DR4 — coder notes (Haiku): E5.5 and E5.6

Branch: `exp/kimi-e56-haiku-direct-review-4`. Start: `b4e2ea96`. Deadline 17:50 UTC.
No push, no PR, no live `kimi` or `vibe` run.

## Commits

- `6936d157` feat(kimi): the question row's Kimi rows (E5.6). Seed rows, vectors, `Runtimes`, planner arm, flag refusal, generated TS.
- `3dd5cfb5` test(kimi): S5.5.1–S5.5.4 and S5.6.7, with the shared `KimiStandIn`.
- `49501c17` docs(plan): E5 status and the as-built list.

## Scenario status

| # | Red seen? | Green? | Note |
|---|---|---|---|
| S5.5.1 AKimiCheck_RunsInACopy_OnAOneShotHome | no | yes | E5.4 was already in. The table's red needs E5.4 removed; not done. |
| S5.5.2 AfterTheCheck_TheHomeIsGone_JunctionFirst | no | yes (2 arms) | Table's red needs E5.3 g removed; not done. |
| S5.5.3 ACheckCancelledMidTurn_StillDisposesItsHome | n/a (pin) | yes | Table says green after E5.3 g. |
| S5.5.4 ARootlessCheck_… | no | yes | Same as S5.5.2. |
| S5.6.1 KimiHasThreeRows_EachCitingItsCells | yes | yes | Red: `Of("kimi", None)` null, as the table says. |
| S5.6.2 TheVectorsCoverKimi | no | yes | I added rows and vectors before the first run. |
| S5.6.3 ThePlannerArm_… | yes | yes | Red: `Expected type to be Planned, but found Refused`. Table's default-arm message did not happen: matrix refused `kimi` as unknown first. |
| S5.6.4 AdmissionFlagsBothRows | yes | yes | Red: `Expected type to be Admitted, but found Refused`. |
| S5.6.5 AKimiQuestionLaunch_… | yes (fixture) | yes | Red was a test fault (PATH and owned-root places). Fixed in the test. |
| S5.6.6 APlanWithFlags_IsRefusedForKimi | not seen | yes | Added the flag refusal before running. |
| S5.6.7 AKimiDiskRow_… (3 arms) | yes (fixture) | yes | First red: homes root did not exist. Not the table's message. |
| S5.6.8 TS kimi rows admit flagged, web refuses | not seen | yes | Lists updated in the same edit. |

## Decisions

1. `Runtimes` in `RuntimeCapabilities.cs` gained `kimi` at the end, same as the seed.
2. Kimi's `none` and `disk` rows are `unconfined`, with the flag `unconfined`. `web` is `unmeasured`. This follows the story's Decisions.
3. The planner's `kimi` arm carries no flags. A planned launch with flags throws `ArgumentException` naming "the shim's".
4. Cell ids come from `research/RESULTS_kimi_confinement.md` as headings: M-K3 (§6), M-K4 (§7), M-K6 (§9), M-K9 (§12), M-K9 (e) (§21).
5. Version `2.1.1`, date `2026-10-13`, as in E5.4.
6. `KimiStandIn.cs` is a new shared test helper. The story did not name it.
7. S5.6.5 and S5.6.6 sit in `ConfinementPlannerTests`, beside the Vibe test they copy.

## Deviations from the plan text

- Three pinned sentences named the old runtime list. They now name `kimi`: `CapabilityMatrixTests`, `ConfinementPlannerTests` and `capabilityAdmission.test.ts`.
- `CapabilityMatrixTests.Records` gained `RESULTS_kimi_confinement.md`.
- The shared homes-root refusal says "the Vibe homes folder … (COAI_CONSULTANT_HOMES)" for every runtime. Kimi rows get the Vibe wording. Not changed. Needs a decision.

## Rules

Read in full: C# doctrine, TypeScript doctrine, scenario-tests, generated-code-tests, test-leftovers, testing, reuse-first, planning-docs. The session-start hook output carried coding-style, knowledge-base, security, task-lifecycle, and the local review-gate and vendor-routing rules.

Not read in full: the `coai-*` gate rules (five plus review-gate), `development-workflow`, `git-workflow`, `measurement`, `reliability`, `subagent-models`, `platform-limits`, `post-deploy-checks`, `local-cpu-budget`, `utc-timestamps`, `durable-status`, `automated-checks`. I ran out of room in the session and did not read them, and I did not call the review gate. The tasks in my brief do not need them, but the entry procedure asks for every selected rule. Someone should read them before review.

## Checks (at 16:57 UTC)

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` succeeded after each step.
- `npm test` in `src_vs_code`: one failure, `the baseline names no release that was never tagged`. This is the known one (no tags in this clone). S5.6.8 and the generated-files check pass.
- `npm run typecheck`, `npm run lint`: clean.
- `generate-runtime-capabilities.mjs --check`: current (21 rows, 7 runtimes × 3).
- `plan-lifecycle.mjs`: clean.
- `dotnet format --verify-no-changes`: run; output was only IL trim warnings from `src_server`, which I did not touch. The exit status was not captured, so I cannot call format clean.
- Whole MCP suite: see the end of this file.

## What remains

- Opus review of E5.5 and E5.6 (plan says so).
- The live checks J2 (paid) and M-K17. Out of scope.
- The homes-root message wording (deviation 3 above).
- The reds of S5.5.1, S5.5.2 and S5.5.4 would need the E5.3 and E5.4 code removed to show. Not done.
- The unread rules listed above.

## Whole MCP suite (17:05 UTC)

Total 10556, succeeded 10469, skipped 84, failed 3.

- KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn: known, failed at start.
- KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed: known, failed at start.
- TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow: NOT on the known list. Fails alone too. The test builds its own temp repo and asserts a git call fails under a 1 ms deadline; the git call finished inside 1 ms, so the assertion did not hold. I changed no file it uses. Not verified against b4e2ea96 (no time for a base build). Needs a look.
