# T2 t4 — review notes

Change reviewed: `d88bd305` (one new file, `src_mcp/tests/AKimiQuestionRowTests.cs`, 190 lines). Only that file changed in `b79dbbce..HEAD`.

## Findings (all "right as it was"; nothing fixed, nothing committed)

- Contracts: class name, `[Collection("fakecli-env")]`, `IAsyncLifetime`, the fields, `Repo`/`Homes`/`OneShotRoot`, `InitializeAsync`, `DisposeAsync`
  (`GitScratch.AfterUnlink = null` first), `Scopes`/`StandIn` (no `CallingAs`), `RepositoryAsync` (same as the Vibe test), `Service`
  (`ConsultantHomesRoot = Homes`, `DataDir`, the one `kimi-disk` row), `AskAsync`, `TheRow`, `After` all match the document.
- Test: name, `[Theory]` with the three `InlineData`, body order and every assertion text match the document. The coder merged the
  per-ending `played`/`model` choice into one `switch` and one ternary. Same behaviour, so left as is.
- No `Assert.SkipUnless` copied: right (the test must run on every host).
- Red reason: the coder's mutation (`Release(homes);` commented out) turned all three cases red. The refused case is red too: the refusal
  path reserves its one-shot folder before the model check. This is a true fact, and the case's assertions are still right.
- Conventions: temp folder is `testsToDelete-qkimi-` and is removed in `DisposeAsync`; no `testsToDelete-qkimi-*` folder in /tmp after my run.
- Scope: no production file changed.

## Results

- Build (`dew_flow_connect_other_ais.slnx`, Debug, `-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.AKimiQuestionRowTests`: 3 total, 3 passed, 0 failed.
- I did not re-run the whole suite or the mutation; the coder's notes show both (whole suite: only the 2 known failures).
