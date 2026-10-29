# DR2 review — E5.5 and E5.6 (reviewer and fixer: Haiku, branch exp/kimi-e56-haiku-direct-review-2)

Reviewed at 12:57 UTC, range b4e2ea96..ca0b1722, before any code change. Deadline 13:24 UTC.

## Findings

- **H1 — major — `src_mcp/tests/AKimiQuestionRowThroughTheServiceTests.cs:110-153`.** S5.6.7 is a Theory of three endings (answered; refused before the launch; cancelled mid-turn). Only answered and cancelled are written. The refused arm is missing.
  Fix: a refused arm that asserts no launch, no `coai-vh` entry and the person intact.

- **H2 — major — `AKimiQuestionRowThroughTheServiceTests.cs:110-153`.** The Then of S5.6.7 is not fully asserted. Missing: the recorder (`GitScratch.AfterUnlink`) never invoked; the row's scratch gone; the fake person's folder intact. Only the one-shot folder is checked.
  Fix: add the recorder (pattern from `AKimiConsultationScenarioTests.cs:200-215`), the person's `Hash()` equal to `before`, and the scratch check.

- **H3 — major — `src_mcp/tests/AKimiConsultantCheckTests.cs:132-144, 182-196`.** S5.5.2 and S5.5.4 say "the recorder never invoked". No recorder is wired. The junction-first claim is not pinned.
  Fix: wire `GitScratch.AfterUnlink` in both and assert it stays empty.

- **H4 — major — `AKimiConsultantCheckTests.cs:195`.** S5.5.4 asserts `_person.Hash().Should().NotBeEmpty(...)`. This cannot fail when the person's folder is changed. The Then says the folder is intact.
  Fix: take `before` at the start and assert `Equal(before)`.

- **H5 — major — `shared/runtime-capabilities.json` (kimi `none`, `disk` notes) and `src_vs_code/src/runtimeCapabilities.generated.ts`.** E5.6 decision 2 requires that each row's note carry the version (2.1.1) and, for `none` and `disk`, the sentence that WebSearch and FetchURL are announced and unmeasured. Neither note has either.
  Fix: add both to the two notes, regenerate the TS table with its generator, and assert the sentence in `KimiHasThreeRows_EachCitingItsCells`.

- **H6 — minor — `AKimiConsultantCheckTests.cs:146-158`.** S5.5.2's second arm should end in exit 70 (sign-in refused). It uses a throwing shim. The coder documented this as a deviation.
  Fix: not planned unless time allows. The arm still pins the home's disposal.

- **H7 — minor — `AKimiConsultantCheckTests.cs:176`.** S5.5.3 says "the record says cancelled". The test only asserts `NotBe(Answered)`.
  Fix: assert the failed state and its cancelled failure kind, if the kind has a name.

- **H8 — minor — `shared/capability-matrix-vectors.json` / `runtime-capabilities.json` (kimi disk cells).** E5.6 decision 2 names `M-K9 (e)` as a disk cell. It is absent.
  Fix: add it to the disk cells. It is a heading in RESULTS §21.

- **H9 — minor — `AKimiConsultantCheckTests.cs:9-13`.** The `using CoaiMcp.Runners.Vibe;` line is out of order.
  Fix: reorder the usings.

- **H10 — minor — S5.5.1 Then "the scratch repository and its canary unchanged" is not asserted directly.** The product's `Broken` check decides the state, so "answered" implies it. Not fixed (the product pins it).

- **H11 — process — the S5.5.2–4 reds were never seen.** The coder records them as green on arrival. A break of the deciding line should show teeth. Not run for lack of time; see the report.

## Verified, no finding

- The 12 scenario names exist; S5.6.2 uses the existing test with its new `kimi` pairs.
- The planner arm, the no-flags rule, the `ArgumentException` for a plan with flags, and the admission flags are as decided.
- The RESULTS citations (§6 M-K3, §7 M-K4 and the announced web tools, §9 M-K6, §12 M-K9, §14 web not measured) match the record.
- The one-shot home is disposed through `OneShotHome.Dispose`, junction-first (E5.3 g).
- The test folders are `testsToDelete-` and the homes root is a test folder.

## Fixes

