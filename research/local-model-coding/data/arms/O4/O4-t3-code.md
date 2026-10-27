# O4 t3 — coder notes (S5.6.7, E5.6 decision 5)

Commit: `aaa7e70e` on `exp/kimi-e56-opus-split-haiku-4`. Only `src_mcp/tests/AKimiQuestionRowTests.cs` added.
`src_mcp/src/Server/QuestionConsult/QuestionRowLaunch.cs` is UNCHANGED (decision 5 held).

## What was done

- New test class `AKimiQuestionRowTests` with the contracts from the task document: one theory over
  `answered` / `refused` / `cancelled`, through `PanelService.AskConsultantsAsync` with `PlayedKimi`, git for real,
  `WindowsStandIn` + `KimiSeams` + `KimiCredentialsLock` stood in per flow, `KimiFixture` as the fake person.
- Formatted with `dotnet format` (the file's `switch` braces were mis-indented; fixed by the formatter, nothing else).

## Deviation from the document (one)

- **`refused` arm uses "not signed in", not a re-login.** The document says to rewrite the slot key to
  `oauth/kimi-code-env-y`, citing `AKimiConsultationScenarioTests.cs:244`. Run first, it FAILED: a first launch was
  NOT refused and the shim started (`played.Shims` not empty). That scenario's refusal is a FOLLOW-UP turn, where
  the home is checked against the conversation's recorded sign-in; a fresh row's home is made new, so a re-login
  does not refuse it.
  Fix in the test: `Directory.Delete(_person.Credentials, recursive: true)` before the `before` hash. The launch
  control then refuses in `KimiLaunch.Rendered` ("has no credentials folder"), before any shim starts. The `before`
  hash is taken after this change, so the "person's folder unchanged" check still holds.
  No production change was needed for this.

## Results

- Red message from the first run (before the fix above): `Expected played.Shims to be empty ... but found at least one item`
  (refused arm). This was a test mistake, fixed in the test.
- **`refused` arm status word: `failed`** (`reason`: "not started: not signed in to Kimi Code — …has no credentials
  folder…"). Found with a temporary probe print, since removed; the assertion stays `BeOneOf(Failed, Refused)`.
- Teeth mutation: `Release(homes);` commented out in `QuestionRowLaunch.cs` `finally`. Red on all three arms:
  - answered: `Expected Directory.Exists(Path.GetDirectoryName(HomeOf(request))) to be False ... but found True.`
  - cancelled: `Expected Directory.Exists(Path.GetDirectoryName(HomeOf(shim.Request))) to be False, but found True.`
  - refused: `Expected ... EnumerateDirectories(OneShots) ... to be empty ... found at least one item {…/homes/coai-vh/91f17949}`
  File restored; `git diff --exit-code` on it: identical.
- `AKimiQuestionRowTests`: green 3 runs of 3 (3/3 each run).
- `AVibeQuestionRowReadsACopyOfItsRootsTests`: 18 total, 0 failed, 6 passed, 12 skipped (Windows-only skips, as before).
- Whole suite: 10556 total, 10469 passed, 84 skipped, 3 failed:
  1. `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — KNOWN, not mine.
  2. `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — KNOWN, not mine.
  3. `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT on the document's known list.
     It fails when run alone too. Its failure: `"recorded"` where `"unavailable: git…"` was expected. The test gives
     git a 1 ms deadline, so it looks timing-dependent on this fast host. My change does not touch the Rounds code.
     **I did not verify it fails at `b4e2ea96`.** Someone should check it there.
- `dotnet format`: my two files verify clean (`--verify-no-changes --no-restore --include …`). The solution-wide
  `dotnet format --verify-no-changes` does not finish cleanly on this host: it reports IL2026/IL3050 trimming-analyzer
  errors in `src_server/` (not my files), and the `--include` form without `--no-restore` fails on restore.

## Not done / left

- The numstat test in the suite (item 3 above) is unverified at `b4e2ea96`.
- The solution-wide `dotnet format --verify-no-changes` is not clean on this host because of `src_server` analyzer errors; I did not touch those files.
- Nothing pushed (per the rules).
