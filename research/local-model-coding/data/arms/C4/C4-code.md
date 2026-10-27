# C4 — coder notes, E5.5 and E5.6 (Sonnet), branch `exp/kimi-e56-classic-4`

Written 2026-10-26. Commits on the branch, after `b4e2ea96`:

1. `test(kimi): the consultant check for Kimi — S5.5.1-S5.5.4 (E5.5)` — `src_mcp/tests/AKimiConsultantCheckTests.cs`
2. `feat(kimi): a Kimi question-consultant row …(E5.6)` — seed rows, vectors, generated TS, planner arm, `KimiConsultant.MustCarryNoFlags`, all E5.6 tests, TS test
3. `docs(plan): E5 status — E5.5 and E5.6 built, deviations 50-57` — the plan's status line and its "E5.5–E5.6 as built" list (the deviations are there too)

## Decisions (made alone, nobody to ask)

- **E5.5 needed no production change.** E5.4 (`KimiConsultant` declares `NeedsASanitisedSnapshot` and `HomesFolder`) and E5.3 decision g (junction-first
  disposal) were already in. So all four E5.5 scenarios were green on arrival; I gave them teeth by temporary breaks (list below). The class is
  `AKimiConsultantCheckTests`; it uses `PlayedKimi`, `KimiFixture`, `WindowsStandIn` and `KimiSeams.Using` (the E5.4 service arms' way), so it runs on Linux too.
- **S5.5.2 is a Theory** (check passed; turn ends exit 70 through `PlayedKimi.Lost`). S5.5.1 asserts the cwd is *under* `<scratch>\own\tree\` because the copy is
  `tree\snapshot-<id>`. "The record says cancelled" in S5.5.3 is `failureKind: cancelled`.
- **Standings, rows, cells** as the plan's decision 2: none and disk `unconfined`, web `unmeasured`; version 2.1.1; date 2026-10-13. `resultRef` cites §9 (M-K6),
  §6 (M-K3), §7 (M-K4), §12/§21 (M-K9); disk cells `M-K6, M-K3, M-K4, M-K9`; none cells `M-K6, M-K3, M-K4`; web no cell, a note. Section numbers read from the record's
  own headings. The `note` of none/disk says WebSearch/FetchURL are announced and no cell exercised them (until M-K17).
- **Planner arm** `kimi => Kimi(grant, flag)`: disk → `Snapshot` + `SnapshotSources = grant.Roots`, none → `Scratch`, no flags, no leading, no add-dirs.
- **S5.6.6**: `KimiConsultant.MustCarryNoFlags` refuses a plan with any `Flags` OR `Leading` (decision 3 says "any flag"); the message names the shim.
- **Tests placed:** S5.6.5/S5.6.6 in `KimiConsultantTests` (they need `KimiFixture`'s places), S5.6.1/S5.6.4 in `CapabilityMatrixTests`, S5.6.3 in
  `ConfinementPlannerTests`, S5.6.7 in new `AKimiQuestionRowTests`, S5.6.8 in `capabilityAdmission.test.ts`.
- **An existing test had to change:** `CapabilityMatrixTests.Records` (the list of records a row may cite) gained the Kimi record; without it
  `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` failed for every kimi row. That list is a copy of a list the code holds (testing.md says so), but I did not
  derive it — it carries a date and a cell prefix per record, which only the file's rows can give; left as a table.
- **`capabilityAdmission.test.ts`**: the runtimes list gained `kimi`; one new test with S5.6.8's name. The refusal-sentence regex test still passes (it matches a
  prefix of the list).

## Reds seen, and how they differ from the tables' last column

| scenario | red seen | differs from the table? |
|---|---|---|
| S5.5.1–S5.5.4 | green on arrival (E5.4 and E5.3 g in); teeth below | the table's reds are from before E5.4/E5.3 g — not reproducible |
| S5.6.1 | *Expected none not to be <null> because the table holds a kimi × none row.* | same |
| S5.6.2 | `TheVectorsActuallyLoaded…`: *Expected Vectors … to contain … kimi × none row must have a vector deciding it*; plus `EveryRow_CitesTheRecord…` (see decisions) | same, plus the second red |
| S5.6.3 | *Expected type to be Confinement+Planned, but found Confinement+Refused.* | yes — the planner asks the matrix first, so `kimi` is refused before the default arm the table names |
| S5.6.4 | *Expected type to be Admission+Admitted, but found Admission+Refused.* | same in substance |
| S5.6.5 | *Expected type to be Confinement+Planned, but found Confinement+Refused.* (the test builds its plan through the planner) | yes — table says "against a stub ignoring the plan"; I added a third teeth break (planned cwd ignored → *Expected none.WorkingDirectory to be … differ at index 35*) |
| S5.6.6 | *Expected a <System.ArgumentException> to be thrown, but no exception was thrown.* | same |
| S5.6.7 | row refused ("not a runtime the question consultant can launch") → *Expected row … status to be "answered"* ; cancelled arm *Expected a <OperationCanceledException> to be thrown* | yes — the table's red is the disposal; reproduced by breaks: one-shot disposal skipped → *recorder … found …\coai-vh\<8>\kh-…\home\credentials*; scratch removal dropped → *Expected Directory.Exists(<scratch>) to be False … found True* |
| S5.6.8 | run against the stale generated table: deep-equal of the table (18 rows vs 21) and the four kimi vectors | yes — not the runtimes-list message first |

### Teeth runs (temporary breaks, each reverted with `git checkout`, tree clean after)

- A `VibeHomes.EmptiedUnderItsLock` skips `KimiHome.DisposeAllUnder` → S5.5.2 (both arms), S5.5.3, S5.6.7 (answered, cancelled) red on the recorder.
- B `ConsultCheckScratch.Delete` skips `KimiHomesDisposed` → S5.5.4 red on the recorder.
- C `ConsultantCheck` finally does not dispose the one-shot home → S5.5.2, S5.5.3 red (`coai-vh\<8>` still there).
- D `KimiConsultant.NeedsASanitisedSnapshot = false` → all five `AKimiConsultantCheckTests` red.
- E `QuestionRowLaunch` finally does not `Remove(scratch)` → S5.6.7 answered and cancelled red.
- F `KimiConsultant.Cwd` ignores the plan → S5.6.5 red.
  (The first attempt to script these through a helper `.sh` was refused by the sandbox; I did them by hand with Edit + `git checkout`. One of them, F, was left
  on disk for one command by a refused compound command; it was reverted at once and the tree rebuilt before the suite.)

## Results

- MCP: the five new/changed classes were run per class: `AKimiConsultantCheckTests` 5/5, `CapabilityMatrixTests`, `ConfinementPlannerTests`, `KimiConsultantTests`,
  `AKimiQuestionRowTests` — 88/88 together. Whole MCP suite (7m 27s, Debug): 10555 total, 10469 pass, 84 skipped, 2 fail — exactly the two known failures
  (`KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`), nothing new.
- Generator: `node scripts/generate-runtime-capabilities.mjs` written, `--check` up to date (21 rows over 7 runtimes × 3 capabilities).
- Extension `npm test` (compile + all): 6290 tests, 6270 pass, 19 skipped, 1 fail — the known `the baseline names no release that was never tagged` (no git tags in this clone).
- `plan-lifecycle.mjs`: clean.
- `dotnet format … --verify-no-changes --no-restore` over the whole slnx prints a flood of `IL2026`/`IL3050` AOT-analyzer errors from `src_server/src/*.cs` (files I did not touch)
  and exits non-zero; I did not run a baseline. Over my own changed C# files (`--include`) it prints nothing — no formatting finding.

## What remains

- Review of E5.5/E5.6 (Opus) — the plan says so; deviations 50–57 are the list for the reviewer.
- M-K17 (web tools) — decides whether the "announced, no cell exercised them" sentence in the none/disk notes stays.
- The live checks of J2 (one three-turn consult, one live row per capability, one live `--check-consultant`) → RESULTS §31. Not run, by the brief.
- `research/module_tests.md` must name the new flows (E5.5 check, E5.6 question row) — out of scope here (`research/module_*.md`).
- The `unconfined` caveat ("a shell is always there, and no flag removes it") is false for Kimi (the shim refuses every shell). Shared with codex; owner to decide a split.
- The `none`/`disk` admission for Kimi rests on Windows-only measurements; D18 still refuses Linux/WSL/macOS at launch, and the tests here run through the Windows stand-in.
