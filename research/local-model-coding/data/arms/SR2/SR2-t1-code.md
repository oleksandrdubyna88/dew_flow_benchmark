# SR2-t1 — Kimi capability rows (E5.6): code notes

Commit: `7f921b60` on branch `tmp/SR2-t1` (not pushed).
Files changed (only the six in the task list):

- `shared/runtime-capabilities.json` — `kimi` in `runtimes`; the `why` paragraph names the kimi M-K cells; three kimi rows (`none`, `disk` unconfined; `web` unmeasured).
- `shared/capability-matrix-vectors.json` — three kimi vectors after the vibe ones.
- `src_mcp/core/QuestionConsult/RuntimeCapabilities.cs:84` — `Runtimes` gets `"kimi"`.
- `src_vs_code/src/runtimeCapabilities.generated.ts` — written by the generator only (`21 rows over 7 runtimes × 3 capabilities`).
- `src_mcp/tests/CapabilityMatrixTests.cs` — `Records` gets the M-K record; the refusal sentence expects `… vibe, kimi`; new tests `KimiHasThreeRows_EachCitingItsCells` (S5.6.1) and `AdmissionFlagsBothKimiRows_AndRefusesItsWeb_NamingItsRecord` (S5.6.4).
- `src_vs_code/src/test/capabilityAdmission.test.ts` — runtimes list, refusal regex, new test `kimi rows admit flagged, web refuses naming its record`.

## Red messages

Step 1 (tests before production code):

- S5.6.1: `Expected none not to be <null>.` — as the document says.
- S5.6.4: `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found CoaiMcp.Core.QuestionConsult.Admission+Refused.` — same shape as the document. The document's quoted refusal text (`'kimi' is not a runtime …`) was not in the FluentAssertions message; it does not print the reason on a type mismatch.
- Also red in step 1, as expected from the edit to 5b: `AnUnknownRuntimeOrCapability_IsRefused_NamingWhatIsKnown` (expected `… vibe, kimi`; actual `… vibe`). The document did not list it, but it is caused by the same edit and goes green after step 2.
- TS, before the generator: `Expected values to be strictly deep-equal` on the runtimes list (`… 'vibe' ]` vs `… 'vibe', 'kimi' ]`) — as the document says. The regex test failed the same way, and the new kimi test failed with `'kimi' is not a runtime the question consultant can launch …`.

Step 2 (`Runtimes` and seed):

- S5.6.1 and S5.6.4 green.
- S5.6.2 red, as the document says: `the table's kimi × none row must have a vector deciding it` (the first missing pair; kimi × disk and kimi × web are the same failure for the other rows).

Step 3 (vectors): `CapabilityMatrixTests` 39 of 39 green.

## Checks

- `CoaiMcp.Tests.CapabilityMatrixTests`: 39 passed, 0 failed.
- Generator `--check`: exit 0.
- Extension `npm run typecheck`: clean. `npm run lint`: no output (eslint silent).
- Extension `node scripts/run-tests.mjs` (via `npm test`'s runner): 6290 tests, 6270 pass, 1 fail — `the baseline names no release that was never tagged` (the known tag failure). `capabilityAdmission` green, including `kimi rows admit flagged, web refuses naming its record`.
- Whole MCP suite (`CoaiMcp.Tests.dll`): 10542 total, 10455 passed, 84 skipped, 3 failed:
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known, not mine.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — known, not mine.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT in the document's known list. It failed once in the full run (`AllChangesAsync(...).Failed` expected True, found False). Re-run alone: 10 of 10 pass. It uses a 1 ms timeout on a git call and touches no capability file, so I read it as a timing flake under full-suite load. I did not prove this against the base commit.
- `dotnet format --verify-no-changes`: exit 2. The output has only `IL2026` / `IL3050` analyzer errors in `src_bugs` and `src_server` (files I did not touch). None names a file I changed, and no whitespace or format finding names one. The exit code is not clean, so the document's "clean" check is not met as written.

## Other notes

- The document ends with "Do not commit." under *Out of bounds*. I read that as "do not commit files outside the list", because the Rules section asks for a conventional commit of my files by name. I committed the six files only.
- `npm test` / `npm run compile` runs `prepare:gate` and `generate-security-lane`. They did not change any tracked file.
- Nothing was pushed.

## Left

- Nothing in scope.
- Open question for the lead: whether `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` is a known flake, and whether the `dotnet format` IL analyzer errors in `src_bugs` and `src_server` are known on this host.
