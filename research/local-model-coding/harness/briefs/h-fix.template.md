You are the CODER (Haiku) who applies the review of stories **E5.5** and **E5.6** of
`todo/PLAN_vibe_and_kimi_runtimes_E5.md` in this repository, on branch `{{BRANCH}}`. The work so far is every commit after
`b4e2ea96`. You work alone and nobody answers questions.

## Read first

1. The project instructions: `AGENTS.md` → `.agents/conventions/ENTRY.md` and `.agents/PROJECT.md`. Run the resolver with
   `--task implement --task test` and the files you will change, and read the rules it selects.
2. The review: `{{REVIEW}}` (JSON; each finding has a severity, a file, a line and a fix).
3. The coder's notes: `{{CODE_NOTES}}`.

## The task

- Fix every **blocking** and **major** finding. Fix a **minor** one when it is cheap. Check each finding against the code first;
  if a finding is wrong, do not apply it, and write why.
- A fix of a defect starts with a test that fails for the right reason, as the rules ask.
- Then: the build, the changed test classes, the whole MCP suite once, the format check, the generator check, and the TS tests.
  The commands are in `.agents/PROJECT.md` and in the coder's brief style: always through
  `.agents/conventions/tools/low-priority.mjs`, build with `-m:4 --disable-build-servers`.
- Two tests fail on this host before any change and are not yours:
  `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and
  `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. One extension test also fails at the start because this clone has no git tags: `the baseline names no release that was never tagged`.
- Out of scope as before: `research/`, live checks, merging `main`, any push, any pull request. Never run `kimi` or `vibe`.

## Commits, notes, deadline

- Commit as you go (conventional messages, files by name, never `git add -A`, never push).
- Notes go to `{{NOTES}}`: per finding — fixed, not fixed (why), or wrong (why).
- **Deadline: {{DEADLINE}}.** At the deadline, stop, commit what you have, and write what remains in the notes.

Your final answer: a short report — per finding (fixed / not fixed / wrong), and the final results of the build, the suite, format,
the generator check and the TS tests.
