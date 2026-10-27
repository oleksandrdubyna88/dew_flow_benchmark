# T4 t1 — code notes (E5.6: Kimi's capability rows)

Commit: `b3dae445` on branch `tmp/T4-t1` (worktree `/home/jinx/git/coai-exp/wt/T4-t1`). Not pushed.

## Done

- `shared/runtime-capabilities.json`: `kimi` appended to `runtimes`; the `why` record sentence gains the Kimi record (lines 18–21 replaced with the five strings of the document); three `kimi` rows after the `vibe` × `web` row: `none` unconfined, `disk` unconfined, `web` unmeasured.
- `shared/capability-matrix-vectors.json`: three `kimi` vectors between `vibe` × `web` and `gemini`.
- `src_mcp/core/QuestionConsult/RuntimeCapabilities.cs:84`: `Runtimes` ends `"vibe", "kimi"`.
- `src_mcp/tests/CapabilityMatrixTests.cs`: `Records` gains the Kimi record (third entry) and its summary clause; new tests `KimiHasThreeRows_EachCitingItsCells` (S5.6.1) and `AdmissionFlagsBothRows` (S5.6.4).
- `src_vs_code/src/test/capabilityAdmission.test.ts`: runtimes list line includes `'kimi'`; new test `kimi rows admit flagged, web refuses naming its record` (S5.6.8).
- `src_vs_code/src/runtimeCapabilities.generated.ts`: regenerated with `generate-runtime-capabilities.mjs` (21 rows over 7 runtimes × 3 capabilities); `--check` prints `up to date`.

## Red messages (step by step)

Step 1 (C#, before any production change):
- `KimiHasThreeRows_EachCitingItsCells`: `Expected none not to be <null> because kimi is a question-consultant runtime (E5.6).` — matches the document.
- `AdmissionFlagsBothRows`: `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found CoaiMcp.Core.QuestionConsult.Admission+Refused.` — matches.

Step 2 (TS, before any production change):
- `the words are the file's words…`: `deepStrictEqual` on the runtimes list, missing `'kimi'` — matches.
- `kimi rows admit flagged, web refuses naming its record`: `'kimi' is not a runtime the question consultant can launch for 'none' — it launches on: claude, codex, antigravity, local, api, vibe` — matches.

Step 3 (seed, `Runtimes`, `Records` in; rebuilt):
- `KimiHasThreeRows_EachCitingItsCells` and `AdmissionFlagsBothRows` green.
- `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` red: `…because the table's kimi × none row must have a vector deciding it`. Matches S5.6.2.

Step 4 (vectors added; rebuilt): `CapabilityMatrixTests` 39 of 39 green.

## Test results

- `CapabilityMatrixTests`: 39 / 39 green.
- Whole MCP suite (10542 tests): 2 failed, both known pre-existing: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`. No new failure.
- `generate-runtime-capabilities.mjs --check`: `up to date`.
- `npm run typecheck`: pass. `npm run lint`: pass.
- `npm test`: only failure is the known `the baseline names no release that was never tagged`. `kimi rows admit flagged, web refuses naming its record` green.
- `dotnet format --verify-no-changes`, whole solution: exits with IL2026/IL3050 errors in `src_server/src/Jobs/ReviewEndpoints.cs` and `src_server/src/Usage/UsageEndpoints.cs`. I did not touch `src_server`; these are not from my change, but I did not check them against the pre-change commit. With `--include` on my two C# files (`CapabilityMatrixTests.cs`, `RuntimeCapabilities.cs`), the check printed nothing, which I read as clean.

## Left / notes

- Nothing left from t1.
- Planner arm (`ConfinementPlanner.cs`) is t2, as the document says: until t2 merges, a `kimi` row plans with no confinement.
- `ConfinementPlannerTests.cs:167`, `CapabilityMatrixTests.cs:273`, `capabilityAdmission.test.ts:166` (substring checks) stayed green.
- `src_vs_code/node_modules` is untracked from before the session; not committed.
- I ran the first `npm run typecheck` and `npm run lint` without the low-priority wrapper by mistake, then reran both through it. Same result.
