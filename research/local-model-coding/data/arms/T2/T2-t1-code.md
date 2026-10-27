# T2-t1 — code notes (E5.6: kimi in the capability matrix)

Commit: `dc4a82b6` on `tmp/T2-t1` (not pushed). Files changed: the eight files the task names, and nothing else.

## What I did

- `shared/runtime-capabilities.json`: `kimi` added to `runtimes`; the `why` sentence extended with the kimi
  rows' record; three kimi rows appended after the last vibe row (none unconfined, disk unconfined, web unmeasured).
  The `cells` arrays are one item per line, as the other rows.
- `shared/capability-matrix-vectors.json`: three kimi vectors after the vibe vectors.
- `src_mcp/core/QuestionConsult/RuntimeCapabilities.cs`: `Runtimes` ends with `kimi`.
- `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`: `"kimi" => Kimi(grant, flag)` arm and the `Kimi` method, as the contract gives it.
- `src_vs_code/src/runtimeCapabilities.generated.ts`: written by `scripts/generate-runtime-capabilities.mjs` only. `--check` exits 0.
- Tests: `CapabilityMatrixTests.cs` (section `// ---------- E5.6: kimi ----------`, two tests),
  `ConfinementPlannerTests.cs` (the arm test and the InlineData), `capabilityAdmission.test.ts` (line 143 and the new test).

## Red runs (before the production change)

- Step 1, `CapabilityMatrixTests` + `ConfinementPlannerTests`: 4 failures.
  - `KimiHasThreeRows…`: *Expected none not to be <null>* — as the document says.
  - `AdmissionFlagsBothRows`: *Expected type to be Admitted, but found Refused* — the message is different
    from the document's text (see deviation 1).
  - `ThePlannerArm…`: *Expected type to be Planned, but found Refused* — the planner refuses kimi before the arm is reached.
  - InlineData `kimi`/web: *"'kimi' is not a runtime the question consultant can launch for 'web' …"*.
- Step 2 (rows and `Runtimes`):
  - `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`: red, *the table's kimi × none row must have a vector deciding it* (S5.6.2's red, as the document describes).
  - `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone`: red, *a row cites exactly one record* (deviation 2).
  - `ThePlannerArm…`: red on `Cwd`, *Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch* — the default arm, as the document describes.
- Step 5 (TS, before regeneration): `the words are the file’s words…` red (the runtimes list), `kimi rows admit flagged…` red
  (*'kimi' is not a runtime the question consultant can launch for 'none'*), `the generated table is the seed…` red
  (*the generated file is behind the seed*), and `generate-runtime-capabilities.mjs still produces the file that is committed` red.

## Green runs

- `CapabilityMatrixTests` and `ConfinementPlannerTests`: green, including the 3 new tests and the new InlineData.
- `npm test` (`src_vs_code`): 6290 tests, 6270 pass, 1 fail: `the baseline names no release that was never tagged` (known, no git tags).
- Generator `--check`: exit 0. Generator run: `21 rows over 7 runtimes × 3 capabilities`.
- Whole MCP suite: 3 failures:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`: the two known failures.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT in the document's known list. See open items.

## Deviations from the document

1. The `AdmissionFlagsBothRows` red message is *Expected type to be Admitted, but found Refused*, not the document's
   "not a runtime … launch" text. The message in the document is the one the `Admit` path gives after the vectors and rows
   are in. The red is the same cause: the matrix does not know kimi yet.
2. `CapabilityMatrixTests.cs` has a list `Records` (the record each row may cite, with its date and cell prefix). It had no
   kimi entry, so `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` failed once the kimi rows existed. I added
   `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")` and updated its doc comment. No assertion changed. The document
   does not mention this test; it is needed for "CapabilityMatrixTests green".
3. The step 1 planner red was caused by the matrix refusing kimi (a different message from the document's
   "red on the web refusal or Cwd"). The step 4 red (`Cwd`) happened as described.
4. Commit: the run asked for one, although the document says "No commit unless the run asks you to". I followed the run.

## Open items

- `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` fails (`AllChangesAsync(...).Failed` is false).
  The test does not reference the capability code, and none of my files is in its path, but I did NOT run it at `b4e2ea96`
  to prove it was already red. It uses a 1 ms timeout and an unknown commit, so it may depend on git timing on this host.
  The owner should check it at the base commit.
- Not done: the `common.coai-review-gate` rule (the ConnectOtherAIs review) was not run. The document lists its steps and
  does not mention the gate; I followed the document.
- Not done: no push (as instructed).
