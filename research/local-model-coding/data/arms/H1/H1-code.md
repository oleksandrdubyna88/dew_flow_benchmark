# H1 — coder notes (Sonnet, skeleton step of E5.5 and E5.6)

Branch `exp/kimi-e56-skeleton-haiku-1`, from `b4e2ea96`. Finished before the 12:45 UTC deadline. Nothing pushed.

## What is in

- Data, written in full: three `kimi` rows in `shared/runtime-capabilities.json` (`none` and `disk` `unconfined`, `web` `unmeasured`; kimi 2.1.1; date 2026-10-13; cells
  from the plan), the `runtimes` list and the `why` text; three vectors in `shared/capability-matrix-vectors.json`; `src_vs_code/src/runtimeCapabilities.generated.ts`
  from its generator (`--check` is clean, 21 rows over 7 runtimes); `RuntimeCapabilities.Runtimes` gained `kimi`.
- Production: `"kimi" => Kimi(grant, flag)` in `ConfinementPlanner.Fragments` (written); `Kimi(...)` is a fill region `s56-planner-kimi`.
  `KimiConsultant.MustCarryNoFlag(Confinement.Planned)`, a fill region `s56-kimi-noflags`, called from `Build` for a planned launch only.
- Tests, with the exact scenario names: `CapabilityMatrixTests` (S5.6.1 fill; S5.6.2 and S5.6.4 written), `ConfinementPlannerTests` (S5.6.3 and S5.6.5 fill; S5.6.6 written),
  new `AKimiConsultantCheckTests` (S5.5.1–S5.5.4, plus two helper regions), new `AKimiQuestionRowTests` (S5.6.7 and three helper regions), and the TS test S5.6.8 in
  `capabilityAdmission.test.ts` (fill; the runtimes-list assertion at the old `:143` gained `kimi`).
- 16 fill regions, 16 context packs in `/home/jinx/git/coai-exp/vllm/work/H1/regions/<id>.md`:
  `s56-planner-kimi`, `s56-kimi-noflags`, `s563-planner-arm`, `s565-kimi-launch`, `s561-kimi-rows`, `s568-kimi-admit` (group A, one shared pack text);
  `s55-check-helper`, `s55-answer`, `s551-copy-oneshot`, `s552-home-gone`, `s553-cancelled`, `s554-rootless` (group B);
  `s567-repo-helper`, `s567-service`, `s567-standin`, `s567-disk-row` (group C). Each pack holds the whole group's signatures, so any region of the group can use it.
- The plan: status line and deviations 50–55 (section *E5.5–E5.6 skeleton*). `plan-lifecycle.mjs` is clean.

## Build and checks

- `dotnet build dew_flow_connect_other_ais.slnx -c Debug` — 0 errors, 0 warnings (twice, last after the last code edit).
- `generate-runtime-capabilities.mjs --check` — up to date. `npm run typecheck` — clean.
- I ran four classes of C# tests and the extension suite once, to see what the skeleton does to existing tests (the brief said not to run the tests for the scenarios; these are the old ones):
  - E5.4's `KimiConsultantTests` and `AKimiConsultationScenarioTests`, `QuestionConsultServiceTests`: green.
  - `ConfinementPlannerTests`: five red, all from my placeholders: the three scenarios of mine, and `DisallowedTools_AppearsInNoPlan` and `NoAdapter_SendsASandboxFlag_ThePlanDidNotGiveIt`,
    which loop over every runtime, so `kimi` now reaches `Kimi(...)` and throws `NotImplementedException: s56-planner-kimi`. They go green when `s56-planner-kimi` is filled.
  - Extension `npm test`: 6290 tests, 2 fail — my S5.6.8 placeholder, and the known `the baseline names no release that was never tagged`.
- `dotnet format --verify-no-changes --no-restore` fails on this host with IL2026 and IL3050 in `src_server/src/*Endpoints.cs` (trim and AOT analysers). Those files are not mine. I did not run it on my own
  files only; this must be re-run later on the finished code.

## Decisions

1. Everything that is data or a signature is written; every body over three lines is a fill region — including the Kimi planner arm. So S5.6.1, S5.6.2 and S5.6.4 are green on data alone.
2. `MustCarryNoFlag` takes the plan, not the launch, and is called only for a planned launch. First I had it take the launch for every call: while it was a placeholder it broke every E5.4 conversation launch. Deviation 52.
3. The check and question-row tests run on every OS through `WindowsStandIn`, `KimiSeams.Using` and `KimiCredentialsLock.RootFor`, as `AKimiConsultationScenarioTests` does (E5.4 deviation 23). No Windows-only skip.
4. `AKimiConsultantCheckTests` is not in the `fakecli-env` collection: it uses `PlayedKimi`, not the fake CLI, and sets no environment variable. It sets `GitScratch.AfterUnlink` (a static) in `RecordingTheDeleter` and clears it in `Dispose`, as `AOneShotKimiHomeIsDisposedJunctionFirstTests` does; neither class is in a collection.
5. Two Theory parameters are read by a `_ = parameter;` line before the fill region, because xUnit1026 is a build error while the body is a placeholder (deviation 55).
6. S5.6.2: the existing cover test (`TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`) needed no change; but `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` pins the list of records a row may cite, so
   I added `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")` to its `Records`. Without it every kimi row is red there. This is a data line, not in the plan's list; add it to the plan's deviations if reviewed.
