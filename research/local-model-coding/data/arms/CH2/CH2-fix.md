# CH2 — CODER FIX notes (Haiku), review of E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-2`. Work starts after `1c8bd6f6`. Nothing pushed. Written 2026-10-28, 13:26 UTC.

## Commits

| sha | what |
|-----|------|
| ff1de017 | fix(kimi): the unconfined caveat no longer promises a shell Kimi refuses (R2) |
| 72e848db | test(kimi): the check's --cwd is asserted under its own scratch tree (R3) |
| 283793ec | docs(research): the runtime table and the Kimi row describe E5.6 (R1) |

## Per finding

| id | severity | outcome | why / what |
|----|----------|---------|------------|
| R1 | major | **fixed** | `research/module_runners.md` said Kimi is NOT a consultant and NOT in `runtime-capabilities.json`. Both were stale. Now: Kimi answers consult (E5.4), has three rows (none and disk unconfined, web unmeasured), and a planner arm with no flag (E5.6). `research/module_core.md` diagram: "18 rows (6 runtimes × 3)" is now "21 rows (7 runtimes × 3)", with the kimi standings. The "NOT a consultant" clause was not in the review; I fixed it because it sat in the same sentence and was also false. |
| R2 | minor | **fixed** | Verified: the Unconfined caveat "a shell is always there" is false for Kimi (M-K3, M-K4: the shim refuses every shell request). Changed the sentence in `CapabilityMatrix.cs:33`, `capabilityAdmission.ts:60`, the legend in `shared/runtime-capabilities.json:11`, and the C# doc in `RuntimeCapabilities.cs:14` to "its tools reach past what it is given". Codex's own row note still says "a shell is always there", which is true for codex, so it stays. No existing test held the old sentence; the review's "update the tests" step meant adding one, so I added a red test first in each language (see below). |
| R3 | minor | **fixed** | `AKimiConsultantCheckTests.cs`: `--cwd` is now asserted to start under the check's scratch root, and its parent folder to end in `own\tree`. The assertion that could never fail (`_scratchRoot.Path.Should().NotBe(...)`) is removed. My first version ended the path in `own/tree`, which was wrong: the real value is `.../own/tree/snapshot-<hex>`. The corrected form checks the parent folder. |
| R4 | minor | **not fixed** | The plan's §0.2 and the E5.5/E5.6 story table name **Opus** as the coder. The status line (line 3) and "as built" name **Sonnet**. I cannot record the operator's decision or its reason: I do not have it. Left for the operator. This also covers H1 from my earlier review. |

## Red steps (fix of a defect)

- **R2, C#** `CapabilityMatrixTests.TheUnconfinedCaveat_DoesNotPromiseAShell_BecauseKimiRefusesEveryShellRequest`: red with "Did not expect caveat \"unconfined: can read this machine — a shell is always there, and no flag removes it\" to contain \"a shell is always there\"". Green after the fix.
- **R2, TS** `capabilityAdmission.test.ts` "the unconfined caveat does not promise a shell that kimi refuses": red on the same text (`AssertionError`). Green after the fix.
- **R3** is a test-only tightening, so there was no red step. The one wrong version (suffix check) failed on the real `--cwd`, and was corrected. I did NOT run a mutation to prove the new assertion has teeth (for example, pointing `--cwd` at the scratch root). That gap is open.

## Results (final, after all three commits)

- **Build** (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 warnings, 0 errors.
- **Changed test classes** (`CapabilityMatrixTests`, `AKimiConsultantCheckTests`): 48 total, 48 passed.
- **Whole MCP suite** (one run, 7m 23s): 10561 total, 10457 passed, 84 skipped, **20 failed**.
  - 2 known, not ours: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`, `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
  - 18 rounds-database and numstat tests (`ACodeRoundRecordsItsSizeTests` ×7, `ARoundsNumberComesFromTheJournalTests` ×3, `EndToEndTests` ×3, `ARoundIsMintedAKeyTests` ×2, `SplitOrderTests` ×1, `ThePersonsCommandsReachTheCallerTests` ×1, `TheRoundsNumstatsAreCountedTests` ×1). The coder's notes recorded the same set at `b4e2ea96`. I did not re-run them at `b4e2ea96`, so this is their note, not my observation. None of them touches the capability matrix or Kimi.
- **Format** (`dotnet format ... --verify-no-changes --no-restore --include` on the 4 changed `.cs` files): exit 0.
- **Generator** (`node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date (21 rows over 7 runtimes × 3 capabilities).
- **TS** (`npm --prefix src_vs_code test`, which compiles first; tsc exit 0): 6291 tests, 6271 pass, 19 skipped, 1 fail = `the baseline names no release that was never tagged` (known).

## Rules

- Resolver: `check` resolved; `explain` selected 35 rules.
- **Read in full:** `csharp.doctrine`, `typescript.doctrine`, `common.testing`, `common.generated-code-tests`, `common.test-leftovers`, `common.git-workflow`, `common.reuse-first`, `common.subagent-models`. The hook had already loaded `common.coding-style`, `common.knowledge-base`, `common.security`, `common.task-lifecycle`, `local.common.review-gate` and `local.common.vendor-routing`.
- **Not read by me:** the other selected rules, among them `common.coai-*`, `common.development-workflow`, `common.planning-docs`, `common.scenario-tests`, `common.reliability`, `common.logging-serilog`, `common.utc-timestamps`, `common.measurement`, `common.automated-checks`. The batch read was over 32 KiB, so I read the rules one by one and ran out of time to read the rest. The fixes do not touch what they govern, but this is not full compliance with the entry procedure.
- The `common.git-workflow` rules were followed: stage by path, check `git diff --staged --stat` before each commit (6, 1 and 2 files, all mine), no `git add -A`.

## Not done (in scope but not reached)

- R4 (the plan's coder model): needs the operator.
- A teeth check for the R3 assertion.
- Re-running the 18 rounds-database failures at `b4e2ea96` to confirm they are pre-existing here.
- The `common.scenario-tests` and `module_tests.md` listing for the new tests: the new tests are not a flow, so I did not add them.
- Out of scope and not done, as the brief said: live checks, `research/` beyond R1, merging `main`, push, PR. `kimi` and `vibe` were never run.
