# CH1 — coder notes: E5.5 and E5.6 (Sonnet, branch `exp/kimi-e56-classic-haiku-review-haiku-fix-1`)

Base `b4e2ea96`. Host: Linux (WSL2). Commits:
- `17b7bba0` feat(kimi): the question consultant's Kimi row (E5.6: S5.6.1–S5.6.8, seed, vectors, generated TS, planner arm, `KimiConsultant` refuses a flagged plan)
- `2d849b5d` test(kimi): the consultant check on a Kimi row (E5.5: S5.5.1–S5.5.4)
- the plan commit (status line and deviations 50–59), see `git log`

## Decisions
- E5.5 needs no product code (the check already gives a copy and a one-shot home; E5.3 decision g disposes it). Only tests.
- E5.6 product code: `"kimi" => Kimi(...)` in `ConfinementPlanner.Fragments` (no flags; `disk` = Snapshot + `SnapshotSources`, `none` = Scratch);
  `kimi` in `RuntimeCapabilities.Runtimes`; three seed rows (none/disk `unconfined`, web `unmeasured`); three vectors;
  `KimiConsultant.MustCarryNoFlags` (refuses `Leading` or `Flags`, message "a Kimi row's confinement is the shim's; …").
- Service / check / row scenarios run on Linux through `WindowsStandIn` + `KimiSeams.Using` (as E5.4's do).
- The `Records` list in `CapabilityMatrixTests` gained `RESULTS_kimi_confinement.md` (date 2026-10-13, prefix `M-K`).

## Red seen, per scenario (message vs the table)
| # | red seen | message |
|---|---|---|
| S5.5.1 | yes (break 3) | green on arrival; with `NeedsASanitisedSnapshot = false`: `Expected Text(said, "state") to be "answered" … "failed"` / `failureKind: record-failed` (table predicted a pre-E5.4 message; not applicable now) |
| S5.5.2 | yes (break 1) | recorder red: `Expected _unlinkedByTheDeleter to be empty … found …/coai-vh/<8>/kh-…/home/credentials` (the table's wording, other variable name) |
| S5.5.3 | yes (break 1) | same recorder red; the table says "green, pins finally" |
| S5.5.4 | yes (break 2) | same recorder red under `<scratch>/own/kh-…` |
| S5.6.1 | yes | `Expected none not to be <null>.` (as table) |
| S5.6.2 | yes | the existing test, with seed rows and no vectors: its own sentence naming the missing kimi vectors |
| S5.6.3 | yes, twice | first `Expected type … Planned … found … Refused` (matrix refuses the unknown runtime first); then, with the rows in, `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch` (as table) |
| S5.6.4 | yes | `Expected type … Admitted … found … Refused` (table quotes the refusal sentence; same fact) |
| S5.6.5 | yes | first `…Planned … Refused`; the later red was my own test bug (argv order), fixed. Against the real code it is green. |
| S5.6.6 | yes | `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` (as table) |
| S5.6.7 | yes (break 1) | answered and cancelled arms: recorder red; refused arm has no home made → green by nature |
| S5.6.8 | yes | AssertionError on the runtimes list (deepStrictEqual) and `'kimi' is not a runtime the question consultant can launch` (as table) |

Teeth: break 1 = remove `KimiHome.DisposeAllUnder` from `VibeHomes.EmptiedUnderItsLock`; break 2 = remove it from
`ConsultCheckScratch.KimiHomesDisposed`. Both reverted with `git checkout` (verified by `git status`).
Break 3 = `KimiConsultant.NeedsASanitisedSnapshot => false` (S5.5.1 red, all five check tests red). All breaks reverted; `git status` clean.

## Deviations
See the plan, `todo/PLAN_vibe_and_kimi_runtimes_E5.md`, deviations 50–59. Short: S5.5.x green on arrival; S5.6.3/5 first red is a Refused (matrix first);
S5.6.7 through the service and the refused arm does not assert the scratch; one seed date; `dotnet format` prints 54 pre-existing IL errors.

## Results
- Whole MCP suite (4 runs, 10555 tests, ~6 min): always the 2 known failures (KimiRuntimeTests, KimiHomeTests); in addition flaky, host-caused failures that
  differ per run (run 1: 4 in QuestionRowLookupScenarioTests; run 2: Numstats + 2 AVibeReviewer; run 3 without my two new classes: only the 2 known;
  run 4: 17 round/git tests). The round tests fail in a subset run WITHOUT my classes, and SplitOrderTests fails alone on a fresh build of b4e2ea96
  (a worktree, removed). So: not my change; not fixed. A clean whole-suite run was NOT obtained (run 3 was clean but excluded the two new classes;
  the new classes pass alone and together: 93/93 with KimiConsultantTests, CapabilityMatrixTests, ConfinementPlannerTests).
- `dotnet format --verify-no-changes --no-restore`: no formatting diagnostic; 54 IL2026/IL3050 errors from `src_server` are identical at `b4e2ea96`.
- Generator: written and `--check` clean (21 rows, 7 runtimes × 3 capabilities).
- Extension `npm test`: 6290 tests, 6270 pass, 1 fail = the known tag test ("the baseline names no release that was never tagged"); `capabilityAdmission` 31/31.
- `plan-lifecycle`: clean.

## What remains
- Review (Opus) of both stories.
- Live checks (J2), M-K17, RESULTS §31 — out of scope.
- `research/module_*.md` updates — out of scope.