7. The rows' `resultRef` cites section numbers I took from the plan text (§17 M-K6, §7, §14), not from reading `RESULTS_kimi_confinement.md` (out of scope). The cell ids are the plan's. The reviewer should check the section numbers
   against the record: the plan itself says M-K6 is "RESULTS §17" in E5.4 decision 7 for Grep/Glob, and §5, §8, §11, §12, §19, §23, §25–§28 elsewhere; the M-K3/M-K4 section I wrote without a number.

## Differences from the plan's tables

- No red messages were watched (this step does not make tests pass or fail on purpose). Predicted differences to watch for:
  - S5.6.1, S5.6.2 and S5.6.4 are green already (the data is complete), so their "expected red today" messages cannot be produced by a later step unless the rows are removed.
  - S5.6.6 reds first with `NotImplementedException : s56-kimi-noflags` (placeholder), then with the table's "no exception was thrown" once the body is stubbed to a no-op.
  - S5.6.3's table red ("found CwdKind.Scratch") needs the default arm; with the placeholder it reads `NotImplementedException : s56-planner-kimi`.
- A caveat sentence mismatch, not mine to fix: the TS and C# caveat for `unconfined` says "a shell is always there, and no flag removes it". For Kimi a shell is refused (M-K3/M-K4); the true reason is `Grep` and `Glob`.
  The row's `note` says so, but the sentence the person sees under the flag does not. A later step may decide whether to reword it (it is shared with codex).

## Risks for the filling step

