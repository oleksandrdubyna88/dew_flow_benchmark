# SR4 t3 — code notes (S5.6.7: a Kimi disk question row reads its copies and leaves nothing behind)

## What I did

- Added `src_mcp/tests/AKimiQuestionRowTests.cs` (new, namespace `CoaiMcp.Tests`, `public sealed class AKimiQuestionRowTests : IAsyncLifetime`).
  No production code changed. No `[Collection("fakecli-env")]` (not needed: the three arms ran green without it).
- One `[Theory]` `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with three endings: `answered`, `refused-before-the-launch`, `cancelled-mid-turn`.
- Through `PanelService.AskConsultantsAsync`, git for real, `PlayedKimi` as the launcher over a `KimiFixture`, `WindowsStandIn` + `KimiSeams` + `KimiCredentialsLock` stood in with a synchronous `StandIn(person)` (Scopes class copied from `AKimiConsultationScenarioTests`).
- Assertions per ending:
  - answered: status `answered`; one shim launch; two copies enumerated during the shim run; each copy named in the prompt; neither granted root named in the prompt; no `--session`; `--home` under `<homes>\coai-vh`; the one-shot folder and the row's working folder are gone.
  - refused: status `refused`; no shim launch.
  - cancelled: `OperationCanceledException`; the one-shot folder and the working folder are gone.
  - all endings: `coai-vh` has no entries left (guarded by `Directory.Exists`); `GitScratch.AfterUnlink` recorder empty; `person.Hash()` equal to before.

## Deviations from the task document (and why)

1. **Test runner path.** The document's command runs `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe`. That file does not exist on this Linux host, and `low-priority.mjs` fails with `spawn ... ENOENT`. I ran the same classes through `dotnet ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll --filter-class ...` (same wrapper, same filters).
2. **`During` assignment.** The document says `played.During = ...`. `PlayedKimi.During` and `Turn` are `init`-only, so the assignment does not compile. I set `During` in the object initializer instead. Same behaviour.
3. **Copy count.** The document asks for "copies not empty". I asserted `HaveCount(2)` (one copy per granted root), as the Vibe test does.
4. **Scratch check.** The document asks to scan the scratch parent for `coai-question-*`. Those folders are made in the system temp directory, which is shared, so a scan there is not isolated. I asserted instead that the shim's working folder (which holds the copies) no longer exists, for the answered and cancelled arms.
5. **Refused arm.** The document's `homes` check is guarded by `Directory.Exists`, so I used the same guard. No shim launch is asserted.

## Test results

- Build (`dew_flow_connect_other_ais.slnx`, Debug, `-m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- `AKimiQuestionRowTests`: **3 of 3 passed** on arrival (answered, refused-before-the-launch, cancelled-mid-turn). Green on arrival is expected here: E5.3's junction-first disposal is already in the tree.
- `NoTestHoldsAnInheritableHandleTests`: **2 of 2 passed**.
- Not run: the whole suite (per the brief). The two known failures named in the document (`KimiRuntimeTests.ALaunchThatNamesItsHome...`, `KimiHomeTests.AHomeWhoseDataPathPasses260...`) are not in the classes I ran.

## Teeth check (Step 2) — temporary, reverted

- Control 1: `Directory.CreateDirectory(<homes>\coai-vh\left-behind)` in the answered arm, before the one-shot folder assertion. Result: red, `Expected Directory.EnumerateFileSystemEntries(coaiVh) to be empty ... found ... left-behind`.
- Control 2: `recorded.Add("control")` before the recorder assertion. Result: red in all three arms, `Expected recorded to be empty ... found {"control"}`.
- Both lines removed (grep for `TEETH` returns 0). Rebuilt, re-ran: 3 of 3 green. The committed file has no control lines.

## Commit

- `dfd4212e` `test(kimi): a Kimi disk question row reads its copies and leaves nothing behind (S5.6.7)` — only `src_mcp/tests/AKimiQuestionRowTests.cs`, added by name.
- Conflict noted: the task document's Out-of-bounds section says "No commit", while the brief says "Commit with a conventional message". I followed the brief. If the document's rule was meant, `git reset --soft HEAD~1` (or `git rm --cached`-style undo) takes it back; nothing is pushed.

## Left

- Nothing in scope left undone.
- Not checked: the Windows-only path of the same arms (this host is Linux; the arms ran through `WindowsStandIn`, which is the path E5.4 chose for every OS).
