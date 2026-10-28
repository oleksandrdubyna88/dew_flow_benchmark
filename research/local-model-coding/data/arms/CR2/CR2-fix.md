# CR2 — fixes after the review (Sonnet coder), E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-2`. Nothing pushed. No `kimi` or `vibe` started.
Commits after the review: `aead2263` (R2), `ef74e06a` (R3, R4), `78f783d3` (R5).

## Per finding

| Id | Severity | Result |
|---|---|---|
| R1 | major | **NOT FIXED — blocked by scope. The finding is right.** |
| R2 | minor | **Fixed.** |
| R3 | minor | **Fixed.** |
| R4 | minor | **Fixed.** |
| R5 | minor | **Fixed in the plan; the decision itself is still open.** |

### R1 — knowledge base not updated (major) — not fixed
- I checked it. It is true: `research/module_runners.md:3637` says Kimi is "NOT in `runtime-capabilities.json`";
  `research/module_core.md:783` says "18 rows (6 runtimes × 3)"; `module_core.md:863` says "eighteen since S3";
  `research/module_tests.md` has 3 hits for "E5.5/E5.6" only in other context, and no E5.5–E5.6 section.
- Why I did not fix it: the brief says `research/` is out of scope "as before". Every fix of R1 is a change inside `research/`.
  The brief also says to fix every major finding. The two instructions conflict, and I could not ask. I followed the explicit
  out-of-scope line and did not overcome it. The rule `common.knowledge-base` says the update is mandatory, so this
  **must be done before the stories are promoted**.
- What to write (so it is cheap for whoever does it):
  1. `module_runners.md:3637`: Kimi is in `runtime-capabilities.json` now (3 rows: `none` = unconfined, `disk`, `web` = unmeasured).
  2. `module_core.md:783`: "21 rows (7 runtimes × 3)". `module_core.md:863`: "twenty-one since E5.6".
     Add the Kimi planner arm and the Kimi rows to the planner section; a plan with `Flags` or `Leading` flags is refused for Kimi.
  3. `module_tests.md`: a new section for E5.5–E5.6 with `AKimiConsultantCheckTests`, `AKimiQuestionRowTests`, the new rows in
     `CapabilityMatrixTests` and `ConfinementPlannerTests`, and the TS test `capabilityAdmission.test.ts`. Put in the red or teeth
     run of plan deviations 50–51 and of this fix (below). Say what is NOT covered: the live check, the live rows, M-K17, and the
     `none` row through `QuestionRowLaunch`.

### R2 — "cancelled" matched in the whole record (minor) — fixed
`AKimiConsultantCheckTests.ACheckCancelledMidTurn_StillDisposesItsHome` now asserts `Text(said, "failureKind").Should().Be("cancelled")`,
the same field `ConsultantCheckTests.cs:471` reads. I did not run a break for this one: the old assertion was a substring over the
record, the new one reads the field the product writes, and it is green.

### R3 — S5.6.7 answered arm granted a folder with no `.git`/AGENTS.md (minor) — fixed
The test repository now commits `AGENTS.md`, `.kimi-code/config.toml` and `.mcp.json` as well as `src/Mailer.cs`. The answered arm grants the
repository root. While the shim runs, the files it can see must include `src/Mailer.cs` and none of `.git`, `AGENTS.md`, `.kimi-code`,
`.mcp.json` (matched as path parts, not as text).
**Teeth:** I set `SnapshotExclusions.Default` to `[".git"]` only. The answered arm went red with: *Expected copied {…"AGENTS.md"…".mcp.json"…".kimi-code","config.toml"} to not have any items matching …
because the copy is sanitised … but found …*. I restored the file (`git checkout`).

### R4 — refused arm returned a null scratch (minor) — fixed
The refused arm now drives `QuestionRowLaunch` directly (admission, `RowLaunchInput`) with its own `MakeScratch`, which makes
`coai-question-refused` inside the test's `testsToDelete-qkimi-` folder and remembers it. The arm returns that folder, so
`NothingIsLeftBehind` checks it is gone. The arm also asserts the scratch was really made before the refusal. The reviewer's
idea (list `coai-question-*` in the temp folder before and after) was not used: other tests run in parallel and make folders with that
prefix, so it would be flaky. The row's reply is still read through the service in the other two arms.
**Teeth:** I commented out `Remove(scratch)` in the `finally` of `QuestionRowLaunch.RunAsync`. All three endings went red; the refused one
with *Expected Directory.Exists(scratch) to be False because the row's scratch … is gone too, but found True*. I restored the file.
**Leftover of that break:** the answered and cancelled arms made real `coai-question-*` folders in the OS temp folder while the break was in.
This session may not look in `/tmp`, so I did not remove them (two small folders, a copy of a 4-file test repository each). The product's own
scratch sweep (`PruneOldScratchDirs`, 6 hours) removes them.

### R5 — Sonnet built the stories, the plan says Opus (minor) — fixed in the plan, decision open
Added deviation 60 to *E5.5–E5.6 as built*, and changed the status line. It says the run was started on Sonnet, that no operator
decision is written down in this repository, and that the operator must record one or have the stories rebuilt on Opus before promotion. I did not
invent an approval: there is none I can see. `plan-lifecycle.mjs`: clean.

## Final results
- **Build** (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 warnings, 0 errors.
- **Changed classes** `AKimiQuestionRowTests` (3) and `AKimiConsultantCheckTests` (5): 8 of 8 pass.
- **Whole MCP suite** (5m51s): 10555 total, 10451 pass, 84 skipped, **20 fail**. Log: `suite.log` in this folder.
  - 2 known: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`.
  - 17 that the coder's notes already list as failing at the base too (rounds database reads empty on this host):
    `ACodeRoundRecordsItsSizeTests` 7, `ARoundsNumberComesFromTheJournalTests` 3, `EndToEndTests` 3, `ARoundIsMintedAKeyTests` 2,
    `SplitOrderTests` 1, `ThePersonsCommandsReachTheCallerTests` 1.
  - **1 new in the list: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`.** It fails again when run alone. It gives the
    git call a 1 ms deadline and expects `unavailable: git…`; the git call finished first and the state was `recorded`. It is timing-dependent. This work and
    its fixes change no file it uses (`RoundSizer`); the product diff since `b4e2ea96` is only `ConfinementPlanner.cs`, `RuntimeCapabilities.cs`,
    `KimiConsultant.cs`. I did **not** run it at the base, so "same at the base" is not proven; "not caused by this work" rests on the diff.
- **Format** (`dotnet format … --verify-no-changes --no-restore --include` the two changed test files): clean. Whole-solution format not run again
  (the coder's notes say it prints IL2026/IL3050 errors in `src_server` on this host, files untouched).
- **Generator check** (`generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
- **TS tests** (`npm test` in `src_vs_code`): 6290 tests, 6270 pass, 1 fail — the known `the baseline names no release that was never tagged`. Log: `ts.log`.
- `plan-lifecycle.mjs`: clean.
