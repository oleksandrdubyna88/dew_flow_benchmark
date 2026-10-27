# O3 t4 — Kimi disk question row through QuestionRowLaunch (notes)

## Done
- New `src_mcp/tests/AKimiQuestionRowTests.cs`: `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` (Theory: answered / refused / cancelled).
  Kimi row `kimi-disk` on `PlayedKimi.Shim`, the Windows stand-in (no Windows skip), `KimiFixture` person, recorder `GitScratch.AfterUnlink`
  reset in `DisposeAsync` together with `KimiHome.AfterUnlink`.
- No production change: `QuestionRowLaunch.cs` is unchanged (`git diff` empty for it). Temporary breaks were reverted; `git status` shows only the new test file.

## Results (Linux, this host)
- `AKimiQuestionRowTests`: 2 of 3 green (answered, cancelled), 1 RED (refused, see below).
- `QuestionRowsTests`: 9 of 9 green.
- `AVibeQuestionRowReadsACopyOfItsRootsTests`: 6 green, 12 skipped (Windows-only, D18). Expected here.
- `dotnet format --verify-no-changes`: the new file is not flagged. The run prints IL analyzer errors from `src_server` (not this task).

## Red that differs from the document
Refused arm, the document's expectation: status `RowOutcomes.Failed`. Actual red:
`Expected TheRow(reply).GetProperty("status").GetString() to be "failed" with a length of 6 ... but "answered" has a length of 8`.
The row ANSWERED, and a shim ran. Cause (read, not changed): `KimiHome.cs:239` (`Config`) checks config.toml only against an
EXISTING home ("is not what coai writes ... a re-login"). A question row makes a fresh home from the current sign-in, so changing the slot key
before a row's ask does not refuse it. S5.4.16 works because its first turn made the home, then the key changed.
The document does not say how a fresh row is refused before the launch. Not invented here. Needs a decision: the document's author either
(a) names a different refusal for the refused arm, or (b) accepts the arm as a two-turn scenario (not possible in a one-shot row).

## Teeth (temporary breaks, recorded, reverted)
- `QuestionRowLaunch.cs` `finally`: `Release(homes);` commented out → answered and cancelled arms red:
  `Expected Directory.Exists(RowFolderOf(launch.Request)) to be False because the row's folder is gone once it ended, but found True.`
- Junction-first order: a bare `GitScratch.DeleteEntry` over the one-shot folder put before `KimiHome.DisposeAllUnder` in
  `src_mcp/runners/Vibe/VibeHomes.cs` (reverted) → answered and cancelled arms red:
  `Expected unlinked to be empty because the junction is unlinked first, never deleted through, but found at least one item {".../coai-vh/<8>/kh-.../home/credentials"}.`

## Deviations from the document (for t5)
- The copy path: the document's `<row folder>` (`Path.GetDirectoryName(--home)`) does NOT contain the copies. The prompt names
  `<coai-question-*>/tree/snapshot-*`, which sits under `--cwd`. The test asserts `StdIn` contains `--cwd` and that `--cwd` is gone after the row.
  The `coai-vh/<8>` folder is still asserted gone, as the document says.
- `CallingAs` was not copied: it is `protected` on `ConsultScenarioBase`, which this class does not derive from (contract: `IAsyncLifetime`).
  `AKimiConsultantCheckTests.StandIn()` also omits it, so the same shape is used.
- `StandIn` `Scopes` is a nested class copy (as in `AKimiConsultantCheckTests`).

## Left
- The refused arm (above). Everything else in the contract is implemented.
- Whole suite not run (only the three named classes). Pre-existing known failures are not checked here.
