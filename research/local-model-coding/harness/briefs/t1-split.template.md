You are the JUDGE (Opus) who splits stories **E5.5** and **E5.6** of `todo/PLAN_vibe_and_kimi_runtimes_E5.md` into
TASKS for other coders. The working tree is a clone at commit `b4e2ea96` on branch `{{BRANCH}}`. You work alone and nobody
answers questions: decide, and write down what you decided. You write NO code into the repository: you write task
documents outside it.

## Read first

1. The project instructions: `AGENTS.md` → `.agents/conventions/ENTRY.md` and `.agents/PROJECT.md`. Run the resolver as
   ENTRY.md says, with `--task plan --task implement --task test` and the files the stories touch, and read the rules it
   selects.
2. In `todo/PLAN_vibe_and_kimi_runtimes_E5.md`: §0 *How to read this*, the **E5.4** section (what `KimiConsultant` already
   does), the **E5.5** and **E5.6** sections (the spec), and *Where the parent plan and the code disagree* with the *as
   built* notes.
3. The code those sections name. Open every file and line you will cite: a task's contract must match the code as it is.

## Who works after you

Each task goes to **a coder that reads ONLY its task document and the files that document names** (a smaller, cheaper
model, working as an agent in the clone: it can read files, edit, build and run tests). Then a second coder reviews that
task's diff against the task document and fixes it. Then all tasks together get the usual Opus review and fixes.

So a task document must leave the coder **nothing to design**: the architecture, every contract and every name are yours.

## What you produce

Between **4 and 6 tasks** that together deliver all 12 scenarios (S5.5.1–S5.5.4, S5.6.1–S5.6.8) and the stories' *Decisions*.

1. `/home/jinx/git/coai-exp/vllm/work/{{ARM}}/tasks.json`:

   ```json
   {"tasks": [{"id": "t1", "title": "one line", "scenarios": ["S5.6.1", "S5.6.4"],
               "files": ["shared/runtime-capabilities.json", "src_mcp/tests/CapabilityMatrixTests.cs"],
               "depends": [], "brief": "t1.md"}]}
   ```

   - `files`: EVERY file the task may create or change. **Each file belongs to exactly one task.** Tasks whose files do
     not overlap and whose `depends` are done run AT THE SAME TIME, in separate worktrees, so an unlisted file or a file
     listed twice breaks the run.
   - `depends`: the ids whose result this task needs to build or test against (for example the production method a test
     calls). No cycles.
   - The E5 plan (`todo/PLAN_vibe_and_kimi_runtimes_E5.md`: its status line and a short list of deviations) belongs to
     the LAST task, which depends on all the others.
   - Generated files (`src_vs_code/src/runtimeCapabilities.generated.ts`) belong to the task that changes their source,
     and its brief says to regenerate them with the generator, never by hand.

2. `/home/jinx/git/coai-exp/vllm/work/{{ARM}}/<id>.md` per task, with these sections:

   - **Goal** — one paragraph, and the scenarios it delivers.
   - **Contracts (exact):** for every method, constructor, property, record, DTO, enum member, constant, JSON row or vector
     the task adds or changes: its FULL signature or shape — namespace, visibility, `static`/`sealed`, parameter and return
     types, field names and types, attribute lines, exact JSON keys and values. For every test: its class, its exact
     method name from the scenario table, its attributes, and what it asserts (the table's *Then*, with the exact
     assertion calls).
   - **Existing code to use (exact):** the signatures, copied from the repository with file and line, of every existing
     type, method, helper and fixture the task needs — the coder will not search for them. Name what NOT to use where a
     similar name would mislead.
   - **Steps** — the order of work, red first: the test(s), seen failing against the code at `b4e2ea96` for the table's
     reason (its last column), then the production code, then green.
   - **Commands** — build, the exact test class(es) to run (`--filter-class 'CoaiMcp.Tests.<Class>'`), and any
     generator or `npm test` the task needs; always through `.agents/conventions/tools/low-priority.mjs`, the build with
     `-m:4 --disable-build-servers` (see `.agents/PROJECT.md`).
   - **Done when** — checkable statements.
   - **Out of bounds** — files and changes this task must not touch.

## Known at the start

This host is Linux (WSL2); tests marked Windows-only skip. Two MCP tests already FAIL at `b4e2ea96` on this host:
`KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and
`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. One extension test also fails at the start because the
clone has no git tags: `the baseline names no release that was never tagged`. Say so in each task's brief where it matters.

## Deadline and answer

**Deadline: {{DEADLINE}}.** Do not commit anything to the repository. Your final answer: the task list (id, title,
scenarios, files, depends) and the decisions you took that a task document does not show.
