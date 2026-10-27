# D2 — coder notes: E5.5 and E5.6 (Haiku, branch exp/kimi-e56-haiku-direct-2)

Start: b4e2ea96. Known reds at start (not mine): KimiRuntimeTests.ALaunchThatNamesItsHome_…, KimiHomeTests.AHomeWhoseDataPathPasses260_…, and the extension's "the baseline names no release that was never tagged".

## Commits

- 701193a7 feat(kimi): the question consultant's Kimi rows — table, planner arm, refusal of flags (E5.6)
- d98dfe01 test(kimi): the consultant check on a Kimi row (E5.5, S5.5.1, S5.5.2, S5.5.4)
- (this note and the plan edit are in the next commit)

## Scenario status

| # | Scenario | Red seen? | Green? | Note |
|---|---|---|---|---|
| S5.5.1 | a Kimi check runs in a copy, on a one-shot home | green on arrival | yes | table red predates E5.4; teeth: HomesFolder broken → red |
| S5.5.2 | after the check the home is gone; person's files intact | green on arrival | yes | teeth as S5.5.1. The `GitScratch.AfterUnlink` recorder is NOT written |
| S5.5.3 | check cancelled mid-turn still disposes its home | not written | no | remaining |
| S5.5.4 | rootless check keeps its home in scratch, scratch gone after | green on arrival | yes | pins the fallback; not red by design |
| S5.6.1 | kimi has three rows, each citing its cells | yes (*Expected … not to be <null>*) | yes | |
| S5.6.2 | vectors cover kimi | not watched red alone | yes | see H5 |
| S5.6.3 | planner arm: copy for disk, empty for none, refuses web | yes (*found Refused*, the default arm was not reached) | yes | in ConfinementPlannerTests, see H3 |
| S5.6.4 | both kimi rows admitted, flagged unconfined | yes (*found Refused … not a runtime*) | yes | |
| S5.6.5 | a kimi question launch is the shim request on the row's home | yes (*found Refused* from the planner) | yes | see H4 |
| S5.6.6 | a plan with flags is refused for kimi | yes (red at the Planned helper, H4) | yes | the refusal itself is in KimiConsultant |
| S5.6.7 | a disk row runs through QuestionRowLaunch, leaves nothing behind (three arms) | not written | no | remaining |
| S5.6.8 | TS: kimi rows admit flagged; web refuses naming its record | yes (*'kimi' is not a runtime …*) | yes | |

## Decisions I took

1. **Order of red.** I wrote the table code before the tests by mistake. I then stashed the table files, wrote the tests against the baseline, watched them red, and popped the stash. The stash was my own and was restored cleanly.
2. **Branch.** The brief names exp/kimi-e56-haiku-direct-2 in this clone. I committed there, not in a new worktree. The rule about a worktree per task is not applied, because the brief names this clone and its branch.
3. **Coai review gate: not called.** The brief says the review is by Opus later, and the gate sends diffs to other vendors' models. I did not run it.
4. **Haiku as coder.** The subagent-models rule says "Haiku has no role". The brief assigns me as the Haiku coder for this experiment. I followed the brief and say so here.
5. **`none` and `disk` standings.** Both `unconfined`, as E5.6 decision 2 says, with the web sentence kept until M-K17 is recorded.
6. **Planned flags.** `KimiConsultant.Build` refuses any planned launch with flags (E5.6 decision 3). The planner gives Kimi no flags.

## Deviations (also in the plan, *E5.5–E5.6 as built*, H1–H9)

H1 seed `runtimes` key; H2 kimi record in the citation registry; H3 tests in ConfinementPlannerTests, not a new AKimiQuestionRowTests; H4 red reasons for S5.6.5/6; H5 S5.6.2 red not isolated; H6 S5.5.1/2/4 green on arrival with teeth; H7 the refusal of flags; H8 not written: S5.5.3, AfterUnlink recorder, S5.6.7; H9 web sentence kept.

## Checks

- dotnet format: the three changed projects (CoaiMcp.Core, CoaiMcp.Runners, CoaiMcp.Tests) verify clean (exit 0 each). The solution-level `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` exits 2 on IL analyzer errors in `src_server` (ReviewEndpoints.cs, CatalogEndpoints.cs, UsageEndpoints.cs). I did not touch src_server. Log: notes/D2-format.log.
- generator: `node scripts/generate-runtime-capabilities.mjs` written; `--check` exit 0 (21 rows over 7 runtimes × 3 capabilities).
- TS: typecheck exit 0, lint exit 0. capabilityAdmission.test.js all green (kimi cases shown as ✔).
- Plan lifecycle: clean.

## Not done (remaining)

- S5.5.3, the AfterUnlink recorder of S5.5.2, S5.6.7 (three arms).
- The live checks (J2) and RESULTS §31: out of scope.
- research/module_*.md and architecture.md: out of scope per the brief. The knowledge-base rule asks for them after a code change, so the owner must update them.
- The `why` text of the vectors file still says the vibe rows only; kimi rows carry their own why.
- Full suite and npm test: see the results below.

## Results (observed)

- Full MCP suite (`CoaiMcp.Tests`, after the last code change): 10463 passed, 2 failed. The two failures are the ones known at the start: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`. Log: notes/D2-full-suite.log.
- Extension `npm test`: 6290 tests, 6270 passed, 1 failed. The failure is `the baseline names no release that was never tagged`, known at the start. Log: notes/D2-npm-test.log.
- Typecheck and lint: exit 0. Generator `--check`: exit 0. Plan lifecycle: clean.
- Format: CoaiMcp.Core, CoaiMcp.Runners and CoaiMcp.Tests verify clean. The solution-level check exits 2 on pre-existing src_server analyzer errors.

## Commits

- 701193a7 feat(kimi): E5.6 table, planner arm, refusal of flags
- d98dfe01 test(kimi): E5.5 S5.5.1, S5.5.2, S5.5.4
- 31e866ef docs(plan): E5.5–E5.6 status and deviations H1–H9

Nothing pushed. No PR. Research docs not updated (out of scope); the knowledge-base rule still applies to the owner.
