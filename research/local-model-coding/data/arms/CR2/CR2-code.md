# CR2 — coder notes (Sonnet), E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-2`, from `b4e2ea96`. Host: Linux (WSL2). Nothing pushed, no `kimi`/`vibe` started.
Full list of deviations: `todo/PLAN_vibe_and_kimi_runtimes_E5.md`, *E5.5–E5.6 as built*, items 50–59. This file is the short form.

## Commits

1. `feat(question): a Kimi row — three capability rows, the vectors, the planner arm, no flag ever (E5.6)` — S5.6.1–S5.6.6, S5.6.8, the seed rows, the vectors,
   the regenerated `runtimeCapabilities.generated.ts`, `RuntimeCapabilities.Runtimes`, the planner arm, `KimiConsultant.MustCarryNoFlag`.
2. `test(question): a Kimi disk row reads its copy and leaves nothing behind …` — S5.6.7 (`AKimiQuestionRowTests`).
3. `test(consult): the Kimi consultant check …` — S5.5.1–S5.5.4 (`AKimiConsultantCheckTests`).
4. `docs(plan): E5.5-E5.6 implemented — status and deviations 50-59`.

## Decisions

- **Order of the data change.** Tests first. Then the seed rows plus `Runtimes` (S5.6.1, S5.6.4 green; S5.6.2 red because no vectors), then the vectors, then the
  planner arm and the flag refusal. Each step's red was watched.
- **`none` is `unconfined`** for Kimi (decision 2), cells `M-K6`, `M-K3`, `M-K4`; `disk` adds `M-K9`, `M-K9 (e)`; `web` is `unmeasured` with a note. The version is `2.1.1`
  and the date `2026-10-13`, as E5.4 decision 8 has them. The `web` row cites RESULTS §7 and §14; the others cite §6, §7, §9, §12, §21 by section number
  (I checked these headings in `RESULTS_kimi_confinement.md`; I did not edit that file).
- **A plan with `Leading` flags is refused too**, not only `Flags` (decision 3 says "ANY flag").
- **S5.6.7 and S5.5 run through the product's own flow** (the service, the real launch control, a real junction or symlink through `WindowsStandIn`), with only the shim played.
  The deleter's `GitScratch.AfterUnlink` is armed in every arm that makes a junction, so "the recorder never invoked" is a positive check.
- **Tests that set no environment variable are not in the `fakecli-env` collection.**

## Red messages that differ from the tables

- S5.5.1–S5.5.4, S5.6.7: no red exists on this tree (E5.3 and E5.4 are in). I broke the deciding lines one by one (see plan deviation 50) and saw each test red.
  The wording of the S5.6.7 red is *…EnumerateFileSystemEntries(…coai-vh) to be empty…* instead of the table's `Directory.Exists`.
- S5.6.3, S5.6.5, S5.6.6: first red was *Expected type to be Confinement+Planned, but found Confinement+Refused* (the matrix did not know `kimi`); after the seed rows, the table's reds.
- S5.6.1: *Expected none not to be <null>*. S5.6.4: *Expected type to be Admission+Admitted, but found Admission+Refused* (type asserted first).
- S5.6.8 (TS): red was exactly as the table says (runtimes list; then admit for kimi). The generated file was stale at that moment; so the "seed actually loaded" tests were red too.

## Things I found that the plan does not say

- `CapabilityMatrixTests.EveryRow_CitesTheRecord_…` needs a `Records` entry for the kimi record (done, deviation 53).
- The first S5.6.7 draft guessed the copy's folder name; the real name is `snapshot-<8>`. Fixed by looking at the folder while the shim runs.
- The first S5.5.1 draft never armed the recorder; fixed. It was not re-run against the junction-first break afterwards (S5.5.2 and S5.5.3 cover the same break).
- Whole-solution `dotnet format --verify-no-changes --no-restore` prints IL2026/IL3050 errors for `src_server` on this host. I did not touch those files. The files I changed pass `--include`.

## Results at the end

- All 12 scenarios green: `ConfinementPlannerTests` + `CapabilityMatrixTests` + `KimiConsultantTests` (85 pass), `AKimiQuestionRowTests` (3), `AKimiConsultantCheckTests` (5),
  TS `capabilityAdmission.test.ts` (31 pass).
- Whole MCP suite (6m36s): 10555 total, 10452 pass, 84 skipped, **19 fail**. 2 are the known ones. **The other 17 are NOT in the brief's list:**
  `ARoundsNumberComesFromTheJournalTests` (3), `EndToEndTests` (3), `ARoundIsMintedAKeyTests` (2), `SplitOrderTests` (1), `ACodeRoundRecordsItsSizeTests` (7),
  `ThePersonsCommandsReachTheCallerTests` (1) — all read the rounds database and find it empty. I built `b4e2ea96` in a separate worktree (removed after) and ran
  `ARoundIsMintedAKeyTests` + `ACodeRoundRecordsItsSizeTests` there: the same 9 fail at the base. So they are not caused by this work (host-specific; cause not investigated).
  I ran only those two classes at the base, not the other four.
- `dotnet format --verify-no-changes --no-restore`: clean for every file I changed (`--include`). Whole-solution: IL2026/IL3050 errors in `src_server` (untouched).
- `generate-runtime-capabilities.mjs` then `--check`: up to date (21 rows, 7 runtimes × 3 capabilities).
- Extension `npm test`: 6270 pass in the main run, 1 fail: the known `the baseline names no release that was never tagged`.
- `plan-lifecycle.mjs`: clean.

## What remains

- The Opus review of E5.5–E5.6.
- Live checks (J2) and M-K17: out of scope.
- `research/module_*.md` and `research/RESULTS_*.md` do not yet mention the Kimi question row or check (out of scope, as the task says).
- The two known-red C# tests and the extension's untagged-baseline test are not mine and were not touched.
