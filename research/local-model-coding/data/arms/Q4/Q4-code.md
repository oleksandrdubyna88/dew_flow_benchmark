# Q4 — polish of E5.5 and E5.6 (CODER, Sonnet)

Branch `exp/kimi-e56-skeleton-4`. Started 11:12 UTC. Deadline 11:52 UTC.

## What I found at the start

- HEAD `011e28ee` did not build: 6 compile errors, all in test fills (`cwd` declared twice and an unused local function in the S5.5 played
  shim, `ConsultCheckStates.NotAttempted` (the word is `CanaryReadings.NotAttempted`), `KimiPlaces` and `It.IsAny` in S5.6.5).
- The two production regions were right as filled.
- The instructions loaded: resolver `check` = resolved; `explain --task implement --task test` for the changed files. I read
  `csharp.doctrine`, `common.scenario-tests` and `common.test-leftovers` in full; the others by their role (testing, local-cpu-budget:
  every command is run lowered, every test folder is `testsToDelete-…`).

## Per region

| Region | File | Verdict |
|---|---|---|
| s56-planner | ConfinementPlanner.cs | kept as-is |
| s56-noflags | KimiConsultant.cs | kept as-is |
| s555-settings | AKimiConsultantCheckTests | kept as-is |
| s555-check | AKimiConsultantCheckTests | kept as-is |
| s555-played | AKimiConsultantCheckTests | rewritten: the fill was a pile of dead code (two `cwd`, a never-used local function, a placeholder call); now one lambda that reads `--cwd` from the request, reads `CHECK.md` there, answers the marker |
| s551-copy | AKimiConsultantCheckTests | fixed: `ConsultCheckStates.NotAttempted` → `CanaryReadings.NotAttempted` |
| s552-gone | AKimiConsultantCheckTests | fixed: `(_, request)` → `(_, _)` (unused) |
| s553-cancel | AKimiConsultantCheckTests | fixed: a cancelled check's record is `failed` with `failureKind` `cancelled`, not `unavailable`; asserted that |
| s554-rootless | AKimiConsultantCheckTests | kept as-is |
| s567-init | AKimiQuestionRowTests | rewritten: the fill wrote `src\Mailer.cs` with a backslash (on Linux that is ONE file name) and built the git steps by hand; now two folders and the same five git commands as the Vibe twin |
| s567-service | AKimiQuestionRowTests | kept as-is |
| s567-row | AKimiQuestionRowTests | rewritten: it asserted the prompt contains `Repo/src` and NOT `Repo/src` (the same string); the "refused" arm deleted the sign-in (a launch refusal is `failed`, not "refused before the launch"); the "cancelled" arm expected a reply, but a cancelled `ask_consultants` throws `OperationCanceledException` (as the Vibe twin's test says); it never looked at the home folder, only at `Directory.Exists(coai-vh)` (the folder stays, empty). Now: answered → prompt names the copy (`--cwd`) and never the root, copy gone, no `--session`; refused → a granted root outside any Git work tree, nothing launched; cancelled → throws, the turn had started; all three → `coai-vh` empty, recorder never invoked, person's folder equal |
| s561-rows | CapabilityMatrixTests | fixed: `Of(...)!` threw `NullReferenceException` before the table's message; a helper `KimiRow` asserts "not null" first |
| s564-admit | CapabilityMatrixTests | kept as-is |
| s563-planner | ConfinementPlannerTests | kept as-is |
| s565-launch | ConfinementPlannerTests | rewritten: `KimiPlaces.ForShim` was not in scope and `It.IsAny` is not a thing here; one loop over both requests, `--home` read with `KimiEnvironment.FlagOf`, the refusal checked against the places of THAT home |
| s566-flags | ConfinementPlannerTests | kept as-is |
| s568-ts | capabilityAdmission.test.ts | fixed: the loop over `none`/`disk`/`web` expected `web` admitted; `web` is only checked as refused |

Not in the regions: `CapabilityMatrixTests.Records` (the existing test `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` lists the records a row may
cite, with the cell prefix) did not know `RESULTS_kimi_confinement.md` — added `("RESULTS_kimi_confinement.md", "2026-10-16", "M-K")`. Without it
the skeleton's data turned one existing test red. That is a test of E5.6's own data, so it is in scope.

All `QWEN-FILL` markers and instruction comments are removed.

## Red first

Two stages, because the table's reasons assume the rows exist (they do, from the skeleton) and the arm does not:

1. Production + data at `b4e2ea96` (no rows, no arm): the table's data-reds, e.g. S5.6.4 `Expected type to be Admission+Admitted, but found Admission+Refused`,
   S5.6.7 rows `'kimi' is not a runtime the question consultant can launch … status blocked`. S5.6.1 was `NullReferenceException` here (fixed after, see above).
2. Data at HEAD, `ConfinementPlanner.cs` and `KimiConsultant.cs` at `b4e2ea96` (the arm and the guard missing): the table's reasons.
   - S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch` — as the table says.
   - S5.6.5: `Expected disk.Request.WorkingDirectory to be … "/tmp/coai-question-…" but … "/tmp/coai-scratch-…"` — the table's reason (the plan ignored).
   - S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` — as the table says.
   - S5.6.7 answered: the prompt does not name the copy (it names the root); refused: with no arm the non-git root is ANSWERED with the note
     "not a git checkout: changes there are not watched" — the arm is what makes it a copy and so a refusal.
3. S5.6.8 (TS) against the base generated table: `kimi rows admit flagged, web refuses naming its record` ✖, with the generated-file checks.
4. S5.5.1–S5.5.4 and the cancelled arm of S5.6.7 are GREEN at the base (E5.3's disposal and E5.4's consultant already exist), as the plan says of S5.5.3.
   Teeth runs, each reverted: `held.Home.Dispose()` removed in `ConsultantCheck.TurnAsync`, `Release` emptied in `QuestionRowLaunch`, and
   `ConsultCheckScratch.Delete` calling the plain deleter without `KimiHomesDisposed`:
   - S5.5.2, S5.5.3 and the three arms of S5.6.7: `Expected Directory.EnumerateFileSystemEntries(…coai-vh) to be empty … found …coai-vh/<8>`;
   - S5.5.4: `Expected _unlinkedByTheDeleter to be empty … found …/own/kh-…/home/credentials` (the deleter met the junction).
   - S5.5.1: teeth with `KimiConsultant.NeedsASanitisedSnapshot => false` (reverted): red — the check fails (`failed`, the consultant refuses
     a launch with no snapshot), so the test says `Expected … state to be "answered"`, not the `--cwd` sentence. The `--cwd` line has no teeth of its own.
   - S5.6.1's new "not null" message was not observed red after I changed it (the first red was the `NullReferenceException`).

## Deviations from the plan (appended to the plan, numbers 52 on)

See the plan, "E5.5–E5.6 polish".

## Results

Commits: `8e069292` (tests and the two prod files, markers out), `6e8c0593` (S5.6.1), `ba2c9466` (plan). Nothing pushed. Working tree clean.

- **The 12 scenarios:** all green on HEAD (80 tests in the four classes pass; TS `capabilityAdmission.test.js` 31 pass).
  Red seen: S5.6.1 (NRE at base — the "not null" message after my change was not watched), S5.6.2 (the existing test; its own message was not
  watched, the data were never removed alone), S5.6.3, S5.6.4, S5.6.5, S5.6.6, S5.6.7, S5.6.8 — for the table's reasons or close to them (above);
  S5.5.1–S5.5.4 only by teeth (they are green at the base). S5.6.2 is covered by the existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`,
  which passes on HEAD (the class: 39 pass).
- **Whole MCP suite** (once, 7m38s): 10554 total, 10467 pass, 84 skipped, **3 failed**:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — known at the start.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known at the start.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — **NOT in the brief's list.** It fails alone too (a 1 ms deadline on `git`:
    `AllChangesAsync(…, oneMillisecond)` was not `Failed`). It reads no file this diff changes (the numstat assembler); I did not run it at `b4e2ea96`
    (a full second build), so "not caused by this diff" is by reading, not by a run. Looks like a timing test that a fast host answers inside 1 ms.
- **Format:** `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore` exits 2 on the whole solution: IL2026/IL3050 analyzer errors in
  `src_bugs` and `src_server` (AOT warnings made errors when the format run loads those projects) — files this diff does not touch. Scoped to the six
  `.cs` files this story changed (`--include …`), it prints no diff.
- **Generator:** `generate-runtime-capabilities.mjs --check` — up to date (21 rows, 7 runtimes × 3). I did not need to write it: the skeleton's file is current.
- **TS tests** (`npm test`, lowered): 6290 tests, 6270 pass, 1 fail = the known `the baseline names no release that was never tagged`. `kimi rows admit flagged, web refuses naming its record` ✔.
- **Plan:** status line and deviations 52–56 written; `plan-lifecycle` clean.

## What remains

- Review (Opus), the live checks J2 / RESULTS §31, M-K17 — out of scope.
- The third failing test above, to be run at `b4e2ea96` by whoever owns it.
- The whole-solution `dotnet format` AOT noise (above) is not mine.
- `npm run typecheck` and `npm run lint` (the plan lists them for E5.6): both clean.
