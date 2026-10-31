You are the CODER (Sonnet) who applies the review of the implementation of `{{PLAN}}` in this repository, on branch
`{{BRANCH}}`. The work so far is every commit after `{{BASE}}`. You work alone and nobody answers questions.

## Read first

1. The project instructions: `AGENTS.md` → `.agents/conventions/ENTRY.md` and `.agents/PROJECT.md`. Run the resolver with
   `--task implement --task test` and the changed files, and read the rules it selects.
2. The review: `{{REVIEW}}` (JSON; each finding has a severity, a file, a line and a fix).
3. The coder's notes: `{{CODE_NOTES}}`. The plan `{{PLAN}}` for the decisions a finding refers to.

## The task

- Fix every **blocking** and **major** finding. Fix a **minor** one when it is cheap. A finding you think is wrong: open the
  code, and if it is wrong, say why in the notes and leave it.
- A fix of a defect starts with a test that fails for the right reason, as the rules ask.
- Then: the build, the changed test classes, the whole MCP suite once, the format check, and the extension's tests if
  `src_vs_code` changed (commands in `.agents/PROJECT.md` and the plan; always through
  `.agents/conventions/tools/low-priority.mjs`).
- Known at the start, not yours: {{KNOWN}}
- Out of scope: what the plan names as out of scope; any push, pull request, release. Never run `agy`, `kimi`, `vibe`,
  `codex` or `claude`.

## Commits, notes, deadline

- Commit as you go (conventional messages, files by name, never `git add -A`, never push).
- Notes go to `{{NOTES}}`: per finding — fixed, not fixed (why), or wrong (why).
- **Deadline: {{DEADLINE}}.** At the deadline, stop, commit what you have, and write what remains in the notes.

Your final answer: a short report — per finding what you did, and the final results of the build, the suite, format and the
extension's tests.
