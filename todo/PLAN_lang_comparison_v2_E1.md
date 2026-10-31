# PLAN — language comparison v2, E1 stories: the task repository — the runner, the execution layer, FORMAT v2, samples, specs gated and frozen after a pre-freeze execution gate, workspaces, toolchains, caches, process templates

> Status: **plan only, nothing implemented yet, 2026-10-31; rewritten for the parent's epoch 4.** Epic E1 of
> [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md) (§14 step 1), written when the epic started — a stories document is
> not a plan change (`common.plan-change-control`) and keeps no ledger: its changes are entries in the parent. Its first
> version (`51a80ba`) found the two changes that stopped epoch 3 (3.1 and 3.2); the epoch-4 re-review judged it `fits` and
> asked for this rewrite — E1.S8b (the execution layer, D43), E1.S7 aligned with the GitHub broker (D42, built in E3.S7a),
> OP-2 to OP-11 as text, and no trap content (D41). The parent is read at `0ea0e25` (`docs/plan-lang-v2-epoch4`, PR #129,
> open when this was written). Twelve stories (E1.S1 … E1.S9, with E1.S2a, E1.S8a and E1.S8b); cornerstone **E1.S2**
> (`model: fable`), the one mark the parent's ledger gives this epic (`Cornerstones: 3 of 3 — E1.S2, E2.S2, E3.S2`); no further
> mark is possible. Scope: the public task repository `dew_flow_ai_language_comparison` (created by the owner: MIT, one
> LICENSE commit), the spike `spikes/lang-bench/` in this repository, and the private area `~/rsd/lang-v2/` outside every git
> tree. Open points: §9 — one of them (the conventions submodule under D42) may be a change; none is decided here.

## 1. The epic, restated (parent § *Epics*, E1)

**Goal.** Ten specs written by the team lead, gated and frozen; ten workspaces per stack an agent can start in, identical
across stacks but for the stack, on pinned toolchains and offline caches; the one execution layer every program of the series
runs on; the process arm's run-base machinery; in a public repository that leaks nothing (parent D2, D5, D6, D11, D12, D19,
D21, D25, D31, D35–D43; §4.1, §4.5, §4.6, §5, §6.1).

**Done when (parent).** T-L1, T-L2, T-L13, T-L20's guard half green; `workspace-diff` clean on ten tasks; both snapshots
hashed; the preflight passes inside a box on SDK 10.0.403, Node 26.13.0, **TypeScript 6.0.x** (the patch pinned in E1.S5) and
the pinned Python; the execution layer's probes T-L30 … T-L34 green on the series host (E1.S8b); `FORMAT.md` v2 gated and its
three examples run; the ten specs conform to it, carry a gate verdict and the freeze tag; the README carries the credits and
the Node stack table; `leakcheck` clean.

