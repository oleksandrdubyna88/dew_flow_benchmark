You are a fresh REVIEWER who also FIXES (Haiku). You did not write this code. You review stories **E5.5** and **E5.6** of
`todo/PLAN_vibe_and_kimi_runtimes_E5.md` in this repository, on branch `{{BRANCH}}`, and then you fix what you found. The work
so far is every commit after `b4e2ea96` (`git log b4e2ea96..HEAD`, `git diff b4e2ea96..HEAD`). You work alone and nobody
answers questions. After you, a separate reviewer will check the whole work again.

## Read first

1. The project instructions: `AGENTS.md` → `.agents/conventions/ENTRY.md` and `.agents/PROJECT.md`. Run the resolver with
   `--task audit --task implement --task test` and the changed files, and read the rules it selects.
2. In the E5 plan: §0, the E5.4, E5.5 and E5.6 sections, and *Where the parent plan and the code disagree*.
3. The coder's notes, if any: `{{CODE_NOTES}}`.

## Step 1 — review (do not edit anything yet)

Check, and settle every claim by opening the file:
- Each of the 12 scenarios (S5.5.1–S5.5.4, S5.6.1–S5.6.8): a test with that name exists, asserts what the table's *Then* says,
  and would fail without the change for the reason in the table. A test that can never fail is a defect.
- The stories' *Decisions*: the planner arm and its standings, no flags for kimi, the refusal of a plan with flags, the one-shot
  home disposed junction-first, the rows and vectors, the generated table made by its generator.
- Correctness, security (nothing admitted unconfined without its flag; nothing outside a test's own folder touched), the
  project's rules and conventions, simplicity, and scope (nothing outside the stories).

Write your findings to `{{NOTES}}` BEFORE you change any code: per finding an id (H1, H2, …), a severity (blocking / major /
minor), the file and line, one sentence, and the fix.

## Step 2 — fix

- Fix every **blocking** and **major** finding. Fix a **minor** one when it is cheap.
- A fix of a defect starts with a test that fails for the right reason, as the rules ask.
- Then: the build, the changed test classes, the whole MCP suite once, the format check, the generator check, and the TS tests.
  The commands are in `.agents/PROJECT.md`: always through `.agents/conventions/tools/low-priority.mjs`, build with
  `-m:4 --disable-build-servers`.
- Two tests fail on this host before any change and are not yours:
  `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. One extension test also fails at the start because this clone has no git tags: `the baseline names no release that was never tagged`.
- Out of scope: `research/`, live checks, merging `main`, any push, any pull request. Never run `kimi` or `vibe`.

## Commits, notes, deadline

- Commit as you go (conventional messages, files by name, never `git add -A`, never push).
- In `{{NOTES}}`, after each finding: fixed, not fixed (why), or wrong on a second look (why).
- **Deadline: {{DEADLINE}}.** At the deadline, stop, commit what you have, and write what remains in the notes.

Your final answer: a short report — your findings with what you did for each, and the final results of the build, the suite,
format, the generator check and the TS tests.
