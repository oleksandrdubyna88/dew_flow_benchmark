# Q3 — polish of E5.5 and E5.6 (CODER, Sonnet)

Branch `exp/kimi-e56-skeleton-3`. No push. The fill reports (`results/Q3/fill-test.json`, `fill-prod.json`) were read: the TEST fill
had 13 regions, all "filled", and the build after it failed with 3 compile errors; the PROD fill had NO regions (the production
code of E5.5–E5.6 was already written in the skeleton), and the build still failed with the same 3 errors.

## Decisions

1. **The Kimi stand-in works on every host, so the tests do not skip on Linux.** `AKimiConsultationScenarioTests.StandIn` puts a symbolic link
   where Windows has a junction (`WindowsStandIn`) and stands the host in as Windows (`KimiSeams.Using`). The fill put
   `Assert.SkipUnless(OperatingSystem.IsWindows(), …)` in five tests; with it, S5.5.1–S5.5.4 and S5.6.7 would all have SKIPPED here and no
   red could ever be seen. I removed every one. All 12 scenario tests (14 cases) run on this host.
2. **"The scratch repository and its canary unchanged" is asserted through the record's own words.** The fill invented JSON properties
   (`canaryTouched`, `treeChanged`) that the check never writes. The check ends a broken invariant as `state: failed`, `failureKind:
   tree-changed`. S5.5.1 asserts `state == answered` (which IS the invariant) and `canary == not-attempted`.
