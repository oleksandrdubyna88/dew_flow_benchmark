# T1 t5 — review notes (S5.6.7, E5.6)

Result: nothing needed a change. No commit made by the reviewer. The coder's commit 617326f4 stays as it is.

## Findings

1. Scope — right as it was. The diff `dac76033..HEAD` is one commit and one file: `src_mcp/tests/AKimiQuestionRowTests.cs` (new, 190 lines). No production file, no fixture, no other test.
2. Contracts — right as it was. Class name, attributes (`[Collection("fakecli-env")]`, `IAsyncLifetime`), fields, `Repo`/`Homes`/`OneShotRoot`, `InitializeAsync`, `DisposeAsync`, `RepositoryAsync`, `StandIn` (no `CallingAs`), `Scopes`, `Service`, `AskAsync`, `TheRow`, `Ending` enum, theory name and the three `InlineData` match the document. The assertions of steps 5 and 6 match the document word for word.
3. Deviation 1 (RefusedBeforeTheLaunch) — right as it was. The document says `File.Delete(_person.Credentials)`. `KimiFixture.Credentials` is a FOLDER (`KimiFixture.cs:50`), and `KimiLaunch.Rendered` refuses only when this folder is missing (`KimiLaunch.cs:194`). So the test clears the read-only bits and deletes the folder. This is the smallest change that makes the arm work. The shim is not started (`Shims` is empty), so the arm proves what the document wants.
4. Deviation 2 (order of removal and `before`) — right as it was. The removal changes the person's folder, so it must come before the snapshot, or the last hash check can never pass. The snapshot still proves that the row changes nothing.
5. Red reason — not shown again. The tree at `dac76033` already holds t1 and t2 (the document's red needs them absent). The coder's notes show the bite check (`NotBeEmpty` on the unlink recorder: 3 of 3 arms red) and the red of the first try of arm 2. I did not repeat them.
6. Windows-only part — not verified here: the read-only clearing and the junction disposal run only in the Windows stand-in path of the fixture. Same as the coder said.

## Tests (Linux, WSL2, Debug)

- Build: `dotnet build src_mcp/tests/CoaiMcp.Tests.csproj -c Debug -m:4 --disable-build-servers` through `low-priority.mjs`: 0 warnings, 0 errors.
- `CoaiMcp.Tests.AKimiQuestionRowTests`: total 3, passed 3, failed 0, skipped 0.
- The whole suite was not run by the reviewer (the task asked for the task's classes only). The coder reported one failure outside the known two: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. It does not touch this file. It needs an owner.