- `s553-cancelled`: the check's reaction to a cancelled token (a record, or an `OperationCanceledException`) is not known from the code I read; the region lists both outcomes.
- `s567-disk-row`: the status word of a refused row (`refused` or `failed`) is not known; the region says to read it off the first run.
- `s565-kimi-launch`: `KimiEnvironment.Refusal` needs real `KimiPlaces`; the pack gives the `KimiFixture.PlacesAt` route that `KimiConsultantTests` uses (the file's `KimiForQuestions()` helper uses `KimiPlaces.ForShim`).
- `AKimiConsultantCheckTests` has a using for `CoaiMcp.Runners.Consultation` and `CoaiMcp.Runners.Files`, and `AKimiQuestionRowTests` for several namespaces; some may stay unused until the bodies are filled (the build does not flag them).

## What remains (not done)

- All 16 regions (fill), then red/green checks, the whole MCP suite (~7 min), `dotnet format` on the finished code, the extension suite.
- Not touched, by the brief: `research/module_*.md`, `research/RESULTS_*.md`, J2 live checks, M-K17, merging main.

---

# POLISH step (Sonnet) — E5.5 and E5.6

Started 12:35 UTC, deadline 13:15 UTC. Nothing pushed. The fill reports: both builds were red with 2 compile errors (`VibeHomes`, `Scopes`) — the test fill was the cause.

## Per region (kept / fixed / rewritten / written)

| Region | Verdict |
|---|---|
| `s56-planner-kimi` (`ConfinementPlanner.Kimi`) | **kept** the logic; the shape changed to an expression body like its siblings (`Vibe`, `Codex`). Markers removed. |
| `s56-kimi-noflags` (`KimiConsultant.MustCarryNoFlag`) | **kept as-is** (checks `Leading`, `Flags`, `AddDirs`; message names the shim). Marker removed. |
| `s55-check-helper` (`CheckAsync`) | **kept as-is**. |
| `s55-answer` (`AnswersFromTheCopy`) | **kept as-is** (the twin's `AnswerFor` shape). |
| `s551-copy-oneshot` | **fixed**: `VibeHomes` through a `using` (it did not compile); added the assertion on the canary reading (`not-attempted`), which the table asks for ("canary unchanged") and the fill left out. |
| `s552-home-gone` | **fixed**: the `using` only. Logic kept. |
| `s553-cancelled` | **fixed**: the `using` only. Logic kept. |
| `s554-rootless` | **kept as-is**. |
| `s567-repo-helper` | **kept as-is** (copy of the Vibe twin's helper). |
| `s567-service` | **kept as-is**. |
| `s567-standin` | **fixed**: `Scopes` was a private nested class of `AKimiConsultationScenarioTests`. Lifted to the internal `src_mcp/tests/Scopes.cs`; nothing else changed. |
| `s567-disk-row` | **fixed in place**: (a) the `Answered` arm looked for the copies under the home's folder (`<coai-vh>\<8>\tree`) — wrong, the copies stand under the row's scratch, so it now asserts the prompt contains the shim request's `WorkingDirectory`, never the root, and that the row's scratch is gone too; (b) the `Refused` arm had an instruction comment ("First run: read the real refusal word…") and accepted `Refused` or `Failed` — now `Refused` and the reason "not inside a Git work tree" (the twin's words); (c) the cancel lambda in a ternary (formatter error at `:149`) became a helper `Cancelling(caller)`; (d) `new[] { plain }` became a collection expression. |
| `s561-kimi-rows` | **kept as-is**. |
| `s563-planner-arm` | **kept as-is**. |
| `s565-kimi-launch` | **kept as-is**. |
| `s568-kimi-admit` (TS) | **kept as-is**. |
| the `_ = parameter;` lines (deviation 55) | **removed**, with their comments — the parameters are read now. |

No region was left unfilled; none was rewritten as a whole.

## Red first

Production files restored to `b4e2ea96` in the working tree for each run, then put back (`git checkout HEAD -- …`).

- **Data red** (all prod + data at `b4e2ea96`): S5.6.1 *Expected row not to be <null> because kimi × None is a row of the table* (as the table). S5.6.2 *Expected Vectors … to contain …* (the cover test's own sentence). S5.6.4 *Expected type to be Admission+Admitted, but found Admission+Refused* (as the table). S5.6.3/5/6 fail earlier, for the matrix's refusal — not the table's reason, so I repeated them with the data back:
- **Code red** (data new; `ConfinementPlanner.cs` and `KimiConsultant.cs` at `b4e2ea96`): S5.6.3 *Expected disk.Cwd to be CwdKind.Snapshot, but found CwdKind.Scratch* (as the table). S5.6.5 *Expected disk.Request.WorkingDirectory to be … they differ* (`coai-scratch-…` vs `coai-question-…`; the table's message is for the `none` arm, which passes). S5.6.6 *Expected a <System.ArgumentException> to be thrown, but no exception was thrown* (as the table). S5.6.7 `Answered`: *Expected launch.Request.StdIn … to contain …/tree* — the prompt named no copy; `Refused` arm red at first on the wrong test line (accepting a word) — see below.
- **S5.5.1–S5.5.4 were green on arrival** (E5.3 g and E5.4 are in the code they run on; the table's red is "before E5.4"). Teeth by two mutations, both reverted: (1) `ConsultCheckScratch.Delete` without `KimiHomesDisposed` → S5.5.4 red: *Expected _unlinkedByTheDeleter to be empty because the deleter's unlink recorder must not be invoked … found …/own/kh-…/home/credentials*. (2) `VibeHomes.EmptiedUnderItsLock` without its `DisposeAllUnder` → S5.5.2 (both arms), S5.5.3 and S5.6.7 `Answered`/`CancelledMidTurn` red with the same recorder message (the table's red). The `RefusedBeforeTheLaunch` arm of S5.6.7 launches nothing: green with the mutation, by design.
- **S5.6.8 (TS)**: with `runtimeCapabilities.generated.ts` from `b4e2ea96`: red on the generated table (*one row per runtime × capability*, the generated file "behind the seed") and the kimi vectors — not on the runtimes list as the table says.

## Results

(filled at the end)
- Whole MCP suite (after the last prod change, 12:46 start): 10557 tests, 10470 passed, 84 skipped, 3 failed: the two known (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_…`) and `SecuritySourcesCharacterizationTests.The_collection_deadline_ends_the_file_it_interrupts_and_every_later_one` (30 s deadline test; ran while `npm test` and `dotnet format` used the CPU; green alone, 7 of 7). After the run I changed two test files only (the canary assertion in S5.5.1, the `Cancelling` helper in S5.6.7) and re-ran `AKimiConsultantCheckTests` (5/5), `AKimiQuestionRowTests` (3/3). Full log: `H1-full-suite.txt`.
- `dotnet format --verify-no-changes --no-restore`: no whitespace error in any file of this change. It still exits 2 because of 54 IL2026/IL3050 lines in `src_server` (present with my changes stashed too, so not mine). One WHITESPACE error of mine (`AKimiQuestionRowTests.cs:149`) was fixed.
- Generator: written, then `--check` clean (21 rows over 7 runtimes); no diff against the skeleton's file.
- TS `npm test`: 6290 tests, 6270 pass, 1 fail = the known tag test (`the baseline names no release that was never tagged`). `capabilityAdmission` green, S5.6.8 included. Log: `H1-ts.txt`.
- `plan-lifecycle.mjs`: clean.
- Commits: `3a7d55d4` (polish) and the one after it (canary assertion, helper, plan).

## What remains
Nothing in scope. Not done by design: the live checks (J2), M-K17, the review. Deviations 56–60 are in the plan.
