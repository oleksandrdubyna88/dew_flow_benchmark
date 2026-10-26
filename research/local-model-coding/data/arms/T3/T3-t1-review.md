# T3-t1 — review notes

Reviewed commit `ea9badf0` (branch `tmp/T3-t1`) against `/home/jinx/git/coai-exp/vllm/work/T3/t1.md`.
Result: **no change needed. I committed nothing.**

## Findings (all "right as it was")

1. **Files in scope.** `git diff --name-only` lists exactly the six files of the task. Right.
2. **`RuntimeCapabilities.cs:84`.** `Runtimes` is the exact list of contract 1. Right.
3. **`runtime-capabilities.json`.** `runtimes` list, three kimi rows (values, order, key layout, 2-space indent) match
   contract 2 character for character. Right.
4. **`why` lines.** The kimi sentence is added after the vibe sentence, as its own string, with the contract text. The coder
   also moved the "`cells` may be empty…" sentence into its own string. The contract allows a split into a new string.
   Right.
5. **`capability-matrix-vectors.json`.** Three kimi lines after the vibe vectors, same text as the contract. Right.
6. **Generated TS.** `generate-runtime-capabilities.mjs --check` says "up to date" (21 rows, 7 runtimes). The diff is
   what the generator writes. Right.
7. **C# tests.** `KimiHasThreeRows_EachCitingItsCells` and `AdmissionFlagsBothRows` are as in the document (placed beside
   the Vibe test); `:273` list string updated. Right.
8. **TS tests.** `RUNTIMES` list, regex, and the new test `kimi rows admit flagged, web refuses naming its record` are as in
   the document, placed after the flagged-admission test. Right.
9. **Extra edit: `Records` table in `CapabilityMatrixTests.cs`** (`("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")`).
   The task document does not name it, but `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` fails without it. The file
   is in scope and the change is the smallest one. Right.
10. **Red reason.** I did not re-run the red myself (the time left was too short for a second build at `b4e2ea96`).
    The code notes record the red messages; they match the document except the FluentAssertions variable name (`none`
    instead of the full call) — a harmless difference, explained in the notes. Not verified by me.
11. **Commit trailer.** The commit says `Co-Authored-By: Claude Haiku 5.5`. This is the coder's model; I left it.
12. **Instruction loading.** `rules.mjs check` ran and `explain` resolved. I did not read every selected rule in full
    (deadline); no finding depends on a rule I skipped.

## Test results (this host: Linux)

- Build: `dotnet build … -m:4 --disable-build-servers` — 0 warnings, 0 errors.
- `CoaiMcp.Tests.CapabilityMatrixTests`: 39 of 39 passed. (On Linux the test host is `CoaiMcp.Tests`, not `.exe`.)
- `generate-runtime-capabilities.mjs --check`: up to date.
- `npm run typecheck`: clean. `npm run lint`: clean.
- `npm test`: only `the baseline names no release that was never tagged` fails (known).
- Whole MCP suite: not run (the review brief says only the task's classes). The coder's notes report two extra failures in
  `QuestionRowOnAgyScenarioTests` that pass alone; they are not in this task's area and I did not check them.
