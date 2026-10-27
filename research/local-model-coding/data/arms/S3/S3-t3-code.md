# S3 t3 — code notes: AKimiQuestionRowTests (S5.6.7)

## What I did

- Added `src_mcp/tests/AKimiQuestionRowTests.cs` (one file, no production code). Class `AKimiQuestionRowTests`, `[Collection("fakecli-env")]`, `IAsyncLifetime`.
- One theory `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with arms `answered`, `refused`, `cancelled`.
- Stand-in per test: `WindowsStandIn.Begin()`, `KimiCredentialsLock.RootFor`, `KimiSeams.Using(HostKind.Windows, ...)` over a fresh `KimiFixture`. No `CallingAs`.
- Service built like the Vibe test's `ServiceFor`, with `QuestionRow("kimi-disk", "my-kimi", "kimi", "", "", "", "", "question-disk", Enabled: true)`.
- Recorder: `GitScratch.AfterUnlink` set before the ask, reset to `null` in `finally`.
- Assertions per arm: answered (one shim launch, `StdIn` names the launch folder, not under the repo, not the original `src`); refused (`status` Refused, reason contains "not inside a Git work tree", no shim launched); cancelled (`OperationCanceledException`). After every arm: `coai-vh` empty (guarded by `Directory.Exists`), scratch `coai-question-*` ancestor of the launch folder gone (answered and cancelled), unlink recorder empty, person's `Hash()` unchanged.

## Deviations from the document

- The document's `Other` property is included as written but nothing uses it. InitializeAsync does not create `other/`, so it is a dead property. Kept for contract fidelity; delete it if reviewers flag it.
- The `NothingMidTurn` static field sits after the method that reads it. Legal C#; placement is a style choice.
- The executable on Linux is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (no `.exe`). The low-priority wrapper cannot spawn the `.exe` path from the document. Used the path without `.exe`.

## Results

- Build (`dew_flow_connect_other_ais.slnx`, Debug, `-m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- First run on arrival (t1/t2 already in tree): 3 of 3 green. The document's expectation held: the arms were green on arrival.

## Teeth runs (both reverted; `git status` clean apart from the new file before commit)

(a) `src_mcp/src/Server/QuestionConsult/QuestionRowLaunch.cs` line 135: `Release(homes);` removed. All three arms red, e.g.:
> Expected Directory.EnumerateFileSystemEntries(OneShotRoot) to be empty because the row's folder under coai-vh is deleted on every way out, but found at least one item {".../homes/coai-vh/0d902b3f"}.

(b) `src_mcp/runners/Kimi/KimiConsultant.cs` line 40: `HomesFolder => ConsultantHomes.Kimi;` changed to `string.Empty`. Answered and cancelled arms red; refused arm stayed green (refusal happens before any launch, so the home location never matters there). Red messages:
- answered: `status` was `failed`, reason "a Kimi consultant keeps its home in a coai-owned folder of the conversation or the row (OwnedScratch), and none was given … (Parameter 'launch')". The document expected a path/`StdIn` assertion; the real failure is earlier, at the row's status.
- cancelled: "Expected a <System.OperationCanceledException> to be thrown, but no exception was thrown." The document did not predict this message.

After revert and rebuild: 3 of 3 green.

## Commit

- `47e4adc7` test(kimi): a Kimi disk question row reads copies and leaves nothing behind (E5.6, S5.6.7). Only `src_mcp/tests/AKimiQuestionRowTests.cs`. Not pushed.

## Left undone / for the reviewer

- Nothing in the contract is missing.
- The red message for teeth (b) on the answered arm differs from what the document implied; the arm still goes red, for a different reason.
- Two MCP tests named in the task (`KimiRuntimeTests`, `KimiHomeTests`) were not run; the task said not to run the whole suite.
