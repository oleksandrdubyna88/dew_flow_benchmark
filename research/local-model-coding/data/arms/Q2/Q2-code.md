# Q2 — polish of E5.5 and E5.6 (Sonnet, 2026-10-26)

Branch `exp/kimi-e56-skeleton-2`. Commits: `a6ad0c74` (tests build and run, guard wired), `050b8797` (plan status and deviations 58–63).

## Per region

| Region | File | Verdict |
|---|---|---|
| s566-guard (prod) | `KimiConsultant.MustCarryNoFlags` | **kept as-is** (it refuses `Leading` and `Flags`); the CALL in `Build` was missing — **written by me** |
| s55-standin | `AKimiConsultantCheckTests` | **rewritten**: the fill put a nested class and a second `StandIn` inside the method (does not compile). Now `Scopes` is a class member and `StandIn` an expression-bodied method, as in `AKimiConsultationScenarioTests` |
| s55-settings | same | **fixed**: object initialiser kept; `ConsultantChoice` positional, `DataDir = _data` (TempDir conversion) |
| s55-check | same | **fixed**: fill used `await` in a non-async method, returned a tuple of 3 names; now `async` and `_` for stderr |
| s551 | same | **rewritten**: `await` in a `Task` method (did not build), and no `markerRead`/`home` shape check (`<root>/coai-vh/<8>/kh-<10 hex>`); the turn's marker read is now one shared helper (`TheMarkerRead`) |
| s552 | same | **rewritten**: the fill assigned the init-only `Turn` (did not build) and did not assert that the shim ran; now `Turn` is chosen at construction |
| s553 | same | **fixed**: `await` in a non-async method; the wait on `started` is bounded by the test's token, `cts` is linked to it |
| s554 | same | **rewritten**: the fill compared `_person.Hash()` with a second call to itself (always true), so it could not fail; now compared with `before` taken ahead of the check |
| s567-repo | `AKimiQuestionRowTests` | **kept as-is** (copy of the Vibe helper) |
| s567-standin | same | **rewritten** like s55-standin |
| s567-service | same | **kept**, turned into an expression-bodied `PanelService` as the Vibe test has it (the homes root is `Homes`) |
| s567-ask | same | **kept** |
| s567 | same | **rewritten**: the fill asserted `played.Shims` empty for the cancelled arm (the shim ran there), read `Directory.Exists(...).Parent` (does not compile), asserted the prompt names `<cwd>/Mailer.cs` (the copy has a folder of its own under the cwd), cancelled from a side task. Now: copies listed at the turn, prompt names the copy and never the root, cancellation as Vibe's, no `coai-vh` entry left, recorder never invoked, person whole |
| s561 | `CapabilityMatrixTests` | **fixed**: `Spelled()` strings → `CapabilityStanding` values, nullable `Of` handled, no `switch` in a loop |
| s564 | same | **kept as-is** |
| s563 | `ConfinementPlannerTests` | **fixed**: `web.Reason` did not compile (`Confinement` has no `Reason`); now `BeOfType<Refused>().Which.Reason` |
| s565 | same | **kept as-is** |
| s566 | same | **kept as-is** (both the `Flags` and the `Leading` arm) |
| s568 (TS) | `capabilityAdmission.test.ts` | **kept as-is** |

All `QWEN-FILL` markers and instruction comments are gone (grep over `src_mcp`, `src_vs_code/src`, `shared`: 0).

## Red first

Production restored to `b4e2ea96` for `ConfinementPlanner.cs`, `RuntimeCapabilities.cs`, `KimiConsultant.cs`, the two shared JSON files (and, for the TS test, the generated table), tests as filled:

| Scenario | Red seen | Message |
|---|---|---|
| S5.6.1 | yes | `Expected none not to be <null>.` (table's) |
| S5.6.2 | yes | `Expected Vectors … ` naming the kimi pairs (the test's own sentence) |
| S5.6.3 | yes, other text | `Expected type to be Confinement+Planned, but found Confinement+Refused.` — the matrix refuses kimi first, so the table's `Cwd … Scratch` red cannot show |
| S5.6.4 | yes | `Expected type to be Admission+Admitted, but found Admission+Refused.` |
| S5.6.5 | yes, other text | same Planned/Refused text as S5.6.3 |
| S5.6.6 | yes (twice) | with the data in and the guard unwired: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` (table's); against `b4e2ea96`: Planned/Refused |
| S5.6.7 ×3 | yes | the row ends `none_available` (kimi is not a runtime the row can launch), cancelled: `Expected a <OperationCanceledException> to be thrown, but no exception was thrown.` |
| S5.6.8 (TS) | yes | runtimes deepEqual (`…'vibe'` vs `…'vibe', 'kimi'`), then `none: expected admitted` — as the table |
| S5.5.1–S5.5.4 | not red against `b4e2ea96` | E5.3 g and E5.4 are in: green on arrival. Teeth: see below |

Teeth runs (each reverted, tree clean afterwards):
- `OneShotHome.Emptied` deleting by the bare deleter → red: S5.5.2 (both arms), S5.5.3, S5.6.7 answered and cancelled, message `Expected _unlinkedByTheDeleter to be empty because … found … coai-vh/<8>/kh-<10 hex>/home/credentials`. S5.5.1 and S5.5.4 and S5.6.7 refused stay green, as they should.
- `ConsultCheckScratch.Delete` without `KimiHomesDisposed` → red: S5.5.4 only (`…/own/kh-<10 hex>/home/credentials`).

## Results

- Four classes (`AKimiConsultantCheckTests` 5, `AKimiQuestionRowTests` 3, `ConfinementPlannerTests` 34, `CapabilityMatrixTests` 40): all green after the guard call was added.
- Generator: `generate-runtime-capabilities.mjs` wrote no change; `--check` up to date (21 rows over 7 runtimes × 3).
- Extension: `npm test` → one failure, the known `the baseline names no release that was never tagged`; `typecheck` and `lint` clean.
- `dotnet format whitespace --verify-no-changes` on the five changed C# files: clean.
- `plan-lifecycle.mjs`: clean.
- Whole MCP suite: see the end of this file.

## Decisions and deviations

See the plan's deviations 58–63. In short: S5.6.7's cancelled arm ends as Vibe's; the guard refuses `Leading` too; S5.5.1's "unchanged" is the check's own judgement; first-fill test bodies were rewritten where they did not build.

## Not done / remains

- No review round (out of scope for this step); the plan says "not yet reviewed".
- `GitScratch.AfterUnlink` is a static: the new classes set it like `AOneShotKimiHomeIsDisposedJunctionFirstTests` does, with no xunit collection. If a CI run ever shows the recorder seeing another test's unlink, put both in one collection.
- Live checks (J2), M-K17, RESULTS: not touched.

## Whole MCP suite (10:23 UTC, after the last code change)

10556 total, 10470 passed, 84 skipped, **2 failed** — exactly the two known at the start (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`). Nothing new is red. Done before the deadline; nothing else remains for this step.
