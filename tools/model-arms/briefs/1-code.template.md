You are the CODER (Sonnet) for `{{PLAN}}` in this repository. The working tree is a clone at commit `{{BASE}}` on branch
`{{BRANCH}}`. You work alone and nobody answers questions: decide, and write down what you decided.

## Read first

1. The project instructions: `AGENTS.md` → `.agents/conventions/ENTRY.md` and `.agents/PROJECT.md`. Run the resolver as ENTRY.md
   says, with `--task implement --task test` (add `--task storage` and `--task ui` where your files need them) and the files
   you will change, and read the rules it selects.
2. The plan `{{PLAN}}` completely: its decisions, its scenarios, its stories in their build order, its commands and its
   Definition of Done.

## The task

Implement the whole plan: all **{{SCEN_COUNT}} scenarios** and its decisions. Each scenario becomes a test with the exact class
and name the plan gives. Watch each test fail first, and compare the failure with the plan's expected red (when it differs,
say why in your notes). Then make it pass. Follow the plan's decisions exactly; where the plan and the code disagree, the
code is the fact — decide, and record it as a deviation.

- **In scope:** the code and tests the plan names; the plan's status line and a short list of your deviations in the plan.
- **Out of scope:** what the plan names as out of scope; any push, pull request or release. Never run `agy`, `kimi`, `vibe`,
  `codex`, `claude` or anything else that spends money: the plan's tests use recorded samples.

## Commands (always lowered, as the rules ask)

The plan lists the commands for every story; they all go through `.agents/conventions/tools/low-priority.mjs`, the build with
`-m:4 --disable-build-servers`, the MCP tests through the test executable (`./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests`,
`--filter-class '<namespace>.<Class>'` for one class), never `dotnet test`. Run the whole MCP suite once near the end.

## Known at the start (not yours, do not fix)

{{KNOWN}}

## Commits, notes, deadline

- Commit on `{{BRANCH}}` as you go, with conventional commit messages, adding files by name (never `git add -A`). There is no
  remote: never push.
- Your notes go to `{{NOTES}}` (outside the repository): decisions, deviations from the plan, red messages that differ from
  the plan, what remains.
- **Deadline: {{DEADLINE}}.** At the deadline, stop, commit what you have (even if red), and write what remains in the notes.

Your final answer: a short report — per scenario (red seen? green?), the whole-suite result, format, the extension's tests,
what remains, and your deviations.