3. **S5.6.5 checks the launch control's refusal**, which the fill had left as a comment ("we lack a fixture"): `KimiPlaces.ForShim(home,
   <the persons-bin the adapter was built with>)` is enough, so no fixture is needed. The persons-bin is now one constant
   (`KimiPersonsBin`) shared by `Adapters()` and the test.
4. **S5.6.6 also refuses a plan with a leading word**, because the guard (skeleton deviation 51) refuses it and nothing tested that.

## Per region (the fill's regions)

| Region | File | Result |
|---|---|---|
| s55-played | AKimiConsultantCheckTests | **rewritten** — it did not compile (a two-argument lambda for a one-argument `Func`; target-typed conditional). Same idea, one expression. |
| s551-check | AKimiConsultantCheckTests | **fixed** — Windows-only skip removed; the invented JSON properties replaced by the record's own fields (decision 2); the three path assertions kept. |
| s552-gone | AKimiConsultantCheckTests | **fixed** — the lambda arity, the skip, and the failing arm now asserts `state == failed`; "the turn really ran" added. |
| s553-cancel | AKimiConsultantCheckTests | **fixed** — the skip removed; `played.Shims` single added; kept the rest. |
| s554-rootless | AKimiConsultantCheckTests | **fixed** — the skip removed; `home` must be directly under `…\own`; kept the rest. |
| s567-repo | AKimiQuestionRowTests | **kept as-is** (markers removed). |
| s567-row | AKimiQuestionRowTests | **rewritten** — it called `Dequeue` on a `ConcurrentQueue` (compile error), took the launch out of the queue twice, and was skipped off Windows. Same shape now as the Vibe row tests: one `ContainSingle`, the prompt names the copy and never the root, the copy's scratch folder is gone, `coai-vh` is empty. The refused arm uses a folder outside any Git work tree (`PlainFolder`). |
| s561-rows | CapabilityMatrixTests | **fixed** — a loop with an if/else chain replaced by three named rows (`KimiRow` helper, so a missing row reads "Expected row not to be <null>", the table's red, not a NullReferenceException). |
| s564-admit | CapabilityMatrixTests | **fixed** — the web assertion removed (the table row names `none` and `disk` only; web is S5.6.3/S5.6.8). |
| s563-planner | ConfinementPlannerTests | **kept** (small tidy: two `And` chains, reasons on the assertions). |
| s565-launch | ConfinementPlannerTests | **fixed** — `AcpWords` using missing (compile error); the refusal check done (decision 3); `--session` named by `AcpWords.SessionFlag`. |
| s566-flags | ConfinementPlannerTests | **fixed** — a second hand-made plan with a leading word (decision 4). |
| s568-ts | capabilityAdmission.test.ts | **kept as-is** (markers removed, two reasons added). |

Not in the fill's list, and needed: `CapabilityMatrixTests.EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` (an existing test) failed on
the new kimi rows, because its list of citable records had no Kimi record. **Written by me:** one line, `("RESULTS_kimi_confinement.md",
"2026-10-13", "M-K")`. The skeleton had missed it.

## Red first — what I saw

The production files were put back to `b4e2ea96` (`git checkout b4e2ea96 -- <files>`), built, run, and put back to HEAD. Three runs:

* **A, all production at `b4e2ea96`** (planner arm, flag guard, `Runtimes`, the table, the vectors out):
  S5.6.7 ×3 red: *the row is `blocked`: 'kimi' is not a runtime the question consultant can launch for 'disk'*. S5.6.1 red
  (*Expected row not to be <null> because the table has a row for kimi × None*). S5.6.4 red (*Expected Admitted, found Refused*). S5.6.3, S5.6.5, S5.6.6:
  *Expected type to be Planned but found Refused* — from the `Plan` helper, since the matrix refuses an unknown runtime before the planner arm
  is reached. **That is not the table's wording** (S5.6.3 predicted `Cwd` Scratch instead of Snapshot).
* **B, the table, the vectors and `Runtimes` in, the planner arm and the flag guard out:** the table's own reds.
  S5.6.3 *Expected disk.Cwd to be Snapshot, but found Scratch.* S5.6.5 *Expected disk.Request.WorkingDirectory to be …/coai-question-…, but found …/coai-scratch-…*.
  S5.6.6 *Expected a <System.ArgumentException> to be thrown, but no exception was thrown.*
* **C, the table in, the vectors out:** S5.6.2 red — `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` (the missing `kimi` pairs).
* **TS (S5.6.8):** with the generated table at `b4e2ea96`, `capabilityAdmission.test.js` fails on the runtimes list (`actual [... 'vibe']`, expected
  `[... 'vibe', 'kimi']`) and on `admit('kimi', …)`. Green after the generator wrote the table (it wrote the same bytes as HEAD).
* **S5.5.1–S5.5.4: NO RED IS POSSIBLE at `b4e2ea96`.** E5.4 (the adapter, its registration) and E5.3 g (the junction-first disposal) are already in;
  the plan's own table says S5.5.3 is "green after E5.3 g — pins `finally`", and its S5.5.1/2/4 reds name states from BEFORE those stories. All five cases
  pass against `b4e2ea96` production. So I ran **teeth** instead — a mutation of production, each watched red, each put back:
  * the check's `finally` no longer disposes the one-shot home → S5.5.2 (both arms) and S5.5.3 red: *Expected the coai-vh folder to be empty … found …/coai-vh/<8>*.
  * `ConsultCheckScratch.Delete` no longer disposes the Kimi homes first → S5.5.4 red: *Expected _unlinkedByTheDeleter to be empty, but found …/own/kh-…/home/credentials* — the table's "recorder invoked".
  * `NeedsASanitisedSnapshot => false` → S5.5.1 red (the check fails, the copy is gone).
  * `HomesFolder => string.Empty` → S5.5.1 red (*the home is not under …/coai-vh*) and S5.5.2/3 red.
  * `QuestionRowLaunch.Release` no longer disposes the home → S5.6.7 ×3 red: *Expected …/coai-vh to be empty … found …/coai-vh/<8>*.

## Findings, not fixed (not in scope)

* The TypeScript caveat for `unconfined` reads "can read this machine — **a shell is always there**, and no flag removes it"
  (`capabilityAdmission.ts`, `CAVEATS`). For Kimi that is not true: every shell command is refused (M-K3, M-K4); what it can read is
  through `Grep` and `Glob`. The test asserts only `can read this machine`, which is true of both. The sentence should be per row (or
  say "a search tool"). The server's `Admission` flag has no text of its own, so this is the extension's half. For the plan owner.
* `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore` exits **2 on this host with and without my change**:
  its output is only `IL2026`/`IL3050` AOT-analyzer errors from `src_server`. `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj
  --verify-no-changes --no-restore` exits 0.

## Final results (HEAD after the last commit)

* The 12 scenarios (14 cases): all green, none skipped, on this Linux/WSL host.
* The whole MCP suite: total 10555, succeeded 10469, skipped 84, **failed 2 — exactly the two known at the start**
  (`KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`).
* Extension `npm test`: one failure, the known one (`the baseline names no release that was never tagged`). The generated-files check passes.
* Generator: `generate-runtime-capabilities.mjs` wrote the same bytes as HEAD; `--check` clean (21 rows over 7 runtimes × 3 capabilities).
* Format: the tests, core and runners projects are clean (exit 0); the solution-wide run exits 2 with or without my change.

## What remains

* Not done, out of scope: the live checks (J2), M-K17, research files, any push/PR/release.
* Not done: the coai review gate (`review_code`) — not asked for in this step; the plan's status line says "not yet reviewed".
* For the plan owner: the `unconfined` caveat sentence in `capabilityAdmission.ts` (deviation 62).
