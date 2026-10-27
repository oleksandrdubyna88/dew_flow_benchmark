# H1 — fix notes (Sonnet, review of E5.5 and E5.6)

Branch `exp/kimi-e56-skeleton-haiku-1`. Nothing pushed. Verdict of the review: fix-then-ready; 5 findings, all minor.

## Per finding

| Id | Result | What |
|---|---|---|
| R1 | **fixed** (62a7d157) | Checked: M-K6 is `## 9` in `RESULTS_kimi_confinement.md`; §17 is M-K11 (a). Both rows now say `§9 M-K6`; the `disk` row also names `M-K9 (§12)` and `M-K9 (e) (§21)`. Generator run again; `--check` clean. No test first: this is a text reference in a data field, and no test reads the record's headings. |
| R2 | **fixed** (05744668) | Checked: right, the catch branch was dead. S5.5.3 now has no try/catch; it asserts `state` = `failed` and `failureKind` = `cancelled`. It was green before and after (it strengthens the test; the code was already right). |
| R3 | **fixed** (05744668) | S5.5.2 keeps the check's result and asserts `state` = `failed` when `theTurnFails`, else `answered`. Green before and after (code was right). |
| R4 | **fixed, with a change to the proposed fix** (05744668) | Checked: right. The proposed fix (list the only directory under `WorkingDirectory` after the call) fails: the row's scratch is already deleted then (`DirectoryNotFoundException`, seen). So the played turn reads the copy folder while the turn is alive (`Turn` lambda), and the test asserts that StdIn contains that path. Still asserts that StdIn does not contain the granted root. |
| R5 | **fixed** (plan commit) | Mutation: `KimiConsultant.NeedsASanitisedSnapshot => false`. S5.5.1 red: *Expected said.GetProperty("state").GetString() to be "answered" …* (the other four scenarios of the class went red too). Reverted (`git checkout`), recorded in deviation 57. `plan-lifecycle.mjs` clean. |

No finding was wrong.

## Results

(Final run 13:50 UTC; deadline 14:02.)

- **Build** `dotnet build … -c Debug -m:4 --disable-build-servers`: 0 errors, 0 warnings (after the last code edit; the S5.5.1 mutation was reverted and the build re-run).
- **Changed test classes** `AKimiConsultantCheckTests` + `AKimiQuestionRowTests`: 8 of 8 pass.
- **Whole MCP suite** (`H1-fix-suite.txt`): 10557 tests, 10470 pass, 84 skipped, 3 failed:
  - the two known: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_…`;
  - one more, **not mine**: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. It uses a 1 ms git deadline and expects `unavailable: git…`, but got `recorded`. It failed again when its whole class ran, and passed when run alone. No file of this change touches it (the diff against `b4e2ea96` has no Rounds or numstat file). I did not run it on the old commit. It looks timing-dependent.
  - The suite also printed "the log could not be flushed: the log's disk is full". The disks have free space (`/tmp` 22G, `/` 901G), so I take it as a line from a test that plays a full disk.
- **Format** `dotnet format --verify-no-changes --no-restore`: no whitespace error. It still exits non-zero because of the same 54 IL2026/IL3050 lines in `src_server` (not mine, as in the earlier notes).
- **Generator** `generate-runtime-capabilities.mjs --check`: up to date (21 rows over 7 runtimes × 3 capabilities).
- **TS tests** `npm test` (`H1-fix-ts.txt`): 6290 tests, 6270 pass, 1 fail = the known tag test (`the baseline names no release that was never tagged`).
- `plan-lifecycle.mjs`: clean.

## What remains

Nothing in scope. Commits: `62a7d157` (R1), `05744668` (R2–R4), and the plan commit (R5).
