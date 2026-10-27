# T3-t1 — code notes (Kimi's capability rows, vectors, Runtimes, regenerated TS table)

Task document: `/home/jinx/git/coai-exp/vllm/work/T3/t1.md` (with `common.md` in the same folder).
Worktree: `/home/jinx/git/coai-exp/wt/T3-t1`, branch `tmp/T3-t1`.
Commit: `ea9badf0` on `tmp/T3-t1` (not pushed).

## What I did

1. Red first. Added to `src_mcp/tests/CapabilityMatrixTests.cs`: `KimiHasThreeRows_EachCitingItsCells`,
   `AdmissionFlagsBothRows` (beside the Vibe test), and changed the `:273` list string. Added to
   `src_vs_code/src/test/capabilityAdmission.test.ts`: the `RUNTIMES` list gains `'kimi'`, the `:166` regex gains `kimi`,
   and the new S5.6.8 test `kimi rows admit flagged, web refuses naming its record`.
2. Production data:
   - `shared/runtime-capabilities.json`: `runtimes` gains `kimi`; a new `why` string cites the M-K cells; three kimi rows
     (`none` unconfined, `disk` unconfined, `web` unmeasured) appended after the last vibe row, with the contract values.
   - `shared/capability-matrix-vectors.json`: three kimi vectors after the vibe vectors.
   - `src_mcp/core/QuestionConsult/RuntimeCapabilities.cs:84`: `Runtimes` gains `"kimi"`.
   - `src_vs_code/src/runtimeCapabilities.generated.ts`: regenerated with `src_vs_code/scripts/generate-runtime-capabilities.mjs`
     (21 rows over 7 runtimes × 3 capabilities); not edited by hand.
3. Green. Build clean (0 warnings, 0 errors).

## Red messages that differed from the document

- `KimiHasThreeRows_EachCitingItsCells`: got `Expected none not to be <null>.`; the document said
  `Expected RuntimeCapabilities.Builtin.Of("kimi", Capability.None) not to be <null>.`. FluentAssertions names the variable.
- `AdmissionFlagsBothRows`: got `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found
  CoaiMcp.Core.QuestionConsult.Admission+Refused.` The document quoted the refusal reason too; the reason is not in the
  message at that line.
- `AnUnknownRuntimeOrCapability_IsRefused_NamingWhatIsKnown` (`:273`, not in the document's red list): got the old list
  without `kimi` in the refusal text. Expected and fixed by the list change.
- TS `the words are the file's words…`: `Expected values to be strictly deep-equal` with `- 'kimi'` in the diff. As the document says.
- TS `a refusal says what cures it…` (`:166`, not in the document's red list): `/claude, codex, antigravity, local, api, vibe, kimi/`
  did not match the old refusal text. Expected once the regex and the list changed together.
- TS `kimi rows admit flagged, web refuses naming its record`: `kimi × none false !== true`. The document gave no message.

## Things the document did not foresee

- `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` (`CapabilityMatrixTests.cs:164`) has a `Records` table of the
  records a row may cite. Its kimi rows failed with `cited … the collection is empty`. I added
  `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")` to that table and extended its doc comment. The file is in
  my scope, so I made the change.
- `ConfinementPlannerTests.cs:167` (`AnUnknownRuntime_IsRefused_NamingTheOnesItKnows`) did NOT fail. Its substring
  `claude, codex, antigravity, local, api, vibe` still occurs inside the new list. So the file needs no change from me.
  t2 may still update it for exactness.
- `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` (`:80`) was green before and after. It checks only that every
  seed row has a vector, and kimi rows and vectors were added together, so it is not a red for this task.

## Test results

- `CapabilityMatrixTests`: 39 of 39 passed (after the red run showed 3 failures).
- `src_vs_code`: `npm run typecheck` clean; `npm run lint` clean; `npm test` — only
  `the baseline names no release that was never tagged` fails (known; the clone has no tags).
- `generate-runtime-capabilities.mjs --check`: "up to date", exit 0.
- Whole MCP suite: 10542 total, 10454 passed, 84 skipped, 4 failed:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — known.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known.
  - `QuestionRowOnAgyScenarioTests.ACallerWhoGivesUpBetweenTheLaunches_LeavesTheFirstBilled_AsInterruptedNotOk` — NOT on the
    known list. Passed when the class ran alone (5 of 5). Its messages are about cancellation and timing. I did not
    touch that area. I did not check whether it also fails at `b4e2ea96`, so it may be timing-dependent under load.
  - `QuestionRowOnAgyScenarioTests.AFollowUpCutShortByTheCaller_StillBillsTheFirstLaunch_AndNeverAsOk` — same as above.
- `dotnet format … --verify-no-changes`: exit 2, with IL analyzer errors in `src_server` (for example
  `src_server/src/Jobs/ReviewEndpoints.cs`). None of the errors names one of my files. I did not check against `b4e2ea96`.

## Deviations from the brief

- The brief names branch `exp/kimi-e56-tasks-3`. The worktree is on `tmp/T3-t1`, so I committed there. I did not switch branches.
- `common.md` says "Do not commit". The coder brief says commit with a conventional message, adding files by name. I followed
  the brief and committed the six files. I did not push.

## Left

- Nothing in my scope. `src_vs_code/node_modules` is untracked from before the task; I did not add it.
- The two `QuestionRowOnAgyScenarioTests` failures and the `dotnet format` analyzer errors are not checked against the
  baseline commit (see above).
