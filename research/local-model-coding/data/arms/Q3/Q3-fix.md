# Q3 — fixes after the review of E5.5 and E5.6 (CODER, Sonnet)

Branch `exp/kimi-e56-skeleton-3`. No push. Commits after the review: `e4fd6904` (R2), `55394c56` (R5), `e2c02936` (plan: R2, R3, R4, R5, R6).
Review: `Q3-review.json`, verdict `fix-then-ready`, 1 major and 5 minor findings. No blocking finding.

## Per finding

| Id | Severity | Result |
|---|---|---|
| R1 | major | **NOT FIXED — out of scope by the task's own rule.** The fix is only in `research/module_tests.md`, `module_core.md`, `module_server.md`. The brief says `research/` is out of scope "as before", and the finding is correct (E5.5–E5.6 changed no `research/module_*.md`). The plan owner, or a task that opens `research/`, must add: catalogue rows for `AKimiConsultantCheckTests` (S5.5.1–S5.5.4), `AKimiQuestionRowTests` (S5.6.7) and the Kimi rows of `CapabilityMatrixTests` / `ConfinementPlannerTests`, each with its red or mutation red (they are in plan deviations 58, 60, 63, 66); a "Kimi's arm" paragraph in `module_core.md`; the Kimi check and row in `module_server.md`. This is the one open major finding; `common.testing` DoD says `research/module_tests.md` must name every flow added. |
| R2 | minor | **FIXED.** The `unconfined` caveat is now *unconfined: can read this machine — no flag bounds what it reads*, in both twins (`CapabilityMatrix.Caveat`, `capabilityAdmission.ts` `CAVEATS`). No test pinned the old sentence (they match only `can read this machine`). Red first: `TheUnconfinedCaveat_NamesNoShell_ForKimiRefusesEveryShellCommand` failed with *Did not expect AdmissionFlag.Unconfined.Caveat() to contain equivalent of "shell" … but found "unconfined: can read this machine — a shell is always there, and no flag removes it"*; green after. The TypeScript twin's `doesNotMatch(/shell/iu)` was added in the same step; **I did not watch the TypeScript line red on its own** (the C# red is the same sentence). Not changed: the legend line `(a shell is always there)` in `shared/runtime-capabilities.json` and the codex row note — they describe codex and are true. |
| R3 | minor | **FIXED (note).** Deviation 63 records the reds of S5.6.1, S5.6.4 and S5.6.8 that the earlier "all production at `b4e2ea96`" run had already shown (they are in `Q3-code.md`, not in the plan). I did not re-run them. |
| R4 | minor | **PARTLY — recorded, authority OPEN.** Deviation 65 states that E5.5–E5.6 were built on Sonnet while §0/§0.2 say Opus. I cannot write the authority for the change: none is written in the repository. The plan owner must confirm the change or have the stories rebuilt on Opus (`subagent-models.md`). |
| R5 | minor | **FIXED.** `PlayedKimi.Launch` has a new field `CredentialsLinked`, set when the shim starts (`<--home>/home/credentials` is a link, read through `KimiPlaces.ForShim(...).Link`). S5.5.1, S5.5.2 (both cases), S5.5.3, S5.5.4 and S5.6.7 (three cases) assert it. Teeth: with `KimiHome.Fill` making a real folder instead of the link, all 5 check tests go red — but at `ContainSingle` (*Expected played.Shims to contain a single item … but the collection is empty*), before the new assertion, because the launch control refuses a home without the link and no shim starts. So the new assertion is the record that the home held the link, not the first line to fail. Said so in deviation 66. Production file put back (`git checkout`), rebuilt. |
| R6 | minor | **FIXED.** Added `TheVectorsCoverKimi` (one vector per capability for kimi, two admitted `unconfined`, web refused) and deviation 64 (S5.6.2 is two tests; the existing one was red with the vectors out). The new test was not watched red on its own; it passes with the vectors in and fails on its first assertion if a pair is missing. |

No finding was wrong.

## Results
HEAD `e2c02936`, Debug, everything through `low-priority.mjs`, build `-m:4 --disable-build-servers`.

* **Build** (solution): 0 warnings, 0 errors.
* **Changed classes:** `AKimiConsultantCheckTests` 5/5, `AKimiQuestionRowTests` 3/3, `CapabilityMatrixTests` 41 cases (the caveat test red before the fix, green after).
* **Whole MCP suite:** total 10557, succeeded 10470, skipped 84, **failed 3**:
  * `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…` — the two known ones.
  * `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — **new in this run, not mine.** It gives git a 1 ms deadline and expects a failure. It fails alone on my HEAD (*State "recorded", expected "unavailable: git…"*) and **also fails at `6ceb9f2c`, the commit before my fixes, built in a separate worktree** (there on the next line: *AllChangesAsync … Failed to be True, but found False*). It depends on how fast git runs on this host; the earlier run (Q3-code.md) did not hit it. No file I changed is in its path. Not fixed, out of scope. The worktree was removed.
* **Format:** `dotnet format` on the tests project and on the core project: clean. Solution-wide: nothing printed except the known `IL2026`/`IL3050` AOT lines of `src_server` (as in Q3-code.md; exit code 2 there with or without my change).
* **Generator check** (`generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
* **TS tests** (`npm test`): 1 failure, the known `the baseline names no release that was never tagged`; the Kimi and caveat tests are green.
* `plan-lifecycle.mjs`: clean.

## What remains
* R1 (major) — needs a task that may edit `research/`. See the table.
* R4 — the plan owner's confirmation of the Sonnet coder (deviation 65).
* The numstat timing test above (host-dependent, also red at the base commit).
