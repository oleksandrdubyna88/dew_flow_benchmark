You are the JUDGE (Opus) who reviews stories **E5.5** and **E5.6** of `todo/PLAN_vibe_and_kimi_runtimes_E5.md` in this
repository. The coder's work is every commit after `b4e2ea96` on the current branch (`git log b4e2ea96..HEAD`,
`git diff b4e2ea96..HEAD`). You are READ-ONLY: never edit, create or delete a file in the repository, and never commit.

## Read first

1. The project instructions: `AGENTS.md` → `.agents/conventions/ENTRY.md` and `.agents/PROJECT.md`. Run the resolver with
   `--task audit` and the changed files, and read the rules it selects.
2. In the E5 plan: §0, the E5.4, E5.5 and E5.6 sections, and *Where the parent plan and the code disagree*.

## What to check

- Each of the 12 scenarios (S5.5.1–S5.5.4, S5.6.1–S5.6.8): a test with that name exists, asserts what the table's *Then* says,
  and would fail without the change for the reason in the table.
- The stories' *Decisions*: the planner arm and its standings, no flags for kimi, the refusal of a plan with flags, the one-shot
  home disposed junction-first, the rows and vectors, the generated table made by its generator.
- Correctness, security (nothing admitted unconfined without its flag; nothing outside a test's own folder touched), the
  project's rules and conventions, simplicity, and scope (nothing outside the stories).
- You may build and run tests (commands in `.agents/PROJECT.md`; run them through `.agents/conventions/tools/low-priority.mjs`).
  Two tests fail on this host before any change and are not the coder's:
  `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. One extension test also fails at the start because this clone has no git tags: `the baseline names no release that was never tagged`.

Settle every claim about code by opening the file. Report only findings you checked.

## Your answer

Your final message is ONLY this JSON, nothing before or after it:

```json
{"verdict": "ready | fix-then-ready | not-ready",
 "findings": [{"id": "R1", "severity": "blocking | major | minor", "file": "path", "line": 1,
               "summary": "one sentence", "fix": "what to change"}]}
```
