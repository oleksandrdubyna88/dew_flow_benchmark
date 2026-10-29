# PLAN — minigit on two stacks: C# / .NET 10 against Node 26 + TypeScript 7, bare and under the family's full process

> Status: **in progress — E1 (the measurement instruments) implemented 2026-10-29; E2–E4 open.** Scope: a new spike `spikes/minigit-bench/` in this repository
> (reusing `spikes/policy-ab-quick/`), one public GitHub **fork** `oleksandrdubyna88/minigit-bench` of
> `mame/ai-coding-lang-bench` with four template branches (one per arm × stack), two token rows and one snapshot branch in `dew_flow_conventions`, and the series'
> record under `research/minigit/`. Nothing in the C# `bench` product changes.

## 1. The question, before any solution

A widely shared result says Claude- and Codex-class coding agents are slower and dearer in C# than in TypeScript or
JavaScript on a small greenfield task ("minigit"). The C# claim comes from `banteg/minigit-bench` @ `83644e0` (a fork of
`mame/ai-coding-lang-bench`, which had no C#): Codex CLI 0.111.0, gpt-5.4, `fast` tier, 3 trials per language, Node
v25.8.0, .NET SDK 10.0.103. The operator considers it invalid. Checked here, by reading its code and re-pricing its
`results/results.json` (§2), it is invalid in its cost and weak in its quality signal.

The question this plan answers, and nothing wider:

> On the minigit task (SPEC-v1 then SPEC-v2), with **Claude Code** (Opus team lead, effort `high`), how do **C# 14 /
> .NET 10** and **TypeScript 7.0.2 on Node 26** compare in **correctly counted** cost, decomposed wall time, and
> **held-out** correctness — (a) for a bare agent with the upstream prompt, and (b) for the same task done end to end
> under this family's full process (conventions, coai gates, Sonnet coders, PR, CI, merge; no CodeRabbit, D19)?

It is not a replication of banteg's run (other agent, other model, other date); only ratios are compared with it, with
that caveat in the same sentence.

## 2. What was verified about the original (2026-10-29, local read-only clones, never executed)

| # | Defect | Evidence | Effect |
|---|---|---|---|
| O1 | Cached input billed twice: OpenAI `input_tokens` already contains `cached_input_tokens`, and the formula prices all input at the full rate and then the cached part again | `benchmark.rb:162-167` (`codex_cost_usd`), `:213-215` (sums) | Re-priced from its own tokens at its own `fast` rates: C# $2.75 → **$0.60**, TS $1.61 → **$0.46**, JS $1.35 → **$0.37**; C#/TS 1.71× → **1.32×** |
| O2 | The timer covers the agent's whole process, including every build and test run it chose to make | `benchmark.rb:243-245` | C# launch shape (dotnet run vs a built binary) changes the number; the generated code was never published, so which one ran is unknowable |
| O3 | Two tests pass whether the parent pointer is right or wrong | `test-v2.sh:319-324`, `:381-385` (both branches call `pass`) | "All tests pass" does not establish equal completeness |
| O4 | A build failure is swallowed | `test-v1.sh:26`, `test-v2.sh:26`: `make -s 2>/dev/null \|\| true`, same for `build.sh` | A broken build surfaces as test failures, or as nothing |
| O5 | The test script lives in the agent's workspace and is not restored before scoring | `benchmark.rb:353-354` copies it in; `run_tests` runs the copy | The agent could edit the test and nothing would notice |
| O6 | `num_turns` is 1 in every row | one `turn.completed` per `codex exec` | The "turns" columns measure nothing |
| O7 | `set -e` ends the script at the first unguarded failing command, reporting 0/0 | `test-v2.sh:2` | A crash reads as "no tests", not as failures |