**Not in this epic** (do not build here): the held-out suites, mutants, trap coverage table and limit calibration (E2); the
drivers' scenario logic and records, the judge's case loop, scoring per kind, D15's environment in the **agent** box, the
arm/task/round runner, the coai box and bridge, **the GitHub broker (E3.S7a, D42)**, the process runner, the compliance
checker (E3); the pre-registration and the session cap of **$50** (E4; the owner's fixed per-session cap, D7). **Nothing in
this epic launches `claude` for a data or pilot session**; the coai document gate and consultant calls of S2 and S9 are
review calls, not sessions under test.

**The held-out boundary (D41).** Both repositories are public. **No trap, trap-catching scenario, riskiest-contract scenario,
held-out case, mutant or reference appears in this document, in a commit message, a PR body, a test name, a fixture or a
code comment.** The test-design aid lives at `~/rsd/lang-v2/test-design/PLAN-trap-table.md` and
`~/rsd/lang-v2/test-design/PLAN-risky-scenarios.md` (mode 0600; their sha256 are in the parent's D41); the draft references at
`~/rsd/lang-v2/refs/`. This document points at those paths and names no content of them; its author did not read them.

## 2. What exists — verified 2026-10-31 (bench `53f248e`, parent read at `0ea0e25`)

| Fact | Where |
|---|---|
| **The public repository exists:** `oleksandrdubyna88/dew_flow_ai_language_comparison`, public, MIT, default branch `main` = `ed68ce1` (the LICENSE commit only) | `git ls-remote`, `gh repo view --json visibility,licenseInfo`, 2026-10-31 |
| **The local task repository has no remote**, and its history is unrelated to the remote's: `main` = `28bf777` (skeleton `b5e9598`, pilot spec 01 `60e468a`, FORMAT v1 `d7c73ff`, 01 conformed `28bf777`); `docs/spec-05` = `30af434` (spec 05 + four scenario samples); `docs/spec-04` = `0b69a50` (spec 04; its samples only in the spec text) | `/home/jinx/git/dew_flow_ai_language_comparison`, `git remote -v` (empty), `git log --all` |
| `main`'s tree: `README.md` (4 lines), `.gitkeep` in `csharp/`, `node/`, `tasks/`, `todo/tasks/`, the spec `01-descent-to-one.md`, `FORMAT.md` v1 (884 lines). No `AGENTS.md`, conventions pin, `runner/` or sample files for 01 | `git ls-tree -r main` |
| FORMAT v1: §1–§2 spec file, header, fixed sentences; §3 sample pairs; §4 the `scenario 1` grammar; §5 the `transcript 1` comparison; §6 three toy examples (the daemon toy names a real protocol); §7–§9 maintainer sections that name plan ids; §1.5 names `NOTES.md` | `git show main:todo/tasks/FORMAT.md` |
| Specs 01, 04, 05 hard-code T = 10 s and G = 5 s (the series defaults, D14); their samples are FORMAT v1 | the three branches |
| v1's held-out runner protocol (`/usr/bin/python3 -I runner.py --phase --binary --work --box <json argv> --timeout --out`, result `format: 1`); a synthetic Python suite written from JS strings into a test's folder; the spike test runner shape | `spikes/minigit-bench/lib/heldout-run.mjs:19`, `:116`, `:172`; `test/toy-suite.mjs:1-60`; `run-tests.mjs:1-41` |
| bwrap primitives and v1's scoring box: `isolation()` = `--unshare-net --unshare-pid --die-with-parent --new-session`; `scoreEnv` and `agentEnv` set `LANG: 'C.UTF-8'`; `scoreBoxArgv` | `spikes/minigit-bench/lib/bwrap.mjs:15`, `:32`, `:37`, `:58`; `lib/sandbox.mjs:93`, `:97` |
| Reusable: `fetchInputs`, `parseHashes`, `verifyFiles`, `treeHash`, `makeSnapshot`, `registerSnapshot`, `packagesOf`, `leakcheck` (literal tokens and six-word shingles), `runBounded`, `ok`/`fail`, `refuseInsideWorktree`, `preflight`, `removeUnder` | `spikes/minigit-bench/lib/fetch-inputs.mjs:57`; `inputs.mjs:11`, `:49`; `cache-snapshot.mjs:36`, `:162`, `:233`; `packages.mjs:54`; `heldout-leak.mjs:32`, `:111`; `run.mjs:35`; `result.mjs:2-3`; `git-guard.mjs:28`; `preflight.mjs:56`; `spikes/policy-ab-quick/lib/safe-rm.mjs:52` |
| The owned-name helper of `common.test-leftovers` (`ownedDir`, `ownedFor({ token })`) and the barrier scan | `.agents/conventions/tools/lib/owned.mjs:612`, `:725`; `.agents/conventions/tools/sweeper-scan.mjs` |
| `spikes/lang-bench/` does not exist | `ls spikes/` |
| Host: Python 3.14.4 at `/usr/bin/python3`; bubblewrap 0.11.1; `systemd-run`; cgroup2 with `memory`; **`locale -a` lists `de_DE.utf8`** (generated by the owner, 2026-10-31); the D36 scope probe (`memory.peak` readable) and the D43 namespace probe (`unshare -rn`, `lo` up, a Python server and driver exchanged `ping→PING` over 127.0.0.1 in one netns; `--mount-proc` refused) as the parent records them | `python3 --version`, `bwrap --version`, `/sys/fs/cgroup/cgroup.controllers`, `locale -a`; parent D36, D43 |
| The bench repository is public; the trap aid that stood in its plan moved to the private area (D41), the merged history keeps the earlier rows (T18) | parent D41, T18 |

## 3. Conventions for every story

- **Where things live.** Bench: `spikes/lang-bench/` — plain ESM `.mjs` on Node 26, no dependency (v1's spike conventions,
  [PLAN_minigit_stack_bench_E1.md](PLAN_minigit_stack_bench_E1.md) §3), plus the runner in Python 3 standard library only.
  Task repository: only what parent §5 lists — no tool, and **no `todo/PLAN_*`**: it would enter every run base; the
  cross-repository boundary is named in the README's `## Attribution` section (cut from every run base) and in the parent
  (D39). Private area `~/rsd/lang-v2/`: everything that must never be in git.
- **No trap content in public (D41).** Commit messages, PR descriptions, review bodies, test names, fixtures and comments
  describe a check by its mechanism ("a split frame", "a race of two clients"), never by the defect it is meant to catch in a
  stack. A brief that needs the aid gives its **path**; the reader opens it on the host, outside every repository.
- **Shape.** Functions < 50 lines, cyclomatic complexity ≤ 4, files 200–400 typical and 800 max (PROJECT §6,
  `common.coding-style`), in Node and Python alike; expected failures are values, never exceptions for control flow; absent is
  not zero (`{ known: false, why }` renders `unknown`).
- **Tests.** Everything runs through `node spikes/lang-bench/run-tests.mjs [glob…]` (E1.S8a creates it, v1's shape: lowered,
  `max(1, min(8, threads − 1))` workers). A test that needs `bwrap`, `systemd-run`, user namespaces, a toolchain or the task
  repository checkout and cannot find it SKIPS WITH ITS REASON; `BENCH_REQUIRE_SANDBOX=1` (the epic's completion run, parent
  §15) turns every such skip into a failure. Python units run under `python3 -I -m unittest`, started from one Node test file.
- **Leftovers (`common.test-leftovers`).** Every folder a test makes comes from `ownedFor({ token: 'bench' })`; a Python child
  gets an owned folder through `TMPDIR`. A test's transient systemd scope and its namespace holder are named with the owned
  prefix and removed by the test by exact name. Before the first owner-form producer lands,
  `node .agents/conventions/tools/sweeper-scan.mjs` lists no owner-blind sweeper; if it lists one, the coder stops and
  reports (§ *The barrier*).
- **Processes (`common.security`, `common.reliability`).** exe + argv, never a shell string; every wait has a ceiling; a timeout
  kills the whole process group or scope; every buffer and file the harness keeps is bounded.
- **Red, green, red.** A test written first and watched red **for the real symptom**, then green, then the production line the
  behaviour depends on removed and the test watched red again before it is restored (`common.testing`); both observations in
  the story's summary. A guard against a plausible wrong approach is named for it (`…_would_…_and_that_is_the_point`).
- **Roles (`common.subagent-models`).** Scenarios and briefs here are Opus's; code and tests by a Sonnet coder; review by Opus;
  fixes by Sonnet. The task specs, the samples and FORMAT v2 are the team lead's (Opus, main session, D21); E1.S2's story
  brief is Fable's — two kinds of "spec" (OP-10, now D21's text): Fable writes the story's per-task spec briefs, the team lead
  writes the task specs. Every launch names its model.
- **How work lands in the task repository.** Through pull requests with CI once E1.S1's first push has happened
  (`common.task-lifecycle`); until then a story branch waits locally. The bench side lands through pull requests as always.

## 4. The operator's part and the push

The owner has created the repository (public, MIT, `ed68ce1`), generated `de_DE.UTF-8` (`locale -a` lists `de_DE.utf8`) and
decided the licence of the repository's own content (MIT). **Still open and named where used:** the credit terms of the known
tasks' sources (the rest of Q1 — the push of a known-task text waits for it, below); the fine-grained PAT and the deploy key
(E1.S7); E0's parity release for the final conventions pin (E0.S3; E1.S1 pins the current `release` provisionally).

**E1.S1 starts from the remote.** The local skeleton and the drafts reach GitHub only through the pre-push check, one branch
per push:

1. `git -C <task repo> remote add origin https://github.com/oleksandrdubyna88/dew_flow_ai_language_comparison.git` (HTTPS:
   SSH to GitHub times out from this host) and `git fetch origin`.
2. **Join the two histories without rewriting either:** on a branch `chore/join-remote` from the local `main`,
   `git merge --allow-unrelated-histories origin/main` (it brings `LICENSE`; the local files and the remote's do not overlap,
   so no conflict is expected — if one appears it is resolved by hand and named). Force-push is never used.
3. **The pre-push check** `node spikes/lang-bench/push-check.mjs --repo <task repo> --branch <b>` over exactly the commits the
   push would add (`git rev-list <b> --not --remotes=origin`), each a named line, exit 0 / 1 / 3 / 4:
   - `leakcheck` (`spikes/minigit-bench/lib/heldout-leak.mjs:111`) with the v2 leak tokens **and six-word shingles of the two
     private aid files**, both read by the tool from `~/rsd/lang-v2/test-design/` at run time — a hit is reported by commit,
     path and line, never by the matched text;
   - the attribution section present in `README.md` only; no credit, link or bench pointer elsewhere;
   - no plan story, epic, decision or test id in an agent-visible path (`csharp/`, `node/`, `tasks/`, `runner/`, `todo/tasks/`);
   - no secret (the family's token shapes);
   - **a known-task text** (specs 01, 02, 05, 06, 09) only when the credit terms of its source are recorded in
     `spikes/lang-bench/inputs/credits-lang.json` — otherwise that branch is refused and stays local.
4. Push `chore/join-remote` alone and open its PR into `main` (a normal merge on GitHub, CI from E1.S7 not yet required); then
   every other local branch alone, each its own PR. **A tag is never in a push with anything else**; the freeze tag, when it
   exists, is pushed alone and checked to have started no unexpected workflow (`common.task-lifecycle` §3).
5. The bench's `spikes/lang-bench/lib/task-repo.mjs` `TASK_REPO_URL` is the GitHub URL from then on; a local-path fetch stays
   for tests only.

## 5. Story map

| Id | Title | Size | Depends on | Model |
|---|---|---|---|---|
| E1.S1 | Repository skeleton from the remote, the attribution cut, the provisional conventions pin, the pre-push check and the first pushes | S (~350 lines) | the remote (exists) | coder sonnet; operator for the credit terms |
| E1.S8a | The baseline visible-sample runner `runner/`, pinned | L (~2,000 Python + ~900 tests) | — | coder sonnet · brief opus |
| E1.S8b | **The execution layer**: bwrap launch, D15's environment with the culture, the program-only scope, the shared loopback-only netns, deadlines, signals, cleanup; probes T-L30 … T-L34 | L (~900 + ~700 tests) | S8a, S5 | coder sonnet · brief opus |
| E1.S9 | `FORMAT.md` v2, checked against the runner, gated before any spec gate | M (the document; ~150 lines of tools) | S8a | writer: team lead (opus) · extractor: sonnet |
| E1.S8 | FORMAT v1 (built; its gate is v2's) and the conformance check derived from v2 | M (~550) | S9 draft, S8a | coder sonnet |
| E1.S5 | Toolchains pinned read-only with checksums (TypeScript 6.0.x); the preflight | S (~300) | — | coder sonnet |
| E1.S4 | Package sets and the two cache snapshots; `PACKAGES.md` from the manifest | M (~350) | S5 | coder sonnet |
| E1.S3 | The workspaces: templates (phase A), the ten task pairs after the freeze (phase B) | M (~600) | S4, S5, S8a, S8b; phase B: S2 frozen, S6 | coder sonnet |
| E1.S6 | The visible samples, per task, before the spec's conformance gate | M (10 tasks) | S9, S8 | author: team lead (opus) |
| E1.S2 | **The ten task specs, gated and frozen** | L (10 specs) | S9 gated, S8, per task S6, S2a and the D40 gate | **`model: fable`** (story brief) · spec text: team lead (opus) · review opus |
| E1.S2a | Draft references outside git; the pre-freeze tool on the execution layer | L (20 programs outside git; ~350-line tool) | S3 phase A, S4, S5, S6, S8b, the spec draft per task | coders sonnet (one launch per program) · briefs: team lead (opus) |
| E1.S7 | Process-arm templates, CI, rulesets, deploy key, PAT; run bases as orphan generated trees, shaped for the run-bound broker | M (~550) | S1's first push, S3 | coder sonnet; operator (PAT, deploy key, each GitHub write) |

**Waves (§14 step 1's order).** W1: **S8a** ∥ S5 ∥ S1. W2: **S8b** (needs S8a and S5) ∥ **S9** drafted against S8a, then gated
∥ S4. W3: S8 ∥ S3 phase A (on S8b's layer). W4, per task in the order of §4.1 (the pilot specs 01, 05, 04 first, re-conformed
to v2): S6 → S2 draft and conformance → S2a → **the D40 pre-freeze gate on S8b's layer** → S2's document gate, consultant and
bias review → gated. W5: the freeze commit and tag after the tenth; S3 phase B. S7 any time after S1's first push and S3
phase A.

---

## E1.S1 — repository skeleton from the remote, the attribution cut, the provisional conventions pin, the pre-push check

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus · operator (the known tasks' credit terms).

**Goal.** The task repository on GitHub has the layout of parent §5, a README whose credits, links and the bench pointer live
in one attribution section that a generated run-base tree never carries, the family's conventions pinned, and a push path
that cannot publish a leak — so every later story fills a skeleton rather than inventing one.

**Files.**

| Repository | Path | Change |
|---|---|---|
| task | `README.md` | rewritten: what this is; the task list in our own titles; the §4.6 Node stack table and the Fastify reasoning; the closing `## Attribution` section with every credit, every link and the pointer to the bench plan — the only place any of them appears |
| task | `LICENSE` | from the remote's `ed68ce1` (MIT), by the history join of §4 |
| task | `AGENTS.md`, `CLAUDE.md`, `.agents/PROJECT.md`, `.gitmodules`, `.agents/conventions` | the family's adapters; the conventions submodule at the current `release` sha (provisional; E0.S3 moves it to the parity release on a snapshot branch; how a process box gets it under D42 is §9 OP-A) |
| task | `todo/README.md`, `research/architecture.md`, `research/module_tests.md` | the index (FORMAT and the specs, no plan file); stubs as v1's templates; `module_tests.md` names CI's visible-sample job as the repository's scenario harness |
| task | `runner/.gitkeep`, `tasks/.gitkeep`, `csharp/.gitkeep`, `node/.gitkeep` | the folders of §5 |
| bench | `spikes/lang-bench/lib/run-base-tree.mjs` | new — `generateRunBaseTree({ repo, ref, out })` (the tree at `ref` minus the README's `## Attribution` section, minus any credit file) and `needleScan({ root, needles })`; S7 commits that tree as the orphan run base |
| bench | `spikes/lang-bench/lib/workspace-diff.mjs`, `workspace-diff.mjs` | new — per task, `csharp/<t>/` and `node/<t>/` differ only in the marked stack and framework sentences and the stack's pin files |
| bench | `spikes/lang-bench/push-check.mjs`, `lib/push-check.mjs`, `inputs/credits-lang.json` | new — §4's checks; the credit terms per known task (filled when the owner answers) |
| bench | `spikes/lang-bench/test/{run-base-tree,workspace-diff,push-check}.test.mjs` | new |

**Scenarios.**

- *Given* a README with an `## Attribution` section holding a credit, a link and the bench pointer, *when*
  `generateRunBaseTree` runs, *then* every other section is byte for byte the same and no D18 or D39 needle is found in the
  tree. **Failure cases:** a README without the section, or with it twice, is refused; a credit planted under `tasks/`,
  `runner/` or a spec makes the needle scan fail naming file and line.
- *Given* an empty task pair, *then* `workspace-diff` passes; *given* an extra file in `node/<t>/` or one byte changed outside
  a marked sentence, *then* it fails naming the path.
- *Given* a branch whose new commits hold a known-task spec and no recorded credit terms, *then* `push-check` exits 1 with
  `known-task text without its credit terms`; *given* a leak token or an aid shingle in an old commit of the branch, *then* it
  fails naming the commit and path, never the text.
- *Given* the local `main` and the remote's unrelated `main`, *when* §4 step 2 runs, *then* `chore/join-remote` holds both
  histories and `LICENSE`, and `git push` of it needs no force.

**Acceptance (parent S1 and OP-8 as its text).** `workspace-diff` passes on an empty task pair and fails on a planted
difference; the README's links and attribution appear nowhere under `csharp/`, `node/`, `tasks/`, `runner/` or `todo/tasks/`,
and the generated run-base tree holds none of them (a test greps the tree; the one-commit `git rev-list` is S7's, after the
push). Plus: `push-check` exits 0 for every pushed branch and its output is kept with the PR; the conventions pin is the current
`release` and `pin-check` is green; the first pushes happened as §4 says, with the shas recorded.

**Test plan (red, green, red).** `A_README_copied_whole_would_carry_the_credits_into_every_run_base_and_that_is_the_point`
(red against a generator that copies the tree, green with the cut, red with the cut line removed);
`Workspace_diff_fails_on_one_byte_outside_the_stack_sentence`; `Push_check_refuses_known_task_text_without_credit_terms`;
`Push_check_reports_an_aid_shingle_by_position_and_never_by_text` (with a synthetic aid file in an owned folder); the positive
companion of each scan, `The_needle_scan_still_finds_a_planted_needle`.

**Docs.** Task repository: `README.md`, `todo/README.md`, the research stubs. Bench: `research/architecture.md` names
`spikes/lang-bench/` in its module map; `research/module_tests.md` adds the lang-bench test command.

**Brief (Sonnet coder).** Worktrees: bench `feat/lang-v2-e1-s1`, task repository `chore/join-remote` then `chore/skeleton`.
(1) `generateRunBaseTree`: `git archive <ref> | tar -x` into an owned folder (never a checkout in a working tree), cut
`README.md` from the line `## Attribution` to the next `## ` or the end. (2) `needleScan`: every regular file, the literal
needles from a file given by argument (outside git), plus `https?://`, `\bE[0-9]\.S[0-9]+[ab]?\b`, `\bT-L[0-9]+\b`, `\bG-[0-9]+\b`
and `\bD[0-9]{1,2}\b` in agent-visible paths; report `{ path, line, needle }`. (3) `workspace-diff`: file sets minus each
stack's pin-file list (C#: `global.json`, `nuget.config`, `Directory.Build.props`, `*.csproj`; Node: `package.json`,
`package-lock.json`, `tsconfig.json`, `.npmrc`, `eslint.config.mjs`), then bytes; `STATEMENT.md` may differ only on the lines
after `<!-- stack-sentence -->` and `<!-- framework-sentence -->`. (4) `push-check`: compose the checks of §4 over
`git rev-list <branch> --not --remotes=origin`; read the aid files and the token list only from paths given by argument; print
positions only. (5) The README text is the team lead's; you write its structure check. Commands:
`node spikes/lang-bench/run-tests.mjs test/run-base-tree.test.mjs test/workspace-diff.test.mjs test/push-check.test.mjs`.

---

## E1.S8a — the baseline visible-sample runner `runner/`, pinned

**Model:** scenarios + brief Opus · code Sonnet · review Opus. **Not marked** (the ledger is full); the spec is strict instead —
it is the one program the agent, CI, the pre-freeze gate and E2's held-out scoring all share (D37).

**Goal.** A Python standard-library runner reads a directory of `<id>.in` / `<id>.out` pairs, runs `bash build.sh` and `bash
run.sh` as a spec says, drives an olympiad case, a daemon scenario or a web scenario, compares what it observed with the
expected transcript under FORMAT v2's rules and reports `PASS`/`FAIL` with the first mismatch — built **before** FORMAT v2 is
written down, so that v2 is checked against a program that exists. It knows nothing of boxes, scopes or namespaces: the
execution layer (S8b) hands it a launch prefix and the cgroup to read.

**`runner/` is a directory** (OP-5, now the parent's S8a text): standard-library modules with `run_samples.py` as the entry,
hashed as one manifest in `spikes/lang-bench/inputs/specs.json`, the frozen list naming `runner/`; under `-I` Python leaves the
script's directory off `sys.path`, so the entry adds its own frozen, read-only directory itself.

**The grammar it implements — FORMAT v1 §4–§5 plus this delta, which E1.S9 writes into FORMAT v2.** The team lead may change a
row while S9 is drafted; the runner follows in the same branch and S9 is gated against the result.

| # | v2 decision | Runner behaviour |
|---|---|---|
| V1 | the version: `scenario 2` / `transcript 2` | any other version is a format error naming the file (exit 4); v1 files are re-conformed, never run |
| V2 | **exact-decimal JSON numbers**: an integer and a decimal compared by exact value (`int`/`Decimal`, never `float`); `-0` equals `0`; `2` equals `2.0` | `json.loads(…, parse_float=Decimal, parse_int=Decimal)`, constants refused, duplicate member names refused |
| V3 | **default request headers**: `Host: 127.0.0.1:<PORT>` and, with a body, `Content-Length: <n>`, nothing else; a `header` line whose name equals a default (ASCII case-insensitive) replaces it in place | the serialiser of v1 §4.3 with the replacement rule |
| V4 | **`connect` retries** a refused connection every 50 ms until its deadline; still refused → `refused <conn>`, the scenario ends | as stated; the readiness probe keeps v1's 50 ms retry within T |
| V5 | **a closed HTTP connection**: a request that meets a connection the program closed before a single response byte arrived is rewritten once on a new connection within the same step deadline; a second close is `eof <conn> ""` | at most one reconnect per request |
| V6 | **relational expectations** in `.out`: a normalised field may bind what it matched, `${:<type>=<name>}` (inside `repeat` the name may carry `${i}`); `check distinct <name>…` (names or a `<prefix>*` glob; pairwise distinct as bytes or exact JSON values) and `check equal <name> <decimal \| name>`; a `check` produces no entry; a failed one is `FAIL <id> — check: …` | evaluated after the entries matched, in file order |
| V7 | **T and G**: T is the `within` of `start`, G the `within` of the `wait-exit` after `signal TERM`; the conformance check (S8) requires them to equal the spec's T and G; a `--limits` file may carry `readyS` and `graceS`, and a scenario whose values differ is refused (exit 4), never re-timed | as stated |
| V8 | **`sleep` has no deadline** and takes no `within`; it counts toward the 600 s test bound | a `within` on `sleep` is a format error |
| V9 | **`split` offsets index the joined bytes** of the whole string expression after substitution and repetition; strictly increasing, 0 < o < length | as stated |
| V10 | **body precedence**: the transcript's body form decides — `body json` compares JSON values, `body <str>` bytes; the conformance check requires `body json` only where a requirement carries F-JSON | no ambiguity left in the runner |
| V11 | **the program's environment** is the runner's own (which the layer sets: D15's base plus the documented per-stack variables), plus `PORT`, plus the scenario's `env` lines; nothing else | a stub that prints its environment shows exactly that |
| V12 | **runner bounds**: undrained stdout and stderr to files capped at 16 MiB each, truncation recorded; a connection's unread buffer above 256 MiB ends the scenario with the actual-only entry `overflow <conn>`; one test ≤ 600 s; a regex capture runs on at most 16 MiB | as stated |

**Files (bench).** `spikes/lang-bench/run-tests.mjs`, `lib/lower-priority.mjs` (verbatim copy of
`.agents/conventions/tools/templates/lower-priority-preload.mjs`, only `MOUNT` changed); `runner/run_samples.py` (entry, CLI,
kind dispatch, summary, exit codes), `runner/rs_lex.py`, `rs_script.py`, `rs_transcript.py` (lexer, `.in` and `.out` parsers
with validation), `rs_match.py` (byte entries with fields, HTTP replies, exact-decimal JSON with `$unordered`, one-to-one
assignment, V6 checks), `rs_proc.py` (build, start with the launch prefix, readiness, `signal TERM`, `wait-exit`, the group or
cgroup kill and leftover check, memory files when given), `rs_net.py` (TCP with buffers and `recv` modes, `chunk`/`pause`,
`rcvbuf`, `quiet`/`closed`, `shutdown`; the HTTP/1.1 client with V3 and V5), `rs_exec.py`, `rs_olympiad.py`, `rs_report.py`;
`pin-runner.mjs`, `lib/runner-pin.mjs` (manifest → `inputs/specs.json` `runner: { version, files, manifestSha256 }`);
`inputs/specs.json` (new); `test/runner-*.test.mjs`, `test/py/test_*.py`, `test/fixtures/runner/**`.

**The CLI.** `python3 -I runner/run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE]
[--launch-prefix JSON] [--cgroup-of TEMPLATE] [--json OUT] [--only ID] [--parse-only] SAMPLES_DIR`. The kind is read from the
`Kind` row of the workspace's `STATEMENT.md` unless `--kind` is given; the build runs first unless `--no-build`; **without
`--limits` the olympiad time limit is the floor (1 s) and every time verdict says `against the floor`**; without `--cgroup-of`
every case says `memory: not certified`; `--launch-prefix` is a JSON argv put before `bash run.sh` with `{case}` replaced;
`--cgroup-of` is a path template with `{case}` from which `memory.peak`, `memory.events` and `cgroup.procs` are read after the
case; `--parse-only` parses and validates every pair and runs nothing (the conformance check's road in). A `cases.txt` beside
the pairs attaches its R-ids to each case. Exit `0` all passed · `1` a case failed · `3` environment · `4` configuration (usage,
a malformed sample, a version or T/G mismatch — file and line) · `5` the runner itself failed.

**Scenarios.**

- *Given* FORMAT's olympiad example and a replay stub, *then* `PASS s03 — 1 entries`; *given* a stub that drops the final line
  feed, *then* `FAIL s03` with the byte of the first difference, exit 1.
- *Given* the daemon toy example and a correct toy, *then* PASS; *given* a toy that replies to a split frame before it is
  complete, or one that stops serving a second client while a slow reader reads nothing, *then* the two failures FORMAT §6.2
  states (`noise …`, `timeout …`) at their entries.
- *Given* the web toy example, *then* PASS; *given* a toy that lets two racing requests on one key make two orders, *then* the
  report of FORMAT §6.3.
- *Given* two JSON integers that differ only beyond float precision, *then* FAIL; *given* `2.0` against `2`, *then* PASS (V2).
- *Given* a server that closes keep-alive after each reply, *then* the second request on the connection is rewritten once and
  passes; closed twice, *then* `eof` (V5).
- *Given* `check distinct r*` over three bound tokens of which two are equal, *then* `FAIL … — check: r0 and r2 are equal` (V6).
- *Given* a stub that ignores SIGTERM, *then* `stop: still running … killed` and nothing of it is left; a stub whose child
  outlives it, *then* `leftover: 1 process`; a stub that never listens, *then* `not-ready`.
- *Given* a stub that writes 300 MiB nobody reads, *then* `overflow c1` and the runner stays bounded (V12).
- *Given* `scenario 1`, a `within` on `sleep`, a name bound twice or a `split` offset past the end, *then* exit 4 with file and
  line, and nothing is started.
- *Given* `--launch-prefix` and `--cgroup-of` pointing at fake files with `oom_kill 1`, *then* the case is `memory limit`
  whatever the exit status (the real scope is S8b's).
- *Given* a C# toy and a TypeScript toy of one fixture task, *then* the same case list for both (T-L21's shape).

**Acceptance (parent S8a).** T-L21's shape for the three kinds against stub programs (stdlib-only imports; identical case lists
for a C# and a TS stub); FORMAT v2's three examples and the three pilot specs' samples (01, 05, 04, once re-conformed — OP-4,
now the parent's text) run through it with the expected verdicts; the pinned version and the manifest hash are in
`inputs/specs.json` before FORMAT v2's gate; the workspace copy equals the pinned tree (S3's check). Plus: module and function
shape measured by an AST test; no `float` reaches a comparison (an AST test); green with `BENCH_REQUIRE_SANDBOX=1`.

**Test plan (red, green, red).** (1) `A_float_json_compare_would_equal_two_distinct_large_integers_and_that_is_the_point` — red
with `json.loads` defaults, green with V2, red with `parse_int=Decimal` removed. (2)
`A_split_offset_counts_the_joined_bytes_not_the_first_string`. (3) `A_program_that_ignores_SIGTERM_is_killed_with_its_group`
— red before the group kill (the stub's marker survives in `/proc/*/environ`), green, red with `killpg` replaced by `kill`.
(4) `An_unread_flood_ends_the_scenario_instead_of_the_runner`. (5) `Every_runner_import_is_standard_library_or_a_sibling`
beside `The_import_scan_still_finds_a_known_stdlib_import` (`sys.stdlib_module_names`). (6) The FORMAT §6 verdicts, each with
its failing twin.

**Docs.** `research/module_tests.md`: the runner's flows (olympiad case, each `recv` mode, `together`, capture and reuse,
`unordered`, checks, stop, leftover, overflow, readiness), each `covered` with its test, and what it does not prove (memory
without a cgroup; behaviour beyond the scenarios run). `research/module_lang_bench.md` (new): CLI, exit codes, result format,
bounds; E3.S10 completes it.

**Brief (Sonnet coder).** Branch `feat/lang-v2-e1-s8a`. Read FORMAT v1 §3–§6 (`git -C
/home/jinx/git/dew_flow_ai_language_comparison show main:todo/tasks/FORMAT.md`) and the delta above; implement both, nothing
more. Lexer first with unit tests from §4.1; parsers return frozen dataclasses and a list of `(file, line, message)` — never
raise for a malformed sample. Matching: a small backtracking matcher for fields (shortest run first); JSON by recursion with
exact decimals; `unordered` by bipartite matching (≤ 64 entries; more is a format error). Processes: `subprocess.Popen(prefix
+ ['bash', 'run.sh'], start_new_session=True, env=…, stdin=PIPE)`; SIGTERM to the pid (D38: `run.sh` execs the program),
SIGKILL to the group at G; the leftover check by `os.killpg(pgid, 0)` or the given `cgroup.procs`, the result saying which.
Networking with `socket` and `selectors`, one reader thread per connection into a bounded buffer, deadlines from
`time.monotonic()`, `together` by a `threading.Barrier`. `.scenario/` fixtures: refuse a pre-existing `.scenario` you did not
create (`os.lstat`, `O_NOFOLLOW`). Report exactly FORMAT §5.5's lines; JSON result `{ format: 1, runner, grammar: 2, kind,
build, cases: [{ id, status, reason, rIds, entries, firstMismatch, lifecycle: { stop, leftover, leftoverEvidence, memory },
wallMs }], python }`. Stubs are toys (FORMAT's toys and a replay stub that serves a pair's expected bytes once it received the
pair's sent bytes) — **never an implementation of a task of the series**. `node spikes/lang-bench/pin-runner.mjs` last.

---

## E1.S8b — the execution layer

**Model:** scenarios + brief Opus · code Sonnet · review Opus. **Not marked** (the ledger is full); strict spec. It runs every
program the series judges — the D40 gate here, E2's validation and calibration, E3's scoring — so a defect in it is a defect of
every verdict, and a change to it re-dates their evidence (parent §6.1).

**Goal.** One Node module, `spikes/lang-bench/lib/exec-layer/`, that starts a program's `run.sh` in a bwrap box with D15's
environment and `de_DE.UTF-8`, inside a systemd user scope that holds **only the program** (`MemoryMax`, `MemorySwapMax=0`),
in a private loopback-only network namespace it shares with the driver (the runner of S8a) while filesystems, PIDs and
cgroups stay separate; enforces T, the step deadlines and G through the runner; and leaves no process, scope or namespace
behind — with one identity (the tree's sha256) that every record names (D36, D43).

**Files (bench).** `spikes/lang-bench/lib/exec-layer/{netns.mjs, box.mjs, scope.mjs, env.mjs, run.mjs, cleanup.mjs,
identity.mjs}`; `spikes/lang-bench/exec-probe.mjs` (the host probe, printed and recorded); `spikes/lang-bench/inputs/specs.json`
(`execLayer: { treeSha256, probedOn, host }`); `spikes/lang-bench/inputs/exec-probe-<date>.json`;
`test/exec-layer-{netns,isolation,scope,env,exec,cleanup}.test.mjs`; `test/fixtures/exec-stubs/{csharp,node,python}/**`.

**The shape** (the order of the launch is the probe's to settle and record, as D36 and D43 were):

- **Namespace holder** — `unshare -rn` started by the layer (owned name), `ip link set lo up` inside, no other interface; it
  lives for one case and is the anchor both sides join.
- **Driver** — the runner of S8a, started in the holder's network namespace (`nsenter --target <holder> --user --net
  --preserve-credentials`), **outside** the program's scope.
- **Program** — `systemd-run --user --scope --unit=<owned name> -p MemoryMax=<bytes> -p MemorySwapMax=0 -- <join the
  holder's network namespace> -- bwrap <v2 box> -- bash run.sh`, handed to the runner as `--launch-prefix`, with
  `--cgroup-of` naming the scope's cgroup directory. The box: v1's binds (`spikes/minigit-bench/lib/bwrap.mjs:10-19`) **without
  `--unshare-net`** (the namespace is the holder's), with `--unshare-pid --die-with-parent --new-session`, the workspace copy
  writable, the toolchains of S5 and the caches of S4 read-only, the operator's home, `/mnt` and `/run` hidden.
- **Environment** — `env.mjs` builds D15's base (`DOTNET_gcServer=0`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`,
  `NODE_OPTIONS` empty, `LANG` and `LC_ALL` = `de_DE.UTF-8`, no invariant switch) plus the documented per-stack variables,
  from one allow-list; `--clearenv` then exactly those keys.
- **Cleanup** — after the case: read `memory.peak`, `memory.events` and `cgroup.procs`, then stop the scope (every descendant
  ends with it), end the holder, confirm none of the three is left; on a timeout the same path, never a bare "stop waiting".
- **Certification** — the layer refuses to start a data run when `memory.peak` is not readable (the named halt
  `memory-not-certifiable`, exit 3); a non-data run proceeds with `memory bound not certified` (D13, D36).

**Scenarios.**

- *Given* a driver and a stub program started by the layer, *then* their `/proc/<pid>/ns/net` are equal and differ from the
  host's; `/proc/<pid>/cgroup` puts the program in its scope and the driver not; `lo` is the only interface and the driver
  reaches the program's port on 127.0.0.1 (T-L33) — the same test body for a C#, a TS and a Python stub, the same facts.
- *Given* a sentinel file only the driver can see, *then* the program cannot read it; *given* a program that forks a child,
  *when* the scope is stopped, *then* both end while the driver survives and collects `memory.peak`, `memory.events`, the exit
  status and the transcript; afterwards no process, scope or namespace of the case exists (T-L34).
- *Given* a child that allocates past `MemoryMax` while its parent stays small, *then* `memory.events` shows it and the case is
  `memory limit`; an OOM-killed child under a parent that exits 0 is `memory limit`, never a pass; a lingering child is
  `leftover` with `cgroup.procs` as the evidence (T-L30).
- *Given* each stack's stub `run.sh` ending with `exec`, *then* the started pid is `dotnet` or `node`, with no bash parent in
  the scope, and receives SIGTERM; without `exec`, *then* the test fails (T-L31, re-run by S3 on the real templates).
- *Given* the layer's box, *then* `LANG` and `LC_ALL` are `de_DE.UTF-8`, `locale -a` lists it, a .NET probe prints `de-DE`, a
  Node probe's locale starts with `de`, and no invariant switch is set (T-L32's scoring-box half; the agent-box half is E3's).
- **Failure cases:** a host where `memory.peak` cannot be read refuses a data run with `memory-not-certifiable`; a host
  without unprivileged user namespaces is exit 3 with the probe's output; a program still running at G is killed with its
  scope and reported `stop: … killed`.

**Acceptance (parent S8b).** T-L33, T-L34, T-L30, T-L31, T-L32 (scoring-box half) green on the series host with
`BENCH_REQUIRE_SANDBOX=1` and nothing skipped; the probes identical for both stacks; the layer's identity in
`inputs/specs.json` beside the runner's; `exec-probe.mjs` output recorded with its date and host; the pre-freeze gate of S2
runs on this layer (D40 not weakened).

**Test plan (red, green, red).** (1) `A_box_with_its_own_network_namespace_would_leave_the_driver_unable_to_connect_and_that_is_the_point`
— red with v1's `isolation()` (`--unshare-net`), green with the shared holder, red again with the flag restored. (2)
`A_scope_around_the_driver_would_charge_its_memory_to_the_program_and_that_is_the_point` — red when the driver is launched
inside the scope (T-L33's cgroup check fails), green, red. (3) `Stopping_the_scope_ends_a_forked_child_and_the_driver_survives`
— red with a kill of the program's pid only (the child's marker survives), green, red. (4)
`The_box_environment_is_the_allow_list_and_nothing_else` beside `The_allow_list_still_carries_LANG` (the scan's companion).

**Docs.** `research/module_lang_bench.md` (the layer, its shape, its probe, its identity, the invalidation rule of parent §6.1);
`research/module_tests.md` (the layer's flows: shared netns, separate cgroups, sentinel, scope kill, culture, refusal).

**Brief (Sonnet coder).** Branch `feat/lang-v2-e1-s8b`. Start with `exec-probe.mjs`: on this host, try the two launch orders —
`systemd-run --scope` outermost with `nsenter` into the holder inside it, and `nsenter` first with `systemd-run --user` inside
the namespace — and record for each whether the scope is created, which pids land in `cgroup.procs`, and whether the driver
reaches the port; keep the order that passes T-L33 and T-L34 and write the other's failure into the probe record. Reuse v1's
bwrap primitives as they are (add a `boxArgv` without `--unshare-net`, do not edit `isolation()`); `runBounded`
(`spikes/minigit-bench/lib/run.mjs:35`) for every child with a ceiling; scope and holder names from the owned helper in tests.
Cleanup is one function called on every path (normal, timeout, exception) and itself never throws; a resource it cannot remove
stays named and is reported. Commands: `node spikes/lang-bench/exec-probe.mjs`; `BENCH_REQUIRE_SANDBOX=1 node
spikes/lang-bench/run-tests.mjs test/exec-layer-*.test.mjs`.

---

## E1.S9 — `FORMAT.md` v2, checked against the runner, gated before any spec gate

**Model:** the document by the team lead (Opus, main session); the grammar extractor and its test by Sonnet; the gate by coai's
document gate plus a consultant; review Opus.

**Goal.** One versioned format that every spec, sample and runner agrees with, gated before the first spec gate, every gap the
pilot specs and consult `3ea9ef36` found closed in a section or a fixed sentence, no plan identifier in a file a run base
carries, and its grammar sections handed to the agent as `runner/GRAMMAR.md`.

**Files.**

| Repository | Path | Change |
|---|---|---|
| task | `todo/tasks/FORMAT.md` | v2 (branch `docs/format-v2`): `Format version: 2`; the delta V1–V12 of E1.S8a; an F-LOAD fixed sentence with slots for a load scenario's size and duration; `{ref}` lists of any length; a table belongs to the R paragraph it follows; the T and G series defaults (10 s, 5 s), the override with a reason and where the runner reads them (V7); F-BYTES-W against F-JSON (V10); a **neutral daemon example** over a framed TCP protocol that names no real product (D39); the reading block removed from the spec obligations (D37); the base-environment sentence following D15; §1.5's credit sentence without `NOTES.md` (OP-7, now the parent's S9 text); sections 7–9 moved out; a changelog entry per gap |
| task | `runner/GRAMMAR.md` | generated: v2's sections 3–5 verbatim under a two-line header naming the format version and FORMAT's sha256 |
| bench | `spikes/lang-bench/inputs/FORMAT-MAINTENANCE.md` | new — v1's §7–§9 for the maintainers, with the plan ids they need |
| bench | `spikes/lang-bench/tools/grammar-extract.mjs`, `test/grammar-extract.test.mjs` | new |
| bench | `spikes/lang-bench/inputs/specs.json`, `inputs/gates/FORMAT-v2.md` | `format: { version: 2, sha256, gate }`, `grammar: { sha256 }`; the gate's verdict, reviewer count, every finding and decision, the consultant's answer |

**Scenarios.** *Given* v2's three examples as files, *then* the runner gives each example's stated verdict against its toy and
each stated failure against its broken toy. *Given* v2, *then* no plan id matches (`E<n>.S<n>`, `D<n>`, `T-L<n>`, `G-<n>`);
FORMAT's own `F-…` ids stay. *Given* the daemon example, *then* no D18 needle hits — **failure case:** a planted product name
fails the check. *Given* a one-line edit to v2's §4, *then* the stored `GRAMMAR.md` no longer matches and S3's check fails.
*Given* a `revise` verdict that changes a grammar row, *then* the runner changes in the same branch before the next round.

**Acceptance (parent S9).** Every gap of the parent's list has a v2 section or fixed sentence, named in the changelog; the three
examples and the three pilot specs' samples run through the runner against stub programs with the expected verdicts; no plan
id anywhere in `FORMAT.md`; the daemon example names no real protocol or product; v2's gate (document gate + consultant)
recorded before the first spec gate; its sha256 replaces v1's in `inputs/specs.json`; specs 01, 05, 04 re-conformed before
their own gates (S2).

**Test plan (red, green, red).** `GRAMMAR_md_is_the_verbatim_sections_of_FORMAT`; `FORMAT_carries_no_plan_identifier` — red on
v1 (its §7), green on v2, red with one id planted.

**Docs.** `FORMAT.md`; the task repository's `todo/README.md`; `research/module_lang_bench.md`.

**Brief (team lead; extractor to Sonnet).** Write v2 from v1, the delta of E1.S8a and the parent's S9 list; keep every v1
sentence no gap touches. Gate: `mcp__coai__open` on the task repository's checkout and branch; `mcp__coai__review_document`
with `documentPath: todo/tasks/FORMAT.md` and a `purposeText` naming who acts on it (the spec author, the suite author, the
runner's maintainers and, through `GRAMMAR.md`, the agents under test in both stacks), what they must be able to do (write and
read a spec and a sample unambiguously; implement the grammar with a standard library alone) and what it does not cover (the
tasks, the tests); `mcp__coai__resolve` every finding; one `mcp__coai__consult` ("is any rule ambiguous for a runner, a spec
author or an agent reading a sample?"). **Extractor:** `grammar-extract.mjs <FORMAT.md> <out>` copies from the `## 3.` heading
to the line before `## 6.`, prefixes `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex>)`, LF line ends;
refuses when a heading is missing.

---

## E1.S8 — FORMAT v1 (built; its gate is v2's) and the conformance check derived from v2

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** FORMAT v1 stays the draft the epoch-3 re-review kept (`d7c73ff`); it has no gate of its own — v2's gate (S9) is its
gate (OP-3, now the parent's S8 text). What remains is **one conformance checker, derived from v2's maintainer list, that reads
a spec and its samples and names every deviation**.

**Files (bench).** `spikes/lang-bench/conformance.mjs`, `lib/conformance.mjs`, `lib/conformance-sentences.mjs` (the fixed
sentences read from FORMAT v2 itself, never retyped), `test/conformance.test.mjs`, `test/fixtures/conformance/**` (synthetic
specs only).

**The checks** (`FORMAT-MAINTENANCE.md`'s list): file name and title; the five header rows and their values; the eight sections
of the kind; R-ids unique, contiguous, ascending before the freeze, F-REQ's `{n}`; every fixed sentence the kind requires,
verbatim after whitespace collapse, slots filled with allowed values; F-INVALID xor F-ERRORS; tables inside their R paragraph;
**T and G stated and equal to every visible scenario's `start … within` and `wait-exit within`** (V7); the samples — exactly
`s01…s<k>`, the olympiad table equal to the files with `⏎` read as LF, each daemon and web pair parsed **through
`run_samples.py --parse-only`, never a second parser**; no needle (`--needles <file>` outside git, plus S1's patterns); `⏎`
only in section 6; no reading block.

**Scenarios.** *Given* a conforming synthetic spec of each kind, *then* exit 0, one `ok` per check. *Given* F-STOP saying 5 s
and a sample waiting `within 4s`, *then* `T/G: sample s02 waits 4 s, the spec says 5 s`. *Given* a table not preceded by an R
paragraph, *then* it is named. *Given* a pair that does not parse, *then* the runner's `(file, line, message)` is quoted.
**Failure case:** a FORMAT file missing a sentence the checker expects makes it refuse (exit 4), never pass every spec.

**Acceptance.** The parent's S8 criteria as they stand after v2; the checker follows a one-word change in a fixture FORMAT; its
output is part of every spec's gate record.

**Test plan (red, green, red).** `A_retyped_sentence_list_would_pass_a_spec_after_FORMAT_changed_and_that_is_the_point` — red
with a literal list, green reading FORMAT, red with the reader stubbed; `The_checker_still_finds_a_known_deviation` beside each
negative test.

**Docs.** `FORMAT-MAINTENANCE.md` points at the checker; `research/module_lang_bench.md`.

**Brief (Sonnet coder).** `node spikes/lang-bench/conformance.mjs --repo <task repo> --task NN-slug --format
<repo>/todo/tasks/FORMAT.md [--needles <file>]`; one line per check, `ok` or `FAIL <check> — <what, where>`; exit 0 / 1 / 4.
Fixed sentences from FORMAT's `text` blocks by id (the table of §2.1 maps id → kinds → section). The runner through
`runBounded`, 60 s.

---

## E1.S5 — toolchains pinned read-only with checksums; the preflight

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** The gate, the references and later every session run on exactly .NET SDK 10.0.403, Node 26.13.0, **TypeScript
6.0.x** (the newest patch of the 6.0 line on npm on the pin day — the line the pinned `typescript-eslint` 8.72.x supports, D6;
7.0.2 is superseded) and the pinned Python, from read-only directories an unattended upgrade cannot move.

**Files.** Bench: `spikes/lang-bench/inputs/stacks-lang.json` (pins with the exact TS patch and the date it was read, checksums,
probe argv, directories), `install-toolchains.mjs`, `lib/toolchain-pins.mjs`, `lib/preflight-lang.mjs`,
`test/{toolchain-pins,preflight-lang}.test.mjs`. Private area: `~/rsd/lang-v2/toolchains/{dotnet-sdk-10.0.403,
node-v26.13.0-linux-x64, typescript-6.0.<p>}/`, `chmod -R a-w` after install.

**Scenarios.** *Given* the official artefacts, *then* the SDK archive's sha512 equals the .NET 10 release metadata's, Node's
tarball equals its `SHASUMS256.txt` line, TypeScript's tarball equals npm's `dist.integrity`, and each directory is read-only
with its tree hash recorded. *Given* a box binding them, *then* `dotnet --list-sdks`, `node --version`, `tsc --version` and the
sha256 of `/usr/bin/python3`'s real file equal the pins. **Failure cases:** a second SDK on the box's `PATH`, another Node
patch, a TypeScript outside 6.0.x, a changed Python binary — each refused naming the pin and the value seen; `locale -a`
without `de_DE.utf8` — refused with the command that generates it.

**Acceptance (parent S5).** The preflight refuses a box whose `dotnet --list-sdks`, `node --version` or Python checksum differs;
the checksums are in `inputs/stacks-lang.json`; plus `tsc --version` is the pinned 6.0 patch and the SDK directory holds only
10.0.403.

**Test plan (red, green, red).** `A_PATH_that_still_finds_the_apt_SDK_is_refused` (red when the probe trusts the first `dotnet`
found, green with the pinned directory alone, red again); `A_TypeScript_outside_the_pinned_line_is_refused`.

**Docs.** `research/module_lang_bench.md` (the toolchain table); the task repository's README names the versions.

**Brief (Sonnet coder).** Downloads with `curl -fsSL` into an owned folder, verify, extract, `chmod -R a-w`; the SDK by
`dotnet-install.sh --version 10.0.403 --install-dir <dir> --no-path` (the script's own sha256 recorded); TypeScript by `npm view
typescript@6.0 version` → the newest patch, then `npm install --prefix <dir> typescript@<that>` with the pinned Node; never apt.
The preflight probes inside a box built by S8b's `box.mjs` once it exists (v1's bwrap primitives until then).

---

## E1.S4 — package sets and the two cache snapshots

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** Each stack's offered set (parent §4.6) is one hashed offline snapshot with a manifest; `PACKAGES.md` is generated from
it; a build in a box with no registry restores exactly that set and fails naming anything else.

**Files (bench).** `spikes/lang-bench/inputs/cache-recipe-lang.json` — C#: a project referencing
`Microsoft.Extensions.Hosting` 10.0.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x, `xunit.v3` and the test SDK
pieces; Node: `fastify@5.12.7`, `@fastify/jwt@10.2.6`, `@fastify/type-provider-typebox@6.3.0`, `@sinclair/typebox@0.34.52`, pino
as Fastify resolves it, `@types/node` 26.x, `typescript@6.0.<p>` (S5's pin), `typescript-eslint@8.72.x` with its type-aware
rules, and the lint packages S7's template needs — each with a `purpose` field; `inputs/cache-snapshots-lang.json`,
`make-snapshots.mjs`, `lib/packages-md.mjs`, `test/{cache-lang,packages-md}.test.mjs`. Private area:
`~/rsd/lang-v2/caches/<stack>-<treeSha256>/`.

**Scenarios.** *Given* the recipe, *when* `makeSnapshot` (`spikes/minigit-bench/lib/cache-snapshot.mjs:162`) runs per stack in a
box through the egress proxy, *then* the tree hash and the package list are registered (`registerSnapshot`, `:233`). *Given* a
box with the registries refused, *then* an offline `npm install fastify@5.12.7` and `dotnet restore` of the pinned project
succeed, and `typescript-eslint` parses a `.ts` file with type information under the pinned TypeScript. **Failure case:** a
package outside the set fails and the message names it (T-L13).

**Acceptance (parent S4).** T-L13; the manifest lists every package and version; a package outside the set fails naming it; both
hashes in `inputs/cache-snapshots-lang.json`; `PACKAGES.md` derived, never typed.

**Test plan (red, green, red).** `A_registry_reachable_from_the_box_would_hide_a_missing_package_and_that_is_the_point`;
`PACKAGES_md_is_derived_from_the_manifest`; `Type_aware_lint_runs_under_the_pinned_TypeScript` (red under a TypeScript outside
the linter's peer range, green under the pin).

**Docs.** `research/module_lang_bench.md` (recipe and snapshot record).

**Brief (Sonnet coder).** Reuse v1's `makeSnapshot`, `seedCaches`, `treeHash`, `packagesOf` as they are; add only the v2 recipe
and the `PACKAGES.md` renderer (package, version, purpose). Snapshots read-only after registration; the toolchains of S5 in
the box.

---

## E1.S3 — the workspaces

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** Per task and stack a workspace an agent can start in: built by `bash build.sh` offline, started by `bash run.sh`
ending with `exec` (D38), carrying the statement, the samples, the read-only `runner/` and the pin files — identical across
stacks but for the stack. Phase A (the build and start contract the D40 gate needs) before any spec freezes; phase B (the
statements, which need a frozen spec) after.

**Files.** Bench: `spikes/lang-bench/templates/{csharp,node}/{olympiad,daemon,web}/**` (one source: `build.sh`, `run.sh`, the pin
files, the `.csproj` skeleton, `nuget.config` with the offline source only, `Directory.Build.props`, `package.json`,
`package-lock.json`, `tsconfig.json` — `strict`, `noEmit`, `erasableSyntaxOnly`, `verbatimModuleSyntax`, `module nodenext` —
`.npmrc` `offline=true`, `eslint.config.mjs`), `build-workspaces.mjs`, `lib/statement.mjs`, `lib/task-repo.mjs` (fetch at a sha
into `~/.cache/lang-v2/<sha>/`, verified against `inputs/hashes-lang.txt`, v1's `fetchInputs` shape), `inputs/hashes-lang.txt`,
`test/{templates,statement,task-repo,workspace-exec}.test.mjs`, `test/fixtures/hello/**`. Task repository: `runner/` (S8a's
pinned tree plus `GRAMMAR.md`), `csharp/<NN-slug>/**`, `node/<NN-slug>/**` (phase B).

**Phase B's statement.** `STATEMENT.md` = the frozen spec's text + the stack sentence (C#: *"Use C# 14 on .NET 10 (SDK 10.0.403
is installed; the project files in this folder are pinned)."*; TypeScript: *"Use TypeScript 6.0 with `strict`, run by Node 26
with native type stripping (no build step), with `tsc --noEmit` as the type check."*, with S5's exact patch written into the
registered sentence) + the framework sentence for a web task, each on its own marked line.

**Scenarios.**

- *Given* each stack's template on S8b's layer with no registry, *then* the hello program builds within 600 s and
  `python3 -I runner/run_samples.py samples/` reports its sample verdicts.
- *Given* the started pid, *then* it is `dotnet` or `node` with no bash parent and receives SIGTERM (T-L31 on the real
  templates); **failure case:** a `run.sh` without `exec` fails.
- *Given* a fetched task repository, *when* one workspace file differs from `hashes-lang.txt`, *then* the fetch is refused naming
  it (T-L1); a bare session's workspace holds exactly the task folder's files (T-L2).
- *Given* task 9, *then* both workspaces hold the same file set as every other olympiad workspace and no parser, adapter or
  grader file (D33).
- *Given* a frozen spec, *then* `STATEMENT.md` equals it but for the marked sentences; a one-word edit in the spec fails the
  test.

**Acceptance (parent S3).** Each empty workspace builds offline and `run.sh` starts a hello program; T-L1, T-L2, T-L31; the runner
runs inside the box; the `runner/` hash equals `inputs/specs.json`; the statements equal the spec plus the sentences; the
task-9 file set; `workspace-diff` clean on ten tasks (phase B).

**Test plan (red, green, red).** `A_run_sh_without_exec_leaves_a_bash_parent_that_eats_SIGTERM_and_that_is_the_point`;
`A_workspace_file_off_the_hash_list_is_refused`; `The_statement_differs_from_the_spec_only_in_the_marked_sentences`.

**Docs.** The task repository's `research/module_tests.md`; bench `research/module_lang_bench.md`.

**Brief (Sonnet coder).** C#: `build.sh` = `dotnet build App.csproj -c Release --nologo` against the offline source of
`nuget.config`; `run.sh` = `#!/usr/bin/env bash`, `set -euo pipefail`, `cd "$(dirname "$0")"`, `exec dotnet
bin/Release/net10.0/App.dll`. Node: `build.sh` = `npm ci --offline` and `npx --offline tsc --noEmit -p tsconfig.json`; `run.sh`
= … `exec node src/main.ts`. Neither script sets D15's variables (the layer does). Generate into a worktree of the task
repository, never into a checkout someone works in.

---

## E1.S6 — the visible samples

**Model:** the samples by the team lead (Opus — samples are test design); the checks are S8's and S8a's; review Opus.

**Goal.** Per task a few small, clean samples in v2's pair format, existing **before** the spec's conformance gate, run against
both draft references on the execution layer before the freeze (D40), and validated again with the held-out cases in E2.

**Files.** Task repository: `tasks/<NN-slug>/samples/s01.in` … `s<k>.out` (01's from its spec table, 04's from its text, 05's
re-conformed); the spec's section 6 with the same bytes. Bench: `spikes/lang-bench/inputs/samples-lang.json` — the files' sha256
and a dated line *"checked against the private test-design notes on <date>"*, with no case text and no reason.

**Scenarios.** *Given* a spec and its `samples/`, *then* the conformance check finds the table or code blocks equal to the files;
one byte changed in a file fails naming the sample. *Given* a scenario pair, *then* `--parse-only` parses it. *Given* both draft
references, *then* both pass the samples in the pre-freeze tool (S2a). **Failure case:** a sample a reference cannot meet within
the spec's T, G or step deadlines is changed before the freeze; after it, a sample change is a spec amendment.

**Acceptance (parent S6, OP-9 as its text).** Table and files hold the same bytes; both draft references pass them on the layer
before the freeze; the same against the final references closes in E2; "no sample reaches a trap" is checked now against the
private notes (`~/rsd/lang-v2/test-design/`, D41) and formally in E2 once the trap table exists.

**Test plan.** The checks are S8's; this story's red is real and recorded: the first conformance run on each pilot spec is red
(01 has no sample files, 05's are v1, 04's exist only in its text), then green.

**Docs.** The spec's section 6; `inputs/samples-lang.json`.

**Brief (team lead).** One sample per kind of behaviour section 1 names, at most five; every deadline stated in the file; T and G
as the spec states them; open the private notes on the host before writing and never copy anything held-out-shaped into
`samples/`.

---

## E1.S2 — the ten task specs, gated and frozen — cornerstone

**Model:** **`model: fable`** — Fable writes this story's per-task spec briefs; the spec text is the team lead's (Opus, main
session, D21); Opus reviews; the gate is coai's document gate plus a consultant plus the bias review. If Fable refuses on a
limit, Opus writes the briefs and the summary says so.

**Goal.** Ten stack-neutral specs that two references written apart implement identically, that name no source, trap or
language, that conform to FORMAT v2, whose riskiest contracts both draft references executed on the execution layer before the
freeze, gated per task and frozen together under one tag — because an ambiguous or biased spec makes every later failure
unadjudicable.

**Files.** Task repository: `todo/tasks/NN-slug.md` × 10 (branch `docs/spec-NN` each; 01, 04, 05 re-conformed to v2 by a fix
forward). Bench: `spikes/lang-bench/inputs/bias-review/NN.md` (verdict, reviewer count, notes, every finding and decision, the
bias answer per stack — no case text), `inputs/specs.json` (per spec `sha256`, `state: draft | gated | frozen`, `gate`, `rIds`,
`T`, `G`), `lib/spec-freeze.mjs` (`specBlobMatches({ repo, tag, path })`, T-L20's guard half) and its test. Private area:
`~/rsd/lang-v2/test-design/spec-briefs/NN.md` (Fable's briefs).

**Per task, in the order of §4.1** (the pilot specs first): Fable's brief → the spec draft → the samples (S6) → conformance
green (S8) → the two draft references (S2a) → **the D40 gate on S8b's layer**: both drafts pass every visible sample and **the
task's riskiest held-out-shaped scenarios listed in `~/rsd/lang-v2/test-design/PLAN-risky-scenarios.md`** with the spec's T, G
and step deadlines in force — a value a reference cannot meet is changed in the spec now → the document gate, the consultant,
the bias question, two readers (an Opus judge and the consultant) with no open question → `Gate verdict` row, `state: gated`,
the sha256 → the PR merged. **Per task the spec is gated; one tag freezes all ten after the tenth** (OP-2, now the parent's
text): one commit on `main` sets every `Freeze tag` row, the annotated tag `spec-frozen-<yyyymmdd>` on it, `state: frozen` and
the frozen hashes, the tag pushed alone. From its gate on, a spec changes only by a dated amendment applied to both stacks;
**a change to the runner or the layer re-runs the D40 gate of every spec gated on the old one** (parent §6.1).

**Scenarios.** *Given* a gated spec edited outside its header rows without an amendment item, *then* the check fails naming it.
*Given* the frozen tag and a run base whose spec blob differs, *then* `specBlobMatches` refuses it. *Given* a known task, *then*
D18's needles find nothing. *Given* `revise`, *then* the accepted findings change the text and conformance runs again before
the next round; *given* `call_human`, *then* the spec stops and the person is asked. *Given* a draft reference that misses a
deadline the spec states, *then* the spec or the reference is fixed before the gate round — never a wider deadline after the
freeze.

**Acceptance (parent S2, per task).** The verdict is `proceed`, `good_enough` or `continue_anyway`; the consultant answered; the
bias question answered per stack with no open finding; no id, title, site or trap in the text; a known task's needles find
nothing; the spec conforms to v2; the floors and rule, T, G and step deadlines, semantic bounds, I/O and start contracts, the
frozen list and "hidden tests exist" present; the D40 gate passed for both drafts, the private aid's scenarios for the task
included (their count is the aid's; the record names only the count); two readers list no open question. **Whole story:** the
ten verdicts and the tag exist before E2.S1 finalises a reference.

**Test plan (red, green, red).** `A_spec_edited_after_its_gate_without_an_amendment_is_refused` (red before the check, green, red
with the header-row exemption widened to the whole file); `A_run_base_spec_blob_off_the_tag_is_refused_before_launch`
(T-L20); per spec the first conformance run recorded red (or why it was green at once), then green.

**Docs.** The specs; the task repository's `todo/README.md`; `inputs/bias-review/NN.md`; `inputs/specs.json`.

**Brief (Fable spec writer).** For each task write `~/rsd/lang-v2/test-design/spec-briefs/NN.md` (outside git) from the parent's
§4.1 row, D3, D4, D13, D14, D18, D25, D33, D35, D39, FORMAT v2, and the private aid —
`~/rsd/lang-v2/test-design/PLAN-trap-table.md` and `PLAN-risky-scenarios.md`, opened on the host. It holds: (a) **the
requirement inventory** — every behaviour a held-out case will assert, as a sentence the spec must contain, so that D25's
"every held-out test checks behaviour the statement requires" holds by construction; (b) **the contract decisions** the spec
must make (protocol matters, semantic bounds, clock semantics, T and G or an override with its reason, error behaviour or
F-INVALID's scope); (c) **the ambiguity list** — every place two careful references could read differently, each with the
sentence that closes it; (d) **the bias check** — per requirement, whether either stack needs more than the required behaviour
to meet it, and the neutral wording, judged against the aid and the declared culture (D35); (e) **the sample plan** for S6;
(f) **the riskiest-contract scenarios** of the aid for the task, as scenario pairs beside the notes, never in git. Known tasks
stay paraphrased with no id, title, site or hint that they are known; no trap and no language in the text. Hand each brief to
the team lead; Opus reviews the spec against the brief before the gate. Nothing of the brief is quoted in a PR, commit or
review body.

---

## E1.S2a — draft references outside git; the pre-freeze tool on the execution layer

**Model:** briefs by the team lead (Opus) per task and stack · code Sonnet, one launch per program, the two stacks' coders never
seeing each other's code · review Opus.

**Goal.** For each task one C# and one TypeScript reference, written apart from the spec draft and samples, living in
`~/rsd/lang-v2/refs/` and never in a workspace, a run base or git, so that the D40 gate can execute them (E2.S1 finalises them);
and one tool that runs the gate on S8b's layer the same way every time and records under which identities it ran.

**Files.** Private area: `~/rsd/lang-v2/refs/<NN-slug>/{csharp,node}/` (each a full workspace: S3's template plus sources).
Bench: `spikes/lang-bench/inputs/references.json` (per task and stack `treeSha256`, `state: draft`, `specSha256`, `writtenOn`,
and per gate run `{ result, on, runnerManifest, execLayer, formatSha256, specSha256 }`), `prefreeze.mjs`, `lib/prefreeze.mjs`,
`lib/gate-staleness.mjs`, `test/{prefreeze,gate-staleness}.test.mjs`.

**The tool.** `node spikes/lang-bench/prefreeze.mjs --task NN-slug --refs ~/rsd/lang-v2/refs --extra <private dir>` copies each
reference to an owned pristine folder, runs the runner over the task's `samples/` and over the private scenario pairs given by
`--extra`, on S8b's layer, and records one line per reference with the case ids that failed (ids only). It refuses `--refs` or
`--extra` inside any git worktree (`refuseInsideWorktree`, `spikes/minigit-bench/lib/git-guard.mjs:28`).
`gate-staleness.mjs` is T-L36's E1 half: a gate record whose `runnerManifest` or `execLayer` differs from the current one is
`stale`, and the freeze refuses while any record of a task is stale (E2.S3 owns the limits half of T-L36).

**Scenarios.** *Given* both drafts, *then* each line says `pass` with the hashes it ran under. *Given* `--refs` inside the bench
checkout, *then* exit 4 and nothing runs. *Given* a draft that fails one private scenario, *then* `fail` with its id only, exit 1.
*Given* the layer's identity changed after a gate, *then* that task's record is `stale` and the freeze is refused until the gate
runs again. **Failure case:** the layer refuses (`memory-not-certifiable` or no user namespace) — exit 3, nothing recorded as
passed.

**Acceptance (parent S2a).** For each task both drafts build and run on the layer through the runner and pass every visible sample
and the task's riskiest scenarios listed in the private aid; the drafts are hashed in `inputs/references.json` as `draft`; no
draft or its hash leaks into the task repository (`leakcheck`; a test asserts no file of either repository has a reference
file's sha256).

**Test plan (red, green, red).** `A_reference_inside_a_worktree_is_refused_before_it_runs`;
`A_gate_record_from_an_older_layer_is_stale_and_blocks_the_freeze` (red without the identity comparison, green, red);
the tool's own tests use toy programs, never a reference.

**Docs.** `research/module_lang_bench.md` (the tool, its record, the private area).

**Brief (team lead → Sonnet coder, per task and stack).** Give the coder the spec draft, the samples, S3's template, `PACKAGES.md`
and the build and run commands, and ask for the spec's requirements implemented completely and idiomatically for its stack —
**not** the private aid, the spec brief, the riskiest scenarios or the other stack's reference. The coder runs `python3 -I
runner/run_samples.py samples/` in its workspace until it passes and reports which requirements it found unclear; each goes back
to the spec before the gate.

---

## E1.S7 — process-arm templates, CI, rulesets, deploy key, PAT; run bases shaped for the run-bound broker

**Model:** scenarios + brief Opus · code Sonnet · review Opus · operator (the PAT, the deploy key, the go for each GitHub
setting). Built to v1's design ([PLAN_minigit_stack_bench_E2.md](PLAN_minigit_stack_bench_E2.md) E2.S4,
`PLAN_minigit_stack_bench.md` §6) for one repository with two stack folders (D32), **and matched to D42**: the process agent has
no direct `github.com` egress and works in an isolated bare repository per run that the broker (E3.S7a) serves and mirrors.

**Goal.** What a process session is given — conventions, instructions, CI, the stack folder — is one source, and each run base
is an orphan commit of the frozen tag's generated tree (D39), holding no credit, link or attribution history, cut only by the
deploy key, protected by rulesets — exported as exactly the one commit the broker's per-run bare repository may hold.

**Files.** Task repository: `.github/workflows/{ci.yml,family-checks.yml,pr-title.yml}` (one job per stack, path filters covering
each stack folder and every shared path it depends on — `runner/`, `todo/tasks/` — or unfiltered; the required `build · test`
job per stack; the visible-sample job not required; `branches: [main, 'run/**']`); settings and rulesets `run-main`,
`template-frozen`, the tag ruleset; the deploy key as the only bypass actor. Bench: `spikes/lang-bench/lib/run-base.mjs`
(`cutRunBase({ rid, tag })`: S1's `generateRunBaseTree`, the needle scan, `git commit-tree` with no parent, pushed to
`run/<rid>/main` with the deploy key; `exportRunBase({ rid, out })`: a bundle holding exactly that commit and its tree — the
seed E3.S7a's per-run bare repository is made from), `lib/github-lang.mjs` (settings and rulesets as data, applied with
`gh api` and read back), `p0-lang.mjs`, `test/{run-base,github-lang,templates-d42}.test.mjs`.

**What the templates must not assume (D42).** No file of the generated tree tells the agent to reach `github.com` directly, names
another repository or another run, or carries a remote URL; CI runs on the branch the broker mirrors, so a workflow needs no
step the box would have to reach GitHub for; the PAT is host-side only (the broker's, then the harness's), never in a box
environment, argv or file the box can read. How a process box gets `.agents/conventions` without reaching GitHub is open
(§9 OP-A); this story builds nothing that decides it.

**Scenarios.** *Given* the frozen tag, *when* a run base is cut, *then* `git rev-list run/<rid>/main` has exactly one commit, its
tree equals the generated tree, and the needle scan over the tree and that one commit finds nothing; **failure case:** a planted
credit refuses the cut. *Given* the export, *then* the bundle lists one commit and no other object reachable from the task
repository's `main`. *Given* the PAT, *when* it pushes directly to `run/<rid>/main`, *then* the ruleset refuses (read back).
*Given* a PR into a run base, *then* CI runs on the non-default base with the stack's job required; a planted lint or analyzer
violation is red per stack; merge is blocked until threads resolve. *Given* the template tree, *then* no `github.com` URL, other
repository name or remote appears in it.

**Acceptance (parent S7, OP-8 as its text).** v1's P0 checks pass here — a run base cut by the deploy key, a direct push with the
PAT refused, CI green on a non-default base, a planted violation red per stack, merge blocked until threads resolve — run from
the harness side (the PAT never in a box); S1's run-base check completed with `git rev-list` of the base (one commit, D39); the
export holds one commit only; the template tree carries no direct-GitHub assumption.

**Test plan (red, green, red).** `A_run_base_with_history_would_carry_the_attribution_commit_and_that_is_the_point` (red with a
normal branch from the tag, green with the orphan commit, red with `commit-tree -p` restored);
`The_export_holds_exactly_the_run_base_commit`; `The_template_tree_names_no_github_host_or_other_repository` beside its
companion that still finds a planted URL. The GitHub checks are live, recorded as dated P0 lines.

**Docs.** The task repository's `README.md` (CI, the run-base namespace); bench `research/module_lang_bench.md`.

**Brief (Sonnet coder).** Follow v1 E2.S4's steps 2–5 with these changes: one repository, two stack folders; path filters cover
the shared paths or the job runs unfiltered (`common.platform-limits` §2); the deploy key's private half mode 600 under
`~/.config/lang-v2/`; every GitHub write listed for the operator's go before it is made; the PAT read from a mode-600 file, never
an argv or a long-lived environment variable; nothing of the broker is built here.

---

## 9. Open points

Classified under `common.plan-change-control`. **Epoch 4 has 0 changes; one recorded change is allowed, a second stops the
plan.** Nothing below is decided here. Line numbers are of the parent at `0ea0e25`.

| # | Where | The point | Class |
|---|---|---|---|
| OP-A | `:240` (D42: the per-run bare repository holds "only the orphan run-base commit and the run's own refs"), `:225` (D11: the process clone "carries the conventions"), `:542` (§5: `.agents/conventions` pinned as a submodule on a snapshot branch) | **The conventions submodule under D42.** The run base carries a gitlink, not files; with no direct GitHub egress and nothing but the run base in the bare repository, the process agent cannot fetch `.agents/conventions` — and without it the rules, hooks and `pin-check` the process arm measures are absent. Options: (a) the per-run bare repository also carries the pinned conventions commit, the session's gitconfig redirecting the submodule URL to the broker — D42's allowed contents widened; (b) the generated run-base tree vendors the conventions files in place of the gitlink — D39's tree and the pin check change; (c) the harness checks the submodule out into the clone from a host-side read-only snapshot before the session starts, so the agent never fetches it — D42 and D11 as written, one more harness step in E3.S7. | **change** for (a) or (b); (c) reads as not a change (a step that satisfies D11 and D42 as written) — the team lead decides, in doubt a change |
| OP-B | `:942` (T-L32: "in the agent box and the scoring box"), `:1200` (S8b accepted when T-L32 green) | S8b proves T-L32 for the box it launches (the scoring side); the agent box is v1's sandbox, which gets D15's environment in E3.S5 — so T-L32's agent-box half closes in E3, as E3's done-when already lists T-L32. | not a change (reading) |
| OP-C | `:941` (T-L36), §6.1 (`:610`) | T-L36 is assigned to no story. Read as: the D40-gate half is E1.S2a's (`gate-staleness.mjs`), the registered-limit half E2.S3's. | not a change (a test placed inside the stories) |
| OP-D | `:1161-1165` (E1.S7: "the PAT"; "a direct push with the PAT refused") against `:240` (D42: the PAT is used by the broker host-side) | Under D42 the PAT never enters a box; S7's P0 checks run from the harness side, which is what they test. | not a change (reading) |
| OP-E | `:1091` (E1 scope lists D2 … D31 only) | E1 also builds D41–D43's parts (the layer, the template alignment, the D41 rule); the scope line was not widened when S8b was added. | not a change (wording) |
| OP-F | §4 here; the remote `main` = `ed68ce1` against the local `main` = `28bf777` | The two histories are unrelated; the first push needs `git merge --allow-unrelated-histories` on a branch, never a force push. | not a change (procedure) |
| OP-G | `:29-34` (status: the credit terms of the known tasks' sources still open) | A known-task spec cannot be pushed until its credit terms are recorded (§4 step 3); the work on it proceeds locally. | not a change (operator action) |

**Read together:** only OP-A can be a change. If it is resolved as (a) or (b), it is epoch 4's first change and is recorded in
the parent's ledger in the commit that accepts it.

## 10. Risk items (for the gate's `riskItems`)

1. **E1.S8a + E1.S8b — the runner and the execution layer.** Together they are the public contract and the environment every
   verdict of the series comes from — the agent (through `GRAMMAR.md`), CI, the D40 gate, E2's calibration and E3's scoring;
   a comparison, lifecycle, isolation or memory defect misjudges sessions that cost money, possibly in one stack only. Unmarked
   only because the cornerstone ledger is full.
2. **E1.S2 — the specs and their freeze.** Irreversible in the series' terms: after the tag a spec changes only by an amendment
   that voids the task's limits (G-17), and after the pre-registration never; an ambiguous or biased requirement makes every
   later failure unadjudicable. The cornerstone.
3. **E1.S1 / E1.S7 — the public pushes and the run bases.** Publication cannot be taken back (D2, D31, D41): a credit, an aid
   fragment, a reference or a secret in any pushed commit is public at once; the PAT and the deploy key carry write access.

## 11. Definition of Done (E1)

- [ ] The parent's E1 done-when holds (§1), with `BENCH_REQUIRE_SANDBOX=1` and nothing skipped.
- [ ] Every story's red, green and second red are in its summary with the messages seen.
- [ ] The runner is pinned and the execution layer probed and identified in `inputs/specs.json` before FORMAT v2's gate and the
      first D40 gate; FORMAT v2 gated before the first spec gate; every spec's samples existed before its conformance gate;
      every spec passed the D40 gate on the current runner and layer before its document gate (no record `stale`).
- [ ] The ten specs gated, frozen under one tag, their hashes, R-ids, T and G in `inputs/specs.json`.
- [ ] No trap content, held-out case, reference, mutant or riskiest scenario in either repository or in any PR, commit or review
      body (D41; `leakcheck` with the aid's shingles; the hash test).
- [ ] Every push went through `push-check`, one branch per push; tags alone.
- [x] OP-A decided by the team lead, 2026-10-31: option (c) — the harness checks the conventions submodule out into the run's clone from a local snapshot of the pinned sha before the session starts; it fits D11 (the clone carries the conventions) and D42 (the bare repository holds only the run base and the run's refs) as written, so it is NOT a change and no ledger entry is written; E1.S7 (run bases) and E3.S7a (the broker) build it.
- [ ] `research/module_tests.md`, `research/module_lang_bench.md` and `research/architecture.md` describe what E1 built; the task
      repository's README, `todo/README.md` and research stubs match its tree.
- [ ] `node .agents/conventions/tools/plan-lifecycle.mjs .` clean.