- **H1 — NOT fixed (written, not built, reverted).** I wrote `AKimiDiskRow_RefusedBeforeTheLaunch_LeavesNothingBehind` (a granted root that is not a git repository; asserts refused, nothing launched, no `coai-vh` entry, nothing unlinked, the person intact). The whole-suite run then stalled with no output for over two minutes, and I could not kill it (the kill needed approval). Building over a running test binary was not safe, so I saved the edit as `/home/jinx/git/coai-exp/notes/DR2-H1-unverified.patch` and reverted it. The tree holds only verified commits. Next step: stop the stalled run, apply the patch, build, run `AKimiQuestionRowThroughTheServiceTests`, and check the refusal really leaves the folder empty.
- **H2 — fixed in afb4d86c.** Recorder wired and the person's `Hash()` compared in the three service arms. The scratch-gone check is still NOT asserted (the row's scratch path is not known to the test); that part of the Then is partly open.
- **H3 — fixed in afb4d86c.** Recorder wired in `CheckAsync` for every check arm; `_unlinked` empty asserted in S5.5.2, S5.5.3 and S5.5.4.
- **H4 — fixed in afb4d86c.** S5.5.4 compares the hash taken before the run.
- **H5 — fixed in afb4d86c.** The none and disk notes carry `kimi 2.1.1` and the WebSearch / FetchURL sentence; the table was regenerated and `--check` is up to date; `KimiHasThreeRows_EachCitingItsCells` asserts both.
- **H6 — not fixed.** The S5.5.2 second arm still uses a throwing shim, not an exit-70 `session_lost`. The arm still pins the home's disposal. Documented deviation; no time left for a new shim mode.
- **H7 — fixed in afb4d86c.** S5.5.3 asserts state `failed` and `failureKind` = `ConsultFailureKinds.Cancelled`.
- **H8 — fixed in afb4d86c.** The disk cells include `M-K9 (e)`.
- **H9 — fixed in afb4d86c.** The usings are sorted.
- **H10 — not fixed.** The scratch and canary are not asserted directly. The product's `Broken` makes a breach a non-answered state, and S5.5.1 asserts `answered`. Accepted as implied.
- **H11 — not run.** Teeth were not shown for S5.5.2–4 by breaking the deciding line (time).

## Update (13:10 UTC)

- **H1 fixed in `f2a2a79a`.** The stalled run finished. I applied the saved patch, rebuilt (0 errors), and ran `AKimiQuestionRowThroughTheServiceTests`: 3 of 3 pass, including the new refused arm.
- **Whole MCP suite (run from 13:00 UTC, on afb4d86c):** total 10555, passed 10468, skipped 84, failed 3, exit 2, duration 8m 26s.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_…` — known failure from the brief.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_…` — known failure from the brief.
  - `QuestionRowOnAgyScenarioTests.AFollowUpCutShortByTheCaller_StillBillsTheFirstLaunch_AndNeverAsOk` — not a Kimi test. It passes when its class runs alone (5 of 5, 4.9 s). Likely a timing flake; not investigated further.
- The suite ran on afb4d86c. The H1 commit (f2a2a79a) was not in the full run; only its class was run.

## Final state (13:08 UTC) — superseded by the update above

- Committed: `afb4d86c` (H2, H3, H4, H5, H7, H8, H9). Working tree clean.
- NOT done: H1 (unverified patch in `notes/DR2-H1-unverified.patch`), H6, H10 (accepted), H11 (teeth not shown).
- **The whole MCP suite did NOT complete in this review.** The run (started about 13:00 UTC, output `notes/DR2-suite.txt`) printed nothing after 13:06:14 UTC. The test process was still alive at 13:08. A kill needed approval, so I left it. The same stall happened on the coder's first run (their notes), which they stopped and re-ran. The suite result is therefore unknown for the current commit. Next step: stop the run, re-run `CoaiMcp.Tests` without a filter, and read the summary.

## Verification (13:05 UTC)

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` → 0 warnings, 0 errors.
- The four changed classes (`AKimiQuestionRowTests`, `AKimiConsultantCheckTests`, `AKimiQuestionRowThroughTheServiceTests`, `CapabilityMatrixTests`): 49 passed, 0 failed (before H1 was added).
- Generator: `generate-runtime-capabilities.mjs --check` → up to date (21 rows, 7 runtimes × 3).
- TS: `npm run typecheck` clean; `npm test` 6290 tests, 6270 pass, 1 fail — `the baseline names no release that was never tagged` (the known failure). `kimi rows admit flagged…` passes. `npm run lint` printed no findings.
- Format: the whole-solution `dotnet format --verify-no-changes` exits 2 on IL2026/IL3050 analyzer errors in `src_server` and `src_bugs` (not touched here). Per project, the three MCP projects were checked alone: `CoaiMcp.Tests.csproj`, `CoaiMcp.Core.csproj`, `CoaiMcp.Runners.csproj` → all exit 0.
