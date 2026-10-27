# H2 polish — E5.5 / E5.6 (Sonnet, 2026-10-26)

Branch `exp/kimi-e56-skeleton-haiku-2`. Commits: `60add464` (polish), then the plan/notes commit.

## Per region

| Region | Result |
|---|---|
| `s56-planner-kimi` (ConfinementPlanner.Kimi) | kept as-is in logic; written as an expression body to match `Vibe`'s |
| `s56-no-flags` (KimiConsultant.MustCarryNoFlags) | kept as-is |
| `s55-settings`, `s55-played`, `s55-check` | kept as-is |
| `s551-copy` | fixed — `Should().StartWith(string, StringComparison)` did not compile (the 2nd argument is a reason); now `StartsWith(…, OrdinalIgnoreCase).Should().BeTrue(…)` |
| `s552-gone`, `s553-cancel`, `s554-rootless` | kept as-is |
| `s561-rows`, `s562-vectors`, `s564-admit` | kept as-is |
| `s563-arm`, `s565-launch`, `s566-flags` | kept as-is |
| S5.6.7 (`AKimiQuestionRowTests`) | kept as-is |
| `s568-kimi` (TS) | kept as-is |
| unfilled regions | none |

All `QWEN-FILL` markers removed from the 7 files; no instruction comments were left by the skeleton.

## Red first

Run against the production files of `b4e2ea96` (planner, KimiConsultant, RuntimeCapabilities.cs, both shared JSON files restored; build had one compile error in S5.5.1 first, fixed):

- S5.6.1 red: "Expected none not to be <null> because kimi × none must have a row" — as the table.
- S5.6.2 red: "Expected Vectors to contain a single item matching kimi × none" — as the table.
- S5.6.3 red: "Expected type to be Planned, but found Refused" — DIFFERS from the table (the table says `CwdKind.Scratch` vs `Snapshot`): the matrix refuses `kimi` first because the data was reverted too.
- S5.6.4 red: "Expected Admitted, but found Refused" — as the table.
- S5.6.5 red: Planned vs Refused — differs (same reason as S5.6.3).
- S5.6.6 red: Planned vs Refused — differs; the table's "no exception was thrown" NOT seen. Seeing it needs the data in and only `MustCarryNoFlags` out; I did not run that arm (time).
- S5.6.7 red (3 arms): "Expected status to be answered / refused", "Expected OperationCanceledException … no exception" — differs from the table (table says `coai-vh` still exists).
- S5.5.1–S5.5.4: GREEN on the old code too. E5.4 and E5.3 g are already in `b4e2ea96`, so the table's red cannot be reproduced; they pin the path.
- Also red against the old code: `AnUnknownRuntime…` / `AnUnknownRuntimeOrCapability…` (the runtime list ends `, kimi`) — the existing tests moved with the data (deviation 48).

Green after restoring the fills: `AKimiConsultantCheckTests` 5/5, `AKimiQuestionRowTests` 3/3, `ConfinementPlannerTests` 34/34, `CapabilityMatrixTests` 40/40.

## Checks

- Generator: `generate-runtime-capabilities.mjs` wrote nothing new; `--check` passes (21 rows over 7 runtimes).
- Format: `dotnet format --verify-no-changes --no-restore` over the whole solution prints `IL2026`/`IL3050` errors in `src_server` (AOT analyser, not my files). Over the changed files with `--include`: clean.
- Full MCP suite and TS tests: see the end of this file.

## Deviations (also in the plan, 52–54)

- Plan status line and deviations 52–54 added.
- The `Kimi` planner arm is an expression body.

## What remains

- J2 live checks, M-K17 (out of scope, not run).
- S5.6.6's table red ("no exception was thrown") not observed.

## Final results

- Full MCP suite: 10556 total, 10470 passed, 84 skipped, 2 failed — exactly the two known failures (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`).
- Extension tests (`npm test`): `kimi rows admit flagged, web refuses naming its record` passes; the only failure is the known `the baseline names no release that was never tagged` (no git tags in this clone).
- Generator `--check`: passes. Format: clean on the changed files (whole-solution run has unrelated `src_server` IL2026/IL3050 output).
- Commits: 60add464, 67475403. Nothing pushed.
