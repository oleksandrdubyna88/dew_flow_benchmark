# CR3 — FIX notes (Sonnet), review of E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-3`. No push. Fix commits: `27bc543b` (R3), `a39d76b6` (R4), `08ca9218` (R5).

## Per finding

| Id | Severity | Result |
|---|---|---|
| R1 | major | **NOT FIXED — blocked by the brief.** |
| R2 | minor | **NOT FIXED** (on purpose). |
| R3 | minor | **FIXED** in `27bc543b`. |
| R4 | minor | **FIXED** in `a39d76b6`. |
| R5 | minor | **PARTLY: gap recorded, decision still open** in `08ca9218`. |

### R1 — `research/module_tests.md` has no section for E5.5–E5.6 (major)
The finding is right: the file needs a section for the new suites. But the fix edits `research/module_tests.md`, and the brief lists `research/` as out of scope.
I did not edit `research/`. The operator must either lift that limit for this file, or let a later session add the section the review describes
("The Kimi consultant check and question row — E5.5–E5.6", after the E5.4 section: each suite, its scenario, what it holds, the red it was seen in — plan
deviations 50–57 — and what it does NOT cover: the live check and question rows (J2), M-K17, the web row, and that the shim is played).
This is the one blocking-class item left open.

### R2 — generated TS table is regenerated one commit late (minor)
Not fixed. The finding is right about commit `62ee29de`, but the fix is a squash of `c0e26084` into it. That rewrites history with a rebase, and the
hashes are cited in `CR3-code.md`. The branch tip is consistent: seed and generated table are equal (`--check` is clean), and R3's fix keeps both
in the same commit. Cost and risk are not worth it for a branch that is not pushed. Do the squash only if the operator asks.

### R3 — kimi × web note without the version (minor)
Fixed. The note now reads `kimi 2.1.1: not measured — WebSearch and FetchURL are announced and no Kimi cell fetches or searches the web`.
`generate-runtime-capabilities.mjs` was run; `--check` is clean; seed and generated file are in one commit.

### R4 — the AGENTS.md fixture is dead (minor)
Checked against the code: the finding is right. `SnapshotOptions.Default` and `KimiAcpProfile.Exclusions` leave out `AGENTS.md`, but no assertion used the fixture.
Fixed in `AKimiQuestionRowTests`: while the shim runs, the test lists the files of the copy it stands in (`_copyFiles`), and after the row ends it asserts
that `Mailer.cs` is there and `AGENTS.md` is not (all three arms; the refusal arm has 0 launches, so nothing is listed).
Red seen: I removed `"AGENTS.md"` from `SnapshotOptions.Default`; the "answered" and "cancelled mid-turn" arms failed with
*Expected files {"Mailer.cs", "AGENTS.md"} to not contain "AGENTS.md" because the root's own AGENTS.md would load from disk…*. I reverted the break; `git status` showed no change in `src_mcp/runners`.
Note: the review said "src/Mailer.cs"; the copy's root is `src` itself, so the file is `Mailer.cs`. The test checks the file name at any depth.

### R5 — model for E5.5–E5.6 (minor)
The finding is right: §0.2 names Opus, the stories were built on Sonnet, and no operator decision is recorded. I cannot invent an operator decision.
I added deviation 59 to the plan: it states the gap and says the operator must confirm the model or ask for a rebuild. The decision stays open.
These fixes were made on Sonnet too, as the brief says.

## Results

- Build (`-m:4 --disable-build-servers`, through `low-priority.mjs`): succeeded, 0 warnings, 0 errors.
- Changed classes (`AKimiQuestionRowTests`, `CapabilityMatrixTests`, `ConfinementPlannerTests`): 77 of 77 pass.
- Whole MCP suite: total 10556, succeeded 10451, skipped 84, **failed 21**.
  - 2 are the known host failures (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`).
  - 18 are the rounds/git numstat tests that `CR3-code.md` showed to fail at the baseline `b4e2ea96` too (`ACodeRoundRecordsItsSizeTests` 7, `ARoundIsMintedAKeyTests` 2, `ARoundsNumberComesFromTheJournalTests` 3, `EndToEndTests` 3, `SplitOrderTests` 1, `ThePersonsCommandsReachTheCallerTests` 1, `TheRoundsNumstatsAreCountedTests` 1).
  - 1 is new in this run: `QuestionRowOnAgyScenarioTests.AFollowUpCutShortByTheCaller_StillBillsTheFirstLaunch_AndNeverAsOk`. It is a timing test on the Agy row, a file this work does not touch. I ran the extension tests at the same time as the suite, so the host was loaded. The class alone: 5 of 5 pass. I take it as a flake from load, not as a defect of the fixes.
- Format: the whole-solution check exits 2 on this host from IL2026/IL3050 errors in `src_server` and `src_bugs` (as before). Scoped with `--include` to the changed C# file: no findings.
- Generator: `generate-runtime-capabilities.mjs --check` is clean (21 rows, 7 runtimes × 3 capabilities).
- TS tests (`npm test` in `src_vs_code`): 6290 tests, 6270 pass, 1 fail = the known `the baseline names no release that was never tagged` (no tags in this clone).
- `plan-lifecycle.mjs`: clean.

## What remains

- R1 (the section in `research/module_tests.md`): needs the operator to lift the `research/` limit.
- R5: the operator's decision on the model for E5.5–E5.6.
- R2: only if the operator wants the history squashed.
