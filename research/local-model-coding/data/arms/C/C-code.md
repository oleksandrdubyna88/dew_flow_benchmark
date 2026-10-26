# Arm C — coder notes, E5.5 and E5.6 (Sonnet, 2026-10-25)

Branch `exp/kimi-e56-classic`, from `b4e2ea96`. Host: Linux (WSL2). No push, no `kimi`, no `vibe`.

## Commits
- `ddd8e2f0` test(kimi): the consultant check — S5.5.1–S5.5.4 (E5.5). No product code.
- `8d2154d7` feat(kimi): the question consultant's Kimi row (E5.6).
- one more: the plan's status line and deviations 50–57.

## Decisions
- E5.5 needs no product code: E5.3 g and E5.4 already gave the check everything. I proved the path with tests and gave them teeth by breaking the
  deciding lines (see the plan, deviation 50).
- `kimi` is the LAST runtime in `RuntimeCapabilities.Runtimes` and in the seed (the file's order = the hand list's order).
- The three rows cite the cells in the spec, version 2.1.1, date 2026-10-13 (the date of M-K6). `none` is `unconfined`, as the spec says.
- The planner arm has no flags, ever. `KimiConsultant.MustCarryNoFlag` refuses a plan with `Flags`, `Leading` or `AddDirs`.
- S5.6.7 is one Theory of three endings (answered, refused, cancelled).

## Red messages that differ from the tables
- S5.5.1, S5.5.2, S5.5.4: green on arrival (E5.3/E5.4 landed). Reds come from teeth runs; the recorder red matches the table.
- S5.5.3: the record is `state: failed`, `failureKind: cancelled`. The test asserts `failureKind`.
- S5.6.3, S5.6.5, S5.6.6: the `Plan(...)` helper asserts `Planned`, so the first red is "Expected type to be Confinement+Planned, but found Confinement+Refused".
  Teeth runs gave the table's wording for S5.6.5 (working directory) and S5.6.6 (no exception).
- S5.6.7: green on arrival. Red only with the junction-first disposal broken.
- S5.6.2: needed the seed rows first; its sibling `EveryRow_CitesTheRecord…` also went red and needed a `Records` entry for the Kimi record (deviation 53).
- S5.6.8 (TS): red as the table says (runtimes list; then "'kimi' is not a runtime the question consultant can launch").

## Not mine / environment
- `dotnet format --verify-no-changes` prints IL2026/IL3050 AOT errors from `src_server` on this host. None are in files I touched. No whitespace or style line.
- Known failing at start (not touched): the two Kimi tests named in the task, and the extension tag test.

## Final runs (after the resume)
- MCP whole suite: 10555 total, 10468 passed, 84 skipped, 3 failed: the two known Kimi tests + `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`
  (also red at b4e2ea96 on this host; checked in a clean worktree, now removed).
- Extension `npm test`: 6290 tests, 1 failed (the known tag test). Generator `--check`: clean. `dotnet format`: no style line (only src_server AOT noise). plan-lifecycle: clean.

## What remains
- Review by Opus (the plan says so). Live checks J2 and M-K17: not run (out of scope).
- `research/module_*.md` and RESULTS: out of scope, not updated.
