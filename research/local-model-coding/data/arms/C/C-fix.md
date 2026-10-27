# Arm C — fix notes, review of E5.5 and E5.6 (Sonnet, 2026-10-25)

Branch `exp/kimi-e56-classic`. Commits: `95d9e15f` (R1, R2), `d7d48c56` (R3, R4). No push, no `kimi`, no `vibe`.

## R1 (major) — FIXED
The finding is right: the unconfined caveat said "a shell is always there", and Kimi's shim refuses every shell command.
- Test first: `CapabilityMatrixTests.TheUnconfinedCaveat_NamesNoShell_SoItIsTrueOfKimiToo` (C#) and an added assert in the TS test
  `kimi rows admit flagged, web refuses naming its record`. The C# test failed with the old sentence
  ("Did not expect flag.Caveat() to contain equivalent of "shell" … found "unconfined: can read this machine — a shell is always there …"").
- Fix: one sentence on both sides, true for every unconfined runtime (codex too):
  `unconfined: can read this machine — its tools reach outside the folder coai gives it, and no flag removes that`
  (`CapabilityMatrix.cs`, `capabilityAdmission.ts`). The definition in `shared/runtime-capabilities.json` `why` now says
  "its tools reach outside the folder it is given".
- Not changed on purpose: the codex `none` row's `note` ("a shell is always there; …") in the seed. It is codex's own note and is true for codex.
  The vectors file `why` for codex ("F2: a shell is always there") is also about codex. The reviewer asked only for the caveat and the definition.

## R2 (minor) — FIXED
`why` in the seed now says the kimi rows cite the M-K cells of `research/RESULTS_kimi_confinement.md` (Windows, kimi 2.1.1, 2026-10-13).
The generator makes the same `runtimeCapabilities.generated.ts` (`why` is not copied into it); `--check` is clean.

## R3 (minor) — PARTLY FIXED
- Fixed: a new arm `launch-refused` in S5.6.7 (`AKimiQuestionRowTests`). The version probe answers exit 1, so the launch control refuses AFTER the
  one-shot home and its junction exist (`PlayedKimi.Version` is a new seam). The arm checks: the row is `failed` (that is the row's own outcome for a
  control refusal), the shim recorder is empty, one version probe ran, the homes folder is empty, the deleter's unlink recorder is empty (junction-first)
  and the person's folder is unchanged.
- Fixed: the row's own `coai-question-*` scratch is asserted gone, not only the `--cwd` copy, in the two arms that launch the shim
  (`TheRowsScratchIsGone`). Teeth: with `Remove(scratch)` taken out of `QuestionRowLaunch`'s `finally`, the answered and cancelled arms go red
  (restored; `git diff` of `src_mcp/src` is empty).
- NOT fixed: the scratch's absence in the two arms that never launch the shim (`refused`, `launch-refused`). `PanelService` does not hand the
  scratch path out, `QuestionFanOut` builds `QuestionRowLaunch` itself (no `MakeScratch` seam from the service), and the git calls that make the copies
  do not go through the test's launcher, so there is nothing to read the path from. A scan of `/tmp` for `coai-question-*` would race with other
  test classes that run at the same time, so I did not write it. A fix needs either a seam on `QuestionFanOut` or a test at `QuestionRowLaunch.RunAsync`
  level (as `AVibeQuestionRowReadsACopyOfItsRootsTests` does). That is a bigger change than "cheap"; I left it.

## R4 (minor) — FIXED
`Adapters()` in `ConfinementPlannerTests` now takes the Kimi consultant from `KimiQuestionConsultant()`; the copy is gone.

## Runs
Last commit also: `style(kimi)` — one line my edit had joined, found by the format check.
- Build (`-m:4 --disable-build-servers`): 0 warnings, 0 errors.
- Changed classes (`AKimiQuestionRowTests`, `CapabilityMatrixTests`, `ConfinementPlannerTests`): 78 total, 78 passed.
- Whole MCP suite: 10557 total, 10470 passed, 84 skipped, 3 failed: the two known Kimi tests and
  `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (red at `b4e2ea96` on this host too, see C-code.md).
- `dotnet format --verify-no-changes`: no style or whitespace line after the fix (only the known src_server IL2026/IL3050 AOT noise).
- Generator `--check`: up to date.
- Extension `npm test`: 6290 tests, 6270 passed, 19 skipped, 1 failed (the known `the baseline names no release that was never tagged`).
