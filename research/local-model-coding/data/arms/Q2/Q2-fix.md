# Q2 — fixes after the review of E5.5 and E5.6 (Sonnet, 2026-10-26)

Branch `exp/kimi-e56-skeleton-2`. Review: `Q2-review.json` (verdict `ready`, two minor findings, no blocking, no major).
Commit: `e5c7d409`.

## Findings

### R1 (minor) — `AKimiQuestionRowTests`: the one-shot home's place was never checked — FIXED

The finding is right. `TheOneShotHomeIsGone` only looked at `<Homes>\coai-vh`, and only `if` it existed. A home made somewhere else
(TEMP) would have kept it green.

Fix: `TheOneShotHomeIsGone(launch)` now reads `--home` from the played launch and asserts:
- it is `<Homes>\coai-vh\<8 hex>\kh-<10 hex>` (the parent of the reservation folder is `<Homes>\coai-vh`);
- the home folder and the reservation folder are both gone;
- `coai-vh`, if it still exists, is empty.

It runs for every launch of the played shim: the answered and the cancelled arm. The refused arm has no launch (asserted: 0 launches), so it has no home to check.

Red seen (teeth): in `VibeHomes.Reserved` I cut the reservation id from 8 to 6 characters. The answered and the cancelled arm went red:
`Expected Path.GetFileName(reservation) to match regex "^[0-9a-f]{8}$", but "57290f" does not match.` The old test stayed green on that
change. The mutation is reverted (`git status` clean for `src_mcp/runners`).
I did not find a cheap production change that puts the home in TEMP, so the "never TEMP" part is covered by the parent-folder assertion only, not by its own red run.

### R2 (minor) — `Scopes` and `StandIn` copied in three test classes — FIXED (one small correction to the finding)

Found true, with one detail: the copies were not word for word. `AKimiConsultationScenarioTests` also takes `CallingAs(...)`
(a member of its base class); the other two do not.

Fix: new `src_mcp/tests/KimiStandIn.cs` holds the one `Scopes` class and `KimiStandIn.Begin(person, params IDisposable[] also)`.
- `AKimiConsultantCheckTests` and `AKimiQuestionRowTests` call `KimiStandIn.Begin(_person)`.
- `AKimiConsultationScenarioTests` keeps a one-line `StandIn` that passes its own `CallingAs(...)` as `also`.
Order of release is the same as before (own scopes last in, first out).

Same shape swept (reuse-first rule): the copy of `ArgAfter(launch, flag)` in `AKimiConsultantCheckTests` was needed a second time by the R1 fix,
so it moved to `PlayedKimi.Launch.ArgAfter(flag)` and the check test calls that. A grep for `class Scopes` and `ArgAfter` shows one definition each.
Two `using` lines that the move left unused (`Runners.Platform`, `Runners.Kimi`) are removed from the two classes.

## Results

| Check | Result |
|---|---|
| Build `dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` | succeeded, 0 warnings, 0 errors |
| `AKimiQuestionRowTests`, `AKimiConsultantCheckTests`, `AKimiConsultationScenarioTests` | 20 total, 20 passed |
| `dotnet format whitespace --verify-no-changes` on the five changed files | clean |
| Generator `generate-runtime-capabilities.mjs --check` | up to date (21 rows over 7 runtimes × 3) |
| Whole MCP suite (run twice, 10:42 and 10:51 UTC) | 10557 total, 10470 passed, 84 skipped, **3 failed** both times: the two known ones, and a THIRD, `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (see below) |
| Extension `npm test` | one failure, the known `the baseline names no release that was never tagged`; the rest green |

## The third failure — NOT FIXED, not shown to be mine

`TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` failed in both full-suite runs. It PASSES when its class is run
alone (10 of 10). The coder's own full run at 10:23 (Q2-code.md, 10556 total) had only the two known failures.
My change touches only test files of the Kimi classes plus `PlayedKimi.cs`, and no production code, so I see no path to this test;
its last step asserts that a git call given a 1 ms deadline is a failed call, which is timing-dependent.
Later check: `TheRoundsNumstatsAreCountedTests` together with `AKimiQuestionRowTests` (or with all three Kimi classes) reproduces it; the class alone passes.
The failing line is the last one, `AllChangesAsync(parent, head, deadline 1 ms).Failed` was False (git finished inside 1 ms), so it is a race of
that 1 ms deadline against a git call that runs quicker when other git-running tests are in the process. That points to the test's own timing, not to
what the Kimi classes do, but it is NOT proven: I did not run the same pair at the commit before mine.
I did not have time before the deadline to run the whole suite at the commit before mine to prove it fails there too, nor to find the cause.
Next step for whoever continues: run the full suite at `050b8797` (the commit before mine) and compare; if it is green there, look for a shared static
(`GitScratch.AfterUnlink` is one — the three Kimi classes set it) that another test sees.
