# S2-t1 — code notes (Kimi capability table, S5.6.1, S5.6.2, S5.6.4)

Commit: 0e9490ff on branch tmp/S2-t1 (the worktree branch; the task named exp/kimi-e56-sonnet-split-haiku-2, which I did not switch to).

## What I did
1. Red: added `KimiHasThreeRows_EachCitingItsCells` (S5.6.1), `AdmissionFlagsBothRows` (S5.6.4), Change A (third `Records` entry, summary clause) and Change B (refusal pin names kimi).
   Red run: 3 failed.
   - S5.6.1: `Expected none not to be <null>.`
   - S5.6.4: `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found CoaiMcp.Core.QuestionConsult.Admission+Refused.` (differs from the doc's "not to be <null>" wording; the doc's wording was an example)
   - Change B: `…claude, codex, antigravity, local, api, vibe, kimi`
2. Production table: `RuntimeCapabilities.Runtimes` + `shared/runtime-capabilities.json` (runtimes, `why` lines, three kimi rows).
   Red message of Step 2 (`TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`):
   `… because the table's kimi × none row must have a vector deciding it.`
3. `shared/capability-matrix-vectors.json`: three kimi vectors. Class green.
4. Generator: `node src_vs_code/scripts/generate-runtime-capabilities.mjs` wrote the TS file (21 rows over 7 runtimes × 3). `--check` exit 0. The TS file was not edited by hand.

## Test results
- `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: 0 errors, 0 warnings.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.CapabilityMatrixTests`: total 39, failed 0.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.ConfinementPlannerTests`: total 31, failed 0.

## Differences from the document
- Run command: the doc names `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe`, which does not exist on Linux. I ran `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (same arguments).
- ConfinementPlannerTests did NOT go red. The doc said `AnUnknownRuntime_IsRefused_NamingTheOnesItKnows` would fail. Its pinned text `claude, codex, antigravity, local, api, vibe` is a substring of the new sentence (`…, vibe, kimi`), so `Contain` still passes. That file was not changed. t3 may still update the pin; it is not red now.
- Commit: the doc's "Out of bounds" says "Do not commit", but the coder rules say to commit by name. I followed the coder rules: five files committed by name, no push.
- Step order: S5.6.4 stayed red after Step 2 because it needs the vectors (Step 3). It went green with Step 3.

## Known failures not touched
- KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn
- KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed
- extension test: the baseline names no release that was never tagged
(Not run in this task: I ran only the CapabilityMatrixTests and ConfinementPlannerTests classes.)

## Left undone
- Nothing in t1 scope. Not run: the extension test suite (capabilityAdmission.test.ts is t2's) and the full MCP suite (not asked).