Re-pricing reproduced to the cent (C# six blocks Σ/3 = 2.74535 published, 0.6037 corrected). This becomes the harness's
first unit test (§12, T-A1).

## 3. Decisions

| # | Decision | Source |
|---|---|---|
| D1 | Two arms: **PROCESS** (full family process) and **BARE** (Claude Code alone, upstream prompt) | operator: bare arm yes |
| D2 | 3 runs per stack per arm = 12 cells, each cell two sessions (v1, then v2 from that cell's v1 result) | operator: 3 runs |
| D3 | Process arm works in **one public GitHub fork**, `oleksandrdubyna88/minigit-bench`, with the real PR → CI → `sleep 300` → merge (no CodeRabbit, D19) cycle. Four template branches, one per arm × stack: `template/process-csharp`, `template/process-node`, `template/bare-csharp`, `template/bare-node`. Every session runs on its own base `run/<rid>/main`. One fork and not two: GitHub allows one fork of a repository per account (verified 2026-10-29: a second `gh repo fork --fork-name` renamed the first). Consultants (consult `5dd114fe`) preferred one repository anyway: configuration is symmetric by construction, and public read exposure is the same either way. What is lost is the per-repo write boundary between the stacks, which the audit covers | operator: "2 public repos", then "forks", then "4 branches, one per arm" |
| D4 | TypeScript **7.0.2**, run by Node 26's native type stripping, `tsc --noEmit` as the type gate. ESLint cannot parse `.ts` through TS 7: `typescript@7.0.2` exports only `./lib/version.cjs` and `./unstable/*`, no classic compiler API (checked: `npm view typescript@7.0.2 exports`), and `typescript-eslint` accepts only `typescript <6.1.0`. So the lint parser gets a **private nested typescript 6.0.3 through npm `overrides`**, used for parsing only, without type information; `tsc`, the type gate and the runtime stay on 7.0.2. Recorded deviation (V12) | operator (TS 7.0.2); the lint shape verified 2026-10-29 |
| D5 | Fully autonomous: nobody answers; `COAI_ON_EXHAUSTED=good_enough`; decisions recorded by the agent | operator |
| D6 | Spend ceiling **$500** for everything — data, void, pilot (the operator chose $400 and pre-authorised a raise to $500 for the case where $400 cannot cover three process pairs; the ceiling is set at $500 from the start so no mid-series stop is needed) | operator, 2026-10-29 |
| D7 | C# and TS run **concurrently as pairs**; bare pairs too; the two arms never overlap | operator (pairs); bare pairs: this plan, so contention matches across arms |
| D8 | Repo name `minigit-bench` (the fork, renamed from the first attempt's name) | operator, 2026-10-29 |
| D9 | The conventions pin follows a **snapshot branch** `bench/minigit-2026-11` cut from `release`, not `release` itself. `pin-check` compares against the branch named in `.gitmodules` (`tools/pin-check.mjs:9-12`), so the pin stays green without freezing `release` for the family. The harness checks the snapshot's tip before AND after every session and stops the series if it moved | consultants (codex-2, antigravity-2, consult `cb169f07`) + verified in `pin-check.mjs`; **needs the operator's go** (an action in another repo) |
| D10 | Prevention, not only detection: each session runs in a **bubblewrap** sandbox (`/usr/bin/bwrap`, user namespaces verified) that hides `/home/jinx` except the cell's own directories; coai runs OUTSIDE the sandbox as the operator, reached through a stdio↔socket bridge, so vendor sign-ins never enter it | consultants; `bwrap` + `unshare -Ur` verified |
| D11 | No `bypassPermissions` / `--dangerously-skip-permissions`: permissions come from an allow-list (the template's `.claude/settings.json` in the process arm, a harness settings file in the bare arm) | this plan: the process arm must run as the family runs, and an agent holding a push token in a public repo gets the narrowest grant |
| D12 | No model pin through `ANTHROPIC_DEFAULT_*_MODEL` (the subagent-model rule forbids freezing an alias). Instead the harness records, per alias, the id it resolved to in the first session (opus, sonnet, fable, and the CLI's internal line), and every later session's `modelUsage` must resolve each alias it USED to the same id — sessions may differ in which aliases they used. A new id for an alias stops the series; a `model_refusal_fallback` is flagged, not stopped (§11.1) | `common.subagent-models` § *Name the model*; consult `9bf40917` |
| D13 | Visible tests = upstream with O3, O4, O7 fixed; every cell is ALSO scored with the unmodified upstream scripts, as a reference-only third score | this plan (§5.2) |
| D14 | Question consultants' **disk-reading rows are off** for the series, and `COAI_QCONSULT_ROOTS` is not set: a row that reads "projects on this disk" could hand a cell another cell's code or the held-out suite as advice (observed live: the `codex-2` row is `unconfined` and read `/home/jinx/git`) | consult `cb169f07` reply, flags read from it |
| D15 | The held-out suite is stored **encrypted at rest** (AES-256-GCM; the key is a mode-600 file `~/.config/minigit-bench/heldout.key`, never an environment variable a sandboxed process could inherit) and decrypted only into a scoring directory under the operator's home after a session ends — never under `/tmp`, which the agent sandbox does not hide: an unconfined vendor CLI reading the disk cannot leak it | this plan; E1 review |
| D16 | Claude authentication for every session is **one** method, chosen by the operator before P0: a subscription OAuth token from `claude setup-token` passed as `CLAUDE_CODE_OAUTH_TOKEN`, or an API key. **Decided: the operator's own current Claude subscription**, through a `claude setup-token` token passed as `CLAUDE_CODE_OAUTH_TOKEN`. The token was partly visible in a screenshot in the planning conversation; the operator accepted that risk on 2026-10-29 and kept it (it lives in `~/.config/minigit-bench/claude-oauth-token`, mode 600, directory 700). It is the only credential in the sandbox besides the PAT; the token scan (§7.6) covers its shape (`sk-ant-…`). So every cost is a list-price API-equivalent estimate (§9), and a rate-limit void means the subscription's usage window, not a spend limit | judge review M1; operator, 2026-10-29 |
| D17 | Effort `high` is pinned in the session's own `--settings` file (`effortLevel` and `modelSettings.{opus,sonnet,fable}.effortLevel`), because `--setting-sources project` and the fresh `CLAUDE_CONFIG_DIR` exclude the user settings where `high` lives today; children's effort is read back from the audit | judge review M2, verified in `~/.claude/settings.json` |
| D18 | **The task inputs have no licence.** SPEC-v1/v2 and test-v1/v2 were written by Yusuke Endoh (`mame/ai-coding-lang-bench`), banteg's fork is byte-identical, and neither repository carries a licence (checked by `gh api …/license`, the clones and their history). The operator's decision: the files live **only inside the GitHub fork**, relying on GitHub ToS D.5 (public repositories may be viewed and forked on GitHub). Our three test fixes live there too, on a branch `inputs`. `dew_flow_benchmark`, which is not a fork, holds only their hashes and `UPSTREAM.md`; the harness fetches the files from the fork at a pinned sha into a local cache and verifies them. The ToS grants viewing and forking; the fixes inside the fork are the operator's call, recorded here | operator, 2026-10-29; consult `31e740fc` |
| D19 | **No CodeRabbit in the series** (operator, 2026-10-29: "not needed"). Every CodeRabbit item elsewhere in this plan is void: the process arm's PR cycle is PR → CI → `sleep 300` → read CI → merge; the templates ship no `.coderabbit.yaml` and no `coderabbit-review.yml`; the CodeRabbit review item is dropped and the 300 s item now reads CI (the checklist is k/17); `coderabbit_wait` stays an empty category; P0 and P1 check no CodeRabbit review. The family rule asks for a reviewer bot only "where the repository has one configured", and the fork has none | operator, 2026-10-29 |

## 4. Design

### 4.1 Cells, blocks, order

- Cell = (arm, stack, n), n ∈ 1..3. Session = cell × phase (v1, v2). 12 cells, 24 sessions. Ids:
  `rid = sha256(seed:arm:block:stack:phase:attempt)[0:6]`, so no two sessions of the series share a rid or a `RUN_ID`.
- Three **blocks**. Each block = one process pair (C# ∥ TS) and one bare pair (C# ∥ TS). Inside a pair both stacks start at
  the same instant; a **barrier** holds both v2 sessions until both v1 sessions are collected and scored.
- Which arm leads each block: one of the six three-block patterns in which each arm leads at least once, chosen by the
  seed through `drawOrder`'s seeded hash (`spikes/policy-ab-quick/lib/order.mjs:4`) — not `drawBlocks`' independent flips
  (`order.mjs:10-16`), which give one arm all three leads with probability 1/4. A = process, B = bare.
- `seed.txt` = `mg-` + 16 random hex, committed **after** `prediction.md` and before the draw.
- CPU: the VM shows 14 vCPUs as 7 cores × 2 SMT threads (`lscpu -e`: core n = CPUs 2n, 2n+1 — the hypervisor's
  topology, not physical cores). The two cells of a pair get `taskset` masks **0-5** and **8-13** (three cores each);
  core 3 (CPUs 6-7) is left to the harness, coai, the vendor CLIs and the OS. Masks swap between blocks — with three
  blocks one stack holds a mask twice, recorded per cell. Every cell still obeys `common.local-cpu-budget` (≤8 workers,
  lowered priority), which oversubscribes only its own six threads; CPU and PSI samples flag what the masks do not fix.

### 4.2 The two arms

| | PROCESS | BARE |
|---|---|---|
| Where | a fresh single-branch clone of `run/<rid>/main` in the public repo | an empty directory (v1); a copy of the v1 result minus `bin/ obj/ node_modules/` (v2) |
| Instructions | the template's AGENTS.md → ENTRY → pinned conventions; all hooks | none |
| MCP | coai (plan gate, code gate, consult, ask_consultants) | none (`--strict-mcp-config`, empty) |
| GitHub | fine-grained PAT for this one repo; Contents/PR RW, Actions/Checks/Statuses R; **no Workflows** | none; `gh` hidden inside the sandbox (`--ro-bind /dev/null /usr/bin/gh`) — PATH is not filtered, because `gh` lives in `/usr/bin` beside bash, git, make and dotnet, and `env.mjs`'s `filterPath` would drop the whole directory |
| Toolchain | per template (`package.json`, `global.json`) | the same versions pre-installed read-only: dotnet 10.0.113, node 26.x, and `typescript@7.0.2` on PATH; the npm registry and nuget.org are reachable and are not counted as contamination |
| Done when | merged into the run base with green required checks, or the session ends | the session ends |
| Session cap | $35, wall 240 min | $6, wall 60 min |

### 4.3 Prompts (byte-identical across stacks except `{STACK}` and `{STACK_SENTENCE}`)

Stack sentences: C# — *"Use C# 14 on .NET 10 (SDK 10.0.113 is installed)."* TS — *"Use TypeScript run directly by Node 26
(native type stripping, no build step), with `tsc --noEmit` as the type check."*

**Process, v1:**

```
Implement version 1 of minigit as specified in SPEC-v1.txt at the root of this repository, in {STACK}. {STACK_SENTENCE}
The executable must be named 'minigit' and be runnable as ./minigit. For compiled languages, include a Makefile or build script. For interpreted languages, ensure the minigit file has a proper shebang line and is executable.

SPEC-v1.txt and test-v1.sh are this task's frozen inputs: do not change them. The work is done when, from a clean checkout of the base branch, `bash test-v1.sh` passes.

Follow this repository's own instructions — its CLAUDE.md, AGENTS.md, .agents/PROJECT.md and the rules they point to — for how work is done here, in full: loading the rules, the plan in todo/, the review gate on the plan and on the code, consultants, subagents, tests first, the research/ documents, the pull request, CI and the review that follows it.

The base branch of this task is the branch checked out when this session starts (`git branch --show-current`). Branch from it, open every pull request against it — never against main — and finish only when your work is merged into it with green CI.

Work autonomously. Nobody answers questions in this session. Where you would ask a person, decide, and record the decision and its reason in the plan and in the pull request description. If the review gate returns call_human, stop shipping: leave the pull request open, record why, and end the session.

Stay inside this task: do not read other run/* branches, other pull requests or issues of this repository, or any other minigit repository, and do not use the web.

End with a summary: the pull request(s) and merge commit, the tests you ran and their results, the gate rounds, and every decision you made.
```

**Process, v2:** paragraph 1 → *"Extend minigit to version 2 as specified in SPEC-v2.txt at the root of this repository,
keeping every version 1 behaviour, in {STACK}. {STACK_SENTENCE}"* (executable sentence kept); paragraph 2 → *"SPEC-v1.txt,
SPEC-v2.txt, test-v1.sh and test-v2.sh are this task's frozen inputs: do not change them. The work is done when, from a
clean checkout of the base branch, `bash test-v2.sh` and `bash test-v1.sh` pass."* Everything else identical.

**Bare** — banteg's wording (`benchmark.rb:356-360`, `:391-394`), the claim being re-measured, with four changes:

```
v1: Implement minigit as described in SPEC-v1.txt using {STACK}. The executable must be named 'minigit' and be runnable as ./minigit. For compiled languages, include a Makefile or build script. For interpreted languages, ensure the minigit file has a proper shebang line and is executable. Verify your implementation passes all tests by running: bash test-v1.sh {STACK_SENTENCE}
v2: You are in a workspace copied from the v1 implementation. Read SPEC-v2.txt and modify the existing minigit implementation in place to satisfy the full v2 spec while preserving all v1 behavior. Do not start over unless it is strictly necessary. Verify your implementation passes all tests by running: bash test-v2.sh {STACK_SENTENCE}
```

Changes: (1) "Typescript" → "TypeScript"; (2) the stack sentence through upstream's own `extra_prompt` slot (the mypy
precedent) — it pins the toolchain under test (upstream TS ran tsx); (3) "a passing v1 implementation" → "the v1
implementation" — the harness cannot promise it passed; (4) banteg's v2 wording, not mame's, which names two of six new
commands.

## 5. Inputs, quality and compliance

### 5.1 Pinned inputs

`SPEC-v1.txt`, `SPEC-v2.txt`, `test-v1.sh`, `test-v2.sh` byte for byte from `mame/ai-coding-lang-bench@7f6ffdc` (identical to
`banteg@83644e0`), held only in the fork (D18); their sha256 and the fixed visible tests' sha256 in
`spikes/minigit-bench/inputs/hashes.txt`, the files themselves fetched from the fork at a pinned sha.

**This repository is public, and sessions have network.** So this plan, and everything committed in clear, names only the
held-out suite's shape, counts and hashes — never its cases, its references or its mutants. Those live only inside the
encrypted suite (D15); its case list is published after the series, beside the report.

### 5.2 Visible tests (both arms, identical)

Upstream with three edits and a header naming the upstream sha and the edits: the build block fails loudly
(`make -s || { echo "BUILD FAILED"; echo "PASSED: 0"; echo "FAILED: 1"; exit 1; }`, same for `build.sh`, stderr shown);
`test-v2.sh:323` and `:384` call `fail` with the expected parent; `set -e` replaced by per-test guards so a crash
counts as failures. Reason: the visible test is what the agent optimises against; one that rewards a wrong parent teaches
the wrong target, and both arms get the same file.

### 5.3 Scoring (harness, after each session, never in the agent's tree)

On a pristine copy (process: fresh clone of the base tip; bare: copy of the workspace), temp HOME/TMPDIR, minimal PATH:
(1) input hashes — mismatch flags `inputs tampered`; (2) build from clean, **600 s**, against a per-scoring copy of the
seeded offline NuGet/npm cache — failure fails every case; (3) harness copy of the visible tests; (4) **held-out suite**;
(5) original upstream scripts (reference only). Every minigit invocation in (3)–(5) has a **10 s** timeout.

**Held-out suite — shape only (the cases are in the encrypted suite).** Python, run with `-I`; one temp repo per case;
every commit stages all working files so both readings of "commit = index" give one tree. Every case names the SPEC
sentence it asserts and checks stdout, exit code, and the resulting `.minigit` state; crash, timeout and missing command
each count as that case failed, never as "not run".

- **H1** (v1 behaviour): N1 cases, at least one per SPEC-v1 command and per SPEC-v1 error message, including the
  MiniHash function on edge inputs, exact output and file formats, and invocation from another working directory.
- **H2** (v2 behaviour): N2 cases, at least one per SPEC-v2 command and per error message, plus all of H1 against the v2
  binary.
- **No format assumptions the SPEC leaves open:** cases build their starting state with the cell's OWN `init`, `add` and
  `commit` — never by writing files in a guessed byte format (the SPEC leaves trailing newlines and raw-blob details
  open). A case whose setup needs a core command that failed its own H1 case is scored **`blocked by H1-<k>`**: reported
  apart, counted neither as passed nor as a second failure, so one defect is not counted twice and nothing cascades
  silently.
- **Secondary, reported, not scored:** robustness inputs outside the SPEC, and **per-invocation latency** (median of 100
  runs of one read-only command) — the runtime start-up cost, measured apart from agent time.
- N1, N2, M (the mutant count) and the suite's sha256 are committed before the pilot (§11).

**Two held-out measures, pre-registered (amended 2026-10-29 after the S2 review):** `percent` = cases passed / N, with a
blocked case counted as not passed — the primary score; and `defects` = the number of distinct failed CORE cases or SPEC
clauses, so one broken command counts once (cores exist for every v1 command and for v2's checkout, reset and rm). Every
comparison "in cases" below uses `defects`; every percentage uses `percent`.

A session is **accepted** when: clean build, visible 100 %, held-out primary 100 % (blocked cases count against it), and
(process) **delivered** = merged into its base. An undelivered process session is scored on its PR head and marked so.

**Calibration before any cell (measurement rule 2):** two Python reference implementations (one per commit reading) score
100 % on visible + held-out; M planted mutants, each a plausible defect of a different SPEC clause, each fail exactly
their pre-registered cases (M ≥ 8, the list encrypted with the suite). Two references written by one author can share one
misreading, and mutants prove sensitivity to the defects chosen, not completeness — the report says so.

**Adjudication, pre-registered.** The **primary score is the unrevised one**. After the series every failing held-out case
in every cell is classed product defect / SPEC ambiguity / harness defect in two steps: first from the SPEC sentence, the
case and its captured output only, with stack and arm replaced by a random id — that step alone decides "SPEC ambiguity";
only then is the code shown, to separate product from harness defects. A case classed ambiguous yields a revised score
(case dropped for all cells), reported beside the primary, never instead of it.

### 5.4 Process compliance (process arm, read from GitHub, coai.db and the stream — never from the agent's summary)

17 items (one dropped by D19): plan file with status line; `rules.mjs check/explain/read` before the first edit; worktree before the first
product edit; a `review_plan` round reached `proceed` before any `review_code`; last `review_code` verdict `proceed`;
every finding resolved, every rejection reasoned; ≥1 Sonnet coder child and ≥1 Opus judge child; a test written before the
source it covers; unit tests ran in CI; `research/module_tests.md` updated and a scenario test runs the built CLI;
`research/architecture.md` or README updated; every commit on the base came through a merged PR with no bypass and green
required checks; push CI green on the base tip; ≥300 s between PR open and the first read of CI (D19: no CodeRabbit);
inputs unchanged; `.github/` required jobs intact; conventions pin unchanged.
Recorded, not scored: consultant calls, providers answered per round (<5 flagged `incomplete review`). C#-only items
(logging, leftovers helper) are excluded from the cross-stack score. **Hand check:** at least 20 verdicts, stratified so
every item and every process cell appears, plus every "not met" (which can make it more than 20); any disagreement above
1 in 20 → fix the checker before any number.

## 6. Template repositories and GitHub

**One source tree builds both templates.** Neither template carries product code or a test: both start from the same
empty feedback loop, and the stack's own strictness (C# analyzers and warnings as errors; TS `strict` and ESLint) is the
treatment, recorded in `inputs/treatment.md`. A `template-diff` check proves the shared files are byte-equal and that the
stack files differ only between `# --- stack:begin/end ---` markers; its output goes into `inputs/treatment.md`.

- **Identical:** `AGENTS.md` (the family bootstrap text, as in `dew_flow_vscode_kit/AGENTS.md`), `CLAUDE.md` (`@AGENTS.md`),
  `.gitmodules` (`.agents/conventions`, `branch = bench/minigit-2026-11`, same sha), `.claude/settings.json` and
  `.claude/hooks/*` copied from the pinned `settings/` (adapter-check needs them byte-equal), `.gitattributes` (LF),
  `.editorconfig`, `.gitignore`, `LICENSE`, `POST_DEPLOY.md` ("nothing deployed; the product is the commit merged into the
  run base; the check is green push CI there"), `todo/README.md`, `research/README.md`, stub `research/architecture.md` and
  `research/module_tests.md`, `.github/workflows/{family-checks,pr-title,coderabbit-review}.yml` — with their
  `push`/`pull_request` branch filters widened from `[main]` to `[main, 'run/**']` (the family's own `family-checks.yml`
  triggers on `main` only, `wsl_care/.github/workflows/family-checks.yml:13-17`, so a required check would never report on
  a run base and no PR could merge) — and the rulesets.
- **Differ only in marked regions:** `.agents/PROJECT.md` (`## Stack`, `## Commands`) carrying one identical precedence
  sentence — *"The SPEC outranks a family rule for what the minigit binary does: no runtime package (External libraries:
  NOT allowed) and nothing on stdout or stderr beyond what the SPEC prints (No debug output). Where a rule asks for more —
  the logging rule's Serilog package or console sink — follow the rest of that rule with the standard library and record
  the deviation in the plan. SPEC-*.txt and test-*.sh at the root are frozen task inputs."*; `README.md` (one line);
  `.coderabbit.yaml` (path glob + stack text; both `knowledge_base: { opt_out: true }` so CodeRabbit carries nothing between
  cells, and `auto_review.base_branches: ["run/.*/main"]`); `ci.yml` (triggers `pull_request` with no branch filter and
  `push: run/**`; two jobs). **`ci · build · test (ubuntu-24.04)`, required:** C#: setup-dotnet from `global.json`,
  `dotnet restore -m:4`, `dotnet format --verify-no-changes`, `dotnet build -c Release -m:4`, the MTP test executables —
  never `dotnet test`; Node: `npm ci`, `typecheck`, `lint`, `npm test`. **`acceptance (ubuntu-24.04)`, NOT required:**
  `bash test-v1.sh`, then `[ ! -f test-v2.sh ] || bash test-v2.sh`. Keeping acceptance out of the required set lets the
  agent land the work in several PRs, as the family's process would; a required acceptance step would turn every PR
  before the last red and force one PR — a distortion of the treatment. The prompt's own done-criterion (the tests pass
  on the base) and the scorer still hold the agent to them.
- **C# only:** `global.json` (sdk 10.0.113, `rollForward: disable`, MTP runner), `Directory.Build.props` (from
  `wsl_care`), `Directory.Build.rsp` (`-nr:false`), `Directory.Packages.props` (xunit.v3, FluentAssertions 7.2.2 for tests;
  no Serilog), `nuget.config`, empty `minigit.slnx`, `scripts/run-tests.sh`.
- **Node only:** `package.json` (`type: module`, `engines.node >=26`, devDeps typescript 7.0.2, eslint, @types/node),
  lockfile, `tsconfig.json` (`noEmit`, `strict`, `erasableSyntaxOnly`, `verbatimModuleSyntax`,
  `allowImportingTsExtensions`, `module nodenext`), `eslint.config.mjs` (no type information), `scripts/run-tests.mjs`
  (`--test-concurrency` = min(8, cpus−1), lowered). **The lint parser is named, not assumed:** `@typescript-eslint/parser`
  without `parserOptions.project` — it still parses through the `typescript` package's JS API, which TS 7.0.2 may not
  provide. The template step decides, by running it: (a) the parser works with typescript 7.0.2 → use it; (b) it does not
  → an npm `overrides` entry gives `typescript-eslint` a private typescript 6.0.3 for parsing only, while `tsc --noEmit`
  and the runtime stay on 7.0.2 — recorded in `treatment.md` as a deviation. **Gate before tagging:** a planted lint
  violation in a `.ts` file turns `npm run lint` red, and a file using TS-only syntax (a type-only import, a type
  annotation, `satisfies`) lints clean — proving `.ts` files are parsed, not skipped.
- **Tag** `bench-template-1` (annotated) on `main` in both; both shas and the identical-set digest recorded.
- **Run base per session:** `rid` as in §4.1 (it already encodes arm, phase and attempt). v1 base = tag + one harness commit (SPEC-v1, fixed
  test-v1, `RUN_ID` canary); v2 base = the v1 base tip after its session + SPEC-v2 and test-v2 (if v1 did not merge: the v1
  PR head, flagged `v1 not delivered`). Pushed with a deploy key that is the rulesets' **only** bypass actor.
- **GitHub settings, identical:** public; issues/wiki/projects off; squash + rebase; auto-delete heads; Actions limited to
  GitHub-owned plus one selected action, `amannn/action-semantic-pull-request` at the sha `pr-title.yml` pins (the
  family's PR-title check, `wsl_care/.github/workflows/pr-title.yml:24`, is third-party and would otherwise be blocked);
  read token; CodeRabbit app installed. Ruleset `run-main` on `refs/heads/run/*/main`: no deletion, no
  force-push, linear history, PR with 0 approvals and thread resolution, required checks (the CI job, family checks, PR
  title), bypass = deploy key. Ruleset `template-frozen` on the default branch (no update: a PR to `main` by mistake cannot
  land). Tag ruleset on `bench-template-*`.

## 7. Isolation and contamination

1. **Sandbox (D10).** `bwrap` with `/` read-only, `/home/jinx` **and `/mnt`** replaced by tmpfs (on WSL2 `/mnt/c` holds
   the Windows user's `.codex/auth.json` and `.claude/`, verified visible through a home-only tmpfs), **`/run/user`, `/tmp`
   and `/var/tmp` replaced by tmpfs too** — verified 2026-10-29: through a home-only box, the user bus at
   `/run/user/1000/bus` let `systemd-run --user` run a command OUTSIDE the box that read `/home/jinx/git`, with network
   (E1.S1 review B1); `/run/user` also holds the ssh and gpg agents. With those hidden the bus connect fails. The agent
   box shares the network namespace, so ABSTRACT unix sockets stay reachable whatever is mounted: `preflight.mjs`
   lists them (`ss -xl`, names starting `@`) and refuses to start unless the list equals an allow-list recorded on the
   series' first day (on 2026-10-29 one unconnected datagram socket of another user), `/run/WSL` masked,
   `WSL_INTEROP` unset and `/mnt/*` entries stripped from PATH (the WSL interop handler otherwise starts `cmd.exe` from
   inside, and any Windows process can read `\\wsl.localhost\…\home\jinx`); `/usr/bin/gh` masked in the bare arm. Then
   binds: the session directory (rw) laid out as `<session>/repo` (the clone) and `<session>/wt` (where the agent's
   worktrees go, inside the rw bind and inside the bridge's allowed paths), its `CLAUDE_CONFIG_DIR` (rw, fresh per
   session — v2 sees none of v1's memory, as upstream's fresh v2 agent), its TMPDIR, per-session NuGet/npm caches seeded
   by copy from one pre-warmed snapshot, the toolchains and the `claude` binary (ro), the coai bridge socket. Network
   shared (Anthropic, GitHub, the package registries). P0 proves from inside: `cmd.exe` fails to start, `/mnt/c` and
   `/home/jinx` are empty, the held-out suite's path does not exist.
2. **coai outside the agent's sandbox, inside its own.** The harness starts one `coai-mcp` per session as the operator,
   in a second `bwrap` that binds only the session directory (ro, plus `<session>/coai` rw), the vendor CLIs' own homes
   (their sign-ins), the coai binary and the toolchain — `/home/jinx` and `/mnt` otherwise hidden. Reason: coai's codex
   reviewers "read anywhere and no flag removes the shell" (`dew_flow_connect_other_ais ·
   src_mcp/tests/ConfinementPlannerTests.cs:69-90`), and two of the five live providers are codex; unconfined, a TS
   cell's reviewer could read the concurrent C# partner's tree. The held-out key is never in coai-mcp's environment (the
   key is a mode-600 file the scorer alone reads, D15). `COAI_DATA_DIR=<session>/coai`, the series' settings snapshot with
   `COAI_QCONSULT_ROOTS` explicitly unset (the operator's file currently holds `/home/git`, flagged by `providers`); the sandboxed `claude` reaches it through a small Node stdio↔unix-socket bridge (no
   dependency). The server runs with the operator's full disk access, so **the bridge is the boundary**: it parses each
   JSON-RPC `tools/call` and refuses any `repoPath` (and any path-shaped argument such as `committedPlan`,
   `suspectedFiles`) that does not resolve inside the session directory, answering a JSON-RPC error that names the rule.
   Verified in P0 before any paid cell, including a refused `consult` with `repoPath` set to this repository.
3. **Single-branch clones** with a restricted fetch refspec and `--no-tags`. Both stacks share the one fork (D3), so the
   PAT can write to the partner stack's branches; rulesets stop it touching any `run/*/main` or `template/*`, and the
   audit flags any push or PR outside the session's own `run/<rid>/` namespace. **This does not prevent reading:** the
   fork is public, so any process with network can fetch another run's branch
   anonymously. For other runs' code the protection is therefore **audited, not prevented**, and the report says exactly
   that; only the held-out suite (D15 + sandbox) and the operator's files (sandbox) are prevented.
4. **Audit** (`hooks/audit.mjs` extended): every tool call with `agent_id`, `tool_use_id`, `duration_ms`; the scan matches
   foreign `run/` namespaces, PR numbers the cell did not create, other `RUN_ID`s, `github.com/.*minigit`,
   `raw.githubusercontent.com`, the name `dew_flow_benchmark`, any `minigit-bench` ref outside the session's own
   `run/<rid>/` namespace, `template/*` refs of the other stack, the partner session's and the harness's paths — in tool inputs AND in MCP results (a reviewer's or
   consultant's answer can carry them) — and any web fetch. `lib/leak.mjs` today resolves with `path.win32` (`leak.mjs:4`); the new spike gets a POSIX
   resolver. High-severity hit → `void: contaminated`. Every hit is read by hand.
5. **Canaries** in the operator's `~/.claude/projects/*`, in memory files, and in each sibling cell's config dir.
6. **Token scan** of every pushed file, PR body, comment and commit message for GitHub token shapes (`ghp_`, `github_pat_`),
   Anthropic shapes (`sk-ant-`) and the session's own credential values.
7. Bare workspaces are pushed to `bare/<rid>` branches only after the last cell.

## 8. The harness — `spikes/minigit-bench/`, reusing `spikes/policy-ab-quick/`

Why a spike and not the `bench` product: the proven envelope reader, span sweep, voids, resume, canaries and order draw are
Node in `spikes/policy-ab-quick/`, and the coai-751 series ran exactly that way (`research/policy-ab/series-coai-751.md:4-6`).
Cost-check semantics are taken from `src/Bench.Domain/Scouts/ScoutCost.cs` (tolerance `:187`, CLI rates `:51`) and cited,
not forked.

**Reused as is:** `lib/order.mjs` (`drawOrder`), `lib/parallel.mjs` (`sweepSpans:55`, `agentTimeline:16`),
`lib/ratelimit.mjs` (`rateLimitSignal:12`), `lib/summary.mjs` (`resultEnvelope`), `lib/resume.mjs`, `lib/safe-rm.mjs`,
`lib/canaries.mjs`.

**Changed in `policy-ab-quick`, each with a test:**
- `lib/spend.mjs:13` — `spendDecision` takes one cap; it is extended to a list of caps plus a live running sum (T-G1).
- `lib/proc.mjs:32-36` — `killTree` sends `SIGKILL` to the one PID on Linux, so dotnet build nodes, vendor CLIs and
  coai-mcp survive a kill. Spawn detached and kill the process group; RED test first, watched failing on the current code.
- `hooks/audit.mjs` — `--out`, PostToolUse/PostToolUseFailure with `tool_use_id` and `duration_ms`, Subagent start/stop;
  backward compatible.
- `lib/env.mjs` — export the DROP/KEEP scrub and the fixed git settings (`env.mjs:33-37`) so the new argv builder composes
  them instead of copying; `filterPath` is not used (it would drop `/usr/bin`).
- `lib/pricing.mjs` stays (history). It is imported transitively by `summary.mjs:6` and `parallel.mjs:4`, so the rule is
  **`costFromMessages` is never called** by the new spike — a test asserts it; its Opus row is fitted
  (`pricing.mjs:13`, `[2.5, 12.5, 0.25, 7]`) against the published 4/20/0.20/5/8, which is why the coai-751 meter read
  $12.75 for a $23.51 cell (`series-coai-751.md:181-182`).

**New:** `series.mjs`, `preflight.mjs`, `prepare-session.mjs`, `run-session.mjs`, `collect-session.mjs`,
`summarize-session.mjs`, `score-session.mjs`, `report.mjs`, `retire.mjs`; `lib/` — `argv.mjs`, `sandbox.mjs`,
`coai-bridge.mjs`, `otel-receiver.mjs` (OTLP/HTTP JSON → ndjson + live cost sum), `cpu-sampler.mjs`, `prices.mjs`,
`claude-usage.mjs`, `coai-usage.mjs`, `github.mjs`, `timeline.mjs`, `classify.mjs`, `contamination.mjs`, `voids.mjs`,
`leak-posix.mjs`; `heldout/` (ONE encrypted archive holding the suite, the references and the mutants — none in clear); `templates/` (the one source tree); `inputs/`
(`statement.md`, `prediction.md`, `seed.txt`, `treatment.md`, `hashes.txt`, `checklist.json`, `prices-<date>.json`,
`stacks.json`, `coai-settings.json`); `test/`.

**Launch** (both arms): `claude -p --model opus --output-format stream-json --verbose --setting-sources project --settings
<session>/settings.json --strict-mcp-config --mcp-config <session>/mcp.json --forward-subagent-text --include-hook-events
--max-budget-usd <cap> --tools Task,Bash,Read,Edit,Write,Glob,Grep,TodoWrite,ToolSearch`. `ToolSearch` is there because
Claude Code defers MCP tool schemas and coai's own orders tell the agent to load them with it. The harness `--settings`
file adds `mcp__coai__*` to the allow-list (the template's pinned `settings.json` allows only Edit, Write, NotebookEdit,
Bash and PowerShell, and must stay byte-equal for adapter-check; nobody answers a permission prompt) and pins effort
(D17). Session persistence ON (transcripts are
the third count). Env: the scrub, plus `CLAUDE_CONFIG_DIR`, `CLAUDE_CODE_DISABLE_BACKGROUND_TASKS=1`,
`CLAUDE_CODE_ENABLE_TELEMETRY=1` with OTLP to the local receiver, `MSBUILDDISABLENODEREUSE=1`, `UseSharedCompilation=false`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0` (no cell builds on its partner's node), `BENCH_CELL=<id>` (the janitor's marker),
`GIT_TERMINAL_PROMPT=0` (a missing credential fails, never hangs), `GIT_CONFIG_GLOBAL=<session>/gitconfig`; process arm
`GH_TOKEN`, `GH_CONFIG_DIR=<session>/gh`, `GH_PROMPT_DISABLED=1`, and the session gitconfig carries
`credential.https://github.com.helper = !gh auth git-credential` plus the fixed bench author — so `git push` inside the
sandbox authenticates with the session's PAT and nothing else (read-back: a `git push --dry-run` from the sandbox
succeeds in P0). Auto-update off; no warm-up. `preflight.mjs` refuses to start unless `dotnet --list-sdks` shows exactly
the `global.json` SDK (10.0.113, `rollForward: disable`) and `node --version` is the pinned 26.x.

**Read-back before the first paid turn counts:** init event (model, tools, `mcp_servers[].status`: coai connected in the
process arm, none in bare, permission mode), `argv.json`, settings/mcp hashes, `coai-mcp --providers` with the exact env
(5 vendors, versions), after the first round the session's `state.config` checked with a port of
`dew_flow_connect_other_ais · src_bench/CoaiBench/Running/SettingsApplied.cs:34-77`, effort `high` for every agent, a
refused push to a foreign `run/*/main` with the session's PAT, the clone's refspec, the canary manifest. One cell per arm and stack is read back by hand in block 1.

**Model smoke before P1 (≈ $0.10):** one headless process-arm session whose only prompt is to call coai `providers` through
the bridge — proves permissions, deferred-schema loading and the bridge before $35 is at stake.

**Clocks:** the launcher's monotonic clock is mapped to epoch once at spawn (one paired reading), and every OTel, hook and
GitHub timestamp is placed on the wall through that mapping; a skew larger than 1 s between the mapping at spawn and at
exit is flagged.

**Watchdogs:** wall (240 / 60 min), cost (kill at 1.1 × cap from the OTel running sum, which is final per request and
includes children; fallback the stream meter at published prices), stall (45 min with no stream event, no OTel event and
no CPU growth). A janitor ends any survivor whose `/proc/<pid>/environ` carries the cell's `BENCH_CELL`; never by name.

## 9. Accounting

**Claude, per session.** Three views — all three come from the same CLI, so they are cross-checks of the reading, not
independent billing authorities; the account's own billing is outside this plan. **E** = the result envelope (`total_cost_usd`, `modelUsage[m]` — both include
subagents; `usage` does not and is never used for totals); **O** = OTel `api_request` (model, four token kinds,
`cost_usd`, `duration_ms`, `query_source`, `agent_id`); **T** = transcripts, main + `subagents/*.jsonl`, grouped by
`message.id` (the stream repeats one response per content block; the coai-751 fixtures show 15 events for 5 messages),
asserting duplicates agree on input/cache fields, reading `usage.cache_creation.ephemeral_5m/1h_input_tokens`.

- **Headline USD** = Σ over models of `(in·Pin + out·Pout + cr·Pcr + w5·P5m + w1·P1h)/1e6` with tokens from
  E.`modelUsage` and the 5m/1h split ratio per model from T; a model with no message-level split (the CLI's internal Haiku
  line) is priced at 5m with 1h as the upper bound — an interval, never a guess. Prices: a dated copy of
  `research/scout-calibration/prices.json`, re-read from the pricing page on the series' first day, with the row it lacks
  added from that page: **Fable 5.1** (read 2026-10-29: $10 in / $50 out / $12.50 5m write / $20 1h write / $0.25 read) —
  without it every Fable child would price as unknown.
- **Beside it:** the CLI's own `total_cost_usd`; per model main / children / internal; tokens per model from `modelUsage`.
- **Checks:** T1 Σ`costUSD` = `total_cost_usd` ± $0.01 — a breach **stops** the series (the envelope disagreeing with
  itself is an instrument failure); T2 O against E per model and kind ≤ max(0.5 %, 100 tokens), cost
  ≤ 1 % (breach → `otel incomplete`, cell kept); **T3** T re-priced at CLI rates (`prices.json` `cliRates`: Sonnet cache
  read observed at $0.20 vs published $0.10) against O **joined request by request** — the stream and transcripts carry `request_id`
  (20 in the two-children fixture), O's `api_request` carries `request_id` — with the query-source ROLE as
  the fallback join (A-J1 held on the capture, so request_id is the join). Raw values observed 2026-10-29 on CLI 2.1.310
  headless: main requests `sdk`, child requests `agent:builtin:general-purpose` with `agent_id` set — not the documented
  `main`/`subagent` — so one mapping turns `sdk`|`main` into main, `agent:*`|`subagent` into subagent, anything else into
  auxiliary; ≤ 2 %, and **T4** T input/cache against the same
  O ≤ 0.1 % — for a small count that is effectively equality, deliberately. They are compared with O and not with E, because E's
  `modelUsage` is aggregated per model and carries no `query_source` (checked: `summary.mjs:46`, and no stream fixture
  holds one), so E cannot be cut down to what T sees. T2 (O against E, all sources) closes the loop. The CLI's internal
  line and compaction calls are `auxiliary` and are checked by O alone. The A/A and P1 must show T covering `main` and
  `subagent` before **T3 becomes a stop**; until then a T3 breach is investigated, not fatal. **Pre-registered fallback:**
  if the A/A cannot establish T's coverage, T2 becomes the stop, with O and E reconciled per model within 1 %, before any
  data cell; if O and E cannot be reconciled either, the series does not start until the instrument is repaired. If the smoke shows transcript `output_tokens` are placeholders like the stream's, T3's
  output column switches to O, decided and recorded before any data cell.
- **The Sonnet cache-read price, settled by the capture (2026-10-29, CLI 2.1.310, $0.18):** the pricing page gives $0.10
  (0.05 × input), and the CLI now bills exactly that — Sonnet costUSD 0.0168287 equals the published recompute, as do
  Opus and Fable to 1e-7. The $0.20 the scout pilot saw was CLI 2.1.289's billing. So `cliRates` are version-scoped:
  each entry names the CLI version it was observed on and applies only to it; for 2.1.310 the CLI-billed view IS the
  published view. The day-1 re-read still runs, and a different CLI version re-checks it with the A-A smoke.
- **A killed session without an envelope:** headline from O at published prices, flagged `no envelope`; never a fitted rate.
- **The cost basis is stated, not assumed:** every figure is list-price API-equivalent; what the account is actually billed
  depends on its auth (subscription or key) and is not this plan's number.

**coai vendors** (`<session>/coai/usage.jsonl` + a `VACUUM INTO` copy of `coai.db`): per provider, model, kind, role,
stage — launches, seconds, tokens in/out/cached/reasoning. Uncached = in − cached, with `cached ≤ in` asserted on every row
(the O1 trap, guarded in our own reader). USD: all null → **"unknown (subscription)"**; mixed → known sum + "N unknown";
never 0 and never added to Claude's USD. "usage not captured" rows → a count and tokens as a lower bound. **The claim is
bounded accordingly:** the report states Claude API-equivalent cost, and beside it the reviewers' consumption in tokens
with its unpriced remainder disclosed — never a "total process cost".

**Basis of the ceiling and the caps:** every spend figure used by a stop rule is the LARGER of the CLI's `total_cost_usd`
and the published-price recompute (they differ where the CLI bills Sonnet cache reads at $0.20).

**Void spend** is kept per attempt and moved aside (`void/<cell>-a<k>`); every table carries data, void and pilot spend
on separate lines; the ceiling counts all three.

## 10. Time

Wall = the launcher's monotonic clock, spawn to exit; `duration_ms` / `duration_api_ms` beside it. Prepare, collect and
score are outside it. Spans per category, from: `model_api` (O `api_request`), tool spans (PostToolUse `duration_ms` by
`tool_use_id`, cross-checked against O `tool_result` within max(1 s, 2 %)), `coai_gate` (MCP spans ∪ `rounds.started..
completed`; MCP calls that cross the bridge are timed at the bridge too), `consultant`, `sleep300` (literal Bash `sleep 300`), `ci_wait` and `coderabbit_wait` (wait spans intersected
with GitHub's in-progress intervals and push → review times), `build_test` (dotnet build/restore/run, test executables,
`bash test-v*.sh`, npm ci/test, `node --test`, `tsc`, make), `hooks` (hook execution, from the `--include-hook-events`
started/response pairs — the conventions' own hooks run on every tool call and cost real time), `other_tool`.

**Exclusive partition:** sweep the elementary intervals of the wall; each gets the highest-priority category active in it,
across ALL agents: **work first** — `build_test > model_api > other_tool > hooks` — then **waits** — `coai_gate >
consultant > ci_wait > coderabbit_wait > sleep300` — then `unattributed`. So an interval counts as waiting only when no
agent is working in it: one child's `sleep 300` cannot erase another child's build or model turn. Categories sum to the
wall exactly — which proves the bookkeeping, not the attribution; the attribution is what T-T1 and the A/A smoke test. Also reported: the inclusive union per category, agent-seconds per
category (labelled as work volume), the pairwise overlap matrix — **never a sum of spans**. GitHub-side durations (CI queue
and run, CodeRabbit latency per push, PR open → merge) are reported apart. **Wall and active time** (active = the
sum of the WORK categories `build_test + model_api + other_tool + hooks`; `unattributed` is its own bucket, never work)
have equal standing for the process arm: active time removes the fixed waits, wall keeps the stack-dependent ones (a
longer CI, a larger diff for the gate). Per-invocation latency (§5.3) is the runtime start-up cost, apart from both.

## 11. Pre-registration and analysis

Committed **before the pilot**: `statement.md`, the analysis contract (this section, §11.1 and §5.3's acceptance), the
exclusion and void rules, `checklist.json`, the held-out suite's sha256, `prediction.md`. Anything the pilot changes is
added as a dated amendment, never rewritten. `seed.txt` is committed after the pilot and before the draw.

**Unit of analysis = the cell**, not the session: v2 inherits v1, so the 24 sessions are 12 observations with two phases
each. Every cell is reported, delivered or not — no successful-only comparison; a capped, undelivered or budget-truncated
cell appears with its outcome.

**Censored and mixed outcomes, pre-registered.** A session killed at its cap has a cost censored at the cap: it is
reported as `≥ cap`, and any ratio it enters is reported as a bound, not a value. A pair whose two cells did not both
deliver (one merged, the other capped, undelivered or on `call_human`) is shown in every pair-ratio table twice — included
as a bound, and excluded — and the medians are given both ways. If the ceiling leaves the process arm at n = 2, the report
says "in k of 2 pairs" and nothing about a third.

**The pilot may not tune the treatment of one stack.** Amendments after the pilot are limited to the instrument
(harness, scorer, counters, read-back). A change to the treatment (template, prompt, settings) is applied identically to
both stacks and requires a further pilot **pair** (C# ∥ TS) before the draw, paid from the same ceiling.

### 11.1 Outcomes and voids (pre-registered)

- **Void — instrument causes only, never quality:** `rate limit` (the CLI's own 429 markers, `ratelimit.mjs:12`);
  `no reviewer` (`call_human` because zero reviewers answered, `dew_flow_connect_other_ais ·
src_mcp/core/Rounds/RoundMachine.cs:404-416`); `contaminated` (a
  high-severity audit hit, a canary, a token leak); `harness` (a crash before the model's first turn); `alias drift` (D12)
  — the last stops the series rather than voiding one cell.
- **Data points, not voids:** killed on wall, cap or stall; `call_human` with ≥1 reviewer answering; `model_refusal_fallback`
  (flagged, medians with and without); OTel missing (time decomposition "not captured").
- **A void session is re-run once** in a fresh namespace `.a<k+1>`: a v1 void from the template tag, a v2 void from the
  recorded v1 base (v1 is kept). Its concurrent partner is not stopped. Re-runs are scheduled after the block's own pairs
  have finished and before the next block starts, so the arms still never overlap; a void v1 is re-run before its cell's
  v2 (the barrier holds the partner's v2 meanwhile only if both v1s voided). A re-run is paired with a re-run of the
  partner only if the partner also voided, otherwise it runs alone and is labelled `ran unpaired`. A second void of the same
  session leaves the cell missing, reported as such. Void spend counts against the ceiling (§13).

**Prediction (to be committed verbatim, written 2026-10-29 before any cell):**
*(The ranges below are descriptive expectations about medians and ratios, stated before the run — not effect sizes
claimed as findings.)*

1. Bare cost: C# > TS in ≥2 of 3 pairs; median C#/TS 1.15–1.5×. Basis: banteg's own tokens re-billed give 1.32×, and C#
   adds a project file, a wrapper and build loops. Per cell (v1+v2): TS $0.8–2.5, C# $1.0–3.5.
2. Bare wall: C#/TS 1.3–1.8×; the API-time ratio smaller (1.1–1.4×) — the difference is dotnet tool time.
3. Process cost: $30–70 per cell; process/bare 15–40×; C#/TS 1.05–1.35× — the fixed overhead dilutes the language effect,
   so the interaction (C#/TS process ÷ C#/TS bare) is below 1.
4. Process wall: 2–5 h per cell; C#/TS 1.1–1.4× on active time; more red CI runs in C#.
5. Quality: every bare session passes the visible tests; bare held-out median `percent` ≥ 90 % in both stacks with at least
   one bare failure; no process cell has more held-out `defects` than the worst bare cell of its stack; no stack difference
   > 1 `defect`.
6. Delivery: ≥5 of 6 process cells deliver both phases; ≤1 session capped or on call_human; compliance median ≥13/17,
   most often missed: test-first order, `sleep 300`, worktree-first.
7. Surprises named in advance: TS costing ≥ C# in ≥2 bare pairs; process scoring below bare on held-out; any process session
   hitting its cap.

**Reported per session:** arm, stack, block, rid, outcome, cost (headline, CLI, per model), tokens per model and kind,
first-turn cache read (warmth flag), wall / API / active / each partition category, turns (checked against assistant
messages), children per model and max concurrent, coai rounds / findings / providers answered, CodeRabbit findings, CI runs
and red runs, build ok, visible / held-out / upstream-original scores, product and test LOC, latency, compliance k/17,
flags (fallback, void, contamination, CPU pressure).

**Per stack × arm:** median and range (n = 3). **Language effect:** the C#/TS ratio within each concurrent pair (three
ratios) and their median. **Process effect:** process/bare per stack. **Interaction:** as in prediction 3.

**What n = 3 licenses:** "in k of 3 pairs", and whether ranges overlap (overlap = not distinguished). A predicted direction
held in 3 of 3 pairs is the strongest available result and still has 1/8 probability under no effect — so no p-values, no
confidence intervals, no effect sizes as findings. It does not license other tasks, languages, models or CLI versions, the
value of the process on real work, or any mechanism.

## 12. Threats to validity

| # | Threat | Control / the sentence the report carries |
|---|---|---|
| V1 | The process arm is a bundle and C# carries more of it: 34 rules / 331,833 B against 32 / 324,781 B (`rules.mjs explain --task implement --task test --task git --task pr`, run 2026-10-29), plus `-m:4`, `Directory.Build.rsp`, MTP, warnings as errors, cyclomatic ≤ 4, `Outcome<T>` | Record rule bytes actually read per session. *"The process arm's C#/TS difference is C#-under-the-family-rules against TS-under-the-family-rules, not a language effect."* |
| V2 | n = 3 against a within-arm spread seen at $12–23 in coai-751 | Paired concurrent design, ranges. *"Differences smaller than the within-condition range are not distinguished."* |
| V3 | External waits dominate process wall | Partition; active time is the comparison. *"Process wall time is largely not agent time."* |
| V4 | The model moves under the series | `modelUsage` keys checked per session; moved alias → stop; fallback flagged |
| V5 | Weak upstream tests, SPEC ambiguity | Fixed visible tests, calibrated held-out suite, upstream score kept. *"Upstream's 100 % measured its weak tests."* |
| V6 | Contamination across runs (public repos, disk, CodeRabbit learnings, memory, consultants) | §7, D14, D15 |
| V7 | CPU contention in pairs | `taskset` masks 0-5 / 8-13 swapped per block, one core left to the harness, CPU and PSI sampled every 5 s |
| V8 | Prompt-cache warmth across sessions | No warm-up; simultaneous starts, so both stacks of a pair meet the same cache state; first-turn cache read flagged per session. An idle gap cannot clear it: the CLI's main session writes 1-hour cache entries (`tests/Bench.Tests/Fixtures/probes/claude-2.1.258-*.ndjson`: `ephemeral_1h_input_tokens` 89,541, 5m 0) |
| V15 | **Resolved 2026-10-29: the operator installed GNU Make 4.4.1.** Was: `make` is not installed on the series machine (checked 2026-10-29): a tree that builds through a Makefile scores BUILD FAILED, while `build.sh` works. Both stacks get the same prompt ("include a Makefile or build script") | Install `make` before the series, or record per cell which build path the agent chose; decided in E3's preflight |
| V16 | The time partition classifies one Bash call as one category: `sleep 300 && dotnet build` counts its 300 s as `build_test` (a build anywhere in a chain wins) | A documented limit of per-call classification; the report shows the count of mixed chains per cell |
| V9 | dotnet start-up cost inside the agent's own test loops | API vs tool time, per-invocation latency. *"Part of C#'s tool time is runtime start-up."* |
| V10 | coai vendor variance (near-disjoint findings between identical runs: `dew_flow_connect_other_ais · research/RESULTS_prompt_measurement.md:21-39`) | Pinned coai sha and settings snapshot, empty `commands` list recorded, providers answered per round; vibe 3.0.2 is a version coai has not measured — recorded |
| V11 | SPEC vs rule conflicts | The identical PROJECT sentence; C#-only items out of the cross-stack compliance score |
| V12 | TS toolchain deviation (7.0.2 for types and runtime, a nested 6.0.3 for the lint parser only, no type-aware lint) | Recorded in `treatment.md`; the template gate proves lint parses `.ts` |
| V13 | Conventions pin drift | D9: snapshot branch, tip checked before and after every session |
| V14 | Designer bias in the held-out suite | Written from the SPEC before any cell, hash-committed, calibrated on mutants |

## 13. Budget

Expected: E1.S3's fixture capture ≤ $1.50, model smoke ≈ $0.10, A/A ≈ $4, P1 ≤ $39, bare 6 × $2–4 ≈ $20, process 6 × $30–50 ≈ $240 → ≈ $300. Caps: $35 per
process session, $6 per bare session. Because parallel children overshoot a cap between turns
(`spikes/policy-ab-quick/lib/watchdog.mjs:1-12`; its `CostMeter` calls the fitted `costFromMessages` at `watchdog.mjs:27`,
so the new spike does NOT reuse it — E1.S3 builds a stream meter at published prices), a pair-phase starts
only if data + void + pilot spend + the live O sum of running sessions + **1.1 ×** Σ caps of the sessions about to start
≤ **$500** (`spendDecision` extended to several caps). Three bases, stated: the in-session kill reads the live OTel sum
(fast, provisional); the stop rule reads settled figures, each the larger of the CLI's total and the published recompute
(§9); the report shows both. At the top of prediction 3 ($70 per process cell) the series needs ≈ $40 + 6 × $70 + $20 ≈
$480 — inside $500. If even $500 refuses a pair, the series stops there and the report says which cells are missing and
reports the process arm at the n it reached (§11). coai vendors, CodeRabbit and Actions on public repos do not bill this ceiling.

## 14. Growth

| Surface | Size | Retired by |
|---|---|---|
| Raw session dirs (stream, transcripts, OTel, audit, CPU, coai dir, gh JSON, config) | ≈ 300 MB peak per process cell, ≈ 30 MB per bare cell; ≈ 2.5 GB peak for the series, on ext4 outside git (`/home/jinx/rsd/minigit/<series>/`) | Trees (`bin/ obj/ node_modules/`) shed after scoring; the rest compressed to ≈ 200 MB archive; `retire.mjs` (safe-rm scoped to the runs root) after the report is promoted |
| `research/minigit/<series>-data.json` | a few hundred KB, committed | kept |
| Public PRs and `run/*` branches in the fork | 12 process session bases + smoke + pilot + re-runs ≈ 20, and ≈ 12–40 PRs; four `template/*` branches and `inputs` | kept permanently: the audit trail |
| coai session dirs per session | ≤ 20 MB each, inside the session dir | with the session dir |
| Vendor CLI stores (`~/.codex` …) | a few MB per session | not pruned by this plan — a decision: they are the operator's stores, written by the operator's own tools; their growth per session is measured and named in the report |
| Credentials: two fine-grained PATs, two deploy keys (the rulesets' only bypass actors), CodeRabbit app access | live for the series only | **teardown** after the last cell is scored and archived: revoke both PATs, delete both deploy keys, remove the bypass actor from every ruleset, record the completion with dates in the report; the rulesets themselves stay, now with no bypass |
| In-flight state | a session interrupted mid-run leaves its dir and possibly an open PR | `series.mjs --resume` re-runs void and never-started sessions; `voids.mjs` marks it; an open PR of a void session is closed by the harness with a comment |

## 15. Build order

1. **Conventions (other repo).** A short plan in `dew_flow_conventions/todo/` names this boundary from that side
   (`common.planning-docs` § *Across repositories*); register tokens `minigitcs` and `minigitnode` in
   `common/test-leftovers.md` `## Tokens` — and in the private twin repository that checks token uniqueness
   (`test-leftovers.md:298-300`); promote to `release`; cut `bench/minigit-2026-11` at that sha with a ruleset forbidding
   updates.
2. **Held-out suite + calibration** (references, mutants) — before anything a model touches; hash committed.
3. **Harness counters** (`claude-usage`, `coai-usage`, `prices`, `timeline`, `classify`) with their fixture tests,
   including T-A1 (banteg re-priced) — before any session exists.
4. **`proc.mjs` group kill** (RED first), audit hook extension, env export.
5. **Sandbox + coai bridge** (P0 proves vendor auth stays outside and reviews still run).
6. **Templates** (one source tree, `template-diff`) on the fork's four `template/*` branches, rulesets, CodeRabbit, deploy key, PATs, tag; then the
   **session runner and series driver** (`series.mjs`, `prepare-/run-/collect-/summarize-/score-session.mjs`, the
   compliance checker of §5.4) — all before step 8.
7. **Pre-register the analysis** (§11 first paragraph): statement, analysis contract, void rules, checklist, held-out hash,
   prediction — committed before any paid session **of the minigit task**. The one fixture-capture session of E1.S3 (an
   unrelated tiny task, ≤ $1, counted as pilot spend) measures nothing and precedes it.
8. **P0 smoke (no model):** a harness-authored PR per process template to `run/smoke-*/main` — base creation by deploy key, a direct
   push with the agent's PAT refused, CI on a non-default base, a planted lint/analyzer violation red, CodeRabbit reviews,
   merge blocked until threads resolve **and a CodeRabbit thread resolved with the agent's own PAT** (GraphQL
   `resolveReviewThread`) unblocks it — the agent must be able to do what the rule obliges it to do — push CI green;
   and the isolation checks of step 5 — from inside the sandbox the
   operator's home and the held-out suite are unreadable, the bridge refuses a `consult` whose `repoPath` is this
   repository, a `providers` call through the bridge lists the 5 vendors with their own sign-in.
   Then the **model smoke** (§8, ≈ $0.10) — the first paid call, before the A/A.
9. **A/A + P1 pilot (paid, void):** two identical bare C# v1 sessions concurrent (A/A: structurally alike, T1–T4 hold as §9
   defines them — T3 and T4 against O over matched sources —
   partition Σ = wall ± 1 s, unattributed < 5 %); then **one process Node v1** session, cap $35 — go only if it merged
   with green checks, coai rounds are in its db, CodeRabbit reviewed, the scorer's numbers are reproduced by hand, T1–T4
   hold, `num_turns` > 1, the audit and canaries ran, read-back matches the pins. Then a v2 base from the pilot merge (no
   session) and one bare Node v1 (≈ $1) to prove the bare scorer.
10. **Amend and draw:** pilot-driven changes recorded as dated amendments; commit `seed.txt`; draw.
11. **Series:** three blocks; stop rules of §13, D12 and §9's T3 (a stop once the A/A and P1 showed T's coverage).
12. **Report** `research/minigit/series-<date>.md` and the data JSON; hand checks of §5.3/§5.4; promote this plan.

## 16. Test plan

`node --test` under `spikes/minigit-bench/test/`, run lowered with the computed concurrency, fixtures in `test/fixtures/`:

| Id | Guarantee |
|---|---|
| T-A1 | banteg's six C# blocks priced with its formula give 2.74535/trial (renders $2.75); the corrected formula gives 0.6037 ($0.60); our coai reader given codex-shaped rows never counts cached twice |
| T-A2 | a stream repeating one `message.id` ×3 with placeholder output dedupes to one, and the reader refuses to price from stream output |
| T-A3 | envelope `usage` (main only) against `modelUsage`: totals come only from `modelUsage`; a child-only model is not lost |
| T-A4 | 5m/1h: a split message prices exactly; an unsplit one yields an interval |
| T-A5 | the CLI-billed view reproduces the scout pilot's Sonnet $1.10243 while the published view stays at $0.10/M |
| T-A6 | coai ledger: null cost reads "unknown", "usage not captured" gives a lower bound, cached > in is flagged |
| T-T1 | partition over overlapping parent/child spans of mixed categories: Σ = wall, unions right, Agent containers excluded |
| T-T2 | the command classifier, including the literal `sleep 300` and `gh pr checks --watch` |
| T-T3 | OTLP/HTTP JSON decoding |
| T-P1 | `killTree` ends a grandchild on Linux — RED on `proc.mjs:35` as it stands, then green |
| T-P2 | contamination scan: a foreign `run/` ref, another `RUN_ID`, the other stack's `template/*`, a push outside the session's namespace each flagged; POSIX paths resolved |
| T-S1 | scorer: a failing build fails the session, test scripts are restored from pinned hashes, the fixed `test-v2` lines fail on a wrong parent |
| T-S2 | held-out calibration: both references 100 %, each mutant fails exactly its registered cases |
| T-G1 | spend decision with several caps, the 1.1 × overshoot factor and a live running sum |
| T-B1 | the bridge refuses a `tools/call` whose `repoPath`, `committedPlan` or `suspectedFiles` resolves outside the session (symlinks and `..` included) and passes one inside |
| T-M1 | alias resolution: a session using a new id for an alias it used stops the series; a session not using an alias does not |
| T-V1 | void and re-run: namespaces `.a<k>`, a v2 re-run from the recorded v1 base, `ran unpaired` labelling, a second void leaves the cell missing |
| T-R1 | `rid` is distinct across arm, block, stack, phase and attempt; the block-order draw only yields patterns where each arm leads |
| T-C1 | `costFromMessages` is never called by the new spike |

Live checks: P0 and P1 (§15 steps 8–9). Read-back per arm and stack (§8).

## 17. Definition of Done

- [ ] Every input except the arm and the stack is pinned in the harness, and one session per arm and stack was read back to
      prove the arms differ only there (measurement rule 1).
- [ ] The held-out suite and the compliance checker were hand-checked before their numbers appear (rule 2).
- [ ] `prediction.md` was committed before `seed.txt`, and the report puts observation beside prediction (rule 4).
- [ ] T1–T4 held for every data session, or the breach is in the report with what was done.
- [ ] Every cost carries its basis; every coai USD is a number or "unknown", never 0; void and pilot spend are separate.
- [ ] Time is partitioned (Σ = wall) with active time as the process arm's comparison; no span sums appear.
- [ ] The report names subject shas (templates, conventions snapshot, coai), the harness path and sha, the date, the CLI
      versions, the machine, and every pinned variable (rule 6); its conclusion is no wider than §11 allows (rule 3).
- [ ] Every failing held-out case was classed by hand; dropped cases are named.
- [ ] Spend stayed within $500, or the stop and the missing cells are reported.
- [ ] `node --test` green for the spike; `proc.mjs`, `audit.mjs`, `env.mjs` changes each have a test watched failing first
      where they fix a defect.
- [ ] Credentials torn down: both PATs revoked, both deploy keys deleted, no bypass actor left on any ruleset (§14).
- [ ] `research/minigit/` and `research/README.md` updated; this plan promoted with its deviations; worktree removed.

## Epics

Four epics on the build order of §15, each one branch from the previous epic's commit, one reviewable diff and a usable
state at its end; each names the sections and §15 steps it implements. Stories are written one epic at a time into
`todo/PLAN_minigit_stack_bench_E<n>.md` when that epic starts (`common.planning-docs` § *A plan split into epics and
stories*); that document links back here, and the intended stories (`S1`…) are listed per epic below so the size is
visible. Cornerstone AREAS are named per epic; the concrete cornerstone stories are marked by the epic's stories document,
at most Q = ⌈2·E/5⌉ = 2 for E = 4. Four areas are named for two marks, so the order of preference is: (1) the accounting
and time model (E1); (2) the held-out acceptance instrument and its adjudication (E1); (3) isolation and treatment
fidelity — both sandboxes, the bridge, the read-back (E2); (4) the go/no-go criteria of P0, the model smoke, the A/A and
P1 (E3). **At most one mark per epic**, so the two marks land on the first-ranked area of E1 and on E2's isolation —
otherwise both would go to E1 and the bridge and sandboxes would get no Fable spec (main-session decision). A mark that
would exceed Q goes to the operator. Template symmetry (E2) gets a strict spec without a mark;
conventions rows and branch, GitHub provisioning, env export and report rendering are ordinary work. Nothing here or in
a stories document names a held-out case, reference or mutant in clear (§5.1).

Cornerstones: 1 of 2 — E1.S3

### E1 — Measurement instruments: held-out suite and scorer, counters and the time partition (§15 steps 2–3 · 4 stories)

- **Stories:** [PLAN_minigit_stack_bench_E1.md](PLAN_minigit_stack_bench_E1.md) — E1.S1–E1.S4; cornerstone **E1.S3**
  (`model: fable`, the Claude counters and T1–T4; spec by Fable, which replaced Opus's draft).

- **Goal:** correctness, cost and time instruments calibrated before any model touches the task (§2 O1–O5, O7; V5, V14).
- **Scope:** §5.1–5.3 — `hashes.txt`, the visible-test edits, the scorer, the held-out suite by shape (state built by the
  cell's own `init`/`add`/`commit`, `blocked by H1-<k>`, a SPEC sentence per case), D13, D15, references, M ≥ 8 mutants,
  the blind two-step adjudication; §9 — E/O/T views, prices, OTel receiver, T1–T4 over what T records, D12; §10.
- **Depends on:** the read-only `banteg@83644e0` clone; the coai-751 fixtures; `research/scout-calibration/prices.json`.
- **Done when:** T-S1, T-S2, T-A1–T-A6, T-T1–T-T3, T-M1, T-C1 green; T-A1 gives 2.74535 → 0.6037; references 100 %, each
  mutant fails exactly its cases; sha256, N1, N2, M committed; the suite is in clear only inside a scoring temp dir.
- S1 — pinned inputs, the visible-test edits with their header, the scorer pipeline on a pristine copy (T-S1).
- S2 — the held-out suite, encryption at rest, references, mutants, the adjudication procedure (T-S2).
- S3 — Claude counters: E/O/T views, dated prices, OTel receiver, T1–T4, the alias record (T-A1–T-A5, T-M1, T-C1).
- S4 — the coai usage ledger and the exclusive work-before-wait time partition (T-A6, T-T1–T-T3).
- **Cornerstone areas:** the accounting and time model (cross-check thresholds, conditional T3 stop, partition, bounded
  claim); the held-out instrument and its adjudication (cell-built state, SPEC traceability, blind classing, calibration).

### E2 — Isolation and the treatment, pinned: process fixes, both sandboxes, bridge, templates (§15 steps 1, 4–6a · 4 stories)

- **Goal:** what a session can reach, what it received and what each stack was given are proven — prevented where
  possible, audited where not (§7.3) — from one template source differing only inside marked regions.
- **Scope:** §8's `policy-ab-quick` changes; §7.1 `sandbox.mjs`; §7.2 coai's own bwrap and `coai-bridge.mjs`; §7.3–7.7
  clones, contamination scan, canaries, token scan; §8 argv, settings (D11, D16, D17), env, watchdogs, janitor, read-back;
  D10, D14; §15 step 1 (D9); §6 templates, `template-diff`, `treatment.md`, CI split, lint gate, tag, GitHub, rulesets.
- **Depends on:** E1's OTel receiver (cost and stall watchdogs); the operator's go for the snapshot branch (another repo).
- **Done when:** T-P1 red on `proc.mjs:35` then green; T-P2, T-B1 green; the bridge refuses a `consult` on this repository
  while a review runs with sign-ins outside; `pin-check` green on the snapshot tip; all four template branches tagged; `template-diff`
  clean; a planted violation red under `npm run lint` on typescript 7.0.2; `treatment.md` names each stack's strictness.
- S1 — `policy-ab-quick` fixes and the contamination scan: group kill, audit hook, env export, POSIX paths (T-P1, T-P2).
- S2 — the agent sandbox and launch: bwrap binds, argv and settings files, credentials, watchdogs, janitor, the read-back.
- S3 — coai in its own bwrap, the stdio↔socket bridge refusing out-of-session paths, the coai settings snapshot (T-B1).
- S4 — the treatment pinned: conventions tokens and snapshot branch, one-source templates, lint gate, tag, GitHub setup.
- **Cornerstone areas:** isolation and treatment fidelity (both bind lists, the bridge on every `tools/call`, read-back).

### E3 — Runner, compliance checker, pre-registration, pilot: P0, smoke, A/A, P1, draw (§15 steps 6b–10 · 4 stories)

- **Goal:** one path runs a cell under the plan's pair, barrier, order, stop and void rules; proven on void spend first.
- **Scope:** §4.1–4.3 blocks, `drawOrder`, barrier, `taskset`, `cpu-sampler`, prompts; §8 `series`, `preflight`, session
  verbs, `argv`, `retire`; §5.4 `checklist.json`, `github.mjs`, the 18-item checker and its hand-check protocol; §11.1
  `voids.mjs`; §13 `spendDecision`; §14 `--resume`; snapshot-tip check; §11 pre-registration; §15 steps 8–10; §8 smoke.
- **Depends on:** E1 and E2 complete; the $500 ceiling counts the pilot (§13).
- **Done when:** T-G1, T-V1, T-R1 green; a dry cell on a stub directory emits every §11 field or "not captured"; the
  pre-registration is committed before the first paid session; P0 passes every step-8 check on both process templates; the smoke lists
  5 vendors through the bridge; A/A and P1 meet every step-9 condition; dated instrument-only amendments; the draw.
- S1 — series driver and session verbs: blocks, barrier, draw, masks, voids, re-runs, resume, spend (T-G1, T-V1, T-R1).
- S2 — the compliance checker over GitHub, `coai.db` and the stream, with the stratified hand-check protocol (§5.4).
- S3 — pre-registration committed; P0 (no model) on both process templates; the ≈ $0.10 model smoke through the bridge.
- S4 — A/A, P1 (process Node v1), the pilot v2 base, the bare Node v1; dated amendments; `seed.txt`; the draw.
- **Cornerstone areas:** the go/no-go criteria of P0, the smoke, the A/A and P1 — what each must show, what a miss
  means, what is amended and what stops (the no-stack-tuning rule). Running them, the driver and the checker are ordinary.

### E4 — Series, hand checks, report, teardown and promotion (§15 steps 11–12 · 3 stories)

- **Goal:** the twelve cells run under the pre-registered contract and the report says no more than §11 licenses.
- **Scope:** §4.1 three blocks with the stop rules of §13, §9 T3 (once the pilot showed T's coverage) and D12; §11.1
  re-runs; the by-hand read-back of one cell per arm and stack in block 1 (§8); the blind hand classing of every failing
  held-out case (§5.3), the ≥ 20-verdict compliance hand check (§5.4); `report.mjs` → `research/minigit/series-<date>.md`
  and the data JSON (per-session, per-stack × arm, pair-ratio and interaction tables, bounds for censored cells, the §12
  sentences, every cost with its basis); §14 archive, `retire.mjs`, credential teardown; promotion with deviations.
- **Depends on:** E3's draw; the operator's decision when the projection after block 1 exceeds the ceiling (§13).
- **Done when:** every §17 box is checked; every cell appears with its outcome, delivered or not; T1–T4 breaches are
  reported with what was done; spend ≤ $500 or the stop and missing cells named; PATs revoked, deploy keys deleted, no
  bypass actor left; session directories archived, `retire.mjs` run; the suite's case list published beside the report.
- S1 — the three blocks with stop rules and re-runs, and the block-1 by-hand read-back.
- S2 — hand checks: blind classing of failing held-out cases, compliance verdicts, the checker fixed before any number.
- S3 — the report and data JSON, credential teardown, archive and retire, promotion of this plan and its stories files.
- **Cornerstone areas:** none new — the report renders what E1 and E3 fixed; rendering, teardown, promotion are ordinary.
