# S2-t2 — code notes (E5.6 S5.6.8: kimi in the admission test)

Worktree: /home/jinx/git/coai-exp/wt/S2-t2 (branch tmp/S2-t2)
Commit: 36b07c6e — `test(kimi): the admission test knows the kimi runtime — S5.6.8 (E5.6 t2)`
File changed: src_vs_code/src/test/capabilityAdmission.test.ts only (+18 / −2). Not pushed.

## What was done

1. Line 143: RUNTIMES expectation now ends with `'kimi'`.
2. Line 166: refusal regex now `/claude, codex, antigravity, local, api, vibe, kimi/u`.
3. New test `kimi rows admit flagged, web refuses naming its record` placed after the test at line 153, exact title from the document (S5.6.8). Contents as in the document.

## Red first

- Before any edit: `node --test out/test/capabilityAdmission.test.js` failed on the RUNTIMES assertion:
  `actual: [ 'claude', …, 'vibe', 'kimi' ]` vs `expected: [ 'claude', …, 'vibe' ]`. Same reason as the document.
- Teeth check: changed the new test's flag expectation `'unconfined'` → `'confined'`, compiled, ran.
  Result: `AssertionError` on `kimi × none: Grep and Glob read the disk directly (M-K6)`.
  Difference from the document: the document expected the default text `Expected values to be strictly equal`.
  The test passes a custom message, so that custom text is what prints. Still red for the right reason.
  Reverted; recompiled before the green run.

## Results (after the change)

- `npm run compile`: exit 0.
- `capabilityAdmission.test.js` alone: 31 tests, 31 pass, 0 fail. New test listed by exact title.
- `npm run typecheck`: exit 0.
- `npm run lint`: exit 0.
- `npm test`: exit 1 for one reason only. Summary `ℹ fail 1`. The only failure is
  `✖ the baseline names no release that was never tagged` (known, the clone has no git tags).
- Generator `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: exit 0
  (`up to date … 21 rows over 7 runtimes × 3 capabilities`).
- `git status`: only the test file changed; `src_vs_code/node_modules` was untracked before I started and is not mine.

## Not seen / not done

- The document says two MCP tests also fail on this host. They did not appear in this `npm test` run
  (`ℹ fail 1` total). I did not run any MCP test separately, so I cannot confirm them here.
- Did not edit `runtimeCapabilities.generated.ts`, `capabilityAdmission.ts`, `shared/*`, or any C# file.

## Conflict in the instructions

- The document's "Out of bounds" section says "Do not commit."
- My brief says "Commit with a conventional message, adding your files by name."
- I followed the brief and committed the test file only (36b07c6e). If the document's rule is the one to keep,
  the commit can be undone with `git reset --soft HEAD~1`; the change stays in the working tree.
