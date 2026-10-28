# CR1 — coder notes (Sonnet), E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-1`, from `b4e2ea96`. Started about 11:01 UTC.

## Decisions

- Order of work: tests first, each watched red, then code. The E5.6 matrix/planner rows were red before code. The E5.5 rows and S5.6.7
  were green on arrival, because E5.3/E5.4 already carry the behaviour. For those I broke the deciding line, saw red, and reverted (see below).
- `RuntimeCapabilities.Runtimes` gets `kimi` BEFORE the seed rows, so the table loads. Rows were added before the vectors so S5.6.2 was red on its own.
- `none` and `disk` are both `unconfined` (E5.6 decision 2); `web` is `unmeasured`.
- The planner arm gives no flags, ever. `KimiConsultant.MustCarryNoFlag` refuses a plan with any `Flags`, `Leading` or `AddDirs`.
- `capabilityAdmission.test.ts`: the runtimes list gains `kimi`, plus one new test (`kimi rows admit flagged, web refuses naming its record`).
- Generated TS written by the generator only; `--check` clean.

## Reds, per scenario

| Scenario | Red seen | Message vs the table |
|---|---|---|
| S5.5.1 | yes (teeth: `NeedsASanitisedSnapshot=false`) | check fails before its answer: `state` not `answered`. Not the table's "cannot hold a consultation" sentence (that was for before E5.4) |
| S5.5.2 | yes (teeth: check's `Home.Dispose()` removed) | `coai-vh\<8>` not empty. Recorder-red for the one-shot home itself not tried |
| S5.5.3 | yes (same teeth) | `coai-vh\<8>` not empty |
| S5.5.4 | yes (teeth: `ConsultCheckScratch.Delete` without the junction-first walk) | the table's message: deleter's unlink recorder invoked |
| S5.6.1 | yes | `Expected none not to be <null>` — as the table |
| S5.6.2 | yes | `TheVectorsActuallyLoaded…` — the test's own message naming the missing pairs |
| S5.6.3 | yes | first `Refused` instead of `Planned` (the matrix refuses an unknown runtime first); with the table rows in and no arm: `Expected disk.Cwd to be Snapshot, but found Scratch` — as the table |
| S5.6.4 | yes | `Expected Admitted, but found Refused` — as the table |
| S5.6.5 | yes | first `Refused` instead of `Planned`; after the rows: `WorkingDirectory` differs (the default arm gave Scratch) |
| S5.6.6 | yes | `Expected ArgumentException … no exception was thrown` — as the table |
| S5.6.7 | yes (teeth: `Release(homes)` removed) | `coai-vh\<8>` not empty, all three endings — as the table |
| S5.6.8 | yes | `'kimi' is not a runtime the question consultant can launch`, and the runtimes list — as the table |

## Results

- MCP suite (whole, 7m11s): 10555 total, 10467 passed, 84 skipped, **4 failed**:
  - the 2 known failures (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`);
  - `SecuritySourcesCharacterizationTests.The_collection_deadline_ends_the_file_it_interrupts_and_every_later_one` (30 s deadline test): passed when re-run
    alone and with the Vibe class — flaky under load, not mine;
  - `AVibeReviewerRunsThroughAFakeVibeTests.WhatARunLeavesInItsHome_IsRefusedBeforeTheRepairStarts`: FAILED in the full run and once in a re-run
    together with the Security class (*Expected NotStarted, but found Ok*). It PASSED alone, in two more re-runs of the same pair, and at the base
    commit `b4e2ea96` (built in a throw-away worktree, since removed). I did not find the cause; it is intermittent, and I cannot prove that my
    tests (which set `GitScratch.AfterUnlink` and reset it in `Dispose`) are innocent. It needs a look by the reviewer.
- Format: `dotnet format --verify-no-changes --no-restore` over the whole slnx prints pre-existing AOT analyzer errors (IL2026/IL3050) in `src_server`
  on this host, so I ran it with `--include` over the 7 files I changed: clean.
- Generator: `generate-runtime-capabilities.mjs` written, `--check` clean (21 rows over 7 runtimes × 3 capabilities).
- Extension `npm test`: 6290 tests, 6270 pass, 19 skipped, **1 fail** — the known `the baseline names no release that was never tagged` (no tags in this clone).
  The new `kimi rows admit flagged, web refuses naming its record` passes, with `capabilityAdmission.test.ts` (31/31).
- `plan-lifecycle.mjs`: clean.

## What remains

- The review of E5.5/E5.6 (Opus). The live checks (J2), M-K17, `research/module_*.md` and RESULTS §31 are out of scope here.
- Not seen red: the recorder arm of S5.5.2 and S5.6.7 for the one-shot home's own order (E5.3's tests S5.3.7 cover it).

## Commits

1. `feat(kimi): the question consultant's Kimi rows …` (E5.6: matrix, vectors, planner, generated TS, TS test, S5.6.1–S5.6.6, S5.6.8)
2. `test(kimi): a Kimi consultant check …` (E5.5)
3. `test(kimi): a Kimi disk question row …` (S5.6.7)
4. `docs(plan): E5.5 and E5.6 implemented …` (status line, deviations 50–58)
