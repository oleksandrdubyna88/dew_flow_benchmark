# H4 — CODER polish of E5.5 and E5.6

Branch `exp/kimi-e56-skeleton-haiku-4`. Commits: `8864f6b8` (tests, marker removal), and one for the plan (see `git log`).

## Per region

| Region | Verdict |
|---|---|
| `s566-noflags` (`KimiConsultant.MustCarryNoFlags`) | kept as-is (refuses a plan with `Leading`, `Flags` or `AddDirs`, message names the shim) |
| `s55-check` (`Check`) | fixed: the method was not `async` while its body awaits (CS4032); body kept |
| `s551-copy` | rewritten small: the marker reader is now one helper (`ReadsTheMarker`), `--cwd`/`--home` read by `ArgumentAfter`; same assertions |
| `s552-gone` | fixed: the filled body had the right idea but its "root is gone" assertion is `coai-vh` empty; now `TheOneShotFolderIsGone`; the arms assert exact `state` (`answered` / `failed`); `_ = theTurnFails` line removed |
| `s553-cancel` | rewritten: the filled body accepted an exception OR any state, so it could not fail. Now exact: exit 0, `state failed`, `failureKind cancelled`, one shim launched, home gone, recorder empty |
| `s554-rootless` | fixed: shared helpers; the stale comment ("name as used in s551-copy") removed |
| `s561-rows` | kept; only the `NotBeNull` reasons were reworded (they copied the table's red message as a reason) |
| `s563-planner` | kept as-is |
| `s564-admit` | kept as-is |
| `s565-launch` | kept as-is |
| `s566-flags` | kept as-is |
| `s567-repo` | kept as-is |
| `s567-copies` | fixed: (1) `played.During = …` assigned an init-only property (CS8852) → moved into the initializer; (2) `Should().Be(before)` on a list (CS1061) → `Equal`; (3) the prompt names the copy as `<tree>\snapshot-<hash>`, not `<tree>\src` → assertion changed to `snapshot-` |
| `s568-kimi-admit` (TS) | kept as-is |
| production planner arm, rows, vectors, generated TS | written whole in the skeleton step; unchanged; the generator was re-run and `--check` is clean |

All `QWEN-FILL` markers and instruction lines are gone (`grep` over src_mcp, src_vs_code/src, shared, todo finds none of the skeleton's; the other hits are the vendor name `QWEN` in older tests).

## Red first (production, rows, vectors and generated TS restored to `b4e2ea96`)

| Scenario | Red at base | Message |
|---|---|---|
| S5.5.1 | no (green) | E5.3g and E5.4 are already in at the base |
| S5.5.2 | no | same (the first run was red only because of my own wrong "root is gone" assertion, fixed) |
| S5.5.3 | no | same |
| S5.5.4 | no | same |
| S5.6.1 | yes | `Expected row not to be <null> because kimi × None is a row of the table` |
| S5.6.2 | yes | `… a collection with 3 item(s) … but found an empty collection` |
| S5.6.3 | yes | `Expected type to be Confinement+Planned, but found Confinement+Refused` (the matrix refuses `kimi` first; the table says `Cwd` Snapshot vs Scratch — differs) |
| S5.6.4 | yes | `Expected type to be Admission+Admitted, but found Admission+Refused` |
| S5.6.5 | yes | `Expected type to be Confinement+Planned, but found Confinement+Refused` (differs from the table: no stub here) |
| S5.6.6 | yes | same `Refused` message, before the flag arm is reached; with the matrix row but without `MustCarryNoFlags` the table's "no exception was thrown" is what would be seen — not run separately |
| S5.6.7 answered | yes | `Expected status to be "answered"` (row refused: not a runtime) |
| S5.6.7 cancelled | yes | `Expected … to contain a single item, but the collection is empty` (no shim started) |
| S5.6.7 refused | no | the refusal comes before any launch, with or without the row |
| S5.6.8 (TS) | yes | the runtimes list test and `kimi rows admit flagged…`, plus the generated-file tests |

NotImplementedException never appeared; no placeholder is left in the production code.

## Results at the end

- MCP suite, whole: 10557 total, 10471 passed, 84 skipped (Windows-only), 2 failed — exactly the two known failures (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`).
- `dotnet format --verify-no-changes --no-restore` over the whole solution exits 2 on `src_server` AOT analyzer errors (IL2026/IL3050 in `src_server/src/*Endpoints.cs`) — not mine, not touched. Filtered to my files with `--include`: clean.
- Generator: `generate-runtime-capabilities.mjs` run, `--check` says up to date (21 rows over 7 runtimes × 3 capabilities); the file did not change.
- Extension tests: 6290 tests, 1 fail — the known `the baseline names no release that was never tagged`. `kimi rows admit flagged, web refuses naming its record` passes.

## Decisions

- A cancelled check: `failed` / `cancelled` / exit 0 (plan deviation 56).
- A cancelled question row: both a status and a thrown cancellation are accepted (57).
- Plan deviations 56–60 written in the plan, the status line changed.

## What remains

- No mutation test of the junction-first disposal: the recorder assertions (`TheDeleterNeverMetTheJunction`) were seen green, never red. The table's red for them cannot be seen at this base.
- J2 live checks, M-K17, RESULTS §31, `research/module_*.md`: out of scope.
- Review by the coai gate and by Opus: not run in this step.
