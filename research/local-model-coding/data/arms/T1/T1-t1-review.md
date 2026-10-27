# T1-t1 — review notes

Reviewed commit `e7d64be6` (branch `tmp/T1-t1`) against `b4e2ea96`. Result: no change needed, nothing committed.

## Findings (all right as they were)
1. **Files changed** — exactly the six the task names. Right.
2. **Contract 1** (`RuntimeCapabilities.cs` `Runtimes` ends with `"kimi"`) — exact. Right.
3. **Contract 2** (`runtime-capabilities.json`) — `runtimes`, the three `why` lines, and the three kimi rows are the same as the document, character by character (checked in the diff). Right.
4. **Contract 3** (vectors) — three lines in the right place, before `gemini`, same spacing. Right.
5. **Contract 4** (generated TS) — `--check` says "up to date" (21 rows, 7 runtimes). The file has no hand edit. Right.
6. **Contract 5** (C# facts) — both facts are the same as the document (names, assertions, summaries). Right. The coder put them after the vibe test, not at the class end: the document says both places; the more specific one is fine.
7. **Contract 6** (TS test) — the two line changes and the new test are exact. Right. `admit` returns `admitted`, `reason`, `caveat`; names match.
8. **Deviation: `Records` list** in `CapabilityMatrixTests.cs` got `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")`. Right: the existing test `EveryRow_CitesTheRecord_…` needs it once kimi rows cite a new record; test data only, in an allowed file, and the doc comment is updated.
9. **Gap in the coder's notes: TS red was not observed.** I did it: I put the old generated file back, compiled, and ran `capabilityAdmission.test.js`. It fails for the document's reasons: `Expected values to be strictly deep-equal` (runtimes list), `'kimi' is not a runtime the question consultant can launch …` (new test), and the gemini regex (list lacks `kimi`). Then I restored the file (`git checkout`); tree is clean.

## Test results (this host: Linux, WSL2)
- Build: succeeded, 0 warnings, 0 errors (`-m:4 --disable-build-servers`).
- `CoaiMcp.Tests.CapabilityMatrixTests`: 39 passed, 0 failed, 0 skipped.
- `CoaiMcp.Tests.ConfinementPlannerTests`: 31 passed, 0 failed, 0 skipped.
- Generator `--check`: up to date.
- Extension `npm test`: 6290 tests, 6270 pass, 1 fail, 19 skipped. The one failure is the known `the baseline names no release that was never tagged`.
- The two known MCP failures (`KimiRuntimeTests…`, `KimiHomeTests…`) were not run; not this task's.
