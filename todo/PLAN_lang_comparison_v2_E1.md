# PLAN — language comparison v2, E1 stories: the task repository — the runner, FORMAT v2, samples, specs gated and frozen after a pre-freeze execution gate, workspaces, toolchains, caches, process templates

> Status: **plan only, nothing implemented yet, 2026-10-31.** Epic E1 of [PLAN_lang_comparison_v2.md](PLAN_lang_comparison_v2.md)
> (§14 step 1), written when the epic started — a stories document is not a plan change (`common.plan-change-control`), and it
> keeps no ledger: its changes are entries in the parent. Eleven stories (E1.S1 … E1.S9, with E1.S2a and E1.S8a); cornerstone
> **E1.S2** (`model: fable`), the one mark the parent's ledger already gives this epic (`Cornerstones: 3 of 3 — E1.S2, E2.S2,
> E3.S2`); no further mark is possible. Scope: the new task repository `dew_flow_ai_language_comparison` (local only until the
> operator creates it on GitHub, E1.S1), a new spike `spikes/lang-bench/` in this repository (the runner, the checks, the
> pins), and the private area `~/rsd/lang-v2/` outside every git tree. **One open point blocks a step**: the pre-freeze
> execution gate needs a scoring launch layer that no E1 story builds (§9, OP-1, classified a change — not decided here).

## 1. The epic, restated (parent § *Epics*, E1)

**Goal.** Ten specs written by the team lead, gated and frozen; ten workspaces per stack an agent can start in, identical
across stacks but for the stack, on pinned toolchains and offline caches; the process arm's run-base machinery; in a public
repository that leaks nothing (parent D2, D5, D6, D11, D12, D19, D21, D25, D31, D35–D40; §4.1, §4.5, §4.6, §5).

**Done when (parent).** T-L1, T-L2, T-L13, T-L20's guard half green; `workspace-diff` clean on ten tasks; both snapshots
hashed; the preflight passes inside a box on SDK 10.0.403, Node 26.13.0, TS 7.0.2 and the pinned Python; `FORMAT.md` (v2)
gated and its three examples run; the ten specs conform to it, carry a gate verdict and the freeze tag; the README carries the
credits and the Node stack table; `leakcheck` clean.

**Not in this epic** (do not build here): the held-out suites, mutants, trap coverage table and limit calibration (E2); the
drivers' Node wrappers, the judge, the scoring pipeline per kind, D15's environment in the agent and scoring boxes, the
arm/task/round runner, the coai box and bridge, the process runner, the compliance checker (E3); the pre-registration (E4).
**Nothing in this epic launches `claude` for a data or pilot session**; the coai document gate and consultant calls of S2 and
S9 are review calls, not sessions under test.

**The held-out boundary.** The bench and the task repository are public. Nothing in this document, in a commit, a test
fixture or a code comment names a held-out case, a mutant, a trap-catching scenario or a reference solution in clear. The
test-design notes, the riskiest-contract scenarios of D40 and the draft references live only under `~/rsd/lang-v2/`
(`test-design/`, `refs/`), outside git; this document names their place and shape, never their content. The author of this
document did not read them.

## 2. What exists — verified 2026-10-31 at `53f248e` (bench) and in the local task repository

| Fact | Where |
|---|---|
| The task repository, local, **no remote**: `main` = `28bf777` (skeleton `b5e9598`, pilot spec 01 `60e468a`, FORMAT v1 `d7c73ff`, 01 conformed `28bf777`); branch `docs/spec-05` = `30af434` (spec 05 + four scenario samples under `tasks/05-key-value-server/samples/`); branch `docs/spec-04` = `0b69a50` (spec 04, its four samples only inside the spec text, no files) | `/home/jinx/git/dew_flow_ai_language_comparison`, `git log --all` |
| `main`'s tree: `README.md` (4 lines, "local only"), `csharp/.gitkeep`, `node/.gitkeep`, `tasks/.gitkeep`, `todo/tasks/.gitkeep`, `todo/tasks/01-descent-to-one.md`, `todo/tasks/FORMAT.md` (884 lines). No `AGENTS.md`, no conventions pin, no LICENSE, no `runner/`, no sample files for 01 | `git ls-tree -r main` |
| FORMAT v1: the spec file and header (§1), fixed sentences (§2), sample pairs (§3), the scenario grammar `scenario 1` (§4: head, steps, `together`, `repeat`, keywords), the transcript `transcript 1` (§5: entries, comparison, normalised fields, `unordered`, report, the reading block 5.6), three examples (§6: x2 names RESP, x3 the toy idempotency race), maintainer sections 7–9 naming plan ids (`E1.S6`, `E1.S8`, `E1.S2`, `G-17`) and `tasks/<NN-slug>/NOTES.md` (§1.5) | `git show main:todo/tasks/FORMAT.md` |
| Spec 05 hard-codes T = 10 s (R4) and G = 5 s (R6); its samples are `scenario 1` / `transcript 1`; spec 04 uses `capture … as token`, `${r}` reuse, a `together` race and `unordered` with `reply *` | `docs/spec-05:todo/tasks/05-key-value-server.md`, `docs/spec-04:todo/tasks/04-rule-api.md` |
| v1's held-out runner protocol: `/usr/bin/python3 -I runner.py --phase --binary --work --box <json argv> --timeout --out`, result `format: 1` with `cases[{id, group, status: pass\|fail\|blocked, spec, reason}]`; the runner itself lives outside git | `spikes/minigit-bench/lib/heldout-run.mjs:19` (`PYTHON`), `:116` (`runSuite`), `:172` (`heldoutScore`) |
| A synthetic Python suite written from JS strings into a test's own folder — the shape for testing a runner without a real suite | `spikes/minigit-bench/test/toy-suite.mjs:1-60` |
| The spike test runner shape: lowered, `max(1, min(8, threads − 1))` workers, exit code or 128 + signal | `spikes/minigit-bench/run-tests.mjs:1-41` |
| bwrap primitives and the scoring box: `isolation()` = `--unshare-net --unshare-pid --die-with-parent --new-session`; `scoreEnv` sets `LANG: 'C.UTF-8'`; `scoreBoxArgv` | `spikes/minigit-bench/lib/bwrap.mjs:15`, `:32`, `:37`, `:58` |
| The agent box environment: `agentEnv` with `LANG: 'C.UTF-8'` | `spikes/minigit-bench/lib/sandbox.mjs:93`, `:97` |
| Reusable: `fetchInputs` (clone at a sha into a cache, verify hashes), `parseHashes`, `verifyFiles`, `treeHash`, `makeSnapshot`, `registerSnapshot`, `packagesOf`, `leakcheck`, `runBounded`, `ok`/`fail`, `refuseInsideWorktree`, `preflight` | `lib/fetch-inputs.mjs:57`; `lib/inputs.mjs:11`, `:49`; `lib/cache-snapshot.mjs:36`, `:162`, `:233`; `lib/packages.mjs:54`; `lib/heldout-leak.mjs:111`; `lib/run.mjs:35`; `lib/result.mjs:2-3`; `lib/git-guard.mjs:28`; `lib/preflight.mjs:56` (all under `spikes/minigit-bench/`) |
| The owned-name helper of `common.test-leftovers`: `ownedDir`, `ownedFor({ token })`; the barrier scan | `.agents/conventions/tools/lib/owned.mjs:612`, `:725`; `.agents/conventions/tools/sweeper-scan.mjs` |
| `spikes/lang-bench/` does not exist | `ls spikes/` |
| `~/rsd/lang-v2/` holds `test-design/` only (not read); no `refs/`, no `toolchains/` | `ls ~/rsd/lang-v2/`, 2026-10-31 |
| Host: Python 3.14.4 at `/usr/bin/python3`; bubblewrap 0.11.1; `systemd-run` present; cgroup2 controllers include `memory`; **`locale -a` lists `C`, `C.utf8`, `POSIX` only — `de_DE.UTF-8` is not generated** (the locale sources are present under `/usr/share/i18n/locales`) | `python3 --version`, `bwrap --version`, `cat /sys/fs/cgroup/cgroup.controllers`, `locale -a`, 2026-10-31 |
| The bench repository is **public** (`gh repo view`: `PUBLIC`); its plan holds the §4.3 trap table | `gh repo view --json visibility`, 2026-10-31; parent §4.3 |

## 3. Conventions for every story

- **Where things live.** Bench side: `spikes/lang-bench/` — plain ESM `.mjs` on Node 26 with no dependency (v1's spike
  conventions, `PLAN_minigit_stack_bench_E1.md` §3), plus the runner in Python 3 standard library only. Task-repository side:
  only what §5 of the parent lists (no tool, no plan file — a `todo/PLAN_*` there would enter every run base, D39). The private
  area `~/rsd/lang-v2/` holds what must never be in git.
- **Functions < 50 lines, cyclomatic complexity ≤ 4, files 200–400 typical and 800 max** (PROJECT §6, `common.coding-style`),
  in Node and in Python alike; expected failures as values (`{ ok, value } | { ok: false, reason }`; in Python a small
  `Fail` value, never an exception for control flow); absent is not zero (`{ known: false, why }` renders `unknown`).
- **Tests.** Every test runs through `node spikes/lang-bench/run-tests.mjs [glob…]` (E1.S8a creates it, v1's shape). A test that
  needs `bwrap`, `systemd-run`, a toolchain or the task repository checkout and cannot find it SKIPS WITH ITS REASON;
  `BENCH_REQUIRE_SANDBOX=1` (the epic's completion run) turns every such skip into a failure. Python units are tested by
  `python3 -I -m unittest` started from one Node test file, so one command runs everything.
- **Leftovers (`common.test-leftovers`).** Every folder a test makes comes from `ownedFor({ token: 'bench' })`
  (`.agents/conventions/tools/lib/owned.mjs:725`); a Python child gets an owned folder through `TMPDIR`, never `mkdtemp` of its
  own. Before the first owner-form producer lands, `node .agents/conventions/tools/sweeper-scan.mjs` must list no owner-blind
  sweeper on this machine; if it lists one, the coder stops and reports (the barrier of §*The barrier*).
- **Processes (`common.security`, `common.reliability`).** exe + argv, never a shell string; every wait has a ceiling; a
  timeout kills the whole process group; the runner's own buffers and files are bounded.
- **Red, green, red.** Every behaviour a story adds gets a test written first and watched red **for the real symptom**, then
  green, then the production line the behaviour depends on is removed and the test watched red again before it is restored
  (`common.testing` § *Every bug fix starts with a RED test*). Both observations go into the story's summary. Where a guard
  exists because a plausible approach is wrong, the test is named for that approach (`…_would_…_and_that_is_the_point`).
- **Roles (`common.subagent-models`).** Scenarios and briefs here are Opus's (this document); code and tests by a Sonnet
  coder; review by Opus; fixes by Sonnet. The task specs, the samples and FORMAT v2 are the team lead's (Opus, the main
  session — D21); E1.S2's story brief is Fable's (the cornerstone). Every launch names its model.
- **Before the push** (E1.S1) work in the task repository lands on its local `main` by `git merge --no-ff` from a story branch
  after the story's checks and gate, with the merge message saying what was checked; **after the push** it lands through pull
  requests with CI (`common.task-lifecycle`). The bench side lands through pull requests as always.

## 4. The operator dependency and the push

E1.S1's first step is the operator's (parent §14 step 1): **create the public GitHub repository
`oleksandrdubyna88/dew_flow_ai_language_comparison`**, empty (no README, licence or `.gitignore` from GitHub), default branch
`main`. Everything else in E1 except E1.S7 proceeds in the local repository until then:

| Proceeds locally, no remote needed | Waits for the push |
|---|---|
| S8a runner, S9 FORMAT v2 (the coai document gate opens on a local checkout and branch), S8 conformance, S5 toolchains, S4 caches, S3 workspaces, S6 samples, S2 specs and their gates, S2a draft references, the D40 gate (once OP-1 is decided), the freeze commit and its tag (local) | S7 entirely (rulesets, the deploy key, the PAT, CI on GitHub, the run-base cut by the deploy key, P0's GitHub checks); the freeze tag's publication; the bench's `TASK_REPO_URL` switch from the local path to the GitHub URL |

**Other operator actions E1 needs, each named where it is used:** the licence answer Q1 (before the push of any known-task
text, below); `sudo locale-gen de_DE.UTF-8` (after enabling the line in `/etc/locale.gen`) so that `locale -a` lists it — the
boxes bind the host root read-only, so the host's locale archive is the box image's (D35; E1.S5); the fine-grained PAT, which
GitHub lets only its owner create (E1.S7); E0's parity release for the final conventions pin (E0.S3; E1.S1 pins the current
`release` provisionally).

**Exactly what happens at the push** (E1.S1 step 5, after the operator's "created"):

1. **Preconditions, all checked by `node spikes/lang-bench/push-check.mjs --repo <task repo>` before anything leaves the
   machine:** `leakcheck` clean over every commit of every branch to be pushed (v1's `leakcheck`, `heldout-leak.mjs:111`, with
   the v2 leak tokens read from a file outside git); the attribution section present in `README.md` and nowhere else; no plan
   story, epic, decision or test id in any agent-visible path (`csharp/`, `node/`, `tasks/`, `runner/`, `todo/tasks/` except
   FORMAT's changelog, which S9 empties of ids); **Q1 answered** if any known-task text (specs 01, 02, 05, 06, 09) is in the
   pushed range — otherwise those branches stay local and only the rest is pushed; no secret (the family's token scan).
2. `git -C <task repo> remote add origin https://github.com/oleksandrdubyna88/dew_flow_ai_language_comparison.git` (HTTPS:
   SSH to GitHub times out from this WSL host; force-push is not used, ever).
