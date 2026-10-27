# t4 — review notes

## Findings

1. **FIXED — the Refused arm's premise was wrong (the coder's open question).** The document expects a re-login before the row to make
   `KimiLaunch` refuse with "config.toml is not what coai writes". It cannot: a fresh one-shot home is written by `KimiHome.Made` from the
   person's CURRENT sign-in, just before the check. The arm answered instead of failing.
   Fix (test file only, smallest diff): removed the re-login line; added a private `TamperedAtVersion(IProcessLauncher inner)` decorator that rewrites
   the home's `config.toml` while `kimi --version` runs (the probe's working directory is `places.Home`). That is after the home is made and
   checked, before the shape is asked again in `KimiLaunch.VerdictAsync`. The refusal is the real one (reason contains
   "config.toml is not what coai writes", status `failed`, no shim started). The junction exists at that point, so the junction-first cleanup is
   really exercised in this arm. `Service` now takes `IProcessLauncher`; only the Refused arm wraps `played`.
   Left as the contract has it: no `PlayedKimi.cs` change, no production change, the assertions of the arm unchanged.
   Deviation from the document: one private nested class + the `Service` parameter type. Needed; the document's seam does not exist.
2. **Right as it was** — everything else: class, enum, Theory, collection, helpers (copied bodies match the Vibe twin), the `Scopes`/`StandIn`
   shape (without `CallingAs`), `HomeOf`, `AScratchStillHoldsTheCopy`, the common assertions, the Answered and Cancelled assertions, `GitScratch.AfterUnlink`
   set and reset in `finally`. Per-ending blocks are in three private methods chosen by a `switch` (complexity). Only one file changed; no production file.
3. The coder's note on the `.exe`: on this host the runner has no `.exe`. Not a defect.

## Red checks
- (a) planner `"kimi"` arm removed — coder's run, verbatim: *Expected copies to contain a single item because one copy per granted root, but the collection is empty.* Not re-run.
- (b) `VibeHomes.EmptiedUnderItsLock`, the `KimiHome.DisposeAllUnder` block removed — I re-ran it: ALL THREE arms red, including the fixed Refused arm:
  *Expected recorded to be empty because the junction was unlinked by KimiHome first, never met by the bare deleter (E5.3 decision g), but found at least one item {".../homes/coai-vh/<8>/kh-<10>/home/credentials"}.*
  Reverted (`git status`: only the test file).
- The Refused arm before the fix failed on the status (`answered`, not `failed`) — the coder's run.

## Results (Linux/WSL2 host)
- Build: 0 warnings, 0 errors.
- `AKimiQuestionRowTests`: 3/3 pass, 0 skipped.
- With `AKimiConsultationScenarioTests`, `AVibeQuestionRowReadsACopyOfItsRootsTests` (Windows-only, 12 skipped) and `QuestionRowLookupScenarioTests`: 27 passed, 12 skipped, 0 failed.
  The two `QuestionRowLookupScenarioTests` failures the coder saw in the whole suite did not reproduce; they pass in this run and alone — the coder found it cross-test interference, not this file. The whole suite was not re-run (deadline).
- `dotnet format --verify-no-changes --include` on the file: clean.
