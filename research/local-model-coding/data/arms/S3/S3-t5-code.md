# S3-t5 — code notes (the E5 plan: status line, E5.5/E5.6 as built)

Commit: 4bf1b0bc on exp/kimi-e56-sonnet-split-haiku-3 (not pushed).

## What I did

1. Read t1–t4 notes and checked the merged code: three `kimi` rows in `runtime-capabilities.json`, three kimi vectors, `RuntimeCapabilities.Runtimes` lists `kimi`, `ConfinementPlanner` has `"kimi" => Kimi(...)`, `KimiConsultant.MustCarryNoFlags` is called from `Build`, `AKimiConsultantCheckTests.cs` and `AKimiQuestionRowTests.cs` exist.
2. Status line (line 3) changed per contract 1; rest of the paragraph kept.
3. Added `### E5.5–E5.6 as built (2026-10-16)` after `### E5.7 as built`, before `## Plan gate`, numbered 50–54.
4. `node .agents/conventions/tools/plan-lifecycle.mjs` run from the repo root: "clean", exit 0.
   (A first run from the notes directory printed "nothing to check" and exit 0; that was not a real pass, so I re-ran from the repo root.)
5. `git status --short` showed only the plan file. Committed it by name.

## Tests

No code or test changed; no build or test run was needed for a document-only task.

## Red messages that differed from the document

Not applicable (no tests written).

## Deviations recorded in the plan (item 54)

Copied from t1–t4 notes. Summary: t1 S5.6.4 red wording; t1 `Records` list data row; t2 optional S5.6.5 assertion not added; t3 unused `Other` property; t4 fixture folders via `TempDir`.

## Left undone

- Nothing in the t5 scope.
- Definition of Done boxes left unchanged, as the contract says (live runs of RESULTS §31 still owed).
- Not pushed.