3. `git push -u origin main`, then each open story branch (`docs/spec-NN`, `docs/format-v2`, …) as its own push; each open
   branch becomes a pull request against `main`, so work that was merged locally stays merged and work in flight is reviewed
   on GitHub. **No tag is pushed in the same push as anything else**; a tag that exists then (`spec-frozen-<yyyymmdd>`) is
   pushed alone and checked to have started no unexpected workflow (`common.task-lifecycle` §3).
4. The bench's `spikes/lang-bench/lib/task-repo.mjs` `TASK_REPO_URL` becomes the GitHub URL in a bench PR; the local-path
   fetch stays allowed for tests only (a data run refuses it, E3).
5. E1.S7 starts: settings, rulesets, the deploy key, the PAT (operator), CI.

## 5. Story map

| Id | Title | Size | Depends on | Model |
|---|---|---|---|---|
| E1.S1 | Repository skeleton, the attribution cut, the provisional conventions pin, the push (the operator creates the repository) | S (~300 lines) | — (operator for the push) | operator; coder sonnet |
| E1.S8a | The baseline visible-sample runner `runner/run_samples.py`, pinned | L (~2,000 Python + ~900 tests) | — | coder sonnet · brief opus |
| E1.S9 | `FORMAT.md` v2, checked against the runner, gated before any spec gate | M (the document; ~150 lines of tools) | S8a | writer: team lead (opus) · extractor: sonnet |
| E1.S8 | FORMAT v1 (built, closes with S9's gate) and the conformance check derived from v2 | M (~550) | S9 draft, S8a | coder sonnet |
| E1.S5 | Toolchains pinned read-only with checksums; the preflight; the declared locale | S (~300) | operator (locale) | coder sonnet |
| E1.S4 | Package sets and the two cache snapshots; `PACKAGES.md` from the manifest | M (~350) | S5 | coder sonnet |
| E1.S3 | The workspaces: templates first (phase A), the ten task pairs after the freeze (phase B) | M (~600) | S4, S5, S8a; phase B: S2 frozen, S6 | coder sonnet |
| E1.S6 | The visible samples, per task, before the spec's conformance gate | M (10 tasks) | S9, S8 | author: team lead (opus) |
| E1.S2 | **The ten task specs, gated and frozen** | L (10 specs) | S9 gated, S8, S6 per task, S2a and the D40 gate per task | **`model: fable`** (story brief) · spec text: team lead (opus) · review opus |
| E1.S2a | Draft references, one per stack per task, outside git; the pre-freeze execution tool | L (20 programs outside git; ~300 lines of tool) | S3 phase A, S4, S5, S6 and the spec draft per task; **OP-1** for the gate run | coders sonnet (one launch per program) · briefs: team lead (opus) |
| E1.S7 | Process-arm templates, CI, rulesets, deploy key, PAT, run bases as orphan generated trees | M (~550) | the push, S1, S3 | coder sonnet; operator (PAT) |

**Waves (§14 step 1's order).** W0 (operator, any time): create the repository, Q1, the locale. W1: **S8a** ∥ S1 (local) ∥ S5.
W2: **S9** drafted against S8a, then gated ∥ S4 ∥ S8. W3: S3 phase A. W4, per task in the order of §4.1 (the three pilot
specs first, re-conformed to v2): S6 → S2 draft and conformance → S2a → **the D40 pre-freeze gate** (blocked on OP-1) → S2's
document gate, consultant and bias review → gated. W5: the freeze commit and tag after the tenth task; S3 phase B. W6: the
push (if not earlier) → S7.

---

## E1.S1 — repository skeleton, the attribution cut, the provisional conventions pin, the push

**Model:** operator (the repository, the push's go) · scenarios + brief Opus (this text) · code Sonnet · review Opus.

**Goal.** The task repository has the layout of parent §5, a README whose credits and links live in one attribution section
that a generated run-base tree never carries, the family's conventions pinned, and a push procedure that cannot publish a
leak — so that every later story fills a skeleton rather than inventing one.

**Files.**

| Repository | Path | Change |
|---|---|---|
| task | `README.md` | rewritten: what this is; the task list (titles of our own wording, no ids); the §4.6 Node stack table and the Fastify reasoning; the closing `## Attribution` section holding every credit, every link (CSES, IOI 2018, RealWorld, the RESP specification) **and the pointer to the bench plan** — the only place any of them appears |
| task | `LICENSE` | the licence Q1 names; **absent until Q1 is answered** (the push of known-task text waits for it too, §4) |
| task | `AGENTS.md`, `CLAUDE.md`, `.agents/PROJECT.md`, `.gitmodules`, `.agents/conventions` | the family's adapters; the conventions submodule at the current `release` sha (provisional; E0.S3 moves it to the parity release on a snapshot branch) |
| task | `todo/README.md`, `research/architecture.md`, `research/module_tests.md` | the index (FORMAT and the specs, no plan file); stubs as v1's templates; `module_tests.md` names CI's visible-sample job as the repository's scenario harness |
| task | `runner/.gitkeep`, `tasks/.gitkeep`, `csharp/.gitkeep`, `node/.gitkeep` | the folders of §5 |
| bench | `spikes/lang-bench/lib/run-base-tree.mjs` | new — `generateRunBaseTree({ repo, ref, out })` (the tree at `ref` minus the README's `## Attribution` section, minus any credit file), `needleScan({ root, needles })`; E1.S7 commits this tree as the orphan run base |
| bench | `spikes/lang-bench/lib/workspace-diff.mjs`, `workspace-diff.mjs` | new — per task, `csharp/<t>/` and `node/<t>/` differ only in the stack sentence, the framework sentence (web) and the stack's pin files; everything else byte-equal |
| bench | `spikes/lang-bench/push-check.mjs`, `lib/push-check.mjs` | new — §4's preconditions, each a named line, exit 0 / 1 / 3 / 4 |
| bench | `spikes/lang-bench/test/{run-base-tree,workspace-diff,push-check}.test.mjs` | new |

**Scenarios.**

- *Given* a README with an `## Attribution` section holding `CSES` and a link, *when* `generateRunBaseTree` runs, *then* the
  generated `README.md` holds every other section byte for byte and no needle of D18 or D39 is found anywhere in the tree.
- *Given* a credit or link planted outside the section (in `tasks/01-*/samples/`, in `runner/`, in a spec), *when* the needle
  scan runs, *then* it fails naming file and line. **Failure case:** a README with no `## Attribution` section is refused, not
  passed through whole.
- *Given* an empty task pair, *when* `workspace-diff` runs, *then* it passes; *given* one extra file in `node/<t>/` or one byte
  changed outside the marked stack sentence, *then* it fails naming the path.
- *Given* a pushed range holding a known-task spec and no `LICENSE`, *when* `push-check` runs, *then* it exits 1 with the line
  `known-task text without the licence answer (Q1)`; *given* a leak token in an old commit of a branch, *then* it fails naming
  the commit.
- *Given* the operator has not created the repository, *when* the push step is started, *then* nothing is pushed and the
  check says `no remote yet` (exit 3, resumable).

**Acceptance.** The plan's S1 criteria: `workspace-diff` passes on an empty task pair and fails on a planted difference; the
README's links and attribution appear nowhere under `csharp/`, `node/`, `tasks/`, `runner/` or `todo/tasks/`, and the
generated run-base tree holds none of them (a test greps the tree; S7's test adds `git rev-list` of the base — one commit,
D39). Plus: `push-check` exits 0 on the real repository before the push; the conventions pin is the current `release` sha and
`pin-check` there is green; the push happened as §4 says, recorded with the shas pushed.

**Test plan (red, green, red).** `A_README_copied_whole_would_carry_the_credits_into_every_run_base_and_that_is_the_point`:
written first against a `generateRunBaseTree` that copies the tree (red: the needle scan finds `CSES`); green with the section
cut; red again with the cut line removed. `Workspace_diff_fails_on_one_byte_outside_the_stack_sentence` (red before the
marker logic, green, red with the comparison loosened to file names). `Push_check_refuses_known_task_text_without_the_licence`.
The positive companion of every scan: `The_needle_scan_still_finds_a_planted_needle` (`common.testing` § *A structural test
that matches nothing passes forever*).

**Docs.** Task repository: `README.md`, `todo/README.md`, the research stubs. Bench: `research/architecture.md` module map
names `spikes/lang-bench/` (one row, "the v2 task repository's checks and runner; see E1"); `research/module_tests.md` adds
the lang-bench test command.

**Brief (Sonnet coder).** Work in a worktree of the bench (`feat/lang-v2-e1-s1`) and a worktree of the task repository
(`chore/skeleton`). (1) Write `generateRunBaseTree` with `git archive <ref> | tar -x` into an owned folder (never `git
checkout` in a working tree), then rewrite `README.md` by cutting from the line `## Attribution` to the next `## ` heading or
the end; refuse when the heading is absent or appears twice. (2) `needleScan` reads every regular file, case-insensitive
literal needles from a file given by argument (the list lives outside git, FORMAT §7 item 7), plus the fixed patterns
`https?://`, the plan id patterns `\bE[0-9]\.S[0-9]+a?\b`, `\bD[0-9]{1,2}\b` only in agent-visible paths, `\bT-L[0-9]+\b`,
`\bG-[0-9]+\b`; report `{ path, line, needle }`. (3) `workspace-diff`: list both trees, compare the file sets minus each
stack's pin-file list (C#: `global.json`, `nuget.config`, `Directory.Build.props`, `*.csproj`; Node: `package.json`,
`package-lock.json`, `tsconfig.json`, `.npmrc`, `eslint.config.mjs`), then compare bytes; `STATEMENT.md` may differ only on
the lines E1.S3 marks `<!-- stack-sentence -->` and `<!-- framework-sentence -->`. (4) `push-check` composes the four checks
over `git rev-list --all` minus what the remote already has; no network call except `git ls-remote` when a remote exists.
(5) The README text: the team lead writes it; you write the structure check (`## Attribution` last, once). Commands:
`node spikes/lang-bench/run-tests.mjs test/run-base-tree.test.mjs test/workspace-diff.test.mjs test/push-check.test.mjs`.

---

## E1.S8a — the baseline visible-sample runner `runner/run_samples.py`, pinned

**Model:** scenarios + brief Opus (this text) · code Sonnet · review Opus. **Not marked** — the ledger is full (k = 3 = Q) —
so the spec is strict instead. It is the one program the agent, CI, the pre-freeze gate and E2's held-out scoring all share
(D37); a defect here is a defect of every number of the series.

**Goal.** One Python standard-library runner reads a directory of `<id>.in` / `<id>.out` pairs, runs `bash build.sh` and
`bash run.sh` exactly as a spec says, drives an olympiad case, a daemon scenario or a web scenario, compares what it observed
with the expected transcript under FORMAT v2's rules, and reports `PASS`/`FAIL` with the first mismatch — built **before**
FORMAT v2 is written down, so that v2 is checked against a program that exists (the FORMAT ↔ runner circularity, parent S8a).

**The grammar it implements — FORMAT v1 §4–§5 plus this delta, which E1.S9 writes into FORMAT v2.** The team lead may change a
row while S9 is drafted; the runner then follows in the same branch, and S9 is gated against the result.

| # | v2 decision | Runner behaviour |
|---|---|---|
| V1 | the version: `scenario 2` / `transcript 2` | any other version is a format error naming the file (exit 4); v1 files are re-conformed, never run |
| V2 | **exact-decimal JSON numbers**: every JSON number is parsed as an exact decimal (`int` and `decimal.Decimal`, never `float`); `2` = `2.0`; `22.0` ≠ `22.01`; `-0` = `0`; `9007199254740993` ≠ `9007199254740992` | `json.loads(…, parse_float=Decimal, parse_int=Decimal, parse_constant=<refuse>)`; duplicate member names refused (`object_pairs_hook`) |
| V3 | **default request headers**: the runner writes `Host: 127.0.0.1:<PORT>` and, when there is a body, `Content-Length: <n>`, and nothing else; a `header` line whose name equals a default (ASCII case-insensitive) replaces it at the default's position | the serialiser of v1 §4.3 with the replacement rule |
| V4 | **`connect` retries** a refused connection every 50 ms until its deadline (`within`, else `default within`); still refused → `refused <conn>`, the scenario ends | as stated; the readiness probe keeps v1's 50 ms retry within T |
| V5 | **a closed HTTP connection**: when a request on an `http` connection meets a connection the program has closed before a single byte of the response arrived, the runner reconnects once and rewrites the request within the same step deadline; a second close is `eof <conn> ""` | as stated; never more than one reconnect per request |
| V6 | **relational expectations** in `.out`: a normalised field may bind what it matched, `${:<type>=<name>}` (inside `repeat`, the name may carry `${i}`); then the statement `check distinct <name>…` (names or a `<prefix>*` glob: pairwise distinct as bytes or exact JSON values) and `check equal <name> <decimal \| name>` (exact decimal equality). A `check` produces no entry; a failed check is `FAIL <id> — check: …` | checks evaluated after the entries matched, in file order |
| V7 | **the T and G binding**: T is the `within` of `start`, G the `within` of the `wait-exit` that follows `signal TERM`; the conformance check (S8) requires them to equal the spec's T and G; a `--limits` file may carry `readyS` and `graceS`, and a scenario whose values differ is refused (exit 4), never silently re-timed | as stated |
| V8 | **`sleep` has no deadline** and takes no `within`; it counts toward the 600 s test bound | a `within` on `sleep` is a format error |
| V9 | **`split` offsets index the joined bytes** of the whole string expression after substitution and repetition; strictly increasing, each 0 < o < length | as stated; an offset outside is a format error |
| V10 | **body comparison precedence**: the transcript's body form decides — `body json` compares JSON values, `body <str>` bytes; the conformance check requires a `body json` only where a requirement carries F-JSON | no ambiguity left in the runner |
| V11 | **the program's environment** is the runner's own environment (which the box sets: D15's base plus the documented per-stack variables), plus `PORT`, plus the scenario's `env` lines; the runner adds nothing else | a stub that prints its environment shows exactly that |
| V12 | **runner bounds** (no wait or buffer without a ceiling): the program's stdout (when no step reads it) and stderr drained to files capped at 16 MiB each, truncation recorded; a connection's unread buffer above 256 MiB ends the scenario with the actual-only entry `overflow <conn>`; one test ≤ 600 s; a regex capture runs on at most 16 MiB | as stated |

**Files (bench).**

| Path | Change |
|---|---|
| `spikes/lang-bench/run-tests.mjs`, `spikes/lang-bench/lib/lower-priority.mjs` | new — v1's runner shape (`spikes/minigit-bench/run-tests.mjs`), the preload a verbatim copy of `.agents/conventions/tools/templates/lower-priority-preload.mjs` with only its `MOUNT` line changed |
| `spikes/lang-bench/runner/run_samples.py` | new — the entry: CLI, kind dispatch, summary, exit codes; adds its own directory to `sys.path` (with `-I` Python does not) |
| `spikes/lang-bench/runner/rs_lex.py`, `rs_script.py`, `rs_transcript.py` | new — strings, escapes, string expressions, durations; the `.in` parser to a step tree with its validation (keywords closed, names bound once and before use, `together`/`repeat` well formed, last statement `wait-exit`); the `.out` parser with `repeat`, `unordered`, binding fields and `check` |
| `spikes/lang-bench/runner/rs_match.py` | new — byte entries with normalised fields (shortest-run matching), HTTP replies, exact-decimal JSON values with `$unordered`, the one-to-one assignment for `unordered` groups, the checks of V6 |
| `spikes/lang-bench/runner/rs_proc.py` | new — the build (600 s), the program's start (`bash run.sh` in a new session, optional launch prefix), readiness, `signal TERM`, `wait-exit`, the kill of the process group, the leftover check (the group, or the cgroup's `cgroup.procs` when given), memory files when given |
| `spikes/lang-bench/runner/rs_net.py` | new — TCP connections with buffers and `recv` modes, `chunk`/`pause`, `rcvbuf`, `quiet`/`closed`, `shutdown`; the HTTP/1.1 client (serialisation, Content-Length, chunked, EOF-delimited, 1xx skipped, V3, V5) |
| `spikes/lang-bench/runner/rs_exec.py`, `rs_olympiad.py`, `rs_report.py` | new — the scenario executor (steps, `together` by a `threading.Barrier`, `repeat`, captures, deadlines, the four scenario-ending failures); the olympiad case (stdin, stdout, time, exit, the closed list of normalisations); PASS/FAIL lines, the mismatch report of v1 §5.5, the JSON result |
| `spikes/lang-bench/pin-runner.mjs`, `spikes/lang-bench/lib/runner-pin.mjs` | new — the manifest of `runner/` (every file's sha256, sorted) and its own sha256, written as the `runner` entry of `spikes/lang-bench/inputs/specs.json` with `version` |
| `spikes/lang-bench/inputs/specs.json` | new — `{ runner: { version, files, manifestSha256 }, format: null, grammar: null, specs: {} }` (S9 and S2 fill the rest) |
| `spikes/lang-bench/test/runner-*.test.mjs`, `spikes/lang-bench/test/py/test_*.py`, `spikes/lang-bench/test/fixtures/runner/**` | new — the toy stubs, the replay stub, the fixtures (§ tests below) |

**The CLI.** `python3 -I runner/run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE]
[--launch-prefix JSON] [--cgroup-of TEMPLATE] [--json OUT] [--only ID] [--parse-only] SAMPLES_DIR`. Defaults: the workspace is
the current directory; the kind is read from the `Kind` row of the workspace's `STATEMENT.md` (the closed list of FORMAT
§1.2), `--kind` overriding it; the build runs first; **without `--limits` the olympiad time limit is the floor (1 s) and every
time verdict says `against the floor`**; without `--cgroup-of` no memory verdict exists and every case says `memory: not
certified`; `--launch-prefix` is a JSON argv put before `bash run.sh` with `{case}` replaced (the hook E3 and OP-1's layer use);
`--cgroup-of` is a path template with `{case}` from which `memory.peak`, `memory.events` and `cgroup.procs` are read after the
case; `--parse-only` parses and validates every pair and runs nothing (the conformance check's road in, S8). Exit: `0` every
case passed · `1` a case failed · `3` environment (no `bash`, no free port, a launch prefix that does not start) · `4`
configuration (usage, a malformed sample, a version or T/G mismatch — named with file and line) · `5` the runner itself
failed (named). A `cases.txt` beside the pairs (FORMAT §3.1) attaches its R-ids to each case of the JSON result.

**Scenarios.**

- *Given* FORMAT's olympiad example and a replay stub, *when* the runner runs, *then* `PASS s03 — 1 entries`; *given* a stub
  that drops the final line feed, *then* `FAIL s03` with `first difference at byte 44` and exit 1.
- *Given* the daemon toy example and a correct toy stub, *then* PASS; *given* a stub that answers the first part of a split
  frame early, *then* `noise c1 "…"` at entry 2; *given* a stub that blocks every client while a slow reader reads nothing,
  *then* `timeout c1 ""` at entry 8 — the two failures FORMAT §6.2 states.
- *Given* the web toy example, *then* PASS; *given* a toy that lets two racing requests make two orders, *then* the report of
  FORMAT §6.3 (`body $.id: expected "${order}" = …, actual …`).
- *Given* a reply body `{"id":9007199254740993}` and the expectation `{"id":9007199254740992}`, *then* FAIL; *given* `2.0`
  against `2`, *then* PASS (V2).
- *Given* a server that closes the keep-alive connection after each reply, *when* two requests follow on one connection,
  *then* the second is rewritten once on a new connection and passes; *given* a server that closes it twice, *then* `eof`
  (V5).
- *Given* `check distinct r*` over three bound tokens of which two are equal, *then* `FAIL … — check: r0 and r2 are equal` (V6).
- *Given* a stub that ignores SIGTERM, *then* `stop: still running 5 s after SIGTERM, killed` and the group is gone after the
  case; *given* a stub that forks a child that outlives it, *then* `leftover: 1 process`; *given* a stub that never listens,
  *then* `not-ready` and the scenario ends.
- *Given* a stub that writes 300 MiB without being read, *then* `overflow c1` and the runner's own memory stays bounded (V12).
- *Given* a `.in` with `scenario 1`, a `within` on `sleep`, a name bound twice or a `split` offset past the end, *then* exit 4
  naming the file and line, and nothing is started.
- *Given* `--launch-prefix '["/usr/bin/env","LAUNCHED={case}"]'` and `--cgroup-of <fixture dir>/{case}` holding fake
  `memory.peak`, `memory.events` with `oom_kill 1` and `cgroup.procs`, *then* the case is `memory limit` whatever the exit
  status (the T-L30 shape, here on fakes; the real scope is OP-1's / E3's).
- *Given* a C# toy and a TypeScript toy of one fixture task, built with the host toolchains, *then* the runner reports the same
  case list for both (T-L21's shape); skipped with its reason when a toolchain is absent.

**Acceptance (parent S8a).** T-L21's shape for the three kinds against stub programs (stdlib-only imports; identical case lists
for a C# and a TS stub); FORMAT v2's three examples and the pilot specs' samples (01, 04, 05, once re-conformed by S9 and S6)
run through it with the expected verdicts; the pinned version and the manifest hash are in `inputs/specs.json` before FORMAT
v2's gate; the workspace copy equals the pinned file (S3's check). Plus: every runner module ≤ 400 lines (800 max), every
function < 50 lines and complexity ≤ 4, measured by a test; no `float` reaches a comparison (a test over the AST); the whole
suite green with `BENCH_REQUIRE_SANDBOX=1`.

**Test plan (red, green, red).**

1. `A_float_json_compare_would_equal_two_ids_one_apart_and_that_is_the_point` — red against a `json.loads` with defaults,
   green with V2, red again when `parse_int=Decimal` is removed.
2. `A_split_offset_counts_the_joined_bytes_not_the_first_string` — red when offsets index the first literal, green, red.
3. `A_program_that_ignores_SIGTERM_is_killed_with_its_group_and_reported` — red before the group kill (the stub survives the
   test: the janitor-style check finds its marker in `/proc/*/environ`), green, red with `killpg` replaced by `kill`.
4. `An_unread_flood_ends_the_scenario_instead_of_the_runner` (V12) — red with an unbounded buffer (the test's RSS ceiling for
   the runner trips), green, red.
5. `The_stdlib_import_scan_finds_a_known_stdlib_import` beside `Every_runner_import_is_standard_library_or_a_sibling`
   (`sys.stdlib_module_names`; a planted `import requests` fails it).
6. The verdict fixtures of FORMAT §6.2/§6.3 as named tests, each with its failing twin.

**Docs.** `research/module_tests.md`: the lang-bench runner's flows (olympiad case, each `recv` mode, `together` race, capture
and reuse, `unordered`, checks, stop, leftover, overflow, readiness), each `covered` with its test, and what the runner does not
prove (memory without a cgroup; a program's behaviour beyond the scenarios run). `research/module_lang_bench.md` (new): the
runner's CLI, exit codes, result format and bounds; E3.S10 completes the module document.

**Brief (Sonnet coder).** Branch `feat/lang-v2-e1-s8a`. Read FORMAT v1 (`git -C /home/jinx/git/dew_flow_ai_language_comparison
show main:todo/tasks/FORMAT.md`) §3–§6 and the delta table above; implement both, nothing more. (1) Lexer first, with unit
tests from FORMAT §4.1's rules (escapes, `\xHH`, `*count` up to 10 000 000 built lazily as `bytes * n`, durations ≤ 600 s,
the `\${` and `${:` rules). (2) Parsers produce frozen dataclasses (`@dataclass(frozen=True)`); validation returns a list of
`(file, line, message)`; never raise for a malformed sample. (3) Matching: a byte entry with fields is matched by a small
backtracking matcher over the field list (shortest run first); a JSON value by recursion with exact decimals; `unordered`
groups by a bipartite matching (Kuhn's algorithm, ≤ 64 entries per group — a larger group is a format error). (4) Processes:
`subprocess.Popen(['bash', 'run.sh'], start_new_session=True, env=…, stdin=PIPE)`, prefix from `--launch-prefix`; SIGTERM to
the pid (D38: `run.sh` execs the program), SIGKILL to the group at G; the leftover check after exit: `os.killpg(pgid, 0)`
or the cgroup's `cgroup.procs`, stated in the result which one was used. (5) Networking with `socket` and `selectors`; one
reader thread per connection that appends to a bounded buffer; deadlines from `time.monotonic()`; `together` prepares every
request's bytes, then a `threading.Barrier(n + 1)`; replies are read by later `recv` steps. (6) `.scenario/` fixtures: refuse
when `.scenario` exists and is not a directory you created (`os.lstat`, `O_NOFOLLOW` on each file) — a planted symlink must
not make the runner write outside the workspace. (7) Report exactly the lines of FORMAT §5.5, abbreviating strings > 120 bytes
as it says; the JSON result `{ format: 1, runner, grammar: 2, kind, build, cases: [{ id, status, reason, rIds, entries,
firstMismatch, lifecycle: { stop, leftover, leftoverEvidence, memory }, wallMs }], python }`. (8) Tests: stub programs are
toys (FORMAT's x2/x3 toys and a "replay stub" that serves exactly a pair's expected bytes once it received the pair's sent
bytes) — **never an implementation of a task of the series**. Folders from the owned helper; `TMPDIR` handed to Python.
Commands: `node spikes/lang-bench/run-tests.mjs`; `BENCH_REQUIRE_SANDBOX=1 node spikes/lang-bench/run-tests.mjs` for the done
run; `node spikes/lang-bench/pin-runner.mjs` last, committed with the runner.

---

## E1.S9 — `FORMAT.md` v2, checked against the runner, gated before any spec gate

**Model:** the document by the team lead (Opus, main session, as FORMAT v1); the grammar extractor and its test by a Sonnet
coder; the gate by coai (document gate) plus a consultant; review Opus.

**Goal.** One versioned format that every spec, sample and runner agrees with, gated before the first spec gate, with every
gap the three pilot specs and consult `3ea9ef36` found closed in a section or a fixed sentence, no plan identifier in a file a
run base carries, and its grammar sections given to the agent as `runner/GRAMMAR.md`.

**Files.**

| Repository | Path | Change |
|---|---|---|
| task | `todo/tasks/FORMAT.md` | v2 (branch `docs/format-v2`): `Format version: 2`; the delta V1–V12 of E1.S8a; an **F-LOAD** fixed sentence with slots for a load scenario's size and duration; `{ref}` lists of any length (`R5, R7 and R9`); a table belongs to the R paragraph it follows; the T and G series defaults (10 s, 5 s), the override with a reason, where the runner reads them (V7); F-BYTES-W against F-JSON (V10); a **neutral daemon example** naming no real protocol or product (D39, D18); the reading block 5.6 **removed** from the spec obligations (D37: the workspace carries `runner/` and `GRAMMAR.md`); the base-environment sentence following D15 ("the same base plus the documented per-stack variables"); §1.5's credit line without `NOTES.md` (D19, D39); sections 7–9 moved out; a changelog entry per gap |
| task | `runner/GRAMMAR.md` | generated: FORMAT v2's sections 3–5 verbatim under a two-line header naming the format version and FORMAT's sha256 |
| bench | `spikes/lang-bench/inputs/FORMAT-MAINTENANCE.md` | new — FORMAT v1's sections 7–9 (conformance list, amendment rules, changelog of decisions) for the maintainers, with the plan ids they need |
| bench | `spikes/lang-bench/tools/grammar-extract.mjs`, `spikes/lang-bench/test/grammar-extract.test.mjs` | new — the extractor and its test |
| bench | `spikes/lang-bench/inputs/specs.json` | `format: { version: 2, sha256, gate }`, `grammar: { sha256 }` |
| bench | `spikes/lang-bench/inputs/gates/FORMAT-v2.md` | new — the gate's verdict, reviewer count, every finding with its decision, the consultant's answer (no case text) |

**Scenarios.**

- *Given* FORMAT v2's three examples as files, *when* `run_samples.py` runs them against the toy stubs, *then* each gives the
  verdict the example states, and each stated failure is produced by its broken toy.
- *Given* FORMAT v2, *when* the plan-id grep runs over it, *then* no plan id matches (`E<n>.S<n>`, `D<n>`, `T-L<n>`,
  `G-<n>`); FORMAT's own `F-…` sentence ids stay, and the grep's pattern list says so; *given* the daemon example, *when* D18's needles run, *then* none
  hits. **Failure case:** a planted `RESP` in the example fails the check.
- *Given* `runner/GRAMMAR.md`, *when* it is regenerated from FORMAT v2, *then* it is byte-identical; *given* a one-line edit to
  FORMAT's §4, *then* the stored `GRAMMAR.md` no longer matches and S3's check fails.
- *Given* the document gate returns `revise`, *when* findings are accepted, *then* the runner changes in the same branch where a
  grammar row changed, and the gate runs again on the revised text — never a gate on text the runner does not implement.

**Acceptance (parent S9).** Every gap of the parent's S9 list has a v2 section or a fixed sentence, named in the changelog;
the three examples and the pilot specs' samples parse and run through the runner against stub programs with the expected
verdicts; no plan story, epic, decision or test id remains anywhere in `FORMAT.md` (a grep); the daemon example names no real
protocol or product (D18's needles find nothing); v2's gate verdict (document gate + consultant) is recorded before the first
spec gate; its sha256 replaces v1's in `inputs/specs.json`; specs 01, 05 and 04 are re-conformed to v2 before their own gates
(done in S2 per task).

**Test plan (red, green, red).** `GRAMMAR_md_is_the_verbatim_sections_of_FORMAT` (red before the extractor, green, red when the
extractor drops a fenced block). `FORMAT_v2_carries_no_plan_identifier` — written first against v1 (red: v1's §7 names
`E1.S6`), green on v2, red with one id planted. The examples' verdicts are S8a's tests, re-pointed at v2's files.

**Docs.** `todo/tasks/FORMAT.md` itself; `todo/README.md` of the task repository; `research/module_lang_bench.md` (FORMAT's place
and its hash in `inputs/specs.json`).

**Brief (team lead; extractor to Sonnet).** Write v2 on `docs/format-v2` from v1, the delta table of E1.S8a and the parent's S9
list; keep every v1 sentence that no gap touches. For each gap write the changelog line `- v2 · <date> · draft — <gap>: <what
changed>`. Run the examples through the runner before the gate. Gate: `mcp__coai__open` on the task repository's checkout and
branch, `mcp__coai__review_document` with `documentPath: todo/tasks/FORMAT.md` and a `purposeText` naming who acts on it (the
spec author, the suite author, the runner's maintainers, and — through `GRAMMAR.md` — the agents under test in both stacks),
what they must be able to do (write and read a spec and a sample unambiguously; implement the grammar with a standard library
alone), and what it does not cover (the tasks, the tests, the traps); `mcp__coai__resolve` every finding; one
`mcp__coai__consult` with the question "is any rule of this format ambiguous for a runner, a spec author or an agent reading a
sample?". **Extractor (Sonnet):** `grammar-extract.mjs <FORMAT.md> <out>` copies from the `## 3.` heading to the line before
`## 6.`, prefixes `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex>)` and one blank line, LF line ends;
refuses when either heading is missing.

---

## E1.S8 — FORMAT v1 (built) and the conformance check derived from v2

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** FORMAT v1 stays as the draft the epoch-3 re-review kept (`d7c73ff`, judged `fits`); it has no gate of its own — v2's
gate (S9) is its gate. What remains of S8 is the check every spec gate uses: **one conformance checker, derived from FORMAT
v2's maintainer list, that reads a spec and its samples and names every deviation** — the "to be built later" of FORMAT v1 §7.

**Files (bench).** `spikes/lang-bench/conformance.mjs`, `spikes/lang-bench/lib/conformance.mjs`,
`spikes/lang-bench/lib/conformance-sentences.mjs` (the fixed sentences read from FORMAT v2 itself, never retyped),
`spikes/lang-bench/test/conformance.test.mjs`, `spikes/lang-bench/test/fixtures/conformance/**` (synthetic specs only).

**The checks** (FORMAT-MAINTENANCE.md's list, v2): the file name and title; the five header rows and their allowed values; the
eight sections of the kind; R-ids unique, contiguous, ascending before the freeze, F-REQ's `{n}`; every fixed sentence the
kind requires, verbatim after whitespace collapse, slots filled with allowed values (`{ref}` lists of any length); F-INVALID
xor F-ERRORS; tables inside their R paragraph; **T and G stated, and equal to every visible scenario's `start … within` and
`wait-exit within`** (V7); the samples (`tasks/<NN-slug>/samples/` holds exactly `s01…s<k>`; the olympiad table's cells equal
the files with `⏎` read as LF; each daemon and web pair parses — **through `run_samples.py --parse-only`, never a second
parser**); no needle (a list given by `--needles <file>` outside git, plus the fixed patterns of S1); `⏎` only in section 6;
no reading block (v2).

**Scenarios.** *Given* a conforming synthetic spec of each kind, *then* exit 0 and one `ok` line per check. *Given* a spec whose
F-STOP says 5 s and whose sample waits `within 4s`, *then* `T/G: sample s02 waits 4 s, the spec says 5 s`. *Given* a table in
section 1 not preceded by an R paragraph, *then* it is named. *Given* a daemon pair that does not parse, *then* the runner's
`(file, line, message)` is quoted. **Failure case:** given a FORMAT file without a fixed sentence the checker expects, the
checker refuses to run (exit 4) rather than passing every spec.

**Acceptance.** The parent's S8 criteria as they stand after v2: the examples run (S9); the conformance check of S2 is derived
from FORMAT v2 (the fixed sentences are read from the document — a test changes one word in a fixture FORMAT and sees the check
follow); its sha256 is in `inputs/specs.json` and frozen with the specs; a change after the freeze is a dated amendment naming
the tasks it touches.

**Test plan (red, green, red).** `A_retyped_sentence_list_would_pass_a_spec_after_FORMAT_changed_and_that_is_the_point`
(enumerate, never retype — `common.testing`): red with a literal list, green reading FORMAT, red with the reader stubbed.
`The_checker_still_finds_a_known_deviation` beside every negative test.

**Docs.** `spikes/lang-bench/inputs/FORMAT-MAINTENANCE.md` points at the checker; `research/module_lang_bench.md`.

**Brief (Sonnet coder).** `node spikes/lang-bench/conformance.mjs --repo <task repo> --task NN-slug --format
<repo>/todo/tasks/FORMAT.md [--needles <file>]`; output one line per check, `ok` or `FAIL <check> — <what, where>`; exit 0 / 1 /
4. Extract the fixed sentences from FORMAT's `text` blocks by their ids (the table of §2.1 maps id → kinds → section);
compare after collapsing whitespace. Call the runner with `runBounded` (`spikes/minigit-bench/lib/run.mjs:35`), 60 s.

---

## E1.S5 — toolchains pinned read-only with checksums; the preflight; the declared locale

**Model:** scenarios + brief Opus · code Sonnet · review Opus · operator (the locale).

**Goal.** The pre-freeze gate, the references and later every session run on exactly SDK 10.0.403, Node 26.13.0, TypeScript
7.0.2 and the pinned Python, from read-only directories an unattended upgrade cannot move (v1's 2026-10-30 lesson), in a box
whose `de_DE.UTF-8` exists.

**Files.** Bench: `spikes/lang-bench/inputs/stacks-lang.json` (pins, checksums, probe argv, toolchain directories),
`spikes/lang-bench/install-toolchains.mjs`, `spikes/lang-bench/lib/toolchain-pins.mjs`, `spikes/lang-bench/lib/preflight-lang.mjs`,
`spikes/lang-bench/test/{toolchain-pins,preflight-lang}.test.mjs`. Private area: `~/rsd/lang-v2/toolchains/{dotnet-sdk-10.0.403,
node-v26.13.0-linux-x64, typescript-7.0.2}/` (outside git, `chmod -R a-w` after install). Host: `/etc/locale.gen` and the
locale archive (operator).

**Scenarios.** *Given* the official artefacts, *when* `install-toolchains.mjs` runs, *then* the SDK archive's sha512 equals the
one in the .NET 10 release metadata, Node's tarball's sha256 equals its `SHASUMS256.txt` line, and each directory is read-only
with its tree hash recorded. *Given* a box binding those directories, *when* the preflight probes `dotnet --list-sdks`, `node
--version`, `tsc --version` and the sha256 of `/usr/bin/python3`'s real file, *then* every value equals the pin. **Failure
cases:** a second SDK on the box's `PATH`, a Node of another patch, a changed Python binary — each refused naming the pin and
the value seen; a host without `de_DE.UTF-8` in `locale -a` — refused with the exact operator command.

**Acceptance (parent S5).** The preflight refuses a box whose `dotnet --list-sdks`, `node --version` or Python checksum differs;
the checksums are in `inputs/stacks-lang.json`. Plus: `locale -a` inside the box lists `de_DE.utf8`; the SDK directory holds only
10.0.403.

**Test plan (red, green, red).** `A_PATH_that_still_finds_the_apt_SDK_is_refused` (red when the probe trusts the first `dotnet`
found, green with the pinned directory first and alone, red again). `Preflight_refuses_a_missing_locale_with_the_operator_command`.

**Docs.** `research/module_lang_bench.md` (the toolchain table); the README of the task repository names the versions (no paths).

**Brief (Sonnet coder).** Download with `curl -fsSL` into an owned folder, verify, extract into `~/rsd/lang-v2/toolchains/…`,
`chmod -R a-w`; the SDK by `dotnet-install.sh --version 10.0.403 --install-dir <dir> --no-path` (the script's own sha256 recorded
too); TypeScript by `npm install --prefix <dir> typescript@7.0.2` with the pinned Node. The preflight reuses v1's bwrap
primitives (`spikes/minigit-bench/lib/bwrap.mjs:10-19`) to probe inside a box; never apt. **Operator step, printed by the
preflight when it fails:** `sudo sed -i 's/^# *de_DE.UTF-8 UTF-8/de_DE.UTF-8 UTF-8/' /etc/locale.gen && sudo locale-gen`.

---

## E1.S4 — package sets and the two cache snapshots

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** Each stack's offered package set (parent §4.6) exists as one hashed offline snapshot and a manifest; `PACKAGES.md` is
generated from it; a build in a box with no registry restores exactly that set and fails naming anything else.

**Files (bench).** `spikes/lang-bench/inputs/cache-recipe-lang.json` (C#: a project referencing `Microsoft.Extensions.Hosting`
10.0.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x, `xunit.v3` and the test SDK pieces; Node: `fastify@5.12.7`,
`@fastify/jwt@10.2.6`, `@fastify/type-provider-typebox@6.3.0`, `@sinclair/typebox@0.34.52`, pino as Fastify resolves it,
`@types/node` 26.x, `typescript@7.0.2`, the lint packages S7's template needs), `inputs/cache-snapshots-lang.json`,
`make-snapshots.mjs`, `lib/packages-md.mjs`, `test/{cache-lang,packages-md}.test.mjs`. Private area:
`~/rsd/lang-v2/caches/<stack>-<treeSha256>/`.

**Scenarios.** *Given* the recipe, *when* `makeSnapshot` (`spikes/minigit-bench/lib/cache-snapshot.mjs:162`) runs per stack in a
box through the egress proxy, *then* the tree hash and the package list are registered (`registerSnapshot`, `:233`). *Given* a
box with the registries refused, *when* `npm install fastify@5.12.7` (offline) and `dotnet restore` of the pinned project run,
*then* both succeed. **Failure case:** `npm install express` and a `PackageReference` outside the set fail, and the message names
the package (T-L13). *Given* the manifest, *then* `PACKAGES.md` lists every package and version, generated, never typed.

**Acceptance (parent S4).** T-L13; the manifest lists every package and version; a package outside the set fails naming it;
both hashes in `inputs/cache-snapshots-lang.json`.

**Test plan (red, green, red).** `A_registry_reachable_from_the_box_would_hide_a_missing_package_and_that_is_the_point` — red when
the proxy allows the registry (the outside package installs), green with the registries refused, red with the refusal removed.
`PACKAGES_md_is_derived_from_the_manifest` (a package added to the manifest appears without a code change).

**Docs.** `research/module_lang_bench.md` (the recipe and the snapshot record).

**Brief (Sonnet coder).** Reuse v1's `makeSnapshot`, `seedCaches`, `treeHash`, `packagesOf` as they are; add only the v2 recipe
and the `PACKAGES.md` renderer (a table: package, version, what it is for — the last column from the recipe's `purpose` field).
Snapshot directories read-only after registration. Pin the toolchains of S5 in the box.

---

## E1.S3 — the workspaces

**Model:** scenarios + brief Opus · code Sonnet · review Opus.

**Goal.** Per task and stack a workspace an agent can start in: built by `bash build.sh` offline, started by `bash run.sh`,
which **ends with `exec`** of the program (D38), carrying the frozen statement, the samples, the read-only `runner/` and the pin
files — identical across stacks but for the stack. Two phases, because the D40 gate needs the build and start contract before
any spec freezes, while `STATEMENT.md` can only be generated from a frozen spec.

**Files.** Bench: `spikes/lang-bench/templates/{csharp,node}/{olympiad,daemon,web}/**` (one source: `build.sh`, `run.sh`, the pin
files, the empty `.csproj` skeleton, `nuget.config` with the offline source only, `Directory.Build.props`, `package.json`,
`package-lock.json`, `tsconfig.json` — v1's `strict`, `noEmit`, `erasableSyntaxOnly`, `verbatimModuleSyntax`, `module nodenext` —
`.npmrc` `offline=true`, `eslint.config.mjs`), `spikes/lang-bench/build-workspaces.mjs`, `lib/statement.mjs`, `lib/task-repo.mjs`
(fetch at a sha from a URL or, for tests, a local path, into `~/.cache/lang-v2/<sha>/`, verified against `inputs/hashes-lang.txt`
— v1's `fetchInputs` shape, `spikes/minigit-bench/lib/fetch-inputs.mjs:57`), `inputs/hashes-lang.txt`,
`test/{templates,statement,task-repo,workspace-exec}.test.mjs`, `test/fixtures/hello/{csharp,node}/**`. Task repository:
`runner/` (the pinned copy, S8a, plus `GRAMMAR.md`, S9), `csharp/<NN-slug>/**`, `node/<NN-slug>/**` (phase B).

**Phase A (before the D40 gate):** the templates, a hello program per stack and kind, `runner/` copied and checked against the
manifest. **Phase B (after the freeze):** `STATEMENT.md` = the frozen spec's text + the stack sentence (+ the framework sentence
for a web task), each sentence on its own marked line; `samples/` and `runner/` copied; `PACKAGES.md` from S4; the twenty
workspaces; `inputs/hashes-lang.txt`; `workspace-diff` over ten tasks.

**Scenarios.**

- *Given* each stack's empty template in a box with no registry, *when* `bash build.sh` and `bash run.sh` run with the hello
  program, *then* it builds within 600 s and `python3 -I runner/run_samples.py samples/` reports the hello samples' verdicts
  inside the box.
- *Given* the started pid, *then* its `comm`/`exe` is `dotnet` or `node`, it has no bash parent, and SIGTERM to that pid reaches
  the program (a hello daemon logs it) (T-L31). **Failure case:** a `run.sh` without `exec` fails the test.
- *Given* a fetched task repository at a sha, *when* one workspace file differs from `hashes-lang.txt`, *then* the fetch is
  refused naming the file (T-L1); *given* a bare session's workspace, *then* it holds exactly the task folder's files — no root
  file, no other stack, no `todo/` (T-L2).
- *Given* task 9, *then* `csharp/09-*` and `node/09-*` hold the same file set as every other olympiad workspace and no parser,
  adapter or grader file (a test lists the files and greps for one) (D33).
- *Given* the frozen spec and a generated `STATEMENT.md`, *then* they are equal but for the marked sentences; *given* a one-word
  edit in the spec, *then* the test fails.

**Acceptance (parent S3).** In a box with no registry each stack's empty workspace builds and `run.sh` starts a hello program;
T-L2, T-L31; the runner runs inside the box and reports the sample verdicts; the `runner/` hash equals `inputs/specs.json`; the
statements equal the spec plus the sentence; the task-9 file set. Plus T-L1 and `workspace-diff` clean on ten tasks (phase B).

**Test plan (red, green, red).** `A_run_sh_without_exec_leaves_a_bash_parent_that_eats_SIGTERM_and_that_is_the_point` — red with
a template lacking `exec` (the stub's SIGTERM log stays empty), green, red with `exec` removed. `A_workspace_file_off_the_hash_list_is_refused`.
`The_statement_differs_from_the_spec_only_in_the_marked_sentences`.

**Docs.** The task repository's `research/module_tests.md` (CI's sample job runs the same runner); bench `research/module_lang_bench.md`.

**Brief (Sonnet coder).** C#: `build.sh` = `dotnet build App.csproj -c Release --nologo -p:RestoreSources=<offline>` with the
offline source from `nuget.config` only; `run.sh` = `#!/usr/bin/env bash`, `set -euo pipefail`, `cd "$(dirname "$0")"`,
`exec dotnet bin/Release/net10.0/App.dll`. Node: `build.sh` = `npm ci --offline` and `npx --offline tsc --noEmit -p
tsconfig.json`; `run.sh` = … `exec node src/main.ts`. Neither script sets D15's variables (the box does, E3). Mark the stack
sentence with `<!-- stack-sentence -->` on the line before it, the framework sentence likewise. The generator writes into a
worktree of the task repository, never into a checkout someone works in.

---

## E1.S6 — the visible samples

**Model:** the samples by the team lead (Opus, with the spec — samples are test design, a judge's work); the checks are S8's and
S8a's; review Opus.

**Goal.** Per task, a few small, clean samples in FORMAT v2's pair format, existing **before** the spec's conformance gate, run
against both draft references through the runner before the freeze (D40), and validated again with the held-out cases in E2.

**Files (task repository).** `tasks/<NN-slug>/samples/s01.in` … `s<k>.out` per task (01's three cases from its spec table; 04's
four from its text; 05's four re-conformed to `scenario 2`); the spec's section 6 showing the same bytes. Bench:
`spikes/lang-bench/inputs/samples-lang.json` — per task the files' sha256 and a dated line *"checked against the test-design
notes on <date>: no sample reaches a trap"*, with no case text.

**Scenarios.** *Given* a spec and its `samples/`, *when* the conformance check runs, *then* the table or the code blocks equal
the files byte for byte; *given* one byte changed in a file only, *then* it fails naming the sample. *Given* a daemon or web pair,
*when* `--parse-only` runs, *then* it parses. *Given* both draft references, *when* the pre-freeze tool runs the samples in the
gate environment, *then* both pass (the D40 half that S2a's tool runs). **Failure case:** a sample whose expected transcript a
reference cannot meet within the spec's T, G or step deadlines is changed before the freeze — a sample changed after the freeze
is a spec amendment.

**Acceptance (parent S6).** Every spec's sample table and its `samples/` hold the same bytes (a test); the runner runs them against
both draft references in the box and both pass before the freeze; the same holds against the final references (**closes in
E2**); no sample reaches a trap — recorded now against the test-design notes, **checked formally against the trap table in E2**
(E2.S2 owns the table).

**Test plan.** The checks are S8's; this story's red is real: the first conformance run on each pilot spec is red (01 has no
sample files; 05's are `scenario 1`; 04's exist only in its text) and is recorded, then green.

**Docs.** The spec's section 6; `inputs/samples-lang.json`.

**Brief (team lead).** Samples reach no trap and say so in their comment line; daemon and web samples state every deadline in
the file; T and G as the spec states them; one sample per kind of behaviour the spec's section 1 names, at most five. Never copy
a held-out-shaped scenario into `samples/`.

---

## E1.S2 — the ten task specs, gated and frozen — cornerstone

**Model:** **`model: fable`** — Fable writes this story's per-task scenarios and brief (the spec brief below); the spec text is
the team lead's (Opus, main session — D21: the specs are written by the team lead, never by the agents under test); Opus reviews;
the gate is coai's document gate plus a consultant plus the bias review. If Fable refuses a launch on a limit, Opus writes the
spec briefs and the summary says so (`common.subagent-models`).

**Goal.** Ten stack-neutral specs that two references written apart implement identically, that name no source, trap or
language, that conform to FORMAT v2, whose riskiest contracts were executed by both draft references before the freeze, and
that are gated and frozen under one tag — because an ambiguous or biased spec makes every later failure unadjudicable.

**Files.** Task repository: `todo/tasks/NN-slug.md` × 10 (branch `docs/spec-NN` each; 01, 04, 05 re-conformed to v2 by a fix
forward on their branches), the header rows updated at the gate and the freeze. Bench: `spikes/lang-bench/inputs/bias-review/NN.md`
(verdict, reviewer count, notes, every finding with its decision, the bias answer per stack — no case text),
`inputs/specs.json` (per spec: `sha256`, `state: draft | gated | frozen`, `gate`, `rIds`, `T`, `G`), `lib/spec-freeze.mjs`
(`specBlobMatches({ repo, tag, path })` — T-L20's guard half) and its test. Private area: `~/rsd/lang-v2/test-design/spec-briefs/NN.md`
(Fable's per-task briefs, outside git) and the riskiest-contract scenarios beside the test-design notes.

**Per task, in the order of §4.1** (the three pilot specs first): (1) Fable's spec brief; (2) the team lead's spec draft on
`docs/spec-NN`; (3) the samples (S6); (4) the conformance check green (S8); (5) the two draft references (S2a); (6) **the D40
pre-freeze execution gate** — both draft references pass every visible sample and the task's riskiest-contract scenarios through
the runner in the gate environment, with the spec's T, G and step deadlines in force; a value a reference cannot meet is changed
in the spec now (**blocked on OP-1** for the environment); (7) the coai document gate, the consultant, the bias question, two
readers (an Opus judge and the consultant) with no open question; (8) the header's `Gate verdict` row, `state: gated` and the
sha256 in `inputs/specs.json`; the branch merged. **After the tenth:** one commit on `main` sets every `Freeze tag` row, the tag
`spec-frozen-<yyyymmdd>` (annotated) on it, `state: frozen` and the frozen sha256 per spec, the tag pushed alone when the remote
exists. From the gate on, a spec changes only by a dated amendment (FORMAT's rule), applied to both stacks.

**Scenarios.**

- *Given* a gated spec, *when* its bytes change outside the header rows without an amendment item, *then* `specs.json`'s check
  fails naming the spec. *Given* the frozen tag, *when* a session's run base carries a spec blob that differs, *then*
  `specBlobMatches` refuses it (T-L20's guard half).
- *Given* a known task's spec, *when* the needle scan runs (D18: ids, titles, site names, `CSES`, `IOI`, `Werewolf`, `RealWorld`,
  `Conduit`, for task 5 `Redis`, `RESP`, `redis.io`, `redis-cli` and the client library names), *then* nothing hits.
- *Given* the document gate returns `revise`, *then* the accepted findings change the text and the conformance check runs again
  before the next round; *given* `call_human`, *then* the spec stops and the person is asked (`mcp__coai__ask_human`).
- *Given* the pre-freeze gate, *when* one draft reference misses a step deadline the spec states, *then* the spec's value (or the
  reference) is fixed before the gate round — never a wider deadline after the freeze. **Failure case:** a task whose gate
  environment is not available (OP-1 undecided) does not freeze; its state stays `draft`.

**Acceptance (parent S2, per task).** The gate's verdict is `proceed`, `good_enough` or `continue_anyway`; the consultant answered;
the bias question got an explicit answer per stack with no open finding; no id, title, site or trap in the text; a known task's
needles find nothing; the spec conforms to FORMAT v2; the limits as floors and rule, the timing contract (T, G, step deadlines),
the semantic bounds, the I/O and start contracts, the frozen list and "hidden tests exist" are present; the D40 gate passed for
both draft references, including the riskiest contracts the parent names (task 10's virtual clock and fixture interface, task 6's
JWT and time semantics, task 8's concurrency oracle, task 4's token capture-and-reuse and whole-rule concurrency, task 5's partial
frames, pipelining and slow reader); two readers list no open question. **Whole story:** the ten verdicts and the tag exist
before E2.S1 finalises a reference; after the tag a spec changes only by a dated amendment applied to both stacks and before the
pre-registration.

**Test plan (red, green, red).** `A_spec_edited_after_its_gate_without_an_amendment_is_refused` (red before the check, green, red
with the header-row exemption widened to the whole file). `A_run_base_spec_blob_off_the_tag_is_refused_before_launch` (T-L20).
For each spec the first conformance run is recorded red (or why it was green at once), then green.

**Docs.** The specs; the task repository's `todo/README.md`; `inputs/bias-review/NN.md`; `inputs/specs.json`.

**Brief (Fable spec writer).** For each task, write `~/rsd/lang-v2/test-design/spec-briefs/NN.md` (outside git), from the parent
§4.1 row, §4.3, D3, D4, D13, D14, D18, D25, D33, D35, D39, FORMAT v2 and the test-design notes, holding: (a) **the requirement
inventory** — every behaviour a held-out case will assert, as a sentence the spec must contain, so that D25's "every held-out
test checks behaviour the statement requires" holds by construction; (b) **the contract decisions** the spec must make and the
plan leaves to it — the protocol matters (statuses, content types, frames, numeric syntax), the semantic bounds, the clock
semantics, T and G (or the override and its reason), the error behaviour or F-INVALID's scope; (c) **the ambiguity list** — every
place two careful references could read differently, each with the sentence that closes it; (d) **the bias check** — per
requirement, whether either stack meets it with more than the required behaviour needs (the culture: `de_DE.UTF-8`, D35; 64-bit
integers in JSON; Unicode and byte semantics; time zones), and the neutral wording; (e) **the sample plan** for S6 (no sample
reaches a trap); (f) **the riskiest-contract scenarios** of D40 for the task, written as scenario pairs beside the test-design
notes, never in git. Keep known tasks paraphrased with no id, title, site or hint that they are known; keep a trap out of the
text; name no language. Hand each brief to the team lead, who writes the spec; Opus reviews the spec against the brief before
the gate.

---

## E1.S2a — draft references outside git; the pre-freeze execution tool

**Model:** briefs by the team lead (Opus) per task and stack · code Sonnet, **one launch per program, the two stacks' coders never
seeing each other's code** · review Opus.

**Goal.** For each task one C# and one TypeScript reference, written apart from the spec draft and samples, living in
`~/rsd/lang-v2/refs/` beside the held-out material and never in a workspace, a run base or git — so that the D40 gate can execute
them; E2.S1 finalises, validates and calibrates them. And one tool that runs the gate the same way every time.

**Files.** Private area: `~/rsd/lang-v2/refs/<NN-slug>/{csharp,node}/` (each a full workspace: S3's template plus sources).
Bench: `spikes/lang-bench/inputs/references.json` (per task and stack: `treeSha256` by `treeHash`, `state: draft`, `specSha256`,
`writtenOn`), `spikes/lang-bench/prefreeze.mjs`, `lib/prefreeze.mjs`, `test/prefreeze.test.mjs`.

**The tool.** `node spikes/lang-bench/prefreeze.mjs --task NN-slug --refs ~/rsd/lang-v2/refs --extra <dir outside git>` copies
each reference into an owned scoring folder (pristine copy), runs `run_samples.py` over the task's `samples/` and over the
riskiest-contract pairs given by `--extra`, **in the gate environment** (the launch layer OP-1 decides: today only its shape is
known — the runner's `--launch-prefix` and `--cgroup-of` hooks), and writes a dated line per reference into
`inputs/references.json` (`prefreeze: pass | fail, on, runnerManifest, formatSha256, specSha256`) with no case text. It refuses a
`--refs` or `--extra` inside any git worktree (`refuseInsideWorktree`, `spikes/minigit-bench/lib/git-guard.mjs:28`).

**Scenarios.** *Given* both drafts of a task, *when* the tool runs, *then* each reference's line says `pass` with the manifest,
FORMAT and spec hashes it ran under. *Given* `--refs` inside the bench checkout, *then* exit 4 and nothing runs. *Given* a draft
failing one riskiest-contract scenario, *then* `fail` with the case id only (never its text), exit 1. **Failure case:** the gate
environment is unavailable (OP-1) — the tool exits 3 with `gate environment not available` and records nothing as passed.

**Acceptance (parent S2a).** For each task both drafts build and run in the gate environment through `runner/run_samples.py` and
pass every visible sample and the task's riskiest-contract scenarios; the drafts are hashed in `inputs/references.json` as
`draft`; no draft or its hash leaks into the task repository (`leakcheck`; a test asserts no file of either repository has a
reference file's sha256).

**Test plan (red, green, red).** `A_reference_inside_a_worktree_is_refused_before_it_runs` (red without the guard, green, red).
`The_prefreeze_record_names_the_hashes_it_ran_under`. The tool's own tests use toy programs, never a reference.

**Docs.** `research/module_lang_bench.md` (the tool, its record, the private area).

**Brief (team lead → Sonnet coder, per task and stack).** Give the coder: the spec draft, the samples, S3's template for its stack
and kind, `PACKAGES.md`, the build and run commands, and the instruction to implement the spec's requirements completely and
idiomatically for its stack — **not** the test-design notes, the trap table, the riskiest-contract scenarios or the other stack's
reference. The coder runs `python3 -I runner/run_samples.py samples/` in its workspace until it passes and reports which
requirements it found unclear; every unclear requirement goes back to the spec (S2) before the gate.

---

## E1.S7 — process-arm templates, CI, rulesets, deploy key, PAT, run bases as orphan generated trees

**Model:** scenarios + brief Opus · code Sonnet · review Opus · operator (the PAT; the go for each GitHub setting). Built here to
v1's design (`PLAN_minigit_stack_bench.md` §6, [PLAN_minigit_stack_bench_E2.md](PLAN_minigit_stack_bench_E2.md) E2.S4), for one
repository with two stack folders (D32); nothing imported from code that does not exist.

**Goal.** What a process session is given — conventions, instructions, CI, the stack folder — is one source, and a run base is an
orphan commit of the generated tree of the frozen tag (D39) that holds no credit, link or attribution history, cut only by the
deploy key, protected by rulesets.

**Files.** Task repository: `.github/workflows/{ci.yml,family-checks.yml,pr-title.yml}` (one job per stack, path-filtered; the
required `build · test` job per stack; the visible-sample acceptance job not required; `branches: [main, 'run/**']`); settings,
rulesets `run-main`, `template-frozen`, the tag ruleset; the deploy key as the only bypass actor. Bench:
`spikes/lang-bench/lib/run-base.mjs` (`cutRunBase({ rid, tag })`: S1's `generateRunBaseTree`, the needle scan over the tree,
`git commit-tree` with no parent, pushed to `run/<rid>/main` with the deploy key), `spikes/lang-bench/lib/github-lang.mjs`
(settings and rulesets as data, applied with `gh api`, read back), `spikes/lang-bench/p0-lang.mjs`,
`test/{run-base,github-lang}.test.mjs`.

**Scenarios.** *Given* the frozen tag, *when* a run base is cut, *then* `git rev-list run/<rid>/main` has exactly one commit, its
tree equals the generated tree, and the needle scan over the tree and that one commit finds nothing; **failure case:** a planted
credit in the tree refuses the cut. *Given* the PAT, *when* it pushes directly to `run/<rid>/main`, *then* the ruleset refuses
(read back). *Given* a PR into a run base, *then* CI runs on the non-default base and the stack's job is required; a planted lint
or analyzer violation turns it red per stack; merge is blocked until threads resolve.

**Acceptance (parent S7).** v1's P0 checks pass here: a run base cut by the deploy key, a direct push with the PAT refused, CI
green on a non-default base, a planted violation red per stack, merge blocked until threads resolve; plus S1's run-base check
completed with `git rev-list` of the base (one commit, D39).

**Test plan (red, green, red).** `A_run_base_with_history_would_carry_the_attribution_commit_and_that_is_the_point` (red with a
normal branch from the tag, green with the orphan commit, red with `commit-tree -p` restored). The GitHub checks are live,
recorded as P0 lines with dates.

**Docs.** The task repository's `README.md` (CI, the run-base namespace); bench `research/module_lang_bench.md`.

**Brief (Sonnet coder).** Follow v1 E2.S4's steps 2–5 (`PLAN_minigit_stack_bench_E2.md:724-800`) with these changes: one
repository, two stack folders, the CI path filters cover each stack folder and every shared path it depends on (`runner/`,
`todo/tasks/`) — or the job runs unfiltered (`common.platform-limits` §2); the deploy key's private half mode 600 under
`~/.config/lang-v2/`; every GitHub write is listed for the operator's go before it is made; the PAT is read from a mode-600 file,
never an argv or an environment variable of a long-lived process.

---

## 9. Open points

Each names where the plan cannot be built as written, or reads two ways, with its classification under
`common.plan-change-control`. **Epoch 3 has 0 changes; one recorded change is allowed, a second stops the plan.** Nothing below
is decided in this document.

| # | Where | The point | Class |
|---|---|---|---|
| OP-1 | `todo/PLAN_lang_comparison_v2.md:184` (D40), `:788` (§14 step 1), `:1025-1026` (E1.S2: "the final sandbox environment (the box, the scope, the declared culture)"), `:1039` (S2a), `:1062` (S6); against `:180` (D36), `:315`, `:329` (§4.3), `:1190`, `:1205`, `:1210` (E3.S2, S3, S5), `:850` (T-L30), `:933` (Q4) | **The pre-freeze gate needs the final scoring environment in E1, and no E1 story builds it.** The v2 scoring box, D15's environment with the culture, the program's own `systemd-run --user --scope` (D36) and the layout in which a driver reaches a daemon or web program are E3 deliverables (E3.S2, S3, S5; T-L14, T-L30). And the network layout is designed nowhere: the scoring box has no network (v1's `isolation()` is `--unshare-net`, `spikes/minigit-bench/lib/bwrap.mjs:15`) while the driver runs outside the program's scope (D36), so a driver outside the box cannot reach the program's port. Options: (a) E1 builds a minimal launch layer for the gate (a loopback-only network namespace shared by the driver and the boxed program, the scope around the program only), which E3 reuses and widens — work moved from E3 to E1; (b) the E1 gate runs in a narrower environment (the box and the culture, no scope, memory uncertified as D13 allows for a non-data run) and a second execution check runs in E3 before the pre-registration — D40 narrowed; (c) the freeze waits for E3's launch layer — the build order changes. One cause, so one change whichever option. Until decided, every E1 step but the gate run and the freeze proceeds (§5). | **change** (build order, approach) |
| OP-2 | `:1015` (S2: "the tag … on the commit holding all ten") against `:788-789` (§14: "per task, in order: … then the gate … and the freeze (tag, hashes)") and `:186` (D21) | One tag for all ten, or a freeze per task? Read here as: per task the spec is **gated** (verdict, hash, the amendment rule in force from the gate on); the one tag freezes all ten together after the tenth. Both texts hold under this reading, and E2 starts after E1 in any case (one branch per epic). | not a change (a reading that satisfies the plan as written) |
| OP-3 | `:1079` (S8: "its own gate (document gate + consultant)"), `:1080` ("E2.S2's runner"), `:1005` (E1 done-when: "`FORMAT.md` is gated … (S8)") | FORMAT v1 has no gate of its own: the epoch-3 re-review kept it as a draft to be re-conformed to v2 "before its gate" (`:84`), so v2's gate (S9) is its gate; "E2.S2's runner" is S8a's runner since D37 (E2 owns no runner, `:1126-1128`). | not a change (repaired references) |
| OP-4 | `:1094`, `:1113` ("the four pilot specs' samples") | Three pilot specs exist (01 `28bf777`, 05 `30af434`, 04 `0b69a50`); "four" counts the four drafts of the re-review table (`:84-87`), FORMAT v1 among them. Read as the three specs' samples. | not a change (repaired reference) |
| OP-5 | `:181` (D37: `runner/run_samples.py`), `:497` (§5) against `common.coding-style` (800 lines max) | The runner cannot be one file under the size rule; `runner/` is a directory of standard-library modules with `run_samples.py` the entry, hashed as one manifest (`inputs/specs.json`), the frozen list naming `runner/`. With `-I` Python leaves the script's directory off `sys.path`, so the entry adds its own frozen, read-only directory itself. | not a change (the plan names `runner/` and its hash) |
| OP-6 | `:179` (D35), `:791` (§14: "the locale `de_DE.UTF-8` in the box image") | `locale -a` on the host lists `C`, `C.utf8`, `POSIX` only (2026-10-31); the boxes bind the host root read-only, so generating the locale needs root — an operator action (§4, E1.S5). | not a change (operator action) |
| OP-7 | FORMAT v1 §1.5 (task repo `main:todo/tasks/FORMAT.md`), against `:178` (D19), `:183` (D39), `:496` (§5) | FORMAT v1 still says credit lives in `tasks/<NN-slug>/NOTES.md`; v2 drops it (E1.S9's file row). | not a change (satisfies D19/D39 as written) |
| OP-8 | `:1009` (S1: "the generated run-base tree holds none of them … `git rev-list` of the base") | The tree generator is S1's (`generateRunBaseTree`); the orphan commit and its `git rev-list` are S7's (after the push). S1 tests the tree, S7 the history. | not a change (order inside the epic) |
| OP-9 | `:1066` (S6: "no sample reaches a trap (the trap table says …)") | The trap table is E2.S2's; S6 records the check against the test-design notes now and the formal check closes in E2, as the plan already says for the final references. | not a change |
| OP-10 | `:1010` (S2: "written by the team lead"), `:186` (D21) against the role table's cornerstone row (Fable writes "the spec") | Two kinds of "spec": Fable writes the story's spec (the per-task spec briefs); the team lead writes the task specs (D21). | not a change |
| OP-11 | `planning-docs` § *Across repositories* (a boundary named in both repositories' plans) against `:183` (D39) | A `todo/PLAN_*` in the task repository would enter every run base; the boundary is named in the README's `## Attribution` section instead (cut from run bases) and in this document. | not a change (D39 as written) |
| OP-12 | `:521` (§6: `EGRESS_HOSTS.langProcess` = the GitHub hosts), parent §4.3 (the trap table), D25 (`:190`) | **Outside E1's build, reported because it was found here:** the bench repository is public and its plan carries the §4.3 trap table, while the process arm's egress reaches GitHub by host (SNI), so a process agent could read the test-design aid. E1 keeps the bench pointer inside the README's attribution section (cut from run bases); closing the channel (a needle `dew_flow_benchmark`/the plan path in the audit, or moving the table out of the public plan) is E3's or the plan's. | **change if acted on** (approach) — not E1's; for the E3 stories or the team lead |

**Read together:** OP-1 is a change in every option. If OP-12 is acted on as a plan change too, the two are independent causes
and the second stops the plan; the team lead decides both, and their order, before either is recorded.

## 10. Risk items (for the gate's `riskItems`)

1. **E1.S8a — the runner.** It is a public contract read by four users at once (the agent through `GRAMMAR.md`, CI, the pre-freeze
   gate, E2's held-out scoring), and it judges the money: a comparison or lifecycle defect misclassifies every session of a task,
   in one stack or both. Unmarked only because the cornerstone ledger is full.
2. **E1.S2 — the specs and their freeze.** Irreversible in the series' terms: after the tag a spec changes only by a dated amendment
   that voids the task's limits (G-17), and after the pre-registration never; an ambiguous or biased requirement makes every later
   failure unadjudicable. The cornerstone.
3. **E1.S1 / E1.S7 — the public repository, the push and the run bases.** Publication cannot be taken back: a leaked credit, held-out
   needle, reference or secret in any pushed commit is public at once (D2, D31), and the PAT and deploy key are credentials with
   write access.

## 11. Definition of Done (E1)

- [ ] The parent's E1 done-when holds (§1 above), with `BENCH_REQUIRE_SANDBOX=1` and nothing skipped.
- [ ] Every story's red, green and second red are recorded in its summary with the messages seen.
- [ ] The runner is pinned in `inputs/specs.json` before FORMAT v2's gate; FORMAT v2 is gated before the first spec gate; every
      spec's samples existed before its conformance gate; every spec passed the D40 gate before its document gate.
- [ ] The ten specs are gated, frozen under one tag, and their hashes, R-id lists, T and G are in `inputs/specs.json`.
- [ ] No held-out case, reference, mutant or riskiest-contract scenario is in either repository (`leakcheck`, the hash test).
- [ ] The push happened as §4 says, or the summary says what waits for it and why.
- [ ] OP-1 was decided by the team lead and, if recorded as a change, entered in the parent's ledger in the commit that accepts it.
- [ ] `research/module_tests.md`, `research/module_lang_bench.md` and `research/architecture.md` describe what E1 built; the task
      repository's README, `todo/README.md` and research stubs match its tree.
- [ ] `node .agents/conventions/tools/plan-lifecycle.mjs .` clean.
