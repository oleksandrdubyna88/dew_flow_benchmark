# SR4 fix notes — E5.5 and E5.6 review (branch exp/kimi-e56-sonnet-split-haiku-review-4)

Input: `notes/SR4-review.json` (verdict fix-then-ready; R1–R5, all minor; no blocking, no major).
`notes/SR4-code.md` was NOT present, so the coder's notes could not be read.

## Per finding

- **R1 (plan: deviation 50 names a "Theory")** — FIXED. Deviation 50 now names the two Facts of S5.5.2 (`AnAnsweredCheck_…`, `AFailedCheck_…`). Doc-only change; no red test applies.
- **R2 (plan: no red messages; references to files outside the repo)** — NOT FIXED (partial). The references to `notes/SR4-t3-code.md` and `notes/SR4-t4-code.md` are removed from deviation 54, and the as-built now says the red runs are not recorded here. Writing the temporary breaks and their red messages for every E5.5/E5.6 scenario was not done: it needs one build and one test run per break, which did not fit the 18:01 UTC deadline.
- **R3 (S5.6.7 refused arm does not check the row's `coai-question-*` scratch)** — FIXED as a stated limit, per the review's own option. Deviation 53 records it: the scratch is made inside the row and no seam hands out its path, so only the one-shot folder is checked on the refused arm. No code change.
- **R4 (S5.5.3 lacks the H1–H3 home checks; `if (Directory.Exists…)` guard)** — FIXED. `ACheckCancelledMidTurn_StillDisposesItsHome` now captures `--home` in `During`, asserts the one-shot folder existed during the turn, and asserts it is gone after, with no guard. File: `src_mcp/tests/AKimiConsultantCheckTests.cs`.
- **R5 (S5.6.6 does not test the `Leading` half of `MustCarryNoFlags`)** — FIXED. `APlanWithFlags_IsRefusedForKimi` is a Theory with a flag case and a leading-argument case (`--search`). File: `src_mcp/tests/ConfinementPlannerTests.cs`.

## Verification

Commits (no push, no PR): `cb5e879d` (R4), `f570a6ff` (R5), `5472fefb` (R1–R3).

- **Teeth of R5:** with the `Leading` half of `MustCarryNoFlags` removed (temporary, reverted), `APlanWithFlags_IsRefusedForKimi(leading: True)` went red with *Expected a <System.ArgumentException> to be thrown, but no exception was thrown.* The production file is back to its committed state.
- **Changed test classes (after the fixes):** `AKimiConsultantCheckTests` 5/5, `ConfinementPlannerTests` 36/36, `AKimiQuestionRowTests` 3/3 — all green.
- **Build:** `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — 0 errors, 0 warnings (after the revert).
- **Whole MCP suite:** run once. Result: failed 2, succeeded 10471, skipped 84. The two failures are `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…` — both known host failures, not mine. The runner's summary says "Failed!"; the wrapper's exit code was 0, so read the summary, not the wrapper.
- **Format:** `dotnet format … --verify-no-changes` on the whole solution exits 2, but only with IL2026/IL3050 analyzer errors in `src_server` (`ReviewEndpoints.cs`, `UsageEndpoints.cs`, `CatalogEndpoints.cs`). This branch does not touch `src_server`, so these are not from this work. Narrowed to the two changed C# test files: exit 0.
- **Generator checks:** `generate-feature-availability.mjs --check` up to date; `generate-runtime-capabilities.mjs --check` up to date.
- **plan-lifecycle:** clean.
- **TS (`src_vs_code`):** typecheck exit 0; lint exit 0; `npm test`: 6290 tests, 6270 pass, 1 fail — `the baseline names no release that was never tagged` (no git tags on this clone, the known one). Exit 1 is that failure.

## Not done

- R2: the red messages of each E5.5/E5.6 scenario were not produced again; only the R5 teeth check was run. The plan now says so (deviation 54).
